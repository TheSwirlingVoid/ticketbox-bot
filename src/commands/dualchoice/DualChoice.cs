using Discord;
using Discord.WebSocket;
using MongoDB.Bson;
using MongoDB.Driver;
using TicketBox;

class DualChoice {
	public DualChoiceCoreData CoreData { get; set; }
	public DualChoiceEmbedData EmbedData { get; set; }
	public decimal TotalVoters { get; set; }
	public decimal PercentUpvoted { get; set; }
	public decimal PercentDownvoted { get; set; }
	public long ExpiryTime {get; set;}
	private bool DisabledButtons { get; set; }
	public IMessage? Message { get; set; }
	public MessageScope messageScope { get; set; }

	private static readonly string[] writeStrings = {"downvotes", "upvotes"};

	public DualChoice(DualChoiceCoreData coreData, BotSettings settings)
	{
		CoreData = coreData;
		EmbedData = new DualChoiceEmbedData(
			"",
			"",
			DateTimeOffset.Now,
			""
		);
		DisabledButtons = false;
		ExpiryTime = getUnixExpiryTimeFromNow(settings.ExpiryDays);

		messageScope = coreData.messageScope;
	}

	public static DualChoice getPollByMessage(BsonDocument document, MessageScope scope)
	{
		var polls = document[$"{FieldNames.CURRENT_POLLS}"].AsBsonArray;
		var index = indexOfPoll(polls, scope.MessageID);

		var poll = polls[index];
		var pollVotes = poll["votes"];

		var coreData = new DualChoiceCoreData(
			poll["poll_text"].AsString,
			scope,
			pollVotes["upvotes"].AsInt32,
			pollVotes["downvotes"].AsInt32

		);

		var settings = BotSettings.getServerSettings(document);
		return new DualChoice(coreData, settings);
	}

	public EmbedBuilder createEmbed()
	{
		var upvotes = this.CoreData.Upvotes;
		var downvotes = this.CoreData.Downvotes;
		var pollText = this.CoreData.PollText;
		var pollDate = this.EmbedData.PollDate;

		/* --------------------- Vote Percentages & Multipliers --------------------- */
		// So that you can't divide by 0
		decimal totalVoters = (upvotes + downvotes);
		decimal votesNonZero = Math.Max(totalVoters, 1);
		decimal percentUpvoted = Math.Round(Convert.ToDecimal((upvotes/votesNonZero)*1000))/10;
		decimal percentDownvoted = Math.Round(Convert.ToDecimal((downvotes/votesNonZero)*1000))/10;

		this.TotalVoters = totalVoters;
		this.PercentUpvoted = percentUpvoted;
		this.PercentDownvoted = percentDownvoted;
		this.EmbedData.PollDate = pollDate;

		// Return the embed build with the data above
		return buildEmbedWithData();
	}

	private EmbedBuilder buildEmbedWithData()
	{

		// Multipliers for emote percentage bars
		int upvotedMultiplier = Convert.ToInt32(this.PercentUpvoted/10);
		int downvotedMultiplier = Convert.ToInt32(this.PercentDownvoted/10);

		var upvoteBar = DualChoiceEmbedData.getBarString(upvotedMultiplier, VoteStyle.UPVOTE);
		var downvoteBar = DualChoiceEmbedData.getBarString(downvotedMultiplier, VoteStyle.DOWNVOTE);

		// footer string (ex. "1 user has voted" vs "2 users have voted")
		var footerString = " users have voted. ";
		if (this.TotalVoters == 1)
			footerString = " user has voted. ";

		// Build final embed
		return new EmbedBuilder()
			.WithDescription("**———————————————**")
			.WithFooter("\n\n"+this.TotalVoters+footerString)
			.WithTimestamp(this.EmbedData.PollDate)
			.AddField("Poll", $"{this.CoreData.PollText}")
				.WithAuthor(new EmbedAuthorBuilder()
					.WithIconUrl(this.EmbedData.UserAvatar)
					.WithName(this.EmbedData.UserName)
				)
			.AddField("Stats", 
				$"{upvoteBar}\n**{this.PercentUpvoted}%** Upvoted"+ 
				$"\n{downvoteBar}\n**{this.PercentDownvoted}%** Downvoted"
			)
			.AddField("Expiry Status", this.EmbedData.ExpiryString);
	}

