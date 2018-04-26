using Botje.DB;
using Botje.Messaging.Models;
using System;

namespace chatbot.ChatMgmt.Model
{
    /// <summary></summary>
    public class Admin : IAtom
    {
        /// <summary></summary>
        public Guid UniqueID { get; set; }

        /// <summary></summary>
        public long ChatID { get; set; }

        /// <summary></summary>
        public long UserID { get; set; }

        /// <summary></summary>
        public User PromotedBy { get; set; }

        /// <summary></summary>
        public DateTime PromotedWhenUtc { get; set; }
    }
}
