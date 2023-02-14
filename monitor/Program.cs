using Microsoft.Extensions.Configuration;
using TwitterSharp.Request.AdvancedSearch;
using TwitterSharp.Request.Option;
using TwitterSharp.Rule;

namespace monitor;
class Program
{
    private static string _BearerTokenKey = "TwitterV2Bearer";
    static async Task Main(string[] args)
    {
        Console.WriteLine("Getting bearer token...");
        var bearer = GetBearerToken();

        Console.WriteLine("Creating Twitter client...");
        var client = new TwitterSharp.Client.TwitterClient(bearer);

        Console.WriteLine("Building monitoring request...");
        var expr = Expression.Author("MirrorReaderBot");
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
        Console.WriteLine("Subscriptions: " + string.Join("\n", subs.Select(x => x.Value.ToString())));

        // NextTweetStreamAsync will continue to run in background
        Task.Run(async () =>
        {
            // Take in parameter a callback called for each new tweet
            // Since we want to get the basic info of the tweet author, we add an empty array of UserOption
            await client.NextTweetStreamAsync((tweet) =>
            {
                Console.WriteLine($"\nFrom {tweet.Author.Name}: {tweet.Text} (Rules: {string.Join(',', tweet.MatchingRules.Select(x => x.Tag))})");
            },
            new TweetSearchOptions
            {
                UserOptions = Array.Empty<UserOption>()
            });
        });

        // Add new high frequent rule after the stream started. No disconnection needed.
        // await client.AddTweetStreamAsync(new TwitterSharp.Request.StreamRequest( Expression.Author("Every3Minutes"), "Frequent"));

        Console.Write("\n");
        var secondsLeft = 60 * 8;

        while(secondsLeft > 0)
        {
            Console.Write($"\rWaiting {secondsLeft.ToString().PadLeft(4, '0')} seconds for tweets to come in.");
            await Task.Delay(1000);
            secondsLeft--;
        }
        Console.WriteLine("\nDone.");
    }

    public static string GetBearerToken()
    {
        var result = System.Environment.GetEnvironmentVariable(_BearerTokenKey, EnvironmentVariableTarget.User);

        if(string.IsNullOrWhiteSpace(result))
        {
            // We didn't find it right in the ENV, build up a config and see if we can suck it out of a secret vault.
            var builder = new ConfigurationBuilder()
                .AddUserSecrets<Program>();
            
            var configurationRoot = builder.Build();

            result = configurationRoot[_BearerTokenKey];
            
            if(string.IsNullOrWhiteSpace(result))
            {
                throw new Exception($"Could not find a value for the key \"{_BearerTokenKey}\"");
            }
        }

        return result;
    }
}
