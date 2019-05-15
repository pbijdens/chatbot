using Botje.Messaging.Models;
using Botje.Messaging.Telegram;

namespace chatbot
{
    public class JeHoofdTelegramClient : ThrottlingTelegramClient
    {
        public JeHoofdTelegramClient()
        {
        }

        private User _me;
        public override User GetMe()
        {
            if (null != _me) return _me;
            var result = base.GetMe();
            _me = result;
            return result;
        }
    }
}
