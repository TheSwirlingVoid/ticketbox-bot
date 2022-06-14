using Discord;
using Discord.WebSocket;
using MongoDB.Bson;
using MongoDB.Driver;
using TicketBox;

static class DualChoiceFunctions {
	private static string[] writeStrings = {"downvotes", "upvotes"};
	public static async Task<Discord.Rest.RestFollowupMessage> createMessage(SocketSlashCommand originalCommand, EmbedBuilder messageEmbedBuilder)
	{
		// Defer the response, meaning the bot will recognize that the command was sent
		// but won't finish its response.
		await originalCommand.DeferAsync();
		// Follow up the deferrance with a final message response
		return await originalCommand.FollowupAsync(embed: messageEmbedBuilder.Build(), components: 
			new ComponentBuilder()
			.AddRow(
				new ActionRowBuilder()
					.WithButton(Emoji.Parse(":thumbsup:").ToString(), "upvote-poll", ButtonStyle.Primary)
					.WithButton(Emoji.Parse(":thumbsdown:").ToString(), "downvote-poll", ButtonStyle.Primary)
			).Build()
		);
	}

	public static EmbedBuilder createEmbed(SocketInteraction interaction, SocketMessage? message, SuggestData embedData)
	{
		var upvotes = embedData.Upvotes;
		var downvotes = embedData.Downvotes;
		var pollText = embedData.PollText;
		/* ----------------- Embed Data (Based on Message Existence) ---------------- */
		// Get avatar, name, etc. based on whether the embed has already been created
		var userAvatar = interaction.User.GetAvatarUrl();
		var userName = interaction.User.ToString();
		var pollDate = interaction.CreatedAt;
		// if message was null, expiry date won't be
		var expiryDate = embedData.ExpiryDate;
		if (message != null) 
		{
			var embed = message.Embeds.ToArray()[0];
			var embedAuthor = embed.Author.GetValueOrDefault();

			userAvatar = embedAuthor.IconUrl;
			userName = embedAuthor.Name;
			expiryDate = embed.Fields[2].Value;
			
			pollDate = message.CreatedAt;
		}

		/* --------------------- Vote Percentages & Multipliers --------------------- */
		// So that you can't divide by 0
		decimal totalVoters = (upvotes + downvotes);
		decimal votesNonZero = Math.Max(totalVoters, 1);
		decimal percentUpvoted = Math.Round(Convert.ToDecimal((upvotes/votesNonZero)*1000))/10;
		decimal percentDownvoted = Math.Round(Convert.ToDecimal((downvotes/votesNonZero)*1000))/10;

		embedData
			.pollText(pollText)
			.userAvatar(userAvatar)
			.userName(userName)
			.totalVoters(totalVoters)
			.percentUpvoted(percentUpvoted)
			.percentDownvoted(percentDownvoted)
			.pollDate(pollDate)
			.expiryDate(expiryDate);

		// Return the embed build with the data above
		return buildEmbedWithData(embedData);
	}

