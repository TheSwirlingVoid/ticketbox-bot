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
			barTypeLeft = "<:bar_left:989295222901063712>";
			barTypeMid = "<:bar:989295226566873168>";
			barTypeRight = "<:bar_right:989295225174360094>";
		}
		else {
			barTypeLeft = "<:negativebar_left:989295192328765511>";
			barTypeMid = "<:negativebar:989295194367221851>";
			barTypeRight = "<:negativebar_right:989295193348014150>";
		}

		Func<int,int> numStartBars = x => Math.Min(Math.Max(0, x), 1);
		Func<int,int> numMidBars = x => Math.Min(Math.Max(0, x-1), 8);
		Func<int,int> numEndBars = x => Math.Max(0, x-9);

		var barString = 
			string.Concat(Enumerable.Repeat(barTypeLeft, numStartBars(multiplier)))
			+ string.Concat(Enumerable.Repeat(barTypeMid, numMidBars(multiplier)))
			+ string.Concat(Enumerable.Repeat(barTypeRight, numEndBars(multiplier)))
			+ string.Concat(Enumerable.Repeat("<:bar_empty_left:989295218849361920>", 1-numStartBars(multiplier)))
			+ string.Concat(Enumerable.Repeat("<:bar_empty:989295221638569985>", 8-numMidBars(multiplier)))
			+ string.Concat(Enumerable.Repeat("<:bar_empty_right:989295220443197500>", 1-numEndBars(multiplier)));

		return barString;
	}

}