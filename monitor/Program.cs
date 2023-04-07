using Blockquote.Models;
using TwitterSharp.Client;
using TwitterSharp.Request.AdvancedSearch;
using TwitterSharp.Request.Option;
using TwitterSharp.Rule;

namespace Blockquote.Monitor;
class Program
{
    private static string _ExpressionKey = "TwitterApiRuleExpression";

    private static string _Now => DateTimeOffset.Now.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz");
    private static void Log(string input) => Console.WriteLine($"{_Now} {input}");
    static async Task Main(string[] args)
    {
        Log($"Getting bearer token...");
        var bearer = EnvHelper.GetBearerToken();

        Log("Creating Twitter client...");
        var client = new TwitterSharp.Client.TwitterClient(bearer);

        Log("Building monitoring request...");
        var expr = Expression.Parse(GetRuleExpression());
        Log($"\tDesired expression is: {expr.ToString()}");

        // Check what our current filters are
        var curStreams = await client.GetInfoTweetStreamAsync();
        if(curStreams.Any(cs => cs.Value.ToString() == expr.ToString()))
        {
            Log("\tExpression already in streams.");
        }
        else
        {
            var request = new TwitterSharp.Request.StreamRequest(expr);
            Log("\tAdding request expression to client's stream...");
            await client.AddTweetStreamAsync(request);
        }

        Log("Display current subscription for stream...");
        var subs = await client.GetInfoTweetStreamAsync();
        Log("Subscriptions: " + string.Join("\n", subs.Select(x => x.Id + " " + x.Value.ToString())));

        Log("Waiting for tweets...");
        await MonitorTweetStream(client);

        // Debugging with a timeout.
        // Console.Write("\n");
        // var secondsLeft = 60 * 8;

        // while(secondsLeft > 0)
        // {
        //     Console.Write($"\rWaiting {secondsLeft.ToString().PadLeft(4, '0')} seconds for tweets to come in.");
        //     await Task.Delay(1000);
        //     secondsLeft--;
        // }

        Log("\nDone.");
    }

    private static async Task MonitorTweetStream(TwitterClient client)
    {
        try
        {
            // NextTweetStreamAsync will continue to run in background
            // Squelching async not awaited warning with a "discard"
            await Task.Run(async () =>
            {
                // Take in parameter a callback called for each new tweet
                // Since we want to get the basic info of the tweet author, we add an empty array of UserOption
                await client.NextTweetStreamAsync(async (tweet) =>
                {
                    Log($"\n{tweet.Id} From {tweet.Author.Name} (Rules: {string.Join(',', tweet.MatchingRules.Select(x => x.Tag))})");
                    var tweetThread = await GetTweetThread(tweet.Id, client);
                    if (tweetThread?.QuotedTweet != null)
                    {
                        await CosmosHelper.UpsertThread(tweetThread);
                    }
                    else
                    {
                        Log("\tQuoted tweet was null, so not adding to database.");
                    }
                },
                new TweetSearchOptions
                {
                    UserOptions = Array.Empty<UserOption>()
                });
            });
        }
        catch (Exception ex)
        {
            Log($"Encountered {ex.GetType().Name}:\n\t{ex.Message}");
        }
        finally
        {
            for(var d = 5; d >= 0; d--)
            {
                Console.Write($"\r\tRestarting monitoring in {d} seconds...");
                await Task.Delay(1000);
            }
            Console.Write("\n");
            Log("Canceling TweetStream...");
            client.CancelTweetStream();
            Log("Monitoring TweetStream...");
            await MonitorTweetStream(client);
        }
    }

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
}
