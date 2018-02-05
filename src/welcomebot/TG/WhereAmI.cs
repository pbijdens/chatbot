using Botje.Core;
using Botje.DB;
using Botje.Messaging;
using Botje.Messaging.Events;
using Botje.Messaging.Models;
using Ninject;
using System;
using System.Linq;

namespace welcomebot.TG
{
    public class WhereAmI : IBotModule
    {
        private ILogger _log;

        [Inject]
        public IMessagingClient Client { get; set; }

        [Inject]
        public IDatabase DB { get; set; }

        [Inject]
        public ILoggerFactory LoggerFactory { set { _log = value.Create(GetType()); } }

        [Inject]
        public ISettingsManager Settings { get; set; }

        public void Startup()
        {
            _log.Trace($"Started {GetType().Name}");
            Client.OnPrivateMessage += Client_OnPrivateMessage;
            Client.OnChannelMessage += Client_OnChannelMessage;
            Client.OnPublicMessage += Client_OnPublicMessage;
        }

        public void Shutdown()
        {
            Client.OnPrivateMessage -= Client_OnPrivateMessage;
            Client.OnChannelMessage -= Client_OnChannelMessage;
            Client.OnPublicMessage -= Client_OnPublicMessage;
            _log.Trace($"Shut down {GetType().Name}");
        }

        private void Client_OnPublicMessage(object sender, PublicMessageEventArgs e)
        {
            var me = Client.GetMe();
            var firstEntity = e.Message?.Entities?.FirstOrDefault();
            if (null != firstEntity && firstEntity.Type == "bot_command" && firstEntity.Offset == 0)
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
                    ProcessCommand(e.Message, commandText);
                }
            }
        }

        private void Client_OnChannelMessage(object sender, ChannelMessageEventArgs e)
        {
            var me = Client.GetMe();
            var firstEntity = e.Message?.Entities?.FirstOrDefault();
            if (null != firstEntity && firstEntity.Type == "bot_command" && firstEntity.Offset == 0)
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
                    ProcessCommand(e.Message, commandText);
                }
            }
        }

        private void Client_OnPrivateMessage(object sender, PrivateMessageEventArgs e)
        {
            var me = Client.GetMe();
            var firstEntity = e.Message?.Entities?.FirstOrDefault();
            if (null != firstEntity && firstEntity.Type == "bot_command" && firstEntity.Offset == 0)
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
                    ProcessCommand(e.Message, commandText);
                }
            }
        }

        private void ProcessCommand(Message message, string commandText)
        {
            var id = message.Chat.ID;
            if (commandText == "/whereami")
            {
                Chat chat = Client.GetChat(id);
                Client.SendMessageToChat(id, $"<b>Private chat:</b> \n"
                    + $"ID = {id}\n"
                    + $"Description = {chat.Description}\n"
                    + $"Username = {chat.Username}\n"
                    + $"FirstName = {chat.FirstName}\n"
                    + $"LastName = {chat.LastName}\n"
                    + $"InviteLink = {chat.InviteLink}\n"
                    + $"PinnedMessage.ID = {chat.PinnedMessage?.MessageID}\n"
                    + $"PinnedMessage.Text = {chat.PinnedMessage?.Text}\n"
                    + $"Title = {chat.Title}\n"
                    + $"Type = {chat.Type}\n"
                    );
            }
        }
    }
}
