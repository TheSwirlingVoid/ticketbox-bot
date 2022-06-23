class DualChoiceCoreData {
	public int Upvotes { get; set; }
	public int Downvotes { get; set; }
	public MessageScope messageScope { get; set; }

	public DualChoiceCoreData(MessageScope messageScope, int upvotes, int downvotes)
	{
		Upvotes = upvotes;
		Downvotes = downvotes;
		this.messageScope = messageScope;
	}
}