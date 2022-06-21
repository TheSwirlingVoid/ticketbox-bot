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
		public static IMongoCollection<BsonDocument> pollCollection = database.GetCollection<BsonDocument>("Polls");

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
			string token = File.ReadAllText("src/token.txt");

			await client.LoginAsync(TokenType.Bot, token);
			await client.StartAsync();

			// Wait until program is closed
			await client.SetStatusAsync(UserStatus.Online);

			await Task.Delay(-1);

		}

		private async Task MessageDeleted(Cacheable<IMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel)
		{
			var socketChannel = (SocketGuildChannel) channel.Value;
			var serverId = socketChannel.Guild.Id;

			var messageScope = new MessageScope(
				serverId,
				socketChannel.Id,
				message.Id
			);

			// var document = DocumentFunctions.getServerDocument(discordServersCollection, serverId);
			var pollDocument = DocumentFunctions.getPollDocument(messageScope);

			if (pollDocument != new BsonDocument{})
				await DualChoice.removePollData(pollDocument);
		}

		public static async Task expiredPollDeleter(TimeSpan timeSpan)
		{
			while (true)
			{
				long unixTimeNow = DateTimeOffset.Now.ToUnixTimeSeconds();
				var timeFilter = Builders<BsonDocument>.Filter.Lte("unix_expiry_time", unixTimeNow);
				var documents = await pollCollection.Find(timeFilter).ToListAsync();
				// foreach DOCUMENT
				foreach (var document in documents)
				{
					var expiryTime = document["unix_expiry_time"];
					// if poll is expired
					if (unixTimeNow >= expiryTime)
					{
						/* ------------------------- Context for Poll Object ------------------------ */
						ulong serverId = (ulong) document["server_id"].AsInt64;
						ulong channelId = (ulong) document["channel_id"].AsInt64;
						ulong messageId = (ulong) document["message_id"].AsInt64;
						// expire the poll
						/* ----------------------------- Get Poll Object ---------------------------- */
						var messageScope = new MessageScope(
							serverId,
							channelId,
							messageId
						);

						var coreData = new DualChoiceCoreData(
							document["poll_text"].AsString,
							messageScope,
							document["votes"]["upvotes"].AsInt32,	
							document["votes"]["downvotes"].AsInt32
						);
						var dualChoice = new DualChoice(
							coreData,
							BotSettings.getServerSettings(
								DocumentFunctions.getServerDocument(serverId)
							)
						);
						/* ------------------------------- Expire Poll ------------------------------ */
						await dualChoice.expire(document);
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

		private async Task ClientReady()
		{
			// my very good server!
			var celestialCentral = client.GetGuild(837935655258554388);

			/* -------------------------------- Commands -------------------------------- */
			List<ApplicationCommandProperties> commands = new CommandList().getCommands();

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
			await StartupFunctions.addMissingDocs();

			// ignore these warnings–these functions need to run concurrently
			//statusUpdater(60);
			numServersStatus(TimeSpan.FromMinutes(30));
			Console.WriteLine("started statusUpdater() or numServersStatus()");

			expiredPollDeleter(TimeSpan.FromHours(1));
			Console.WriteLine("started expiredPollDeleter()");
		}

		public async Task statusUpdater(int minutesToChange)
		{
			var filename = "src/statuses.txt";
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

		public async Task numServersStatus(TimeSpan repeatDelay)
		{
			while (true)
			{
				await numServersStatus();
				await Task.Delay(repeatDelay);
			}
		}

		public async Task numServersStatus()
		{
			await Task.Delay(TimeSpan.FromSeconds(1));
			var numServers = client.Guilds.Count;
			await client.SetGameAsync($"in {numServers} servers | /poll, /settings", null, ActivityType.Playing);
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
			var serverDoc = DocumentFunctions.getServerDocument(command.GuildId.GetValueOrDefault());
			var settings = BotSettings.getServerSettings(serverDoc);

			// get the subCommand
			var subCommand = options.First();
			var subCommandName = subCommand.Name;
			var commandParameters = subCommand.Options.ToArray();

			switch(command.CommandName)
			{
				case "poll":
					switch (subCommandName)
					{
						case "dualchoice":
							await CommandHandlers.PollDualChoiceCommand(command, settings, (string)commandParameters[0].Value);
						break;
					}
				break;

				case "settings":
					await CommandHandlers.SettingsCommand(
						new DocumentWithCollection(discordServersCollection, serverDoc),
						command,
						options
					);
				break;
			}
		}

		private async Task JoinedGuild(SocketGuild guild)
		{
			/* ----------------------------- Server Document ---------------------------- */
			// Create the server's document
			await JoinFunctions.createServerDocument(guild.Id);
			/* ----------------------------- Welcome Message ---------------------------- */
			await sendWelcomeMessage(guild);
			await numServersStatus();
		}

		public static async Task sendWelcomeMessage(SocketGuild guild)
		{
			var welcomeText = File.ReadAllText("src/welcome_message.txt");
			await guild.SystemChannel.SendMessageAsync(welcomeText);
		}

		private async Task LeftGuild(SocketGuild guild)
		{
			var filter = DocumentFunctions.serverIDFilter(guild.Id);
			await discordServersCollection.DeleteOneAsync(filter);
			await pollCollection.DeleteManyAsync(filter);
			await numServersStatus();
		}

		private async Task ButtonExecuted(SocketMessageComponent messageComponent)
		{
			var serverDoc = DocumentFunctions.getServerDocument(
								messageComponent.GuildId.GetValueOrDefault()
							);
			var messageScope = new MessageScope(
								messageComponent.GuildId.GetValueOrDefault(),
								messageComponent.ChannelId.GetValueOrDefault(),
								messageComponent.Message.Id
							);
			switch (messageComponent.Data.CustomId)
			{
				case "upvote-poll-dc":
					try
					{
						await ButtonHandlers.VoteButton(
							BotSettings.getServerSettings(serverDoc),
							messageComponent,
							VoteStyle.UPVOTE
						);
					}
					catch(Discord.Net.HttpException) { }
				break;

				case "downvote-poll-dc":
					try
					{
						await ButtonHandlers.VoteButton(
							BotSettings.getServerSettings(serverDoc),
							messageComponent,
							VoteStyle.DOWNVOTE
						);
					}
					catch(Discord.Net.HttpException) { }
				break;

				case "close-poll-dc":
					var user = (SocketGuildUser) messageComponent.User;
					if (Permissions.userCanClosePoll(user))
						/* --------------------------- Get and Close Poll --------------------------- */
						await DualChoice.getPollByMessage(serverDoc,messageScope)
							.close(
								DocumentFunctions.getPollDocument(messageScope),
								messageComponent
							);
						/* -------------------------------------------------------------------------- */
					else {
						await messageComponent.RespondAsync("You need the **Manage Messages** permission or need to be an **Administrator** to close polls!", ephemeral: true);
					}
				break;
			}
		}
	}
}