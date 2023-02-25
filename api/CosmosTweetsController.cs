using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Blockquote.Models;
using System.Text.Json;

namespace api
{
    public class CosmosTweetsController
    {
        private readonly ILogger _logger;

        public CosmosTweetsController(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<CosmosTweetsController>();
        }

        [Function("GetTweet")]
		public async Task<HttpResponseData> Get([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tweets/{tweetId}")] HttpRequestData req, string tweetId)
		{
			try
            {
                var tweet = await CosmosHelper.GetTweetAsync(tweetId);
                return await JsonResultFromObject(req, tweet);
            }
            catch (Exception ex)
            {
                return await ErrorResultFromException(req, ex);
            }
        }

        [Function("List")]
		public async Task<HttpResponseData> List([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tweets")] HttpRequestData req)
		{
			try
			{
				var resultsPerPage = 25;

                // Determine the requested page, if "page" was provided in the query string.
                var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                var pageString = query["page"];
				int.TryParse(pageString, out int page);

				// If a page was not provided, the int.TryParse() will have resulted in 0
                // Default to 1 if no (valid integer) page was provided.
                _logger.LogInformation($"Page resolved to {page}.");
				page = page < 1 ? 1 : page;
				

				var results = await CosmosHelper.GetTweetsPagedAsync(page, resultsPerPage);

                return await JsonResultFromObject(req, results);
			}
			catch (Exception ex)
			{
				return await ErrorResultFromException(req, ex);
			}
		}

        private static async Task<HttpResponseData> JsonResultFromObject(HttpRequestData req, object obj)
        {
            var success = req.CreateResponse(HttpStatusCode.OK);
            success.Headers.Add("Content-Type", "application/json; charset=utf-8");

            var objJson = JsonSerializer.Serialize(obj);
            await success.WriteStringAsync(objJson);

            return success;
        }

        private static async Task<HttpResponseData> ErrorResultFromException(HttpRequestData req, Exception ex)
        {
            var error = req.CreateResponse(HttpStatusCode.BadRequest);
            error.Headers.Add("Content-Type", "text/plain; charset=utf-8");
            await error.WriteStringAsync($"Unexpected error:\n{ex.Message}");

            return error;
        }
    }
}
