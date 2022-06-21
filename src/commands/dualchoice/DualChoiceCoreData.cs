class DualChoiceCoreData {
	public String PollText { get; set; }
	public decimal Upvotes { get; set; }
	public decimal Downvotes { get; set; }
	public MessageScope messageScope { get; set; }

	public DualChoiceCoreData(String pollText, MessageScope messageScope, decimal upvotes, decimal downvotes)
	{
		PollText = pollText;
		Upvotes = upvotes;
		Downvotes = downvotes;
		this.messageScope = messageScope;
	}
}