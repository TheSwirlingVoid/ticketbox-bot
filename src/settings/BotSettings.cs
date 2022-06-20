using MongoDB.Bson;

class BotSettings {
	public int ExpiryDays { get; private set; }
	public bool CreateThreads { get; private set; }

	public BotSettings()
	{
		ExpiryDays = 7;
		CreateThreads = true;
	}

	public static BotSettings getServerSettings(BsonDocument serverDoc)
	{
		var docSettings = serverDoc["bot_options"];
		return new BotSettings()
			//* [UPDATE OPTIONS]
			.expiryDays(docSettings["expiry_days"].AsInt32)
			.createThreads(docSettings["create_threads"].AsBoolean);
	}

	public BotSettings expiryDays(int expiryDays)
	{
		ExpiryDays = expiryDays;
		return this;
	}

	public BotSettings createThreads(bool threads)
	{
		CreateThreads = threads;
		return this;
	}
}