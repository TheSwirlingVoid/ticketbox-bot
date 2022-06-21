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
	
	public Embed createInitialEmbed(String pollText)
	{

		var upvoteBar = getBarString(0, VoteStyle.UPVOTE);
		var downvoteBar = getBarString(0, VoteStyle.DOWNVOTE);

		// footer string (ex. "1 user has voted" vs "2 users have voted")
		var footerString = " users have voted. ";
		// Build final embed
		return new EmbedBuilder()
			.WithDescription("**———————————————**")
			.WithFooter("\n\n"+0+footerString)
			.WithTimestamp(DateTimeOffset.Now)
			.AddField("Poll", $"{pollText}")
				.WithAuthor(new EmbedAuthorBuilder()
					.WithIconUrl(UserAvatar)
					.WithName(UserName)
				)
			.AddField("Stats", 
				$"{upvoteBar}\n**{0}%** Upvoted"+ 
				$"\n{downvoteBar}\n**{0}%** Downvoted"
			)
			.AddField("Expiry Status", ExpiryString)
			.Build();
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