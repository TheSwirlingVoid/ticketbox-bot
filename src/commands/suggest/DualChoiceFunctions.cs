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
		return await originalCommand.FollowupAsync(embed: messageEmbedBuilder.Build(), components: getButtons(false));
	}

	public static EmbedBuilder createEmbed(IMessage? message, DualChoiceData embedData)
	{
		var upvotes = embedData.Upvotes;
		var downvotes = embedData.Downvotes;
		var pollText = embedData.PollText;
		var pollDate = embedData.PollDate;

		/* --------------------- Vote Percentages & Multipliers --------------------- */
		// So that you can't divide by 0
		decimal totalVoters = (upvotes + downvotes);
		decimal votesNonZero = Math.Max(totalVoters, 1);
		decimal percentUpvoted = Math.Round(Convert.ToDecimal((upvotes/votesNonZero)*1000))/10;
		decimal percentDownvoted = Math.Round(Convert.ToDecimal((downvotes/votesNonZero)*1000))/10;

		embedData
			.totalVoters(totalVoters)
			.percentUpvoted(percentUpvoted)
			.percentDownvoted(percentDownvoted)
			.pollDate(pollDate);

		// Return the embed build with the data above
		return buildEmbedWithData(embedData);
	}

	public static void saveInitialPoll(IMongoCollection<BsonDocument> collection, SocketSlashCommand command, ulong messageId, string pollText, long expiryTime)
	{
		// Get the server's mongoDB Document
		var serverDocument = Program.getServerDocument(collection, command.GuildId.GetValueOrDefault());

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

	public static int votedValue(BsonDocument document, int index, VoteStyle voteType)
	{
		return voteValue(document, index, voteType);
	}

	public static int otherValue(BsonDocument document, int index, VoteStyle voteType)
	{
		return voteValue(document, index, 1-voteType);
	}

	public static bool userChoiceStagnant(bool? previousValue, VoteStyle voteType)
	{
		var boolUserChoice = Convert.ToBoolean(voteType);
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

	public static string voteNumString(VoteStyle voteType)
	{
		return writeStrings[(short)voteType];
	}

	public static string getUpdateString(int index, VoteStyle voteType)
	{
		return $"current_polls_dualchoice.{index}.votes.{voteNumString(voteType)}";
	}

	public static async Task updateEmbed(int index, MessageScope messageScope, DualChoiceData data)
	{
		var channel = (ISocketMessageChannel) Program.client.GetChannel(messageScope.ChannelID);
		var message = await channel.GetMessageAsync(messageScope.MessageID);

		await channel.ModifyMessageAsync(message.Id, m => {
			
			/* --------------------------- Current Embed Data --------------------------- */
			var embed = message.Embeds.First();
			var embedAuthor = embed.Author.GetValueOrDefault();

			if (data.ClosedVoting)
			{
				m.Components = getButtons(true);
			}
			// default expiry string (the date format)
			if (data.ExpiryString == "") {
				data.expiryString(embed.Fields[2].Value);
			}

			data
				.userAvatar(embedAuthor.IconUrl)
				.userName(embedAuthor.Name)
				.pollDate(message.CreatedAt);

			/* ------------------------------ Create Embed ------------------------------ */
			m.Embed = createEmbed(message, data).Build();
		});
	}

	private static MessageComponent getButtons(bool disabled)
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

	public static async Task removePollData(IMongoCollection<BsonDocument> collection, MessageScope scopeSM)
	{
		var serverId = scopeSM.ServerID;
		var messageId = scopeSM.MessageID;

		var filter = DocumentFunctions.serverIDFilter(serverId);
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

	private static int voteValue(BsonDocument document, int index, VoteStyle voteType)
	{
		return document["current_polls_dualchoice"][index]["votes"][writeStrings[(int)voteType]].ToInt32();
	}

	private static String getBarString(int multiplier, VoteStyle style)
	{
		String barTypeLeft;
		String barTypeMid;
		String barTypeRight;
		if (style == VoteStyle.UPVOTE)
		{
			barTypeLeft = "<:bar_full_left:983184373258027028>";
			barTypeMid = "<:bar_full:983184376546345030>";
			barTypeRight = "<:bar_full_right:983184375061569536>";
		}
		else {
			barTypeLeft = $"<:negativebar_full_left:983184377670402118>";
			barTypeMid = $"<:negativebar_full:983184379947917332>";
			barTypeRight = $"<:negativebar_full_right:983184378836422656>";
		}

		Func<int,int> numStartBars = x => Math.Min(Math.Max(0, x), 1);
		Func<int,int> numMidBars = x => Math.Min(Math.Max(0, x-1), 8);
		Func<int,int> numEndBars = x => Math.Max(0, x-9);

		var barString = 
			string.Concat(Enumerable.Repeat(barTypeLeft, numStartBars(multiplier)))
			+ string.Concat(Enumerable.Repeat(barTypeMid, numMidBars(multiplier)))
			+ string.Concat(Enumerable.Repeat(barTypeRight, numEndBars(multiplier)))
			+ string.Concat(Enumerable.Repeat("<:bar_empty_left:983184368501669958>", 1-numStartBars(multiplier)))
			+ string.Concat(Enumerable.Repeat("<:bar_empty:983184371269906454>", 8-numMidBars(multiplier)))
			+ string.Concat(Enumerable.Repeat("<:bar_empty_right:983184370296827904>", 1-numEndBars(multiplier)));

		return barString;
	}

	private static EmbedBuilder buildEmbedWithData(DualChoiceData embedData)
	{

		// Multipliers for emote percentage bars
		int upvotedMultiplier = Convert.ToInt32(embedData.PercentUpvoted/10);
		int downvotedMultiplier = Convert.ToInt32(embedData.PercentDownvoted/10);

		var upvoteBar = getBarString(upvotedMultiplier, VoteStyle.UPVOTE);
		var downvoteBar = getBarString(downvotedMultiplier, VoteStyle.DOWNVOTE);

		// footer string (ex. "1 user has voted" vs "2 users have voted")
		var footerString = " users have voted. ";
		if (embedData.TotalVoters == 1)
			footerString = " user has voted. ";

		// Build final embed
		return new EmbedBuilder()
			.WithDescription("**———————————————**")
			.WithFooter("\n\n"+embedData.TotalVoters+footerString)
			.WithTimestamp(embedData.PollDate)
			.AddField("Poll", $"{embedData.PollText}")
				.WithAuthor(new EmbedAuthorBuilder()
					.WithIconUrl(embedData.UserAvatar)
					.WithName(embedData.UserName)
				)
			.AddField("Stats", 
				$"{upvoteBar}\n**{embedData.PercentUpvoted}%** Upvoted"+ 
				$"\n{downvoteBar}\n**{embedData.PercentDownvoted}%** Downvoted"
			)
			.AddField("Expiry Status", embedData.ExpiryString);
	}
}

enum VoteStyle {
	DOWNVOTE,
	UPVOTE
}