using MongoDB.Bson;
using MongoDB.Driver;
using TicketBox;

static class JoinFunctions {
	public static async Task createServerDocument(IMongoCollection<BsonDocument> collection, ulong guildId)
	{
		// If this server does not have its own document already
		var guildName = Program.client.GetGuild(guildId).Name;
		if (collection.Find(DocumentFunctions.serverIDFilter(guildId)).CountDocuments() == 0)
		{
			// Create the base BSON Document
			BsonDocument newServerDocument = new BsonDocument
			{
				{ "server_id", BsonValue.Create(guildId) },
				{ "server_name", guildName },
				{ "bot_options", 
					new BsonDocument {
						//* [UPDATE OPTIONS] 
						{"expiry_days", 7},
						{"create_threads", true}
					}
				},
				{ $"{FieldNames.CURRENT_POLLS}", new BsonArray {} }
			};
			await collection.InsertOneAsync(newServerDocument);
		}
	}
}