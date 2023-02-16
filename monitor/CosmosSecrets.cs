namespace monitor
{
	public class CosmosSecrets
	{
		public string CosmosDbUrl { get; }
		public string CosmosDbKey { get; }
		public string CosmosDbId { get; }
		public string CosmosDbContainer { get; }

		public CosmosSecrets()
		{
			CosmosDbUrl = EnvHelper.GetEnv("CosmosDbUrl") ?? string.Empty;
			CosmosDbKey = EnvHelper.GetEnv("CosmosDbKey") ?? string.Empty;
			CosmosDbId = EnvHelper.GetEnv("CosmosDbId") ?? string.Empty;
			CosmosDbContainer = EnvHelper.GetEnv("CosmosDbContainer") ?? string.Empty;
		}
	}
}