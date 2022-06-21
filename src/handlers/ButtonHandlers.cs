using Discord;
using Discord.WebSocket;
using MongoDB.Bson;
using MongoDB.Driver;
using TicketBox;

class ButtonHandlers {
	public static async Task VoteButton(IMongoCollection<BsonDocument> collection, SocketMessageComponent messageComponent, VoteStyle voteType)
	{
		/* ------------------------ Server Document Location ------------------------ */
		var channel = messageComponent.Channel;
		var message = messageComponent.Message;
		// Filter for server document
		var serverId = messageComponent.GuildId.GetValueOrDefault();
		var filter = DocumentFunctions.serverIDFilter(serverId);
		var document = collection.Find(filter).ToList()[0];
		/* --------------------------- Index of Poll -------------------------- */
		// Get all polls, find the index of the poll based on message.Id
		var allPolls = document[$"{FieldNames.CURRENT_POLLS}"];
		int index = DualChoice.indexOfPoll(allPolls.AsBsonArray, message.Id);
		// If no poll index was found
		if (index == -1)
		{
			await messageComponent.RespondAsync();
			return;
		}
		/* ----------------------- Check Poll Existence ----------------------- */
		// Check if user is listed under the voters list
		// Check for a previous user vote value (ex. USER_ID: true)
		bool? userPreviousValue = DualChoice.userPreviousValue(document, messageComponent.User.Id, index);
		bool userVoted = DualChoice.userVoted(userPreviousValue);

		/* ------------------------ Upvote/Downvote Handling ------------------------ */
		if (DualChoice.userChoiceStagnant(userPreviousValue, voteType))
		{
			// Return if the user hasn't picked a different option
			await messageComponent.RespondAsync("You have already selected this vote! You can still change your vote by clicking the other option.", ephemeral: true);
			return;
		}
		// Update the user's vote registered based on what they chose
		var update = Builders<BsonDocument>.Update.Set(
			$"{FieldNames.CURRENT_POLLS}.{index}.votes.voters.{messageComponent.User.Id.ToString()}",
			Convert.ToBoolean(voteType)
		);

		// get the value of upvotes/downvotes
		int upvotes;
		int downvotes;
		if (voteType == VoteStyle.UPVOTE) {
			upvotes = DualChoice.voteValue(document, index, voteType);
			downvotes = DualChoice.voteValue(document, index, 1-voteType);

			upvotes++;
			downvotes = userVoted ? downvotes-1 : downvotes;
		}
		else {
			upvotes = DualChoice.voteValue(document, index, 1-voteType);
			downvotes = DualChoice.voteValue(document, index, voteType);

			downvotes++;
			upvotes = userVoted ? upvotes-1 : upvotes;
		}

		// If the user already voted, take their vote out of the other option
		// if (userVoted)
		// 	await collection.UpdateOneAsync(filter, Builders<BsonDocument>.Update.Set(DualChoiceFunctions.getUpdateString(index, 1-voteType), newOtherValue));
		
		update = update.Set($"{FieldNames.CURRENT_POLLS}.{index}.votes.upvotes", upvotes);
		update = update.Set($"{FieldNames.CURRENT_POLLS}.{index}.votes.downvotes", downvotes);

		await collection.UpdateOneAsync(filter, update);

		/* ----------------------------- Message Editing ---------------------------- */
		var pollText = document[$"{FieldNames.CURRENT_POLLS}"][index]["poll_text"].AsString;

		var coreData = new DualChoiceCoreData(
			pollText,
			new MessageScope(serverId, channel.Id, message.Id),
			upvotes,
			downvotes

		);
		var dualChoice = new DualChoice(coreData, BotSettings.getServerSettings(document));
		
		await dualChoice.updateEmbed(document, index);


		if (userVoted)
			await messageComponent.RespondAsync("Vote successfully switched!", ephemeral: true);
		else {
			await messageComponent.RespondAsync("Vote successful!", ephemeral: true);
		}
	}
}