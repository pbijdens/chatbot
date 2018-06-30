using Botje.Core;
using Botje.Core.Utils;
using Botje.DB;
using Botje.Messaging;
using Botje.Messaging.Events;
using Botje.Messaging.PrivateConversation;
using Ninject;
using System;
using System.Linq;
using welcomebot.Model;

namespace welcomebot.TG
{
    public class SendMessageOnJoin : IBotModule
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
            var firstEntity = e.Message?.Entities?.FirstOrDefault();
            if (e.Message.NewChatMembers != null && e.Message.NewChatMembers.Count > 0 && info.PinnedMessage != null && !string.IsNullOrEmpty(info.PinnedMessage.Text))
            {
                string newUsers = string.Join(", ", e.Message.NewChatMembers.Select(x => '@' + x.ShortName()));

                Client.SendMessageToChat(e.Message.Chat.ID, MessageUtils.HtmlEscape(info.PinnedMessage.Text.Replace("$USERS$", newUsers)), "HTML", true, false);

                foreach (var member in e.Message.NewChatMembers)
                {
                    if (string.IsNullOrEmpty(member.Username))
                    {
                        string reply = GetUserSetting(e, "NoUsernameText");
                        if (!string.IsNullOrEmpty(reply))
                        {
                            Client.SendMessageToChat(e.Message.Chat.ID, MessageUtils.HtmlEscape($"{member.ShortName()}: {reply}"), "HTML", true, false);
                        }
                    }
                }
            }
            else if (null != firstEntity && firstEntity.Type == "bot_command" && firstEntity.Offset == 0)
            {
                string myName = Client.GetMe().Username;
                string commandText = e.Message.Text.Substring(firstEntity.Offset, firstEntity.Length);
                if (commandText.Contains("@") && !commandText.EndsWith($"@{myName}", StringComparison.InvariantCultureIgnoreCase))
                {
                    // not for me
                    _log.Trace($"Got command '{commandText}' but it is not for me.");
                }
                else
                {
                    commandText = commandText.Split("@").First();
                    ProcessCommand(e, commandText);
                }
            }
        }

        private void ProcessCommand(PublicMessageEventArgs e, string commandText)
        {
            switch (commandText)
            {
                case "/nousername":
                    string reply = e.Message.Text.Substring(e.Message.Entities.First().Length)?.Trim();
                    if (string.IsNullOrWhiteSpace(reply))
                    {
                        reply = GetUserSetting(e, "NoUsernameText");
                        if (string.IsNullOrWhiteSpace(reply)) reply = "not set.";
                        Client.SendMessageToChat(e.Message.Chat.ID, "<b>New reply: </b>" + MessageUtils.HtmlEscape($"{reply}"), "HTML", true, false);
                    }
                    else
                    {
                        UpdateUserSetting(e, "NoUsernameText", reply);
                        Client.SendMessageToChat(e.Message.Chat.ID, "<b>New reply: </b>" + MessageUtils.HtmlEscape($"{reply}"), "HTML", true, false);
                    }
                    break;
            }
        }

        private string GetUserSetting(PublicMessageEventArgs e, string settingKey)
        {
            var coll = DB.GetCollection<ChatSetting>();
            var kvp = coll.Find(x => x.ChatID == e.Message.Chat.ID && x.Key == settingKey).FirstOrDefault();
            if (null != kvp) return kvp.Value;
            return null;
        }

        private void UpdateUserSetting(PublicMessageEventArgs e, string settingKey, string settingValue)
        {
            var coll = DB.GetCollection<ChatSetting>();
            var kvp = coll.Find(x => x.ChatID == e.Message.Chat.ID && x.Key == settingKey).FirstOrDefault();
            if (null == kvp)
            {
                kvp = new ChatSetting
                {
                    ChatID = e.Message.Chat.ID,
                    Key = settingKey,
                    Value = settingValue,
                };
                coll.Insert(kvp);
            }
            else
            {
                kvp.Value = settingValue;
                coll.Update(kvp);
            }
        }
    }
}
