using Discord;
using Discord.WebSocket;
using MongoDB.Bson;
using MongoDB.Driver;
using TicketBox;

class DualChoice {

	//TODO: MOVE TO SUBCLASS
	public DualChoiceCoreData CoreData { get; set; }

	//TODO: MOVE TO SUBCLASS
	public DualChoiceEmbedData EmbedData { get; set; }

	public long ExpiryTime {get; set;}
	private bool DisabledButtons { get; set; }
	public MessageScope messageScope { get; set; }

	private static readonly string[] writeStrings = {"downvotes", "upvotes"};

	//TODO: MOVE TO SUBCLASS
	public DualChoice(DualChoiceCoreData coreData, BotSettings settings)
	{
		CoreData = coreData;
		EmbedData = new DualChoiceEmbedData(
			"",
			"",
			"",
			DateTimeOffset.Now,
			""
		);
		DisabledButtons = false;
		ExpiryTime = getUnixExpiryTimeFromNow(settings.ExpiryDays);

		messageScope = coreData.messageScope;
	}

	//TODO: MOVE TO SUBCLASS
	public static DualChoice getPollByMessage(BsonDocument serverDoc, MessageScope scope)
	{
		var poll = DocumentFunctions.getPollDocument(scope);

		var pollVotes = poll["votes"];

		var coreData = new DualChoiceCoreData(
			scope,
			pollVotes["upvotes"].AsInt32,
			pollVotes["downvotes"].AsInt32
		);

		var settings = BotSettings.getServerSettings(serverDoc);
		return new DualChoice(coreData, settings);
	}

	//TODO: MOVE TO SUBCLASS
	public Embed createEmbed()
	{
		var upvotes = this.CoreData.Upvotes;
		var downvotes = this.CoreData.Downvotes;

		/* --------------------- Vote Percentages & Multipliers --------------------- */
		// So that you can't divide by 0
		decimal totalVoters = (upvotes + downvotes);
		decimal votesNonZero = Math.Max(totalVoters, 1);
		decimal percentUpvoted = Math.Round(Convert.ToDecimal((upvotes/votesNonZero)*1000))/10;
		decimal percentDownvoted = Math.Round(Convert.ToDecimal((downvotes/votesNonZero)*1000))/10;

		// Return the embed build with the data above
		// Multipliers for emote percentage bars
		int upvotedMultiplier = Convert.ToInt32(percentUpvoted/10);
		int downvotedMultiplier = Convert.ToInt32(percentDownvoted/10);

		var upvoteBar = DualChoiceEmbedData.getBarString(upvotedMultiplier, VoteStyle.UPVOTE);
		var downvoteBar = DualChoiceEmbedData.getBarString(downvotedMultiplier, VoteStyle.DOWNVOTE);

		// footer string (ex. "1 user has voted" vs "2 users have voted")
		var footerString = " users have voted. ";
		if (totalVoters == 1)
			footerString = " user has voted. ";

		/* ---------------------------- Build final embed --------------------------- */
		return new EmbedBuilder()
			.WithDescription("**———————————————**")
			.WithFooter("\n\n"+totalVoters+footerString)
			.WithTimestamp(this.EmbedData.PollDate)
			.WithAuthor(new EmbedAuthorBuilder()
				.WithIconUrl(this.EmbedData.UserAvatar)
				.WithName(this.EmbedData.UserName)
			)
			.AddField("Poll", $"{this.EmbedData.PollText}")
			.AddField("Stats", 
				$"{upvoteBar}\n**{percentUpvoted}%** Upvoted"+ 
				$"\n{downvoteBar}\n**{percentDownvoted}%** Downvoted"
			)
			.AddField("Expiry Status", this.EmbedData.ExpiryString)
			.Build();
	}

