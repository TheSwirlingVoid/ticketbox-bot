using MongoDB.Bson;
using MongoDB.Driver;

static class DocumentFunctions {
	public static FilterDefinition<BsonDocument> serverIDFilter(ulong guildId)
	{
		return Builders<BsonDocument>.Filter.Eq("server_id", guildId);
	}

	public static async Task<bool> serverDocExists(IMongoCollection<BsonDocument> collection, ulong guildId)
	{
		// if the server document isn't there
		if (collection.Find(serverIDFilter(guildId)).CountDocuments() == 0)
		{
			return false;
		}
		else {
			return true;
		}
	}
}