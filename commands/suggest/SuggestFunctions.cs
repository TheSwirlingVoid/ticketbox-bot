using Discord;
using Discord.WebSocket;
using MongoDB.Bson;
using MongoDB.Driver;
using TicketBox;

static class SuggestFunctions {
	private static string[] writeStrings = {"dislikes", "favors"};
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
					.WithButton(Emoji.Parse(":thumbsup:").ToString(), "upvote-suggestion", ButtonStyle.Primary)
					.WithButton(Emoji.Parse(":thumbsdown:").ToString(), "downvote-suggestion", ButtonStyle.Primary)
			).Build()
		);
	}

	public static EmbedBuilder createEmbed(String suggestionText, SocketInteraction interaction, SocketMessage? message, decimal favors, decimal dislikes)
	{
		/* ----------------- Embed Data (Based on Message Existence) ---------------- */
		// Get avatar, name, etc. based on whether the embed has already been created
		var userAvatar = interaction.User.GetAvatarUrl();
		var userName = interaction.User.ToString();
		var suggestionDate = interaction.CreatedAt;
		if (message != null) 
		{
			var embedAuthor = message.Embeds.ToArray()[0].Author;
			if (embedAuthor != null)
			{
				userAvatar = embedAuthor.Value.IconUrl;
				userName = embedAuthor.Value.Name;
			}
			suggestionDate = message.CreatedAt;
		}

		/* --------------------- Vote Percentages & Multipliers --------------------- */
		// So that you can't divide by 0
		decimal totalVoters = (favors + dislikes);
		decimal votesNonZero = Math.Max(totalVoters, 1);
		decimal percentFavored = Math.Round(Convert.ToDecimal((favors/votesNonZero)*1000))/10;
		decimal percentDisliked = Math.Round(Convert.ToDecimal((dislikes/votesNonZero)*1000))/10;

		// Return the embed build with the data above
		return buildEmbedWithData(suggestionText, userAvatar, userName, totalVoters, percentFavored, percentDisliked, suggestionDate);
	}

	public static void saveInitialSuggestion(IMongoCollection<BsonDocument> collection, ulong guildId, SocketSlashCommand command, ulong messageId, string suggestionText)
	{
		// Find the document by filtering by server ID
		var serverFilter = Program.serverIDFilter(guildId);
		// Get the server's mongoDB Document
		var serverDocument = collection.Find(serverFilter).ToList()[0];

		// Prepare the structured data for the document's suggestion list
		BsonDocument suggestionDocument = new BsonDocument
		{
			{ "user_id", BsonValue.Create(command.User.Id) },
			{ "suggestion_text", suggestionText },
			{ "votes", new BsonDocument {
				{ "favors", 0 },
				{ "dislikes", 0 },
				{ "voters", new BsonDocument {} }
			} },
			{ "message_id", BsonValue.Create(messageId) }
		};

		// Update the document with new data
		Program.collection.UpdateOne(serverDocument, Builders<BsonDocument>.Update.AddToSet("current_suggestions", suggestionDocument));
	}

	public static int indexOfSuggestion(BsonArray currentSuggestions, ulong messageId)
	{
		// Linear search w/ suggestion indexes
		for (int index = 0; index < currentSuggestions.Count(); index++)
		{
			var suggestion = currentSuggestions[index];
			// If the suggestion at the current index has the given message ID, return that index
			if (suggestion["message_id"].AsInt64 == ((long)messageId))
			{
				return index;
			}
		}
		// Return -1 if no suggestion with the given messageID exists
		return -1;
	}

	public static int votedValue(BsonDocument document, int indexOfSuggestion, short voteNum)
	{
		return voteValue(document, indexOfSuggestion, Convert.ToInt16(voteNum));
	}

	public static int otherValue(BsonDocument document, int indexOfSuggestion, short voteNum)
	{
		return voteValue(document, indexOfSuggestion, Convert.ToInt16(1-voteNum));
	}

	public static bool userChoiceStagnant(bool? previousValue, short voteNum)
	{
		var boolUserChoice = Convert.ToBoolean(voteNum);
		return (previousValue == boolUserChoice);
	}

	public static bool? userPreviousValue(BsonDocument document, ulong userId, int indexOfSuggestion)
	{
		return (bool?)document["current_suggestions"][indexOfSuggestion]["votes"]["voters"].ToBsonDocument().GetValue(userId.ToString(), null);
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
			case "upvote-suggestion":
			voteChosen = 1; // favor
			break;

			case "downvote-suggestion":
			voteChosen = 0; // dislike
			break;
		}
		return voteChosen;
	}

	public static string voteNumString(short voteNum)
	{
		return writeStrings[voteNum];
	}

	public static string getUpdateString(int indexOfSuggestion, short voteNum)
	{
		return "current_suggestions."+indexOfSuggestion.ToString()+".votes." + voteNumString(voteNum);
	}

	public static async Task updateEmbed(BsonDocument document, int indexOfSuggestion, SocketMessageComponent messageComponent)
	{
		var newVotes = document["current_suggestions"][indexOfSuggestion]["votes"];
		int favors = newVotes["favors"].AsInt32;
		int dislikes = newVotes["dislikes"].AsInt32;
		
		await messageComponent.Channel.ModifyMessageAsync(messageComponent.Message.Id, m => {
			m.Embed = createEmbed(document["current_suggestions"][indexOfSuggestion]["suggestion_text"].AsString, messageComponent, messageComponent.Message, favors, dislikes).Build();
		});
	}

	private static int voteValue(BsonDocument document, int indexOfSuggestion, short voteNum)
	{
		return document["current_suggestions"][indexOfSuggestion]["votes"][writeStrings[voteNum]].ToInt32();
	}

	private static EmbedBuilder buildEmbedWithData(String suggestionText, String userAvatar, String userName, decimal totalVoters, decimal percentFavored, decimal percentDisliked, DateTimeOffset suggestionDate)
	{
		// Multipliers for emote percentage bars
		int favoredMultiplier = Convert.ToInt32(percentFavored/10);
		int dislikedMultiplier = Convert.ToInt32(percentDisliked/10);

		Func<int,int> numStartBars = x => Math.Min(Math.Max(0, x), 1);
		Func<int,int> numMidBars = x => Math.Min(Math.Max(0, x-1), 8);
		Func<int,int> numEndBars = x => Math.Max(0, x-9);

		var dislikeBar = 
			string.Concat(Enumerable.Repeat("<:negativebar_full_left:983184377670402118>", numStartBars(dislikedMultiplier)))
			+ string.Concat(Enumerable.Repeat("<:negativebar_full:983184379947917332>", numMidBars(dislikedMultiplier)))
			+ string.Concat(Enumerable.Repeat("<:negativebar_full_right:983184378836422656>", numEndBars(dislikedMultiplier)))
			+ string.Concat(Enumerable.Repeat("<:bar_empty_left:983184368501669958>", 1-numStartBars(dislikedMultiplier)))
			+ string.Concat(Enumerable.Repeat("<:bar_empty:983184371269906454>", 8-numMidBars(dislikedMultiplier)))
			+ string.Concat(Enumerable.Repeat("<:bar_empty_right:983184370296827904>", 1-numEndBars(dislikedMultiplier)));

		var favorBar = 
			string.Concat(Enumerable.Repeat("<:bar_full_left:983184373258027028>", numStartBars(favoredMultiplier)))
			+ string.Concat(Enumerable.Repeat("<:bar_full:983184376546345030>", numMidBars(favoredMultiplier)))
			+ string.Concat(Enumerable.Repeat("<:bar_full_right:983184375061569536>", numEndBars(favoredMultiplier)))
			+ string.Concat(Enumerable.Repeat("<:bar_empty_left:983184368501669958>", 1-numStartBars(favoredMultiplier)))
			+ string.Concat(Enumerable.Repeat("<:bar_empty:983184371269906454>", 8-numMidBars(favoredMultiplier)))
			+ string.Concat(Enumerable.Repeat("<:bar_empty_right:983184370296827904>", 1-numEndBars(favoredMultiplier)));

		// footer string (ex. "1 user has voted" vs "2 users have voted")
		var footerString = " users have voted. ";
		if (totalVoters == 1)
			footerString = " user has voted. ";

		// Build final embed
		return new EmbedBuilder()
		.WithDescription("**———————————————**")
		.WithFooter("\n\n"+totalVoters+footerString)
		.AddField("Suggestion", suggestionText)
			.WithColor(Discord.Color.Gold)
			.WithAuthor(new EmbedAuthorBuilder()
				.WithIconUrl(userAvatar)
				.WithName(userName)
			)
			.WithTimestamp(suggestionDate)
		.AddField("Stats",
			favorBar
			+ "\n**"+percentFavored+"%** Favored"
			+ "\n"
			+ dislikeBar
			+ "\n**"+percentDisliked+"%** Disliked"
		);
	}
}