class SuggestData {
	public String PollText { get; private set;}
	public String UserAvatar { get; private set;}
	public String UserName { get; private set;}
	public decimal TotalVoters { get; private set;}
	public decimal PercentUpvoted { get; private set;}
	public decimal PercentDownvoted { get; private set;}
	public decimal Upvotes { get; private set;}
	public decimal Downvotes { get; private set;}
	public DateTimeOffset PollDate { get; private set;}
	public String ExpiryDate { get; private set;}

	public SuggestData()
	{
		PollText = "";
		UserAvatar = "";
		UserName = "";
		ExpiryDate = "";
	}

	public SuggestData pollText(string newText)
	{
		PollText = newText;
		return this;
	}
	public SuggestData userAvatar(string avatarURL)
	{
		UserAvatar = avatarURL;
		return this;
	}
	public SuggestData userName(string username)
	{
		UserName = username;
		return this;
	}
	public SuggestData totalVoters(decimal totalVoters)
	{
		TotalVoters = totalVoters;
		return this;
	}
	public SuggestData percentUpvoted(decimal percentUpvoted)
	{
		PercentUpvoted = percentUpvoted;
		return this;
	}
	public SuggestData percentDownvoted(decimal percentDownvoted)
	{
		PercentDownvoted = percentDownvoted;
		return this;
	}
	public SuggestData upvotes(decimal upvotes)
	{
		Upvotes = upvotes;
		return this;
	}
	public SuggestData downvotes(decimal downvotes)
	{
		Downvotes = downvotes;
		return this;
	}
	public SuggestData pollDate(DateTimeOffset pollDate)
	{
		PollDate = pollDate;
		return this;
	}
	public SuggestData expiryDate(String expiryDate)
	{
		ExpiryDate = expiryDate;
		return this;
	}
}