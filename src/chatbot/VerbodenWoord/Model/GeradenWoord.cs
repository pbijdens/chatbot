using Botje.DB;
using System;

namespace chatbot.VerbodenWoord.Model
{
    public class GeradenWoord : IAtom
    {
        public Guid UniqueID { get; set; }

        public long GuessedByUserId { get; set; }

        public string GuessedByUserName { get; set; }

        public string GuessedByUserNameLowerCase { get; set; }

        public long OwnerUserId { get; set; }

        public string OwnerUserName { get; set; }

        public string OwnerUserNameLowerCase { get; set; }

        public VerbodenWoordData VerbodenWoord { get; set; }

        public Botje.Messaging.Models.Message Message { get; set; }

        public DateTime When { get; set; }

        public GeradenWoord()
        {
        }
    }
}
