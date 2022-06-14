class DualChoiceData {
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
	public bool ClosedVoting { get; private set;}

	public DualChoiceData()
	{
		PollText = "";
		UserAvatar = "";
		UserName = "";
		ExpiryDate = "";
	}

	public DualChoiceData pollText(string newText)
	{
		PollText = newText;
		return this;
	}
	public DualChoiceData userAvatar(string avatarURL)
	{
		UserAvatar = avatarURL;
		return this;
	}
	public DualChoiceData userName(string username)
	{
		UserName = username;
		return this;
	}
	public DualChoiceData totalVoters(decimal totalVoters)
	{
		TotalVoters = totalVoters;
		return this;
	}
	public DualChoiceData percentUpvoted(decimal percentUpvoted)
	{
		PercentUpvoted = percentUpvoted;
		return this;
	}
	public DualChoiceData percentDownvoted(decimal percentDownvoted)
	{
		PercentDownvoted = percentDownvoted;
		return this;
	}
	public DualChoiceData upvotes(decimal upvotes)
	{
		Upvotes = upvotes;
		return this;
	}
	public DualChoiceData downvotes(decimal downvotes)
	{
		Downvotes = downvotes;
		return this;
	}
	public DualChoiceData pollDate(DateTimeOffset pollDate)
	{
		PollDate = pollDate;
		return this;
	}
	public DualChoiceData expiryDate(String expiryDate)
	{
		ExpiryDate = expiryDate;
		return this;
	}
	public DualChoiceData closedVoting(bool closedVoting)
	{
		ClosedVoting = closedVoting;
		return this;
	}
}