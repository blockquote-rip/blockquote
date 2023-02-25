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

        [Function("CosmosTweetsController")]
        public HttpResponseData Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            // This one comes from a Value entry in the local.settings.json file
            var name = EnvHelper.GetEnv("PersonName", false);

            // This one comes from our secrets file.
            var rule = EnvHelper.GetEnv("TwitterApiRuleExpression", false);

            response.WriteString($"Welcome to Azure Functions!\n\nName: {name}\n\nRule: {rule}");

            return response;
        }

        [Function("GetTweet")]
		public static async Task<HttpResponseData> Get([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tweets/{tweetId}")] HttpRequestData req, string tweetId, ILogger log)
		{
			try
			{
				var tweet = await CosmosHelper.GetTweetAsync(tweetId);
                var success = req.CreateResponse(HttpStatusCode.OK);
                success.Headers.Add("Content-Type", "text/json; charset=utf-8");

                var tweetJson = JsonSerializer.Serialize(tweet);
                await success.WriteStringAsync(tweetJson);
                
                return success;
			}
			catch (Exception ex)
			{
				var error = req.CreateResponse(HttpStatusCode.BadRequest);
                error.Headers.Add("Content-Type", "text/plain; charset=utf-8");
                await error.WriteStringAsync($"Unexpected error:\n{ex.Message}");

                return error;
			}
		}
    }
}
