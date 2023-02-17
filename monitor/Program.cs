using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using TwitterSharp.Client;
using TwitterSharp.Request.AdvancedSearch;
using TwitterSharp.Request.Option;
using TwitterSharp.Rule;

namespace monitor;
class Program
{
    private static string _BearerTokenKey = "TwitterV2Bearer";
    private static string _ExpressionKey = "TwitterApiRuleExpression";
    static async Task Main(string[] args)
    {
        Console.WriteLine("Getting bearer token...");
        var bearer = GetBearerToken();

        Console.WriteLine("Creating Twitter client...");
        var client = new TwitterSharp.Client.TwitterClient(bearer);

        Console.WriteLine("Building monitoring request...");
        var expr = Expression.Parse(GetRuleExpression());
        Console.WriteLine($"\tDesired expression is: {expr.ToString()}");

        // Check what our current filters are
        var curStreams = await client.GetInfoTweetStreamAsync();
        if(curStreams.Any(cs => cs.Value.ToString() == expr.ToString()))
        {
            Console.WriteLine("\tExpression already in streams.");
        }
        else
        {
            var request = new TwitterSharp.Request.StreamRequest(expr);
            Console.WriteLine("\tAdding request expression to client's stream...");
            await client.AddTweetStreamAsync(request);
        }

        Console.WriteLine("Display current subscription for stream...");
        var subs = await client.GetInfoTweetStreamAsync();
        Console.WriteLine("Subscriptions: " + string.Join("\n", subs.Select(x => x.Id + " " + x.Value.ToString())));

        Console.WriteLine("Waiting for tweets...");
        // NextTweetStreamAsync will continue to run in background
        // Squelching async not awaited warning with a "discard"
        await Task.Run(async () =>
        {
            // Take in parameter a callback called for each new tweet
            // Since we want to get the basic info of the tweet author, we add an empty array of UserOption
            await client.NextTweetStreamAsync(async (tweet) =>
            {
                Console.WriteLine($"\n{DateTime.Now}\n{tweet.Id} From {tweet.Author.Name} (Rules: {string.Join(',', tweet.MatchingRules.Select(x => x.Tag))})");
                var tweetThread = await GetTweetThread(tweet.Id, client);
                await UpsertThread(tweetThread);
            },
            new TweetSearchOptions
            {
                UserOptions = Array.Empty<UserOption>()
            });
        });

        // Debugging with a timeout.
        // Console.Write("\n");
        // var secondsLeft = 60 * 8;

        // while(secondsLeft > 0)
        // {
        //     Console.Write($"\rWaiting {secondsLeft.ToString().PadLeft(4, '0')} seconds for tweets to come in.");
        //     await Task.Delay(1000);
        //     secondsLeft--;
        // }

        Console.WriteLine("\nDone.");
    }

    public static string? GetBearerToken() => EnvHelper.GetEnv(_BearerTokenKey);
    public static string? GetRuleExpression() => EnvHelper.GetEnv(_ExpressionKey);

    private static async Task<CosmosTweet?> GetTweetThread(string? tweetId, TwitterClient client, uint depth = 0)
        {
            CosmosTweet? result = null;

            if(tweetId == null)
            {
                return result;
            }
            else {	
                try
                {
                    var options = new TweetSearchOptions
                    {
                        TweetOptions = new []{ TweetOption.Created_At, TweetOption.Referenced_Tweets, TweetOption.Attachments, TweetOption.Entities },
                        UserOptions = new []{ UserOption.Created_At, UserOption.Profile_Image_Url, UserOption.Verified },
                        MediaOptions = new []{ MediaOption.Url, MediaOption.Preview_Image_Url }
                    };

                    var tweet = await client.GetTweetAsync(tweetId, options);
                    result = new CosmosTweet(tweet);
                    // Finding the quoted tweet or the tweet it was replying to has to be found with some linq,
                    // And the CosmosTweet constructor takes care of that for us.
                    result.QuotedTweet = await GetTweetThread(result.QuotedTweet?.id, client, depth+1);
                    result.InReplyTo = await GetTweetThread(result.InReplyTo?.id, client);
                }
                catch(Exception ex)
                {
                    await Console.Error.WriteLineAsync($"{ex.GetType().Name} - {ex.Message}\n{ex.InnerException?.Message}");
                }

                return result;
            }
        }

        private static async Task<Container> GetDbContainer()
        {
            var secrets = new CosmosSecrets();
            var cosmosClient = new CosmosClient(secrets.CosmosDbUrl, secrets.CosmosDbKey);
            var dbResp = await cosmosClient.CreateDatabaseIfNotExistsAsync(secrets.CosmosDbId);
            var db = dbResp.Database;

            var containerResp = await db.CreateContainerIfNotExistsAsync(secrets.CosmosDbContainer, "/id");
            var container = containerResp.Container;

            return container;
        }

        private static async Task UpsertThread(CosmosTweet? thread)
        {
            // Debugging output
            var cnt = 0;
            var curTweet = thread;
            while(curTweet != null)
            {
                Console.Write("\n");
                for(var n = 0; n < cnt; n++) Console.Write("    ");
                Console.WriteLine($"{curTweet.CreatedBy?.ScreenName}: {curTweet?.Text}");
                cnt++;
                curTweet = curTweet?.InReplyTo ?? curTweet?.QuotedTweet;
            }
            // End Debugging
            
            if(thread?.QuotedTweet != null)
			{
				var db = await GetDbContainer();
				Console.Write($"\nUpserting tweet {thread.id}... ");
				var resp = await db.UpsertItemAsync<CosmosTweet>(thread);
				Console.Write($"{resp.StatusCode}\n");
			}
        }
}
