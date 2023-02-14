using Microsoft.Extensions.Configuration;

namespace monitor;
class Program
{
    private static string _BearerTokenKey = "TwitterV2Bearer";
    static void Main(string[] args)
    {
        Console.WriteLine("Hello, World!");

        var bearer = GetBearerToken();
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
