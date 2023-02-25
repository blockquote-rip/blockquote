using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Blockquote.Models;

namespace Blockquote.Models
{
    public static class ReadCosmosTweets
    {
        [FunctionName("ReadCosmosTweets")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            // string name = req.Query["name"];
            string name = EnvHelper.GetEnv("PersonName", true);

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            name = name ?? data?.name;

            string responseMessage = string.IsNullOrEmpty(name)
                ? "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response."
                : $"Hello, {name}. This HTTP triggered function executed successfully.";

            return new OkObjectResult(responseMessage);
        }

        /*[FunctionName("GetTweet")]
		public static async Task<IActionResult> Get([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tweets/{tweetId}")] HttpRequest request, string tweetId, ILogger log)
		{
			try
			{
                var tweet = await CosmosHelper.GetTweetAsync(tweetId);
				return new OkObjectResult(tweet);
			}
			catch (Exception ex)
			{
				return new BadRequestObjectResult(ex);
			}
		}*/
    }
}
