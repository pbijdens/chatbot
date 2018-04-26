using System.Collections.Generic;

namespace chatbot
{
    /// <summary>
    /// </summary>
    public interface ISettingsService
    {
        /// <summary>
        /// </summary>
        string BotKey { get; }
        /// <summary>
        /// </summary>
        List<string> Timezones { get; }
        /// <summary>
        /// </summary>
        List<string> AdministratorUserIDs { get; }
    }
}
