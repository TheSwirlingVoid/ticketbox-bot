class BotSettings {
	public int ExpiryDays { get; private set; }

	public BotSettings expiryDays(int expiryDays)
	{
		ExpiryDays = expiryDays;
		return this;
	}
}