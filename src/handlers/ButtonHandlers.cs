using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using MongoDB.Bson;
using MongoDB.Driver;
using TicketBox;

class ButtonHandlers {
	public static async Task VoteButton(BotSettings settings, SocketMessageComponent messageComponent, VoteStyle voteType)
	{
		await messageComponent.DeferAsync(ephemeral: true);
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
			await messageComponent.FollowupAsync(Messages.NO_POLL_DATA);
			return;
		}
		// there will only be 1 document for the poll
		/* ----------------------- Check Poll Existence ----------------------- */
		// Check if user is listed under the voters list
		// Check for a previous user vote value (ex. USER_ID: true)
		bool? userPreviousValue = DualChoice.userPreviousValue(pollDoc, messageComponent.User.Id);
		bool userVoted = DualChoice.userVoted(userPreviousValue);

		int otherVoteValue;
		if (userVoted)
		{
			/* ------------------------ Upvote/Downvote Handling ------------------------ */
			if (DualChoice.userChoiceStagnant(userPreviousValue, voteType))
			{
				// Return if the user hasn't picked a different option
				await messageComponent.FollowupAsync(Messages.VOTE_REDUNDANT, ephemeral: true);
				return;
			}
			else {
				otherVoteValue = DualChoice.voteValue(pollDoc, 1-voteType)-1;
			}
		}
		else {
			otherVoteValue = DualChoice.voteValue(pollDoc, 1-voteType);
		}

		// get the value of upvotes/downvotes
		int upvotes;
		int downvotes;

		if (voteType == VoteStyle.UPVOTE) {
			// get the current upvotes/downvotes value
			upvotes = DualChoice.voteValue(pollDoc, voteType)+1;
			downvotes = otherVoteValue;
		}
		else {
			upvotes = otherVoteValue;
			downvotes = DualChoice.voteValue(pollDoc, voteType)+1;
		}
		
		// Update the user's vote registered based on what they chose

		/* ----------------------------- Message Editing ---------------------------- */
		var coreData = new DualChoiceCoreData(
			new MessageScope(serverId, channel.Id, message.Id),
			upvotes,
			downvotes
		);
		var dualChoice = new DualChoice(coreData, settings);
		
		/* ------------------------------ Update Embed ------------------------------ */
		await dualChoice.updateEmbed();

		/* --------------------------------- Update --------------------------------- */
		var update = Builders<BsonDocument>.Update.Set(
			$"votes.voters.{messageComponent.User.Id}",
			Convert.ToBoolean(voteType)
		);
		update = update.Set("votes.upvotes", upvotes);
		update = update.Set("votes.downvotes", downvotes);

		await Program.pollCollection.UpdateOneAsync(pollDoc, update);

		/* -------------------------------- Response -------------------------------- */
		if (userVoted)
			await messageComponent.FollowupAsync(Messages.VOTE_SWITCH_SUCCESS, ephemeral: true);
		else {
			await messageComponent.FollowupAsync(Messages.VOTE_SUCCESS, ephemeral: true);
		}
	}

	public static async Task ClosePoll(BsonDocument serverDoc, MessageScope messageScope, SocketMessageComponent messageComponent)
	{	
		await messageComponent.DeferAsync(ephemeral: true);

		var requiredPerms = Permissions.requiredUserPermsClosePoll(messageComponent);

		if (!requiredPerms)
		{
			await messageComponent.FollowupAsync(
				Messages.INSUFFICIENT_CLOSE_PERMS, ephemeral: true
			);
		}
		/* --------------------------- Get and Close Poll --------------------------- */
		await DualChoice.getPollByMessage(serverDoc,messageScope)
			.close(
				DocumentFunctions.getPollDocument(messageScope),
				messageComponent
			);
		/* -------------------------------------------------------------------------- */
	}

	public static async Task RetractVote(BotSettings settings, SocketMessageComponent messageComponent)
	{
		await messageComponent.DeferAsync(ephemeral: true);

		var messageScope = new MessageScope(
			messageComponent.GuildId.GetValueOrDefault(),
			messageComponent.ChannelId.GetValueOrDefault(),
			messageComponent.Message.Id
		);

		var pollDoc = DocumentFunctions.getPollDocument(messageScope);
		var userId = messageComponent.User.Id;

		var previousValue = DualChoice.userPreviousValue(pollDoc, userId);
		// check for vote
		if (previousValue.HasValue)
		{
			// get the poll's data from the polldoc and represent it as a DualChoice object	
			var coreData = new DualChoiceCoreData(
				messageScope,
				DualChoice.voteValue(pollDoc, VoteStyle.UPVOTE),
				DualChoice.voteValue(pollDoc, VoteStyle.DOWNVOTE)
			);
			var dualChoice = new DualChoice(coreData, settings);

			var update = dualChoice.getRetractUpdate(previousValue.Value, messageComponent);
			await Program.pollCollection.UpdateOneAsync(pollDoc, update);

			await dualChoice.updateEmbed();


			await messageComponent.FollowupAsync(Messages.VOTE_RETRACT_SUCCESS, ephemeral: true);
		}
		else {
			await messageComponent.FollowupAsync(Messages.NO_RETRACTABLE_VOTE, ephemeral: true);
		}
	}
}