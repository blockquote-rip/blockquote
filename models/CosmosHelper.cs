using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;

namespace Blockquote.Models
{
	public class CosmosHelper
	{
		public static async Task<Container> GetDbContainer()
		{
			try
			{
				var secrets = new CosmosSecrets();
				var cosmosClient = new CosmosClient(secrets.CosmosDbUrl, secrets.CosmosDbKey);
				var dbResp = await cosmosClient.CreateDatabaseIfNotExistsAsync(secrets.CosmosDbId);
				var db = dbResp.Database;

				var containerResp = await db.CreateContainerIfNotExistsAsync(secrets.CosmosDbContainer, "/id");
				var container = containerResp.Container;

				return container;
			}
			catch(Exception ex)
			{
				throw new Exception($"Error encountered attempting to get a DbContainer", ex);
			}
		}

		public static async Task UpsertThread(CosmosTweet thread)
		{
			try
			{	
				var db = await CosmosHelper.GetDbContainer();

				var resp = await db.UpsertItemAsync<CosmosTweet>(thread);
				
				if(resp.StatusCode != System.Net.HttpStatusCode.OK)
				{
					throw new Exception($"Failed to update database for tweet {thread.id}.  Code {resp.StatusCode}");
				}
			}
			catch(Exception ex)
			{
				throw new Exception($"Error encountered attempting to upsert tweet {thread.id}", ex);
			}
		}

		public static async Task<CosmosTweet> GetTweetAsync(string tweetId)
		{
			try
			{
				var db = await CosmosHelper.GetDbContainer();
				var pk = new PartitionKey(tweetId);
				var response = await db.ReadItemAsync<CosmosTweet>(tweetId, pk);

				return response.Resource;
			}
			catch(Exception ex)
			{
				throw new Exception($"Error encountered attempting to fetch tweet {tweetId}", ex);
			}
		}

		public static async Task<List<CosmosTweet>> GetTweetsPagedAsync(int page, int resultsPerPage)
		{
			try
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
			catch(Exception ex)
			{
				throw new Exception($"Error encountered attempting to fetch paged tweets page: {page}, resultsPerPage: {resultsPerPage}", ex);
			}
		}
	}
}