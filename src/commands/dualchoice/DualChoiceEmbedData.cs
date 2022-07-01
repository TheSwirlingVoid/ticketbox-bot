using Discord;

class DualChoiceEmbedData {
	public String UserAvatar { get; set; }
	public String UserName { get; set; }
	public DateTimeOffset PollDate { get; set; }
	public String ExpiryString { get; set; }
	public String PollText { get; set; }

	public DualChoiceEmbedData(String pollText, String userAvatar, String userName, DateTimeOffset pollDate, String expiryString)
	{
		UserAvatar = userAvatar;
		UserName = userName;
		PollDate = pollDate;
		ExpiryString = expiryString;
		PollText = pollText;
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
			.WithAuthor(new EmbedAuthorBuilder()
				.WithIconUrl(UserAvatar)
				.WithName(UserName)
			)
			.AddField("Poll", $"{pollText}")
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
			barTypeLeft = "<:bar_left:991957378528456735>";
			barTypeMid = "<:bar:991957382223634432>";
			barTypeRight = "<:bar_right:991957380554313760>";
		}
		else {
			barTypeLeft = "<:negativebar_left:991957389085507624>";
			barTypeMid = "<:negativebar:991957392046706718>";
			barTypeRight = "<:negativebar_right:991957390612234261>";
		}

		Func<int,int> numStartBars = x => Math.Min(Math.Max(0, x), 1);
		Func<int,int> numMidBars = x => Math.Min(Math.Max(0, x-1), 8);
		Func<int,int> numEndBars = x => Math.Max(0, x-9);

		var barString = 
			string.Concat(Enumerable.Repeat(barTypeLeft, numStartBars(multiplier)))
			+ string.Concat(Enumerable.Repeat(barTypeMid, numMidBars(multiplier)))
			+ string.Concat(Enumerable.Repeat(barTypeRight, numEndBars(multiplier)))
			+ string.Concat(Enumerable.Repeat("<:emptybar_left:991957384643747870>", 1-numStartBars(multiplier)))
			+ string.Concat(Enumerable.Repeat("<:emptybar:991957387529437214>", 8-numMidBars(multiplier)))
			+ string.Concat(Enumerable.Repeat("<:emptybar_right:991957386174668820>", 1-numEndBars(multiplier)));

		return barString;
	}

}