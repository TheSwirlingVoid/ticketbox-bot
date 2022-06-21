class DualChoiceCoreData {
	public String PollText { get; set; }
	public int Upvotes { get; set; }
	public int Downvotes { get; set; }
	public MessageScope messageScope { get; set; }
	public ulong UserID { get; set; }

	public DualChoiceCoreData(String pollText, MessageScope messageScope, ulong userId, int upvotes, int downvotes)
	{
		PollText = pollText;
		Upvotes = upvotes;
		Downvotes = downvotes;
		this.messageScope = messageScope;
		UserID = userId;
	}
}