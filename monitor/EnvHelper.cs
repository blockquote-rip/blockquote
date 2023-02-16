using Microsoft.Extensions.Configuration;

namespace monitor
{
	public class EnvHelper
    {
        ///<summary>Attempts to pull values from the environment.  If no matching environment values is found it will attempt to find the value in user-secrets.</summary>
		public static string? GetEnv(string key, bool required = true)
		{
			// Check for a matching environment variable.
			var result = System.Environment.GetEnvironmentVariable(key, EnvironmentVariableTarget.User);

			// If we didn't find an environment variable reach into user-secrets.
			if(string.IsNullOrWhiteSpace(result))
			{
				// We didn't find it right in the ENV, build up a config and see if we can suck it out of a secret vault.
				var builder = new ConfigurationBuilder()
					.AddUserSecrets<Program>();
				
				var configurationRoot = builder.Build();

				result = configurationRoot[key];
			}

			if(required && string.IsNullOrWhiteSpace(result))
			{
				throw new Exception($"Could not find a value for the key \"{key}\"");
			}

			return result;
		}
    }
}