	public static async Task<Discord.Rest.RestFollowupMessage> createBaseMessage(SocketSlashCommand command, EmbedBuilder messageEmbedBuilder)
	{
		// Defer the response, meaning the bot will recognize that the command was sent
		// but won't finish its response.
		await command.DeferAsync();
		// Follow up the deferrance with a final message response
		return await command.FollowupAsync(embed: messageEmbedBuilder.Build(), components: createButtons(false));
	}

	public long getUnixExpiryTimeFromNow(int expiryDays)
	{
		var expiryDate = DateTimeOffset.Now.Date.AddDays(expiryDays);
		var unixExpiryTime = ((DateTimeOffset) expiryDate).ToUnixTimeSeconds();
		return unixExpiryTime;
	}
	public String getUnixExpiryStringFromNow(int expiryDays)
	{
		var expiryDate = DateTimeOffset.Now.Date.AddDays(expiryDays);
		var stringExpiryDate = $"Expires {expiryDate.Date.ToString("MM/dd/yyyy")}";
		return stringExpiryDate;
	}

	public void saveInitialPoll(BsonDocument document, IMongoCollection<BsonDocument> collection, SocketSlashCommand command)
	{
		// Prepare the structured data for the document's poll list
		BsonDocument pollDocument = new BsonDocument
		{
			{ "user_id", BsonValue.Create(command.User.Id) },
			{ "poll_text", this.CoreData.PollText },
			{ "votes", new BsonDocument {
				{ "upvotes", 0 },
				{ "downvotes", 0 },
				{ "voters", new BsonDocument {} }
			} },
			{ "message_id", BsonValue.Create(this.messageScope.MessageID) },
			{ "channel_id", BsonValue.Create(command.Channel.Id) },
			{"expiry_string", this.EmbedData.ExpiryString },
			{ "unix_expiry_time", this.ExpiryTime }
		};

		// Update the document with new data
		Program.discordServersCollection.UpdateOne(document, Builders<BsonDocument>.Update.AddToSet($"{FieldNames.CURRENT_POLLS}", pollDocument));
	}

	public static int indexOfPoll(BsonArray currentPolls, ulong messageId)
	{
		// Linear search w/ poll indexes
		for (int index = 0; index < currentPolls.Count(); index++)
		{
			var poll = currentPolls[index];
			// If the poll at the current index has the given message ID, return that index
			if (poll["message_id"].AsInt64 == ((long)messageId))
			{
				return index;
			}
		}
		// Return -1 if no poll with the given messageID exists
		return -1;
	}

	public static bool userChoiceStagnant(bool? previousValue, VoteStyle voteType)
	{
		var boolUserChoice = Convert.ToBoolean(voteType);
		return (previousValue == boolUserChoice);
	}

	public static bool? userPreviousValue(BsonDocument document, ulong userId, int index)
	{
		return (bool?)document[$"{FieldNames.CURRENT_POLLS}"][index]["votes"]["voters"].ToBsonDocument().GetValue(userId.ToString(), null);
	}

	public static bool userVoted(bool? previousValue)
	{
		if (previousValue.HasValue)
		{
			return true;
		}
		else
		{
			return false;
		}
	}

	public static string voteNumString(VoteStyle voteType)
	{
		return writeStrings[(short)voteType];
	}

	public static string getUpdateString(int index, VoteStyle voteType)
	{
		return $"{FieldNames.CURRENT_POLLS}.{index}.votes.{voteNumString(voteType)}";
	}

	public async Task updateEmbed(BsonDocument document, int index)
	{
		var channel = (ISocketMessageChannel) Program.client.GetChannel(messageScope.ChannelID);
		var message = await channel.GetMessageAsync(messageScope.MessageID);


		Action<MessageProperties> messageEdit = (m) => {
			
			/* --------------------------- Current Embed Data --------------------------- */
			var poll = document[$"{FieldNames.CURRENT_POLLS}"][index];

			var userId = Convert.ToUInt64(poll["user_id"]);
			var embedAuthor = Program.client.GetUser(userId);
			String username;
			String userAvatar;

			/* ------------------------- Validate User Existence ------------------------ */
			if (embedAuthor == null) {
				username = "Unobtainable User#0000";
				userAvatar = "";
			}
			else {
				username = embedAuthor.Username;
				userAvatar = embedAuthor.GetAvatarUrl();
			}

			var expiryString = (string)poll["expiry_string"];

			if (this.DisabledButtons)
			{
				m.Components = createButtons(true);
			}
			// default expiry string (the date format)
			if (this.EmbedData.ExpiryString == "") {
				this.EmbedData.ExpiryString = expiryString;
			}

			this.EmbedData.UserAvatar = userAvatar;
			this.EmbedData.UserName = username;
			this.EmbedData.PollDate = message.CreatedAt;

			/* ------------------------------ Create Embed ------------------------------ */
			m.Embeds = new[] { createEmbed().Build() };
		};

		await channel.ModifyMessageAsync(message.Id, m => messageEdit(m));
	}

