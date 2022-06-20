using MongoDB.Bson;
using MongoDB.Driver;
using TicketBox;

static class JoinFunctions {
	public static async Task createServerDocument(IMongoCollection<BsonDocument> collection, MessageScope scopeS)
	{
		// If this server does not have its own document already
		var guildID = scopeS.ServerID;
		var guildName = Program.client.GetGuild(guildID).Name;
		if (collection.Find(DocumentFunctions.serverIDFilter(guildID)).CountDocuments() == 0)
		{
			// Create the base BSON Document
			BsonDocument newServerDocument = new BsonDocument
			{
				{ "server_id", BsonValue.Create(guildID) },
				{ "server_name", guildName },
				{ "bot_options", 
					new BsonDocument {
						//* [UPDATE OPTIONS] 
						{"expiry_days", 7},
						{"create_threads", true}
					}
				},
				{ "current_polls_dualchoice", new BsonArray {} }
			};
			await collection.InsertOneAsync(newServerDocument);
		}
	}
}