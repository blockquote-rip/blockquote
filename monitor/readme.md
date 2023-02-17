# Blockquote

The tweet monitoring tool that powers [Blockquote.rip](https://www.blockquote.rip). This tool watches a filtered stream from the Twitter V2 API. When a tweet appears in the stream the tool spiders the tweet. The tool fetches the tweet, and recursively walks up the tweets that were quoted or replied to.

## Requirements
* A `Bearer Token` for a [Twitter V2 API app](https://apps.twitter.com/)
* The string representation of [a filtered stream rule](https://developer.twitter.com/en/docs/twitter-api/tweets/filtered-stream/integrate/build-a-rule)
* The `URI`, `(primary) Key`, `DB id/name`, and `container name` for connecting to an Azure Cosmos DB. Obtaining these keys is outlined in the "Setting Up" section of [this tutorial](https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/quickstart-dotnet?tabs=azure-portal%2Clinux%2Cpasswordless%2Csign-in-azure-cli#setting-up)

## Running
You must provide all the keys in `monitor/secrets.example.json` either as user-secrets or as environment variables.

After that you can perform a `dotnet run` in the `monitor` folder.

```
starzen@lelex:~/Documents/projects/blockquote/monitor$ dotnet run
Getting bearer token...
Creating Twitter client...
Building monitoring request...
	Desired expression is: (is:quote (from:michaelmalice OR from:MirrorReaderBot))
	Expression already in streams.
Display current subscription for stream...
Subscriptions: 1111111111111111111 (is:quote (from:michaelmalice OR from:MirrorReaderBot))

Waiting for tweets...

2/17/2023 6:39:38 PM
1626728081091092480 From Mirror Reader Bot (Rules: )

MirrorReaderBot: Example quote tweet. https://t.co/C9kUJ0FFXc

    MirrorReaderBot: Example tweet.

Upserting tweet 1626728081091092480... Created
```

