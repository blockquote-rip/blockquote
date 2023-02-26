using System.Linq.Expressions;
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

        public static async Task<List<CosmosTweet>> GetTweetsAsync(
            Expression<Func<CosmosTweet, bool>> whereClause,
            Func<IQueryable<CosmosTweet>, IOrderedQueryable<CosmosTweet>> orderByClause,
            int skip = 0,
            int take = 25
        )
        {
            try
            {
                var results = new List<CosmosTweet>();
                var db = await GetDbContainer();

                // Apply whereClause
                var query = db.GetItemLinqQueryable<CosmosTweet>()
                        .Where(whereClause);
                // apply the OrderByClause, skip and take the desired number of records, and invoke ToFeedIterator()
                var iterator = orderByClause(query)
                        .Skip(skip)
                        .Take(take)
                        .ToFeedIterator();
                    
                // Read the results out of iterator
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
                throw new Exception($"Error encountered in {nameof(GetTweetsAsync)}. {ex.GetType().Name} {ex.Message}", ex);
            }
        }

        public static async Task<List<CosmosTweet>> GetTweetsPagedAsync(int page, int resultsPerPage)
        {
            try
            {
                // Build the values to pass to GetTweetsAsync()
                Expression<Func<CosmosTweet, bool>> query = (t => t.Deleted);
                Func<IQueryable<CosmosTweet>, IOrderedQueryable<CosmosTweet>> orderBy = (t => t.OrderByDescending(o => o.CreatedAt));
                int skip = resultsPerPage * (page - 1);
                
                var results = await GetTweetsAsync(query, orderBy, skip, resultsPerPage);

                return results;
            }
            catch(Exception ex)
            {
                throw new Exception($"Error encountered attempting to fetch paged tweets page: {page}, resultsPerPage: {resultsPerPage}", ex);
            }
        }

        public static async Task<List<CosmosTweet>> GetTweetsToCheckAsync(int tweetsToTake)
        {
            try
            {
                // Build the values to pass to GetTweetsAsync()
                Expression<Func<CosmosTweet, bool>> query = (t => t.NextUpdate < DateTimeOffset.Now && t.QuotedTweet != null && t.Deleted == false);
                Func<IQueryable<CosmosTweet>, IOrderedQueryable<CosmosTweet>> orderBy = (t => t.OrderBy(o => o.LastUpdated));

                var results = await GetTweetsAsync(query, orderBy, 0, tweetsToTake);
                return results;
            }
            catch(Exception ex)
            {
                throw new Exception($"Error encountered in {nameof(GetTweetsToCheckAsync)}. tweetsToTake: {tweetsToTake}", ex);
            }
        }
    }
}