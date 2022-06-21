using MongoDB.Bson;
using MongoDB.Driver;
using TicketBox;

static class StartupFunctions {
	public static async Task addMissingDocs()
	{
		foreach (var guild in Program.client.Guilds)
		{
			// if the server doc doesn't exist
			if (!DocumentFunctions.serverDocExists(Program.discordServersCollection, guild.Id))
			{
				await JoinFunctions.createServerDocument(guild.Id);
				Console.WriteLine($"Joined (Delayed) {guild.Name}");
			}
		}
	}
}