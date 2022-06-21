class MessageScope {
	public ulong ServerID {get; private set;}
	public ulong ChannelID {get; private set;}
	public ulong MessageID {get; private set;}

	public MessageScope(ulong serverId, ulong channelId, ulong messageId)
	{
		ServerID = serverId;
		ChannelID = channelId;
		MessageID = messageId;
	}

	public MessageScope serverId(ulong serverId)
	{
		ServerID = serverId;
		return this;
	}
	public MessageScope channelId(ulong channelId)
	{
		ChannelID = channelId;
		return this;
	}
	public MessageScope messageId(ulong messageId)
	{
		MessageID = messageId;
		return this;
	}
}