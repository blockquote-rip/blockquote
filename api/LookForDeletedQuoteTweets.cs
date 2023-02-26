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
        public async Task LookForOrphans([TimerTrigger("0 */5 * * * *")] MyInfo myTimer)
        {
            try
            {
                _logger.LogInformation($"{nameof(LookForOrphans)} started.");
                var container = await CosmosHelper.GetDbContainer();
                
                int tweetsToTake = 25;
                var dbTweets = await CosmosHelper.GetTweetsToCheckAsync(tweetsToTake);
                _logger.LogInformation($"\tGot {dbTweets.Count} tweets from the database.");

                if(dbTweets.Count == 0)
                {
                    _logger.LogInformation($"{nameof(LookForOrphans)} finished.  No tweets due to be updated.");
                    return;
                }

                var apiTweets = await GetTweetsFromTwitterApi(dbTweets);
                if(dbTweets.Count != apiTweets.Count())
                {
                    _logger.LogInformation($"{dbTweets.Count} db tweets vs. {apiTweets.Count()} API tweets");
                }

                _logger.LogInformation("Updating db records...");
                await UpsertCosmosTweets(dbTweets, apiTweets);

                _logger.LogInformation("LookForOrphans finished.");
            }
            catch(Exception ex)
            {
                _logger.LogError($"{nameof(LookForOrphans)} failed. {ex.GetType().Name} {ex.Message} \n {ex.InnerException?.Message}", ex);
            }
        }

        ///<summary>Takes a collection of type T and a Func that takes T and returns a Task.
        ///Then runs those Tasks in batches of batchSize at a time.</summary>
        private static async Task BatchRunTasks<T>(IEnumerable<T> items, Func<T, Task> taskGenerator, ILogger logger, string taskDescription, int batchSize = 3)
        {
            var completed = 0;
            var keepLooping = false;

            do
                {
                    // Get a batch of dbTweets to review.
                    var batch = items
                        .Skip(completed)
                        .Take(batchSize)
                        .ToList();
                    
                    // Build a List of tasks to update all the dbTweets in our batch.
                    var tasks = batch
                        .Select(b => taskGenerator(b))
                        .ToList();

                    // Wait for the entire batch's tasks to complete.
                    await Task.WhenAll(tasks);

                    // Log errors for tasks failed
                    tasks.Where(t => t.IsCompletedSuccessfully == false)
                        .ToList()
                        .ForEach(t => logger.LogError($"\tA {taskDescription} task did not complete successfully."));
                    tasks.Where(t => t.Exception != null)
                        .ToList()
                        .ForEach(t => logger.LogError($"{nameof(BatchRunTasks)} update failed.", t.Exception));


                    // Get ready for the next pass
                    completed += batch.Count;
                    keepLooping = batch.Count > 0;
                    if(keepLooping)
                    {
                        logger.LogInformation($"\t{nameof(BatchRunTasks)} completed {completed} of {items.Count()} {taskDescription} tasks.");
                        await Task.Delay(1000);
                    }
                }
                while(keepLooping);
        }

        private async Task<List<Tweet>> GetTweetsFromTwitterApi(List<CosmosTweet> dbTweets)
        {
            var quotedTweetIds = dbTweets
                .Where(t => t.QuotedTweet != null)
                .Select(t => t.QuotedTweet?.id)
                .ToList();

            _logger.LogInformation($"Fetching {quotedTweetIds.Count} tweets quoted tweets from API...");
            var bearer = EnvHelper.GetBearerToken();
            var userclient = new TwitterSharp.Client.TwitterClient(bearer);
            
            // We'll populate apiTweets using BatRunTasks().
            // Do do that we'll need quotedTweetIds, and a Func that given a tweet ID returns a task.
            var apiTweets = new List<Tweet>();
            var fetchTweetsFromTwitterApiTaskGenerator = new Func<string?, Task>(async s => {
                var tweet = await userclient.GetTweetAsync(s);
                apiTweets.Add(tweet);
            });

            try
            {
                await BatchRunTasks(quotedTweetIds, fetchTweetsFromTwitterApiTaskGenerator, _logger, "FetchTweetsFromTwitter", 3);
            }
            catch(TwitterException ex)
            {
                _logger.LogError($"Encountered {ex.GetType().Name} error while bulk-fetching tweets from Twitter API.  {ex.Title} {ex.Message}", ex);
                ex.Errors.ToList().ForEach(error => _logger.LogError($"\tError from Twitter API Title: {error.Title} Code: {error.Code} Type: {error.Type} Value: {error.Value}"));
                
                var notFoundError = ex.Errors.FirstOrDefault(e => e.Type == "https://api.twitter.com/2/problems/resource-not-found");

                if(notFoundError != null)
                {
                    _logger.LogError($"\tThe tweet {notFoundError.Value} returned a {notFoundError.Title} error.");
                }
                else
                {
                    throw;
                }
            }
            catch(AggregateException aex)
            {
                _logger.LogError("Caught an AggregateException.", aex);
                throw;
            }
            catch(Exception ex)
            {
                _logger.LogWarning($"Encountered error bulk-fetching tweets from Twitter API. {ex.GetType()} {ex.Message}");
                throw;
            }

            if(apiTweets.Count == 0)
            {
                var ex = new Exception("Got 0 tweets from the Twitter API. Presumably we've messed up our query.");
                _logger.LogWarning(ex.Message, ex);
                throw ex;
            }

            return apiTweets;
        }

        private async Task UpsertCosmosTweets(List<CosmosTweet> dbTweets, List<Tweet> apiTweets)
        {
            var started = DateTimeOffset.Now;
            var upsertTweetTaskGenerator = new Func<CosmosTweet, Task>(async tweet => {
                tweet.LastUpdated = DateTimeOffset.Now;
                var hasMatch = apiTweets.Any(apiTweet => apiTweet.Id == tweet.QuotedTweet?.id);
                if(hasMatch == false)
                {
                    tweet.Deleted = true;
                }
                
                await CosmosHelper.UpsertThread(tweet);
            });
            
            await BatchRunTasks(dbTweets, upsertTweetTaskGenerator, _logger, "UpsertTweet", 5);
            _logger.LogInformation($"Updated {dbTweets.Count} tweets in {(DateTimeOffset.Now - started).TotalSeconds} seconds.");
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
