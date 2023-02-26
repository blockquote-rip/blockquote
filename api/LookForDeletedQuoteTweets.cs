using System;
using Blockquote.Models;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

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
                _logger.LogInformation($"Grabbing {tweetsToTake} tweets from the database to refresh...");
                var iterator = container.GetItemLinqQueryable<CosmosTweet>()
                    .Where(t => t.NextUpdate < DateTimeOffset.Now && t.QuotedTweet != null && t.Deleted == false)
                    .OrderBy(t => t.NextUpdate)
                    .Take(tweetsToTake)
                    .ToFeedIterator();
                
                var dbTweets = new List<CosmosTweet>();
                using(iterator)
                {
                    while(iterator.HasMoreResults)
                    {
                        foreach(var tweet in await iterator.ReadNextAsync())
                        {
                            if(tweet?.QuotedTweet != null)
                            {
                                dbTweets.Add(tweet);
                            }
                        }
                    }
                }

                _logger.LogInformation($"\tGot {dbTweets.Count} tweets from the database.");

                if(dbTweets.Count == 0)
                {
                    _logger.LogInformation($"{nameof(LookForOrphans)} finished.  No tweets due to be updated.");
                    return;
                }

                var quotedTweetIds = dbTweets
                    .Where(t => t.QuotedTweet != null)
                    .Select(t => t.QuotedTweet.id)
                    .ToList();

                _logger.LogInformation($"Fetching {quotedTweetIds.Count} tweets quoted tweets from API...");
                var bearer = EnvHelper.GetBearerToken();
                var userclient = new TwitterSharp.Client.TwitterClient(bearer);
                
                var apiTweets = await userclient.GetTweetsAsync(quotedTweetIds.ToArray());

                if(dbTweets.Count != apiTweets.Count())
                {
                    _logger.LogInformation($"{dbTweets.Count} db tweets vs. {apiTweets.Count()} API tweets");
                }

                _logger.LogInformation("Updating db records...");
                foreach(var t in dbTweets)
                {
                    t.LastUpdated = DateTimeOffset.Now;
                    var hasMatch = apiTweets.Any(apiTweet => apiTweet.Id == t.QuotedTweet?.id);
                    if(hasMatch == false)
                    {
                        t.Deleted = true;
                    }
                    
                    await CosmosHelper.UpsertThread(t);
                }

                _logger.LogInformation("LookForOrphans finished.");
            }
            catch(Exception ex)
            {
                _logger.LogError($"{nameof(LookForOrphans)} failed. {ex.GetType().Name} {ex.Message} \n {ex.InnerException?.Message}", ex);
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
