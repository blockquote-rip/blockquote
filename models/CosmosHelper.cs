using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;

namespace Blockquote.Models
{
	public class CosmosHelper
	{
		public static async Task<Container> GetDbContainer()
		{
			var secrets = new CosmosSecrets();
			var cosmosClient = new CosmosClient(secrets.CosmosDbUrl, secrets.CosmosDbKey);
			var dbResp = await cosmosClient.CreateDatabaseIfNotExistsAsync(secrets.CosmosDbId);
			var db = dbResp.Database;

			var containerResp = await db.CreateContainerIfNotExistsAsync(secrets.CosmosDbContainer, "/id");
			var container = containerResp.Container;

			return container;
		}

		public static async Task UpsertThread(CosmosTweet thread)
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
			
			var db = await CosmosHelper.GetDbContainer();
			Console.Write($"\nUpserting tweet {thread.id}... ");
			var resp = await db.UpsertItemAsync<CosmosTweet>(thread);
			Console.Write($"{resp.StatusCode}\n");
		}

		public static async Task<CosmosTweet> GetTweetAsync(string tweetId)
		{
			var db = await CosmosHelper.GetDbContainer();
			var pk = new PartitionKey(tweetId);
			var response = await db.ReadItemAsync<CosmosTweet>(tweetId, pk);

			return response.Resource;
		}

		public static async Task<List<CosmosTweet>> GetTweetsPagedAsync(int page, int resultsPerPage)
		{
			var results = new List<CosmosTweet>();
			var db = await GetDbContainer();
			var iterator = db.GetItemLinqQueryable<CosmosTweet>()
				.Where(t => t.Deleted)
				.OrderByDescending(t => t.LastUpdated)
				.Skip(resultsPerPage * (page-1))
				.Take(resultsPerPage)
				.ToFeedIterator();
			
			using(iterator)
			{
				while(iterator.HasMoreResults)
				{
					results.AddRange(await iterator.ReadNextAsync());
				}
			}

			return results;
		}
	}
}