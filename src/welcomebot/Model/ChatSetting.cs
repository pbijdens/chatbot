using Botje.DB;
using System;

namespace welcomebot.Model
{
    public class ChatSetting : IAtom
    {
        public long ChatID { get; set; }

        public string Key { get; set; }

        public string Value { get; set; }

        public Guid UniqueID { get; set; }
    }
}
