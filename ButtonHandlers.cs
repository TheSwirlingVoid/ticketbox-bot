using Discord;
using Discord.WebSocket;
using MongoDB.Bson;
using MongoDB.Driver;
using TicketBox;

static class ButtonHandlers {
	public static async Task VoteButton(SocketMessageComponent messageComponent, string voteType)
	{
		/* ------------------------ Server Document Location ------------------------ */
		ulong messageId = messageComponent.Message.Id;
		// Filter for server document
		var filter = Program.serverIDFilter(messageComponent.GuildId.GetValueOrDefault());
		var document = Program.discordServersCollection.Find(filter).ToList()[0];
		/* --------------------------- Index of Poll -------------------------- */
		// Get all polls, find the index of the poll based on messageId
		var allPolls = document["current_polls_dualchoice"];
		int index = DualChoiceFunctions.indexOfPoll(allPolls.AsBsonArray, messageId);
		// If no poll index was found
		if (index == -1)
		{
			await messageComponent.RespondAsync();
			return;
		}
		/* ----------------------- Check Poll Existence ----------------------- */
		// Check if user is listed under the voters list
		// Check for a previous user vote value (ex. USER_ID: true)
		bool? userPreviousValue = DualChoiceFunctions.userPreviousValue(document, messageComponent.User.Id, index);
		bool userVoted = DualChoiceFunctions.userVoted(userPreviousValue);

		/* ------------------------ Upvote/Downvote Handling ------------------------ */
		short voteNum = DualChoiceFunctions.voteInteger(voteType);
		if (DualChoiceFunctions.userChoiceStagnant(userPreviousValue, voteNum))
		{
			// Return if the user hasn't picked a different option
			await messageComponent.RespondAsync("You have already selected this vote! You can still change your vote by clicking the other option.", ephemeral: true);
			return;
		}
		// Update the user's vote registered based on what they chose
		var updateInstruction = Builders<BsonDocument>.Update.Set(
				"current_polls_dualchoice."
				+ index
				+ ".votes.voters."
				+ messageComponent.User.Id.ToString(),
			Convert.ToBoolean(voteNum));
		// get the value of upvotes/downvotes
		int currentValue = DualChoiceFunctions.votedValue(document, index, voteNum);
		int currentOtherValue = DualChoiceFunctions.otherValue(document, index, voteNum);

		// If the user already voted, take their vote out of the other option
		if (userVoted) 
			// if they changed their vote, subtract their vote from the other value
			await Program.discordServersCollection.UpdateOneAsync(filter, Builders<BsonDocument>.Update.Set(DualChoiceFunctions.getUpdateString(index, Convert.ToInt16(1-voteNum)), currentOtherValue-1));

		await Program.discordServersCollection.UpdateOneAsync(filter, Builders<BsonDocument>.Update.Set(DualChoiceFunctions.getUpdateString(index, voteNum), currentValue+1));
		await Program.discordServersCollection.UpdateOneAsync(filter, updateInstruction);
		/* ----------------------------- Message Editing ---------------------------- */
		var updatedDoc = Program.discordServersCollection.Find(filter).ToList()[0];
		await DualChoiceFunctions.updateEmbed(updatedDoc, index, messageComponent);


		if (userVoted)
			await messageComponent.RespondAsync("Vote successfully switched!", ephemeral: true);
		else {
			await messageComponent.RespondAsync("Vote successful!", ephemeral: true);
		}
	}
}