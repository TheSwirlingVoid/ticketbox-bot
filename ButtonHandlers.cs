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
		var document = Program.collection.Find(filter).ToList()[0];
		/* --------------------------- Index of Suggestion -------------------------- */
		// Get all suggestions, find the index of the suggestion based on messageId
		var allSuggestions = document["current_suggestions"];
		int index = SuggestFunctions.indexOfSuggestion(allSuggestions.AsBsonArray, messageId);
		// If no suggestion index was found
		if (index == -1)
		{
			await messageComponent.RespondAsync();
			return;
		}
		/* ----------------------- Check Suggestion Existence ----------------------- */
		// Check if user is listed under the voters list
		// Check for a previous user vote value (ex. USER_ID: true)
		bool? userPreviousValue = SuggestFunctions.userPreviousValue(document, messageComponent.User.Id, index);
		bool userVoted = SuggestFunctions.userVoted(userPreviousValue);
		//bool? userPreviousValue = (bool?)allSuggestions[index]["votes"]["voters"].ToBsonDocument().GetValue(messageComponent.User.Id.ToString(), null);
		// if (userPreviousValue != null)
		// {
		// 	userVoted = true;
		// }

		/* ------------------------ Upvote/Downvote Handling ------------------------ */
		short voteNum = SuggestFunctions.voteInteger(voteType);
		if (SuggestFunctions.userChoiceStagnant(userPreviousValue, voteNum))
		{
			// Return if the user hasn't picked a different option
			try
			{
				await messageComponent.User.SendMessageAsync("You have already selected this vote! You can still change your vote by clicking the other option.");
			}catch{}
			await messageComponent.RespondAsync();
			return;
		}
		// Update the user's vote registered based on what they chose
		var updateInstruction = Builders<BsonDocument>.Update.Set(
				"current_suggestions."
				+ index
				+ ".votes.voters."
				+ messageComponent.User.Id.ToString(),
			Convert.ToBoolean(voteNum));
		// get the value of favors/dislikes
		int currentValue = SuggestFunctions.votedValue(document, index, voteNum);
		int currentOtherValue = SuggestFunctions.otherValue(document, index, voteNum);

		// If the user already voted, take their vote out of the other option
		if (userVoted) 
			// if they changed their vote, subtract their vote from the other value
			await Program.collection.UpdateOneAsync(filter, Builders<BsonDocument>.Update.Set(SuggestFunctions.getUpdateString(index, Convert.ToInt16(1-voteNum)), currentOtherValue-1));

		await Program.collection.UpdateOneAsync(filter, Builders<BsonDocument>.Update.Set(SuggestFunctions.getUpdateString(index, voteNum), currentValue+1));
		await Program.collection.UpdateOneAsync(filter, updateInstruction);
		/* ----------------------------- Message Editing ---------------------------- */
		var updatedDoc = Program.collection.Find(filter).ToList()[0];
		await SuggestFunctions.updateEmbed(updatedDoc, index, messageComponent);

		try
		{
			if (userVoted)
				await messageComponent.User.SendMessageAsync("Vote successfully switched!");
			else {
				await messageComponent.User.SendMessageAsync("Vote successful!");
			}
		}catch{}

		await messageComponent.RespondAsync();
	}
}