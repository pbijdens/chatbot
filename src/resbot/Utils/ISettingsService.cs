using System.Collections.Generic;

namespace resbot.Utils
{
    /// <summary>
    /// Settings manager.
    /// </summary>
    public interface ISettingsService
    {
        /// <summary>
        /// Array of timezone names to search for when setting up the one single timezone that the bot considers local for all users.
        /// </summary>
        List<string> Timezones { get; }

        /// <summary>
        /// Data directory. Can be an absolute path or a path relative to the startup directory.
        /// </summary>
        string DataDirectory { get; set; }
    }
}