	public static void saveInitialPoll(IMongoCollection<BsonDocument> collection, ulong guildId, SocketSlashCommand command, ulong messageId, string pollText, long expiryTime)
	{
		// Find the document by filtering by server ID
		var serverFilter = Program.serverIDFilter(guildId);
		// Get the server's mongoDB Document
		var serverDocument = collection.Find(serverFilter).ToList()[0];

		// Prepare the structured data for the document's poll list
		BsonDocument pollDocument = new BsonDocument
		{
			{ "user_id", BsonValue.Create(command.User.Id) },
			{ "poll_text", pollText },
			{ "votes", new BsonDocument {
				{ "upvotes", 0 },
				{ "downvotes", 0 },
				{ "voters", new BsonDocument {} }
			} },
			{ "message_id", BsonValue.Create(messageId) },
			{ "channel_id", BsonValue.Create(command.Channel.Id) },
			{ "unix_expiry_time", expiryTime }
		};

		// Update the document with new data
		Program.discordServersCollection.UpdateOne(serverDocument, Builders<BsonDocument>.Update.AddToSet("current_polls_dualchoice", pollDocument));
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

	public static int votedValue(BsonDocument document, int index, short voteNum)
	{
		return voteValue(document, index, Convert.ToInt16(voteNum));
	}

	public static int otherValue(BsonDocument document, int index, short voteNum)
	{
		return voteValue(document, index, Convert.ToInt16(1-voteNum));
	}

	public static bool userChoiceStagnant(bool? previousValue, short voteNum)
	{
		var boolUserChoice = Convert.ToBoolean(voteNum);
		return (previousValue == boolUserChoice);
	}

	public static bool? userPreviousValue(BsonDocument document, ulong userId, int index)
	{
		return (bool?)document["current_polls_dualchoice"][index]["votes"]["voters"].ToBsonDocument().GetValue(userId.ToString(), null);
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

	public static short voteInteger(string voteType)
	{
		short voteChosen = 0;
		switch(voteType)
		{
			case "upvote-poll":
			voteChosen = 1; // upvote
			break;

			case "downvote-poll":
			voteChosen = 0; // downvote
			break;
		}
		return voteChosen;
	}

	public static string voteNumString(short voteNum)
	{
		return writeStrings[voteNum];
	}

	public static string getUpdateString(int index, short voteNum)
	{
		return $"current_polls_dualchoice.{index}.votes.{voteNumString(voteNum)}";
	}

	public static async Task updateEmbed(BsonDocument document, int index, SocketMessageComponent messageComponent)
	{
		var newVotes = document["current_polls_dualchoice"][index]["votes"];
		int upvotes = newVotes["upvotes"].AsInt32;
		int downvotes = newVotes["downvotes"].AsInt32;
		
		await messageComponent.Channel.ModifyMessageAsync(messageComponent.Message.Id, m => {
			var embedData = new SuggestData()
					.upvotes(upvotes)
					.downvotes(downvotes)
					.pollText(document["current_polls_dualchoice"][index]["poll_text"].AsString);
			m.Embed = createEmbed(messageComponent, messageComponent.Message, embedData).Build();
		});
	}

	public static async Task removePollData(IMongoCollection<BsonDocument> collection, ulong messageId, ulong serverId)
	{
		var filter = Program.serverIDFilter(serverId);
		var document = Program.discordServersCollection.Find(filter).ToList()[0];
		var dualChoicePolls = document["current_polls_dualchoice"];
		int index = DualChoiceFunctions.indexOfPoll(dualChoicePolls.AsBsonArray, messageId);
		await removePollData(filter, index);
	}

	public static async Task removePollData(FilterDefinition<BsonDocument> filter, int index)
	{
		// make everything that needs to be removed null for removal
		var unsetInstruction = Builders<BsonDocument>.Update.Unset($"current_polls_dualchoice.{index}");
		// remove all null
		var pullInstruction = Builders<BsonDocument>.Update.PullAll($"current_polls_dualchoice", new string?[] { null });

		await Program.discordServersCollection.UpdateOneAsync(filter, unsetInstruction);
		await Program.discordServersCollection.UpdateOneAsync(filter, pullInstruction);
	}

	private static int voteValue(BsonDocument document, int index, short voteNum)
	{
		return document["current_polls_dualchoice"][index]["votes"][writeStrings[voteNum]].ToInt32();
	}

	private static EmbedBuilder buildEmbedWithData(SuggestData embedData)
	{
		String pollText = embedData.PollText;
		String userAvatar = embedData.UserAvatar;
		String userName = embedData.UserName;
		decimal totalVoters = embedData.TotalVoters;
		decimal percentUpvoted = embedData.PercentUpvoted;
		decimal percentDownvoted = embedData.PercentDownvoted;
		DateTimeOffset pollDate = embedData.PollDate;
		String expiryDate = embedData.ExpiryDate;

		// Multipliers for emote percentage bars
		int upvotedMultiplier = Convert.ToInt32(percentUpvoted/10);
		int downvotedMultiplier = Convert.ToInt32(percentDownvoted/10);

		Func<int,int> numStartBars = x => Math.Min(Math.Max(0, x), 1);
		Func<int,int> numMidBars = x => Math.Min(Math.Max(0, x-1), 8);
		Func<int,int> numEndBars = x => Math.Max(0, x-9);

		var downvoteBar = 
			string.Concat(Enumerable.Repeat("<:negativebar_full_left:983184377670402118>", numStartBars(downvotedMultiplier)))
			+ string.Concat(Enumerable.Repeat("<:negativebar_full:983184379947917332>", numMidBars(downvotedMultiplier)))
			+ string.Concat(Enumerable.Repeat("<:negativebar_full_right:983184378836422656>", numEndBars(downvotedMultiplier)))
			+ string.Concat(Enumerable.Repeat("<:bar_empty_left:983184368501669958>", 1-numStartBars(downvotedMultiplier)))
			+ string.Concat(Enumerable.Repeat("<:bar_empty:983184371269906454>", 8-numMidBars(downvotedMultiplier)))
			+ string.Concat(Enumerable.Repeat("<:bar_empty_right:983184370296827904>", 1-numEndBars(downvotedMultiplier)));

		var upvoteBar = 
			string.Concat(Enumerable.Repeat("<:bar_full_left:983184373258027028>", numStartBars(upvotedMultiplier)))
			+ string.Concat(Enumerable.Repeat("<:bar_full:983184376546345030>", numMidBars(upvotedMultiplier)))
			+ string.Concat(Enumerable.Repeat("<:bar_full_right:983184375061569536>", numEndBars(upvotedMultiplier)))
			+ string.Concat(Enumerable.Repeat("<:bar_empty_left:983184368501669958>", 1-numStartBars(upvotedMultiplier)))
			+ string.Concat(Enumerable.Repeat("<:bar_empty:983184371269906454>", 8-numMidBars(upvotedMultiplier)))
			+ string.Concat(Enumerable.Repeat("<:bar_empty_right:983184370296827904>", 1-numEndBars(upvotedMultiplier)));

		// footer string (ex. "1 user has voted" vs "2 users have voted")
		var footerString = " users have voted. ";
		if (totalVoters == 1)
			footerString = " user has voted. ";

		// Build final embed
		return new EmbedBuilder()
		.WithDescription("**———————————————**")
		.WithFooter("\n\n"+totalVoters+footerString)
		.AddField("Poll", pollText)
			.WithColor(Discord.Color.Gold)
			.WithAuthor(new EmbedAuthorBuilder()
				.WithIconUrl(userAvatar)
				.WithName(userName)
			)
			.WithTimestamp(pollDate)
		.AddField("Stats", 
			$"{upvoteBar}\n**{percentUpvoted}%** Upvoted"+ 
			$"\n{downvoteBar}\n**{percentDownvoted}%** Downvoted"
		)
		.AddField("Expires On", expiryDate);
	}
}