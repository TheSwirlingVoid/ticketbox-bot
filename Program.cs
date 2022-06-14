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
			client.Ready += client_ready;
			client.JoinedGuild += JoinedGuild;

			client.SlashCommandExecuted += SlashCommandHandler;
			client.ButtonExecuted += ButtonExecuted;
			client.MessageDeleted += MessageDeleted;
			

			//* DO NOT PLACE TOKEN HERE FOR SECURITY W/ PUBLIC REPOSITORIES
			string token = File.ReadAllText("token.txt");

			await client.LoginAsync(TokenType.Bot, token);
			await client.StartAsync();

			// Wait until program is closed
			await client.SetStatusAsync(UserStatus.Online);
			await statusUpdater();

			await expiredPollDeleter(discordServersCollection);

			await Task.Delay(-1);

		}

		private async Task MessageDeleted(Cacheable<IMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel)
		{
			await DualChoiceFunctions.removePollData(discordServersCollection, message.Id, ((SocketGuildChannel) channel.Value).Guild.Id);
		}

		public static async Task expiredPollDeleter(IMongoCollection<BsonDocument> collection)
		{
			while (true)
			{
				long unixTimeNow = DateTimeOffset.Now.ToUnixTimeSeconds();
				var timeFilter = Builders<BsonDocument>.Filter.Gte("unix_expiry_time", unixTimeNow);
				var documents = collection.Find(timeFilter).ToList();
				// foreach DOCUMENT
				foreach (var document in documents)
				{
					// foreach of its POLLS
					var polls_DC = document["current_polls_dualchoice"].AsBsonArray.ToList();
					for (int index = 0; index > polls_DC.Count; index++)
					{
						var poll = polls_DC[index];
						var expiryTime = poll["unix_expiry_time"];
						if (expiryTime == unixTimeNow)
						{
							ulong serverId = (ulong) document["server_id"].AsInt64;
							ulong channelId = (ulong) poll["channel_id"].AsInt64;
							ulong messageId = (ulong) poll["message_id"].AsInt64;
							// delete the message
							await ((ISocketMessageChannel) client.GetGuild(serverId).GetChannel(channelId)).DeleteMessageAsync(messageId);
							// remove it from MongoDB
							await DualChoiceFunctions.removePollData(document, index);
						}
					}
				}

				// checks every hour
				await Task.Delay(TimeSpan.FromHours(1));
			}
		}

		private Task Log(LogMessage msg) 
		{
			Console.WriteLine(msg.ToString());
			return Task.CompletedTask;
		}

		public static FilterDefinition<BsonDocument> serverIDFilter(ulong guildId)
		{
			return Builders<BsonDocument>.Filter.Eq("server_id", guildId);
		}

		private async Task client_ready()
		{
			var guild = client.GetGuild(837935655258554388);

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
				await guild.BulkOverwriteApplicationCommandAsync(commands.ToArray());

				//* CLEAR GLOBAL CMDS FOR NOW, TRANSFER LATER
				await client.BulkOverwriteGlobalApplicationCommandsAsync(commands.ToArray());
			}
			catch (Exception e)
			{
				Console.WriteLine(e.ToString());
			}
			/* -------------------------------------------------------------------------- */
		}

		public async Task statusUpdater()
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
				await Task.Delay(TimeSpan.FromMinutes(5));
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
			// If this server does not have its own document already
			if (discordServersCollection.Find(serverIDFilter(guild.Id)).CountDocuments() == 0)
			{
				// Create the base BSON Document
				BsonDocument newServerDocument = new BsonDocument
				{
					{ "server_id", BsonValue.Create(guild.Id) },
					{ "server_name", guild.Name },
					{ "bot_options", 
						new BsonDocument { 
							// {"show_votes", true} 
						}
					},
					{ "current_polls_dualchoice", new BsonArray {} }
				};
				await discordServersCollection.InsertOneAsync(newServerDocument);
			}
		}

		private async Task ButtonExecuted(SocketMessageComponent messageComponent)
		{
			switch (messageComponent.Data.CustomId)
			{
				case "upvote-poll" or "downvote-poll":
				try
				{
					await ButtonHandlers.VoteButton(messageComponent, messageComponent.Data.CustomId);
				}
				catch(Discord.Net.HttpException e)
				{
					if (e.DiscordCode != DiscordErrorCode.CannotSendEmptyMessage)
					{
						// code for discarding "cannot send an empty message" error
					}
				}
				break;
			}
		}
	}
}