using Discord;
using Discord.WebSocket;
using MongoDB.Bson;
using MongoDB.Driver;
using TicketBox;

class ButtonHandlers {
	public static async Task VoteButton(BotSettings settings, SocketMessageComponent messageComponent, VoteStyle voteType)
	{
		/* -------------------------------- Base Info ------------------------------- */
		var channel = messageComponent.Channel;
		var message = messageComponent.Message;
		var serverId = messageComponent.GuildId.GetValueOrDefault();
		/* --------------------------- Index of Poll -------------------------- */
		// Get all polls, find the index of the poll based on message.Id
		var pollDoc = DocumentFunctions.getPollDocument(new MessageScope(
			serverId,
			channel.Id,
			message.Id
		));
		// If no poll index was found
		if (pollDoc == null)
		{
			await messageComponent.RespondAsync();
			return;
		}
		// there will only be 1 document for the poll
		/* ----------------------- Check Poll Existence ----------------------- */
		// Check if user is listed under the voters list
		// Check for a previous user vote value (ex. USER_ID: true)
		bool? userPreviousValue = DualChoice.userPreviousValue(pollDoc, messageComponent.User.Id);

		/* ------------------------ Upvote/Downvote Handling ------------------------ */
		if (DualChoice.userChoiceStagnant(userPreviousValue, voteType))
		{
			// Return if the user hasn't picked a different option
			await messageComponent.RespondAsync("You have already selected this vote! You can still change your vote by clicking the other option.", ephemeral: true);
			return;
		}

		// get the value of upvotes/downvotes
		int upvotes;
		int downvotes;
		bool userVoted = DualChoice.userVoted(userPreviousValue);

		if (voteType == VoteStyle.UPVOTE) {
			// get the current upvotes/downvotes value
			upvotes = DualChoice.voteValue(pollDoc, voteType);
			downvotes = DualChoice.voteValue(pollDoc, 1-voteType);

			// add 1 to the vote they chose
			upvotes++;
			// subtract 1 from the other if they changed their vote
			downvotes = userVoted ? downvotes-1 : downvotes;
		}
		else {
			upvotes = DualChoice.voteValue(pollDoc, 1-voteType);
			downvotes = DualChoice.voteValue(pollDoc, voteType);

			downvotes++;
			upvotes = userVoted ? upvotes-1 : upvotes;
		}
		
		// Update the user's vote registered based on what they chose

		/* --------------------------------- Update --------------------------------- */
		var update = Builders<BsonDocument>.Update.Set(
			$"votes.voters.{messageComponent.User.Id.ToString()}",
			Convert.ToBoolean(voteType)
		);
		update = update.Set($"votes.upvotes", upvotes);
		update = update.Set($"votes.downvotes", downvotes);

		await Program.pollCollection.UpdateOneAsync(pollDoc, update);

		/* ----------------------------- Message Editing ---------------------------- */
		var pollText = pollDoc["poll_text"].AsString;

		var coreData = new DualChoiceCoreData(
			pollText,
			new MessageScope(serverId, channel.Id, message.Id),
			upvotes,
			downvotes

		);
		var dualChoice = new DualChoice(coreData, settings);
		
		/* ------------------------------ Update Embed ------------------------------ */
		await dualChoice.updateEmbed(pollDoc);

		/* -------------------------------- Response -------------------------------- */
		if (userVoted)
			await messageComponent.RespondAsync("Vote successfully switched!", ephemeral: true);
		else {
			await messageComponent.RespondAsync("Vote successful!", ephemeral: true);
		}
	}
}