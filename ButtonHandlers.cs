using Discord;
using Discord.WebSocket;
using MongoDB.Bson;
using MongoDB.Driver;
using TicketBox;

static class ButtonHandlers {
	public static async Task VoteButton(IMongoCollection<BsonDocument> collection, SocketMessageComponent messageComponent, VoteStyle voteType)
	{
		/* ------------------------ Server Document Location ------------------------ */
		ulong messageId = messageComponent.Message.Id;
		// Filter for server document
		var serverId = messageComponent.GuildId.GetValueOrDefault();
		var filter = Program.serverIDFilter(serverId);
		var document = collection.Find(filter).ToList()[0];
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
		if (DualChoiceFunctions.userChoiceStagnant(userPreviousValue, voteType))
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
			Convert.ToBoolean(voteType));
		// get the value of upvotes/downvotes
		int currentValue = DualChoiceFunctions.votedValue(document, index, voteType);
		int currentOtherValue = DualChoiceFunctions.otherValue(document, index, voteType);

		// If the user already voted, take their vote out of the other option
		if (userVoted) 
			// if they changed their vote, subtract their vote from the other value
			await collection.UpdateOneAsync(filter, Builders<BsonDocument>.Update.Set(DualChoiceFunctions.getUpdateString(index, 1-voteType), currentOtherValue-1));

		await collection.UpdateOneAsync(filter, Builders<BsonDocument>.Update.Set(DualChoiceFunctions.getUpdateString(index, voteType), currentValue+1));
		await collection.UpdateOneAsync(filter, updateInstruction);
		/* ----------------------------- Message Editing ---------------------------- */
		var updatedDoc = collection.Find(filter).ToList()[0];
		await DualChoiceFunctions.updateEmbed(updatedDoc, index, false, messageComponent);


		if (userVoted)
			await messageComponent.RespondAsync("Vote successfully switched!", ephemeral: true);
		else {
			await messageComponent.RespondAsync("Vote successful!", ephemeral: true);
		}
	}
	public static async Task CloseDCPoll(IMongoCollection<BsonDocument> collection, SocketMessageComponent messageComponent)
	{
		var serverId = messageComponent.GuildId.GetValueOrDefault();
		var messageId = messageComponent.Message.Id;

		var document = Program.getServerDocument(collection, serverId);
		var index = DualChoiceFunctions.indexOfPoll(document["current_polls_dualchoice"].AsBsonArray, messageId);
		await DualChoiceFunctions.updateEmbed(document, index, true, messageComponent);
		await DualChoiceFunctions.removePollData(collection, messageId, serverId);
		await messageComponent.RespondAsync("Poll successfully closed!", ephemeral: true);
	}
}