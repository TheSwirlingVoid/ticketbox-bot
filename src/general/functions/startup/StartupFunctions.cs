using Discord.WebSocket;
using MongoDB.Bson;
using MongoDB.Driver;
using TicketBox;

static class StartupFunctions {
	public static async Task addMissingDocs()
	{
		foreach (var guild in Program.client.Guilds)
		{
			// if the server doc doesn't exist
			if (!DocumentFunctions.serverDocExists(guild.Id))
			{
				await JoinFunctions.createServerDocument(guild.Id);
				Console.WriteLine($"Joined (Delayed) {guild.Name}");
				await Program.sendWelcomeMessage((SocketGuild) guild);
			}
		}
	}
	public static async Task removeUnusedDocs()
	{
		var nullServerIds = new List<ulong>();
		foreach (var document in DocumentFunctions.getServerSettingsDocuments())
		{
			var serverId = Convert.ToUInt64(document["server_id"]);
			SocketGuild? server = Program.client.GetGuild(serverId);
			if (server == null) {
				nullServerIds.Add(serverId);
			}
		}

		// loop through all polls... :(
		foreach (var nullServerId in nullServerIds)
		{
			var filter = DocumentFunctions.serverIDFilter(nullServerId);
			await Program.discordServersCollection.DeleteOneAsync(filter);
			await Program.pollCollection.DeleteManyAsync(filter);
		}
	}
}