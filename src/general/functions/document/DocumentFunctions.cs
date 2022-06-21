using MongoDB.Bson;
using MongoDB.Driver;
using TicketBox;

static class DocumentFunctions {
	public static FilterDefinition<BsonDocument> serverIDFilter(ulong guildId)
	{
		return Builders<BsonDocument>.Filter.Eq("server_id", guildId);
	}

	public static BsonDocument getServerDocument(ulong serverId)
	{
		return Program.discordServersCollection.Find(DocumentFunctions.serverIDFilter(serverId)).ToList()[0];
	}

	public static bool serverDocExists(ulong guildId)
	{
		// if the server document isn't there
		if (Program.discordServersCollection.Find(serverIDFilter(guildId)).CountDocuments() == 0)
		{
			return false;
		}
		else {
			return true;
		}
	}

	public static List<BsonDocument> getPollDocuments(ulong serverId) 
	{
		var serverFilter = Builders<BsonDocument>.Filter.Eq("server_id", serverId);

		return Program.pollCollection.Find(serverFilter).ToList();
	}

	/// returns empty BsonDocument if no match
	public static BsonDocument getPollDocument(MessageScope scope) 
	{
		var filter = Builders<BsonDocument>.Filter.Eq("server_id", scope.ServerID)
			& Builders<BsonDocument>.Filter.Eq("channel_id", scope.ChannelID)
			& Builders<BsonDocument>.Filter.Eq("message_id", scope.MessageID);

		var pollDocs = Program.pollCollection.Find(filter).ToList();

		if (pollDocs.Count != 0)
			return pollDocs[0];
		else
			return new BsonDocument{};
	}
	
}