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
		var filter = Program.serverIDFilter(messageComponent.GuildId.Value);
		var document = Program.collection.Find(filter).ToList()[0];
		/* --------------------------- Index of Suggestion -------------------------- */
		// Get all suggestions, find the index of the suggestion based on messageId
		var allSuggestions = document["current_suggestions"];
		int index = SuggestFunctions.indexOfSuggestion(allSuggestions.AsBsonArray, messageId);
		// If no suggestion index was found
		if (index == -1)
		{
			await messageComponent.RespondAsync("");
			return;
		}
		/* ----------------------- Check Suggestion Existence ----------------------- */
		// Check if user is listed under the voters list
		// Check for a previous user vote value (ex. USER_ID: true)
		bool userVoted = false;
		bool? userPreviousValue = (bool?)allSuggestions[index]["votes"]["voters"].ToBsonDocument().GetValue(messageComponent.User.Id.ToString(), null);
		if (userPreviousValue != null)
		{
			userVoted = true;
		}

		/* ------------------------ Upvote/Downvote Handling ------------------------ */
		string[] writeStrings = {"dislikes", "favors"};
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
		bool boolUserChoice = Convert.ToBoolean(voteChosen);
		if (userPreviousValue == boolUserChoice)
		{
			// Return if the user hasn't picked a different option
			await messageComponent.RespondAsync("");
			return;
		}
		// Update the user's vote registered based on what they chose
		var updateInstruction = Builders<BsonDocument>.Update.Set("current_suggestions."+index+".votes.voters."+messageComponent.User.Id.ToString(), boolUserChoice);
		// get the value of favors/dislikes
		var suggestionInfo = document["current_suggestions"][index];
		int currentValue = suggestionInfo["votes"][writeStrings[voteChosen]].ToInt32();
		int currentOtherValue = suggestionInfo["votes"][writeStrings[1-voteChosen]].ToInt32();

		// If the user already voted, take their vote out of the other option
		if (userVoted) 
		{
			// if they changed their vote, subtract their vote from the other value
			await Program.collection.UpdateOneAsync(filter, Builders<BsonDocument>.Update.Set("current_suggestions."+index.ToString()+".votes." + writeStrings[1-voteChosen], currentOtherValue-1));
		}
		await Program.collection.UpdateOneAsync(filter, Builders<BsonDocument>.Update.Set("current_suggestions."+index.ToString()+".votes." + writeStrings[voteChosen], currentValue+1));
		await Program.collection.UpdateOneAsync(filter, updateInstruction);
		/* ----------------------------- Message Editing ---------------------------- */
		var updatedDoc = Program.collection.Find(filter).ToList()[0];
		var newVotes = updatedDoc["current_suggestions"][index]["votes"];
		int favors = (int) newVotes["favors"];
		int dislikes = (int) newVotes["dislikes"];
		
		await messageComponent.Channel.ModifyMessageAsync(messageId, m => {
			m.Embed = SuggestFunctions.createEmbed(suggestionInfo["suggestion_text"].ToString(), messageComponent, messageComponent.Message, favors, dislikes).Build();
		});

		await messageComponent.RespondAsync("");
	}
}