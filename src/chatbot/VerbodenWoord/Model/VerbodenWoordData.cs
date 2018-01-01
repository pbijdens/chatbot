using Botje.DB;
using System;
using System.Collections.Generic;

namespace chatbot.VerbodenWoord.Model
{
    public class VerbodenWoordData : IAtom
    {
        public Guid UniqueID { get; set; }

        public string ID { get; set; }

        public long OwnerUserId { get; set; }

        public List<string> Woorden { get; set; }

        public string OwnerName { get; set; }

        public long ReplyChatID { get; set; }

        public long ReplyMessageID { get; set; }

        public DateTime CreationDate { get; set; }

        public VerbodenWoordData()
        {
            Woorden = new List<string>();
        }
    }
}
