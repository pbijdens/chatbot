using Botje.Core;
using Botje.Core.Utils;
using Botje.DB;
using Botje.Messaging;
using Botje.Messaging.PrivateConversation;
using Ninject;
using System.Linq;

namespace welcomebot.TG
{
    public class SendPinnedMessageOnJoin : IBotModule
    {
        private ILogger _log;

        [Inject]
        public IMessagingClient Client { get; set; }

        [Inject]
        public IDatabase DB { get; set; }

        [Inject]
        public IPrivateConversationManager ConversationManager { get; set; }

        [Inject]
        public ILoggerFactory LoggerFactory { set { _log = value.Create(GetType()); } }

        [Inject]
        public IBotModule[] Modules { set { /* _eventHandler = value.OfType<RaidEventHandler>().FirstOrDefault(); */ } }

        public void Startup()
        {
            _log.Trace($"Started {GetType().Name}");
            Client.OnPublicMessage += Client_OnPublicMessage;
        }

        public void Shutdown()
        {
            Client.OnPublicMessage -= Client_OnPublicMessage;
            _log.Trace($"Shut down {GetType().Name}");
        }

        private void Client_OnPublicMessage(object sender, Botje.Messaging.Events.PublicMessageEventArgs e)
        {
            var info = Client.GetChat(e.Message.Chat.ID);
            if (e.Message.NewChatMembers != null && e.Message.NewChatMembers.Count > 0 && info.PinnedMessage != null && !string.IsNullOrEmpty(info.PinnedMessage.Text))
            {
                string newUsers = string.Join(", ", e.Message.NewChatMembers.Select(x => '@' + x.ShortName()));

                Client.SendMessageToChat(e.Message.Chat.ID, MessageUtils.HtmlEscape(info.PinnedMessage.Text.Replace("$USERS$", newUsers)), "HTML", true, false);

                foreach (var member in e.Message.NewChatMembers)
                {
                    if (string.IsNullOrEmpty(member.Username))
                    {
                        Client.SendMessageToChat(e.Message.Chat.ID, MessageUtils.HtmlEscape($"{member.ShortName()}: Je hebt nog geen Telegram gebruikersnaam ingesteld. Je kan pas worden toegevoegd aan chats als je dat doet."), "HTML", true, false);
                    }
                }
            }
        }
    }
}
