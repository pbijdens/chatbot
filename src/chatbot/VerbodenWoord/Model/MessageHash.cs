using Botje.DB;
using System;

namespace chatbot.VerbodenWoord.Model
{
    public class MessageHash : IAtom
    {
        public Guid UniqueID { get; set; }

        public string Hash { get; set; }

        public Botje.Messaging.Models.User User { get; set; }

        public DateTime UtcWhen { get; set; }

        public long ChatID { get; set; }

        public long MessageID { get; set; }

        public MessageHash()
        {
        }
    }
}
