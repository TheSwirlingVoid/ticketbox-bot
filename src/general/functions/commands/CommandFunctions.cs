using Discord.WebSocket;
using MongoDB.Bson;

static class CommandFunctions {

	/// returns an error message, if the conversion failed.
	public static bool validateValue(String subCommand, SocketSlashCommandDataOption value, out BsonValue validated)
	{
		string stringValue = (string)value;
		switch(subCommand)
		{
			// validate expiry days input
			case "expiry_days":
				int result;
				bool isInt = Int32.TryParse(stringValue, out result);
				if (isInt && result>0 && result<=14)
				{
					validated = result;
					return true;
				}
				else
					validated = BsonValue.Create(null);
					return false;

			// will be verified as bool by discord
			case "create_threads":
				validated = Boolean.Parse(stringValue);
				return true;

			default:
				validated = BsonValue.Create(null);
				return false;
		}
	}

	public static String getParameterFailString(String subCommand)
	{
		switch (subCommand)
		{
			case "expiry_days":
				return Messages.INVALID_EXPIRY_DAYS;

			default:
				return "";
		}
	}
}