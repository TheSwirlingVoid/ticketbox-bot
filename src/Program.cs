using Discord;
using Discord.WebSocket;
using MongoDB.Bson;
using MongoDB.Driver;

namespace TicketBox
{
	class Program
	{
		
		public static void Main(string[] args) => new Program().MainAsync().GetAwaiter().GetResult();

		private DiscordSocketConfig config = new DiscordSocketConfig 
		{
			MessageCacheSize = 100
		};
		public static DiscordSocketClient client = new DiscordSocketClient();
		private static readonly string connectionString = "mongodb://localhost:27017/";
		public static MongoClient mongoClient = new MongoClient(connectionString);
		public static IMongoDatabase database = mongoClient.GetDatabase("ticketbox-bot");
		public static IMongoCollection<BsonDocument> discordServersCollection = database.GetCollection<BsonDocument>("DiscordServers");

		public async Task MainAsync()
		{

			client.Log += Log; // add the log function to handle log events
			client.Ready += ClientReady;
			client.JoinedGuild += JoinedGuild;
			client.LeftGuild += LeftGuild;

			client.SlashCommandExecuted += SlashCommandHandler;
			client.ButtonExecuted += ButtonExecuted;
			client.MessageDeleted += MessageDeleted;
			

			//* DO NOT PLACE TOKEN HERE FOR SECURITY W/ PUBLIC REPOSITORIES
			string token = File.ReadAllText("token.txt");

			await client.LoginAsync(TokenType.Bot, token);
			await client.StartAsync();

			// Wait until program is closed
			await client.SetStatusAsync(UserStatus.Online);

			// ignore these warnings–these functions need to run concurrently
			statusUpdater(60);

			expiredPollDeleter(discordServersCollection, TimeSpan.FromHours(1));

			await Task.Delay(-1);

		}

		private async Task MessageDeleted(Cacheable<IMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel)
		{
			var messageScope = new MessageScope()
				.serverId(((SocketGuildChannel) channel.Value).Guild.Id)
				.messageId(message.Id);

			await DualChoiceFunctions.removePollData(discordServersCollection, messageScope);
		}

		public static async Task expiredPollDeleter(IMongoCollection<BsonDocument> collection, TimeSpan timeSpan)
		{
			while (true)
			{
				long unixTimeNow = DateTimeOffset.Now.ToUnixTimeSeconds();
				var timeFilter = Builders<BsonDocument>.Filter.Lte("current_polls_dualchoice.unix_expiry_time", unixTimeNow);
				var documents = collection.Find(timeFilter).ToList();
				// foreach DOCUMENT
				foreach (var document in documents)
				{
					// foreach of its POLLS
					var polls_DC = document["current_polls_dualchoice"].AsBsonArray.ToList();
					for (int index = 0; index < polls_DC.Count; index++)
					{
						var poll = polls_DC[index];
						var expiryTime = poll["unix_expiry_time"];
						if (unixTimeNow >= expiryTime)
						{
							ulong serverId = (ulong) document["server_id"].AsInt64;
							var server = client.GetGuild(serverId);
							if (client.Guilds.Contains(server))
							{
								ulong channelId = (ulong) poll["channel_id"].AsInt64;
								ulong messageId = (ulong) poll["message_id"].AsInt64;
								var channel = server.GetTextChannel(channelId);
								var message = await channel.GetMessageAsync(messageId);
								// expire the poll
								var scopeSCM = new MessageScope()
									.serverId(serverId)
									.channelId(channel.Id)
									.messageId(messageId);
								
								await ButtonHandlers.ExpireDCPoll(discordServersCollection, scopeSCM);
							}
						}
					}
				}
				// checks every hour
				await Task.Delay(timeSpan);
			}
		}

		private Task Log(LogMessage msg) 
		{
			Console.WriteLine(msg.ToString());
			return Task.CompletedTask;
		}

		

		public static BsonDocument getServerDocument(IMongoCollection<BsonDocument> collection, ulong serverId)
		{
			return collection.Find(DocumentFunctions.serverIDFilter(serverId)).ToList()[0];
		}

