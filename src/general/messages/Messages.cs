using MongoDB.Bson;

static class Messages {
	public static readonly String INSUFFICIENT_BASE_BOT_PERMS = (
		"I need __all__ of the following channel perms to create and update polls here: "
		+ $"**Send Messages**, **View Channel**, **Create Public Threads**, and **Create Private Threads**!"
	);

	public static readonly String INSUFFICIENT_CLOSE_PERMS = (
		"You need the **Manage Messages** permission or need to be an **Administrator** to close polls!"
	);

	public static readonly String ADMIN_REQUIRED = (
		"You need to be an **Administrator** to change my server settings!"
	);

	public static readonly String POLL_FAIL = (
		"Interaction failed! (Has this poll been closed?)!"
	);

	public static readonly String POLL_CLOSE_SUCCESS = (
		"Poll successfully closed!"
	);

	public static readonly String VOTE_SWITCH_SUCCESS = (
		"Vote successfully switched!"
	);

	public static readonly String VOTE_SUCCESS = (
		"Vote successful!"
	);

	public static readonly String VOTE_RETRACT_SUCCESS = (
		"Vote successfully removed!"
	);

	public static readonly String VOTE_REDUNDANT = (
		"You have already selected this vote! You can still change your vote by clicking the other option."
	);

	public static readonly String NO_RETRACTABLE_VOTE = (
		"You haven't voted yet! You can retract your vote after you select one."
	);

	public static readonly String INVALID_EXPIRY_DAYS = (
		"You need to enter a whole number from `1-14`!"
	);

	public static readonly String NO_POLL_DATA = (
		"No poll data was found! This poll should be deleted."
	);

	public static String updatedSetting(String subCmdName, BsonValue value)
	{
		return $"The setting `{subCmdName}` was successfully updated to `{value}`!";
	}
}