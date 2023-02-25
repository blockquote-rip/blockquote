using Microsoft.Extensions.Configuration;

namespace Blockquote.Models
{
	public class EnvHelper
	{
		///<summary>The UserSecretsId we created in monitor.csproj  We'll be using these same secrets in future related projects.</summary>
		private const string SecretsId = "81b6de51-b515-4a00-8b96-78019839167c";

		///<summary>Attempts to pull values from the environment.  If no matching environment values is found it will attempt to find the value in user-secrets.</summary>
		public static string? GetEnv(string key, bool required = true)
		{
			// Check for a matching environment variable from a secrets file.
			var result = System.Environment.GetEnvironmentVariable(key, EnvironmentVariableTarget.User);
			
			// Or, if running a function app out of the envs created from local.settings.json import in a function app.
			result = string.IsNullOrWhiteSpace(result)
				? System.Environment.GetEnvironmentVariable(key, EnvironmentVariableTarget.Process)
				: result;

			// If we didn't find an environment variable reach into user-secrets.
			if (string.IsNullOrWhiteSpace(result))
			{
				// We didn't find it right in the ENV, build up a config and see if we can suck it out of a secret vault.
				var builder = new ConfigurationBuilder()
					.AddUserSecrets(SecretsId);

				var configurationRoot = builder.Build();

				result = configurationRoot[key];
			}

			if (required && string.IsNullOrWhiteSpace(result))
			{
				throw new Exception($"Could not find a value for the key \"{key}\"");
			}

			return result;
		}
	}
}
