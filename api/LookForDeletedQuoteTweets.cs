using System;
using Blockquote.Models;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using TwitterSharp.Client;
using TwitterSharp.Response.RTweet;

namespace api
{
    public class LookForDeletedQuoteTweets
    {
        private readonly ILogger _logger;

        public LookForDeletedQuoteTweets(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<LookForDeletedQuoteTweets>();
        }

        [Function("LookForOrphans")]
        public async Task LookForOrphans([TimerTrigger("*/30 * * * * *")] MyInfo myTimer)
        {
            try
            {
                _logger.LogInformation($"{nameof(LookForOrphans)} started.");
                var container = await CosmosHelper.GetDbContainer();
                
                int tweetsToTake = 50;
                var dbTweets = await CosmosHelper.GetTweetsToCheckAsync(tweetsToTake);
                _logger.LogInformation($"\tGot {dbTweets.Count} tweets from the database.");

                if(dbTweets.Count == 0)
                {
                    _logger.LogInformation($"{nameof(LookForOrphans)} finished.  No tweets due to be updated.");
                    return;
                }

                // var apiTweets = await GetTweetsFromTwitterApi(dbTweets);
                var apiTweets = await GetTweetsFromTwitterApiBatched(dbTweets);
                _logger.LogInformation($"Got {apiTweets.Count} from the twitter API.");
                if(dbTweets.Count != apiTweets.Count())
                {
                    _logger.LogInformation($"{dbTweets.Count} db tweets vs. {apiTweets.Count()} API tweets");
                    var dbQts = dbTweets.Where(d => d.QuotedTweet != null).Select(d => d.QuotedTweet!.id);
                    _logger.LogInformation($"\tdbQts: {string.Join(", ", dbQts)}");
                    var apiQts = apiTweets.Select(a => a.Id).ToList();
                    _logger.LogInformation($"\tapiQts: {string.Join(", ", apiQts)}");
                    var missingQts = dbQts.Except(apiQts);
                    throw new Exception($"Y THO? {string.Join(", ", missingQts)}");
                }

                _logger.LogInformation("Updating db records...");
                // await UpsertCosmosTweets(dbTweets, apiTweets);
                await UpsertCosmostTweetsBatched(dbTweets, apiTweets);

                _logger.LogInformation("LookForOrphans finished.");
            }
            catch(Exception ex)
            {
                _logger.LogError($"{nameof(LookForOrphans)} failed. {ex.GetType().Name} {ex.Message} \n {ex.InnerException?.Message}", ex);
            }
        }

        private Func<CosmosTweet, string> CosmosTweetDescriptionGenerator = (ct => $"{nameof(CosmosTweet)} id: {ct.id} qt: {ct.QuotedTweet?.id}");

        private async Task<List<Tweet>> GetTweetsFromTwitterApiBatched(List<CosmosTweet> dbTweets)
        {
            try
            {
                _logger.LogInformation($"Fetching quote-tweets quoted tweets from API...");
                var bearer = EnvHelper.GetBearerToken();
                var userclient = new TwitterSharp.Client.TwitterClient(bearer);

                var tweetTaskGenerator = new Func<CosmosTweet, Task<Tweet>>(async ct => {
                    try
                    {
                        var quotedTweetId = ct.QuotedTweet?.id;
                        // _logger.LogInformation($"\tFetching {CosmosTweetDescriptionGenerator(ct)}...");
                        var quotedTweet = await userclient.GetTweetAsync(quotedTweetId);
                        return quotedTweet;
                    }
                    catch(TwitterException ex)
                    {
                        // There is a type of error you get for a tweet that is actually no longer available.
                        _logger.LogWarning($"Error Fetching {CosmosTweetDescriptionGenerator(ct)}.\n\tTitle:{ex.Title}\n\tType:{ex.Type}\n\tData:{ex.Data}\n\tErrors({ex.Errors?.Count() ?? 0}){string.Join("\n\t\t", ex.Errors?.Select(e => $"Title: {e.Title} Type: {e.Type} Code: {e.Code} Message:{e.Message} Details: {e.Details} Parameter: {e.Parameter} Value: {e.Value}").ToList() ?? new List<string>())}");

                        // Sometimes when we spam the Twitter API we get told to back off.
                        if(ex.Title == "Too Many Requests")
                        {
                            throw new BackOffException();
                        }

                        // Squelch "Not found" errors, returning an empty CosmosTweet, otherwise throw.
                        if(ex.Errors?.Any(e => e.Type == "https://api.twitter.com/2/problems/resource-not-found") ?? false)
                        {
                            _logger.LogWarning($"Quoted tweet not found. {CosmosTweetDescriptionGenerator(ct)}.");
                            return new Tweet();
                        }
                        
                        // Some "Not authorized" errors indicatd an account maybe suspended or privated.
                        if(ex.Errors?.Any(e => e.Type == "https://api.twitter.com/2/problems/not-authorized-for-resource") ?? false)
                        {
                            _logger.LogWarning($"Authorization error fetching quoted tweet, Account is protected, suspended, or deleted. {CosmosTweetDescriptionGenerator(ct)}.");
                            return new Tweet();
                        }

                        throw;
                    }
                });

                
                var batcher = new BatchExecutor<CosmosTweet, Tweet>(5, tweetTaskGenerator, CosmosTweetDescriptionGenerator);
                var results = await batcher.RunFor(dbTweets);

                return results;
            }
            catch(Exception ex)
            {
                _logger.LogError($"Errors encountered when running {nameof(GetTweetsFromTwitterApiBatched)}() {ex.GetType().Name} {ex.Message}");
                throw;
            }
        }

        private async Task UpsertCosmostTweetsBatched(List<CosmosTweet> dbTweets, List<Tweet> apiTweets)
        {
            try
            {
                var upsertTweetTaskGenerator = new Func<CosmosTweet, Task<CosmosTweet>>(async tweet => {
                    // _logger.LogInformation($"\tUpdating {CosmosTweetDescriptionGenerator(tweet)}...");
                    tweet.LastUpdated = DateTimeOffset.Now;
                    var hasMatch = apiTweets.Any(apiTweet => apiTweet.Id == tweet.QuotedTweet?.id);
                    if(hasMatch == false)
                    {
                        _logger.LogInformation($"\t\tQuoted tweet not found, but skipping upserting of {CosmosTweetDescriptionGenerator(tweet)} while testing.");
                        return tweet;
                        tweet.Deleted = true;
                    }
                    
                    await CosmosHelper.UpsertThread(tweet);
                    return tweet;
                });

                var batcher = new BatchExecutor<CosmosTweet, CosmosTweet>(5, upsertTweetTaskGenerator, CosmosTweetDescriptionGenerator);
                await batcher.RunFor(dbTweets);
            }
            catch
            {
                _logger.LogError($"Errors encountered when running {nameof(UpsertCosmostTweetsBatched)}()");
                throw;
            }
        }
    }

    public class MyInfo
    {
        public MyScheduleStatus ScheduleStatus { get; set; } = new MyScheduleStatus();

        public bool IsPastDue { get; set; }
    }

    public class MyScheduleStatus
    {
        public DateTime Last { get; set; }

        public DateTime Next { get; set; }

        public DateTime LastUpdated { get; set; }
    }
}
