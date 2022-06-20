using Discord;
using Discord.WebSocket;
using MongoDB.Bson;
using MongoDB.Driver;
using TicketBox;

static class ButtonHandlers {
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
		var allPolls = document["current_polls_dualchoice"];
		int index = DualChoiceFunctions.indexOfPoll(allPolls.AsBsonArray, message.Id);
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
								$"current_polls_dualchoice.{index}.votes.voters.{messageComponent.User.Id.ToString()}",
								Convert.ToBoolean(voteType));
		// get the value of upvotes/downvotes
		int currentValue = DualChoiceFunctions.votedValue(document, index, voteType);
		int currentOtherValue = DualChoiceFunctions.otherValue(document, index, voteType);

		var newValue = currentValue+1;
		var newOtherValue = currentOtherValue-1;

		decimal upvotes = voteType == VoteStyle.UPVOTE ? newValue : (userVoted ? newOtherValue : currentOtherValue);
		decimal downvotes = voteType == VoteStyle.DOWNVOTE ? newValue : (userVoted ? newOtherValue : currentOtherValue);
		// If the user already voted, take their vote out of the other option
		if (userVoted)
			await collection.UpdateOneAsync(filter, Builders<BsonDocument>.Update.Set(DualChoiceFunctions.getUpdateString(index, 1-voteType), newOtherValue));


		await collection.UpdateOneAsync(filter, Builders<BsonDocument>.Update.Set(DualChoiceFunctions.getUpdateString(index, voteType), newValue));
		await collection.UpdateOneAsync(filter, updateInstruction);


		/* ----------------------------- Message Editing ---------------------------- */
		var pollText = document["current_polls_dualchoice"][index]["poll_text"].AsString;

		var data = new DualChoiceData(upvotes, downvotes, pollText, false);

		var messageScope = new MessageScope()
			.channelId(channel.Id)
			.messageId(message.Id);


		await DualChoiceFunctions.updateEmbed(index, messageScope, data);


		if (userVoted)
			await messageComponent.RespondAsync("Vote successfully switched!", ephemeral: true);
		else {
			await messageComponent.RespondAsync("Vote successful!", ephemeral: true);
		}
	}
	public static async Task CloseDCPoll(IMongoCollection<BsonDocument> collection, SocketMessageComponent messageComponent)
	{
		var messageScope = new MessageScope()
			.serverId(messageComponent.GuildId.GetValueOrDefault())
			.channelId(messageComponent.Channel.Id)
			.messageId(messageComponent.Message.Id);

		await TerminateDCPoll(collection, messageScope, $"Closed by {messageComponent.User}");

		await messageComponent.RespondAsync("Poll successfully closed!", ephemeral: true);
	}

	//* THESE TWO FUNCTIONS BELOW DO NOT RESPOND TO BUTTONS
	// though they are related to the button handler above
	public static async Task ExpireDCPoll(IMongoCollection<BsonDocument> collection, MessageScope scopeSCM)
	{
		await TerminateDCPoll(collection, scopeSCM, "Expired");
	}
	private static async Task TerminateDCPoll(IMongoCollection<BsonDocument> collection, MessageScope scopeSCM, String expiryString)
	{
		var serverId = scopeSCM.ServerID;
		var messageId = scopeSCM.MessageID;

		var channel = (ISocketMessageChannel) Program.client.GetGuild(serverId).GetChannel(scopeSCM.ChannelID);
		var message = await channel.GetMessageAsync(messageId);

		var document = DocumentFunctions.getServerDocument(collection, serverId);
		var index = DualChoiceFunctions.indexOfPoll(document["current_polls_dualchoice"].AsBsonArray, messageId);
		var poll = document["current_polls_dualchoice"][index];
		var pollVotes = poll["votes"];

		var data = new DualChoiceData(pollVotes["upvotes"].AsInt32, pollVotes["downvotes"].AsInt32, poll["poll_text"].AsString, true)
			.expiryString(expiryString);

		await DualChoiceFunctions.updateEmbed(index, scopeSCM, data);
		await DualChoiceFunctions.removePollData(collection, scopeSCM);
	}
}