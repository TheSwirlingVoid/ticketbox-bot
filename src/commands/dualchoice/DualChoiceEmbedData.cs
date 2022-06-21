using Discord;

class DualChoiceEmbedData {
	public String UserAvatar { get; set; }
	public String UserName { get; set; }
	public DateTimeOffset PollDate { get; set; }
	public String ExpiryString { get; set; }

	public DualChoiceEmbedData(String userAvatar, String userName, DateTimeOffset pollDate, String expiryString)
	{
		UserAvatar = userAvatar;
		UserName = userName;
		PollDate = pollDate;
		ExpiryString = expiryString;
	}
	public EmbedBuilder createInitialEmbed(String pollText)
	{
		var upvotes = 0;
		var downvotes = 0;
		var pollDate = DateTimeOffset.Now;

		/* --------------------- Vote Percentages & Multipliers --------------------- */
		// So that you can't divide by 0
		decimal totalVoters = (upvotes + downvotes);
		decimal votesNonZero = Math.Max(totalVoters, 1);
		decimal percentUpvoted = Math.Round(Convert.ToDecimal((upvotes/votesNonZero)*1000))/10;
		decimal percentDownvoted = Math.Round(Convert.ToDecimal((downvotes/votesNonZero)*1000))/10;

		// Return the embed build with the data above
		return buildInitialEmbedWithData(pollText, totalVoters, percentUpvoted, percentDownvoted);
	}
	private EmbedBuilder buildInitialEmbedWithData(String pollText, decimal totalVoters, decimal percentUpvoted, decimal percentDownvoted)
	{

		// Multipliers for emote percentage bars
		int upvotedMultiplier = Convert.ToInt32(percentUpvoted/10);
		int downvotedMultiplier = Convert.ToInt32(percentDownvoted/10);

		var upvoteBar = getBarString(upvotedMultiplier, VoteStyle.UPVOTE);
		var downvoteBar = getBarString(downvotedMultiplier, VoteStyle.DOWNVOTE);

		// footer string (ex. "1 user has voted" vs "2 users have voted")
		var footerString = " users have voted. ";
		if (totalVoters == 1)
			footerString = " user has voted. ";

		// Build final embed
		return new EmbedBuilder()
			.WithDescription("**———————————————**")
			.WithFooter("\n\n"+totalVoters+footerString)
			.WithTimestamp(PollDate)
			.AddField("Poll", $"{pollText}")
				.WithAuthor(new EmbedAuthorBuilder()
					.WithIconUrl(UserAvatar)
					.WithName(UserName)
				)
			.AddField("Stats", 
				$"{upvoteBar}\n**{percentUpvoted}%** Upvoted"+ 
				$"\n{downvoteBar}\n**{percentDownvoted}%** Downvoted"
			)
			.AddField("Expiry Status", ExpiryString);
	}

	public static String getBarString(int multiplier, VoteStyle style)
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

}