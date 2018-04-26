using Botje.DB;
using System;

namespace chatbot.ChatMgmt.Model
{
    /// <summary>
    /// Record for banned users.
    /// </summary>
    public class BanRule : IAtom
    {
        /// <summary></summary>
        public Guid UniqueID { get; set; }

        /// <summary></summary>
        public long ChatID { get; set; }

        /// <summary></summary>
        public long UserID { get; set; }

        /// <summary></summary>
        public string UserNameExpression { get; set; }

        /// <summary></summary>
        public string Reason { get; set; }
    }
}