		private async Task ClientReady()
		{
			// my very good server!
			var celestialCentral = client.GetGuild(837935655258554388);

			/* -------------------------------- Commands -------------------------------- */
			List<ApplicationCommandProperties> commands = new();

			// POLL COMMAND
			var pollDC_command = new SlashCommandBuilder()
				.WithName(BotCommands.POLL)
				.WithDescription("Create a poll!")
				.AddOption(new SlashCommandOptionBuilder()
					.WithName("dualchoice")
					.WithDescription("Create a poll with upvote and downvote vote choices!")
					.WithType(ApplicationCommandOptionType.SubCommand)
					.AddOption(new SlashCommandOptionBuilder()
						.WithName("query")
						.WithDescription("Enter your question, suggestion, or query!")
						.WithType(ApplicationCommandOptionType.String)
						.WithRequired(true)
					)
				).Build();
			commands.Add(pollDC_command);
			/* -------------------------- Commmand Registering -------------------------- */
			try 
			{	
				// SLASH COMMAND CREATION
				await celestialCentral.BulkOverwriteApplicationCommandAsync(commands.ToArray());

				//* GLOBAL COMMANDS–OMIT WHEN TESTING
				//await client.BulkOverwriteGlobalApplicationCommandsAsync(commands.ToArray());
			}
			catch (Exception e)
			{
				Console.WriteLine(e.ToString());
			}
			/* -------------------------- Add Missing Documents ------------------------- */
			await StartupFunctions.addMissingDocs(discordServersCollection);
		}

		public async Task statusUpdater(int minutesToChange)
		{
			var filename = "statuses.txt";
			while (true)
			{
				int numLines = countFileLines(filename);
				var infile = File.OpenRead(filename);
				var sr = new StreamReader(infile);
				Random rand = new Random();
				int chosenStatusNum = rand.Next(0, numLines-1);
				for (int i = 0; i < chosenStatusNum; i++)
				{
					await sr.ReadLineAsync();
				}
				var status = await sr.ReadLineAsync();
				await client.SetGameAsync(status, null, ActivityType.Playing);
				await Task.Delay(TimeSpan.FromMinutes(minutesToChange));
			}
		}

		public int countFileLines(string filename)
		{
			int numLines = 0;
			var infile = File.OpenRead(filename);
			var sr = new StreamReader(infile);
			while (sr.ReadLine() != null)
			{
				numLines++;
			}
			return numLines;
		}

		private async Task SlashCommandHandler(SocketSlashCommand command)
		{
			var options = command.Data.Options.ToArray();
			switch(command.CommandName)
			{
				case "poll":
				var firstOption = options.First();
				switch (firstOption.Name)
				{
					case "dualchoice":
					await CommandHandlers.PollDualChoiceCommand(command, (string)firstOption.Options.First().Value);
					break;
				}
				break;
			}
		}

		private async Task JoinedGuild(SocketGuild guild)
		{
			// Create the server's document
			await JoinFunctions.createServerDocument(discordServersCollection, 
				new MessageScope()
					.serverId(guild.Id)
			);
		}

		private async Task LeftGuild(SocketGuild guild)
		{
			var filter = DocumentFunctions.serverIDFilter(guild.Id);
			await discordServersCollection.DeleteManyAsync(filter);
		}

		private async Task ButtonExecuted(SocketMessageComponent messageComponent)
		{
			switch (messageComponent.Data.CustomId)
			{
				case "upvote-poll-dc":
					try
					{
						await ButtonHandlers.VoteButton(discordServersCollection, messageComponent, VoteStyle.UPVOTE);
					}
					catch(Discord.Net.HttpException) { }
					break;

				case "downvote-poll-dc":
					try
					{
						await ButtonHandlers.VoteButton(discordServersCollection, messageComponent, VoteStyle.DOWNVOTE);
					}
					catch(Discord.Net.HttpException) { }
					break;

				case "close-poll-dc":
					var permissions = ((SocketGuildUser) messageComponent.User).GuildPermissions;
					if (permissions.ManageMessages || permissions.Administrator)
						await ButtonHandlers.CloseDCPoll(discordServersCollection, messageComponent);
					else {
						await messageComponent.RespondAsync("You need the **Manage Messages** permission or need to be an **Administrator** to close polls!", ephemeral: true);
					}
					break;
			}
		}
	}
}