class BaseDocumentData {
	public ulong MessageID {get; private set;}
	public String PollText {get; private set;}
	public long ExpiryTime {get; private set;}

	public BaseDocumentData(BaseMessageData msgData, BaseTimeData timeData)
	{
		PollText = msgData.pollText;
		MessageID = msgData.messageId;
		ExpiryTime = timeData.expiryTime;
	}
}

class BaseMessageData {

	public ulong messageId;
	public String pollText;

	public BaseMessageData(ulong messageId, String pollText)
	{
		this.messageId = messageId;
		this.pollText = pollText;
	}
}

class BaseTimeData {

	public long expiryTime;

	public BaseTimeData(long expiryTime)
	{
		this.expiryTime = expiryTime;
	}
}