	private static MessageComponent createButtons(bool disabled)
	{
		return new ComponentBuilder()
			.AddRow(
				new ActionRowBuilder()
					.WithButton("Upvote", "upvote-poll-dc", ButtonStyle.Primary, Emoji.Parse(":thumbsup:"), disabled: disabled)
					.WithButton("Downvote", "downvote-poll-dc", ButtonStyle.Primary, Emoji.Parse(":thumbsdown:"), disabled: disabled)
			)
			.AddRow(
				new ActionRowBuilder()
					.WithButton("Close Voting", "close-poll-dc", ButtonStyle.Danger, Emoji.Parse(":checkered_flag:"), disabled: disabled)
			).Build();
	}

	public static async Task removePollData(IMongoCollection<BsonDocument> collection, MessageScope scope)
	{
		var serverId = scope.ServerID;
		var messageId = scope.MessageID;

		var filter = DocumentFunctions.serverIDFilter(serverId);
		var document = Program.discordServersCollection.Find(filter).ToList()[0];

		var dualChoicePolls = document[$"{FieldNames.CURRENT_POLLS}"];
		int index = DualChoice.indexOfPoll(dualChoicePolls.AsBsonArray, messageId);
		await DualChoice.removePollData(collection, filter, index);
	}

	private static async Task removePollData(IMongoCollection<BsonDocument> collection, FilterDefinition<BsonDocument> filter, int index)
	{
		// make everything that needs to be removed null for removal
		var unsetInstruction = Builders<BsonDocument>.Update.Unset($"{FieldNames.CURRENT_POLLS}.{index}");
		// remove all null
		var pullInstruction = Builders<BsonDocument>.Update.PullAll($"{FieldNames.CURRENT_POLLS}", new string?[] { null });

		await collection.UpdateOneAsync(filter, unsetInstruction);
		await collection.UpdateOneAsync(filter, pullInstruction);
	}
	
	public async Task close(IMongoCollection<BsonDocument> collection, SocketMessageComponent messageComponent)
	{
		this.EmbedData.ExpiryString = $"Closed by {messageComponent.User}";

		await terminate(collection);
		await messageComponent.RespondAsync("Poll successfully closed!", ephemeral: true);
	}

	public async Task expire(IMongoCollection<BsonDocument> collection)
	{
		this.EmbedData.ExpiryString = "Expired";
		await terminate(collection);
	}

	private async Task terminate(IMongoCollection<BsonDocument> collection)
	{
		var serverId = this.messageScope.ServerID;//scopeSCM.ServerID;
		var messageId = this.messageScope.MessageID;//scopeSCM.MessageID;
		var channelId = this.messageScope.ChannelID;

		var channel = (ISocketMessageChannel) Program.client.GetGuild(serverId).GetChannel(channelId);
		var message = await channel.GetMessageAsync(messageId);

		var document = DocumentFunctions.getServerDocument(collection, serverId);
		var index = DualChoice.indexOfPoll(document[$"{FieldNames.CURRENT_POLLS}"].AsBsonArray, messageId);
		//var poll = document[$"{FieldNames.CURRENT_POLLS}"][index];
		//var pollVotes = poll["votes"];

		var settings = BotSettings.getServerSettings(document);
		// var coreData = new DualChoiceCoreData(
		// 	poll["poll_text"].AsString,
		// 	pollVotes["upvotes"].AsInt32,
		// 	pollVotes["downvotes"].AsInt32

		// );
		//dualChoice = new DualChoice(coreData, settings, true);
		this.DisabledButtons = true;
		await this.updateEmbed(document, index);
		await DualChoice.removePollData(collection, messageScope);
	}

	public static int voteValue(BsonDocument document, int index, VoteStyle voteType)
	{
		return document[$"{FieldNames.CURRENT_POLLS}"][index]["votes"][writeStrings[(int)voteType]].ToInt32();
	}
}

enum VoteStyle {
	DOWNVOTE,
	UPVOTE
}