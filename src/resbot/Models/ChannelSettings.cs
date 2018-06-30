using Botje.DB;
using System;
using System.Collections.Generic;

namespace resbot.Models
{
    /// <summary>
    /// Channel settings
    /// </summary>
    public class ChannelSettings : IAtom
    {
        /// <summary>
        /// 
        /// </summary>
        public ChannelSettings()
        {
            AdminUserIDs = new List<long>();
            Messages = new Dictionary<string, string>();
        }

        /// <summary>
        /// Unique ID of the chat.
        /// </summary>
        public Guid UniqueID { get; set; }

        /// <summary>
        /// Unique identifier of the chat for which these are the settings.
        /// </summary>
        public long ChatID { get; set; }

        /// <summary>
        /// Telegram user who claimed me first. Can't be removed as admin.
        /// </summary>
        public long? OwnerUserID { get; set; }

        /// <summary>
        /// Storing as User structure for convenience purposes only.
        /// </summary>
        public List<long> AdminUserIDs { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public Dictionary<string, string> Messages { get; set; }
    }
}
