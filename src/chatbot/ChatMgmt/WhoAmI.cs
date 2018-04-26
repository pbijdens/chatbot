using Botje.Core;
using Botje.Messaging;
using Botje.Messaging.Events;
using Ninject;
using System;
using System.Linq;
using Models = Botje.Messaging.Models;

namespace chatbot.ChatMgmt
{
    /// <summary>
    /// Responds to the /Whoami command. Informs the user about the chat they are in.
    /// </summary>
    public class WhoAmI : IBotModule
    {
        private ILogger _log;

        /// <summary></summary>
        [Inject]
        public IMessagingClient Client { get; set; }

        /// <summary></summary>
        [Inject]
        public ILoggerFactory LoggerFactory { set { _log = value.Create(GetType()); } }

        /// <summary>
        /// </summary>
        public void Shutdown()
        {
            Client.OnPrivateMessage -= Client_OnPrivateMessage;
            Client.OnChannelMessage -= Client_OnChannelMessage;
            Client.OnPublicMessage -= Client_OnPublicMessage;
            _log.Trace($"Shut down {GetType().Name}");
        }

        /// <summary>
        /// </summary>
        public void Startup()
        {
            _log.Trace($"Started {GetType().Name}");
            Client.OnPrivateMessage += Client_OnPrivateMessage;
            Client.OnChannelMessage += Client_OnChannelMessage;
            Client.OnPublicMessage += Client_OnPublicMessage;
        }

        private void Client_OnPublicMessage(object sender, PublicMessageEventArgs e)
        {
            ProcessMessage(e.Message);
        }

        private void Client_OnChannelMessage(object sender, ChannelMessageEventArgs e)
        {
            ProcessMessage(e.Message);
        }

        private void Client_OnPrivateMessage(object sender, PrivateMessageEventArgs e)
        {
            ProcessMessage(e.Message);
        }

        private void ProcessMessage(Models.Message message)
        {
            var me = Client.GetMe();
            var firstEntity = message?.Entities?.FirstOrDefault();
            if (null != firstEntity && firstEntity.Type == "bot_command" && firstEntity.Offset == 0)
            {
                string myName = Client.GetMe().Username;
                string commandText = message.Text.Substring(firstEntity.Offset, firstEntity.Length);
                if (commandText.Contains("@") && !commandText.EndsWith($"@{myName}", StringComparison.InvariantCultureIgnoreCase))
                {
                    // not for me
                    _log.Trace($"Got command '{commandText}' but it is not for me.");
                }
                else
                {
                    commandText = commandText.Split("@").First();
                    ProcessCommand(message, commandText);
                }
            }
        }

        private void ProcessCommand(Models.Message message, string command)
        {
            command = command?.ToLower() ?? String.Empty;
            string[] args = message.Text.Split(' ').Where(x => !string.IsNullOrWhiteSpace(x)).Skip(1).ToArray();

            var id = message.Chat.ID;

            switch (command.ToLowerInvariant())
            {
                case "/whoami":
                    CmdWhoAmI(message.Chat.ID, message.From);
                    break;
            }
        }

        private void CmdWhoAmI(long conversationID, Models.User who)
        {
            Client.SendMessageToChat(conversationID, $"<b>User properties:</b>"
                + $"\n - FirstName = {who.FirstName}"
                + $"\n - ID = {who.ID}"
                + $"\n - IsBot = {who.IsBot}"
                + $"\n - LanguageCode = {who.LanguageCode}"
                + $"\n - LastName = {who.LastName}"
                + $"\n - Username = {who.Username}"
                + $"\n - DisplayName() = {who.DisplayName()}" // calculated
                + $"\n - ShortName() = {who.ShortName()}" // calculated
                + $"\n - UsernameOrName() = {who.UsernameOrName()}" // calculated
                );
        }
    }
}
