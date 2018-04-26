using Botje.Messaging.Models;
using System.Collections.Generic;

namespace chatbot.ChatMgmt.Model
{
    /// <summary></summary>
    public class UserBan
    {
        /// <summary></summary>
        public User User { get; set; }

        /// <summary></summary>
        public List<BanRule> Rules { get; set; }
    }
}