	public static async Task<Discord.Rest.RestFollowupMessage> createMessage(SocketSlashCommand command, Embed messageEmbed)
	{
		// Follow up the deferrance with a final message response
		return await command.FollowupAsync(embed: messageEmbed, components: createButtons(false));
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

	public void saveInitialPoll(SocketSlashCommand command)
	{
		
		// Prepare the structured data for the document's poll list
		BsonDocument pollDocument = new BsonDocument
		{
			{ "server_id", BsonValue.Create(command.GuildId.GetValueOrDefault()) },
			{ "votes", new BsonDocument {
				{ "upvotes", 0 },
				{ "downvotes", 0 },
				{ "voters", new BsonDocument {} }
			} },
			{ "message_id", BsonValue.Create(this.messageScope.MessageID) },
			{ "channel_id", BsonValue.Create(command.Channel.Id) },
			{ "unix_expiry_time", this.ExpiryTime }
		};

		// Update the document with new data
		Program.pollCollection.InsertOne(pollDocument);
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

	public static bool? userPreviousValue(BsonDocument pollDoc, ulong userId)
	{
		return (bool?)pollDoc["votes"]["voters"].ToBsonDocument().GetValue(userId.ToString(), null);
	}

	public static bool userVoted(bool? previousValue)
	{
		return previousValue.HasValue;
	}

	public static async Task<bool> deleteIfEmbedDeleted(IMessage message)
	{
		var currentEmbeds = message.Embeds;
		// no point in updating the embed if the user didn't want it there ;)
		if (currentEmbeds.Count == 0)
		{
			// delete the poll msg if it's invalid
			await message.DeleteAsync();
			return true;
		}
		else {
			return false;
		}
	}

	//TODO: MOVE TO SUBCLASS
	public async Task updateEmbed()
	{
		var channel = (ISocketMessageChannel) Program.client.GetChannel(messageScope.ChannelID);
		var message = await channel.GetMessageAsync(messageScope.MessageID);

		await channel.ModifyMessageAsync(message.Id, (m) => {
			/* --------------------------- Current Embed Data --------------------------- */
			var currentEmbed = message.Embeds.First();
			var embedAuthor = currentEmbed.Author.GetValueOrDefault();
			this.EmbedData.PollText = currentEmbed.Fields[0].Value;

			String username = embedAuthor.ToString();
			String userAvatar = embedAuthor.IconUrl;

			if (this.DisabledButtons)
			{
				m.Components = createButtons(true);
			}
			// default expiry string (the date format)
			if (this.EmbedData.ExpiryString == "") {
				this.EmbedData.ExpiryString = currentEmbed.Fields[2].Value;
			}

			this.EmbedData.UserAvatar = userAvatar;
			this.EmbedData.UserName = username;
			this.EmbedData.PollDate = message.CreatedAt;

			/* ------------------------------ Create Embed ------------------------------ */
			m.Embeds = new[] { createEmbed() };
		});
	}

	//TODO: MOVE TO SUBCLASS
	private static MessageComponent createButtons(bool disabled)
	{
		return new ComponentBuilder()
			.AddRow(
				new ActionRowBuilder()
					.WithButton(label: "Upvote", customId: "upvote-poll-dc", emote: Emote.Parse("<:upvote:988965597117296670>"), style: ButtonStyle.Primary, disabled: disabled)
					.WithButton(label: "Downvote", customId: "downvote-poll-dc", emote: Emote.Parse("<:downvote:988965570697396255>"), style: ButtonStyle.Primary, disabled: disabled)
			)
			.AddRow(
				new ActionRowBuilder()
					.WithButton(label: "Retract Vote", customId: "retractvote-poll-dc", emote: Emote.Parse("<:retract_vote:988938631643267083>"), style: ButtonStyle.Secondary, disabled: disabled)
					.WithButton(label: "Close Voting", customId: "close-poll-dc", emote: Emote.Parse("<:close_poll:988923692442615889>"), style: ButtonStyle.Danger, disabled: disabled)
			).Build();
	}

	public UpdateDefinition<BsonDocument> getRetractUpdate(bool previousValue, SocketMessageComponent messageComponent)
	{
		var update = Builders<BsonDocument>.Update.Unset(
			$"votes.voters.{messageComponent.User.Id.ToString()}"
		);

		if (previousValue == true)
		{
			this.CoreData.Upvotes--;
			update = update.Set($"votes.upvotes", this.CoreData.Upvotes);
		}
		else
		{
			this.CoreData.Downvotes--;
			update = update.Set($"votes.downvotes", this.CoreData.Downvotes);
		}
		return update;
	}

	public static async Task removePollData(BsonDocument pollDocument)
	{
		await Program.pollCollection.DeleteOneAsync(pollDocument);
	}
	
	public async Task close(BsonDocument pollDocument, SocketMessageComponent messageComponent)
	{
		this.EmbedData.ExpiryString = $"Closed by {messageComponent.User}";

		await terminate(pollDocument);
		await messageComponent.FollowupAsync(Messages.POLL_CLOSE_SUCCESS, ephemeral: true);
	}

	public async Task expire(BsonDocument pollDocument)
	{
		this.EmbedData.ExpiryString = "Expired";
		await terminate(pollDocument);
	}

	private async Task terminate(BsonDocument pollDocument)
	{
		var serverId = this.messageScope.ServerID;
		var messageId = this.messageScope.MessageID;
		var channelId = this.messageScope.ChannelID;

		var channel = (ISocketMessageChannel) Program.client.GetGuild(serverId).GetChannel(channelId);
		var message = await channel.GetMessageAsync(messageId);

		this.DisabledButtons = true;
		await this.updateEmbed();
		await DualChoice.removePollData(pollDocument);
	}

	public static int voteValue(BsonDocument document, VoteStyle voteType)
	{
		return document["votes"][writeStrings[(int)voteType]].AsInt32;
	}
}

enum VoteStyle {
	DOWNVOTE,
	UPVOTE
}