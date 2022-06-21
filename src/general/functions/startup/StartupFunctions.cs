using MongoDB.Bson;
using MongoDB.Driver;
using TicketBox;

static class StartupFunctions {
	public static async Task addMissingDocs(IMongoCollection<BsonDocument> collection)
	{
		foreach (var guild in Program.client.Guilds)
		{
			// if the server doc doesn't exist
			if (!DocumentFunctions.serverDocExists(collection, guild.Id))
			{
				await JoinFunctions.createServerDocument(collection, guild.Id);
				Console.WriteLine($"Joined {guild.Name}");
			}
		}
	}
}