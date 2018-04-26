using Botje.Core;
using Botje.DB;
using Botje.Messaging;
using Botje.Messaging.Events;
using Botje.Messaging.Models;
using chatbot.Utils;
using Ninject;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace chatbot.ChatMgmt
{
    /// <summary>
    /// Admin all players!
    /// </summary>
    public class Admin : IBotModule
    {
        private static readonly Func<string, string> _ = Botje.Core.Utils.MessageUtils.HtmlEscape;
        private ILogger _log;

        [Inject]
        public IMessagingClient Client { get; set; }

        [Inject]
        public IDatabase DB { get; set; }

        [Inject]
        public ISettingsService Settings { get; set; }

        [Inject]
        public ILoggerFactory LoggerFactory { set { _log = value.Create(GetType()); } }

        private DbSet<Model.Admin> GetAdminRuleCollection() => DB.GetCollection<Model.Admin>();


        public void Startup()
        {
            _log.Trace($"Started {GetType().Name}");
            Client.OnPublicMessage += AdminCommandHandler;
        }

        public void Shutdown()
        {
            _log.Trace($"Stopped {GetType().Name}");
            Client.OnPublicMessage -= AdminCommandHandler;
        }

        private void AdminCommandHandler(object sender, Botje.Messaging.Events.PublicMessageEventArgs e)
        {
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
                    ProcessCommand(e, commandText);
                }
            }
        }

        private void ProcessCommand(PublicMessageEventArgs e, string command)
        {
            command = command?.ToLower() ?? String.Empty;
            IEnumerable<string> args = StringUtils.SplitArgs(e.Message.Text).Skip(1);

            switch (command)
            {
                case "/admin":
                    OnAdminCommand(e.Message, command, args);
                    break;
            }
        }

        private void OnAdminCommand(Message message, string command, IEnumerable<string> args)
        {
            if (!ValidateCallerIsAdmin(message.Chat.ID, message.From))
            {
                Client.SendMessageToChat(message.Chat.ID, "Nee");
                return;
            }

            if (args.Count() == 0)
            {
                StringBuilder sb = new StringBuilder();
                string admins = string.Join(", ", Settings.AdministratorUserIDs.Concat(DB.GetCollection<Model.Admin>().Find(x => x.ChatID == message.Chat.ID).Select(x => $"{x.UserID}")));
                sb.AppendLine($"Gebruik: /Admin [+&lt;user ID&gt;] [-&lt;user ID&gt;]\n<b>Admins (/whoami):</b> {(admins)}");
                Client.SendMessageToChat(message.Chat.ID, sb.ToString(), "HTML", true, true, message.MessageID, null);
            }
            else
            {
                foreach (var a in args)
                {
                    if (a[0] == '+')
                    {
                        if (long.TryParse(a.Substring(1), out long userID))
                        {
                            DB.GetCollection<Model.Admin>().Insert(new Model.Admin
                            {
                                ChatID = message.Chat.ID,
                                PromotedBy = message.From,
                                PromotedWhenUtc = DateTime.UtcNow,
                                UserID = userID
                            });
                        }
                    }
                    else if (a[0] == '-')
                    {
                        if (long.TryParse(a.Substring(1), out long userID))
                        {
                            DB.GetCollection<Model.Admin>().Delete(x => (x.UserID == userID) && (x.ChatID == message.Chat.ID));
                        }
                    }
                    StringBuilder sb = new StringBuilder();
                    string admins = string.Join(", ", Settings.AdministratorUserIDs.Concat(DB.GetCollection<Model.Admin>().Find(x => x.ChatID == message.Chat.ID).Select(x => $"{x.UserID}")));
                    sb.AppendLine($"<b>Admins zijn nu:</b> {_(admins)}");
                    Client.SendMessageToChat(message.Chat.ID, sb.ToString(), "HTML", true, true, message.MessageID, null);
                }
            }
        }

        private bool ValidateCallerIsAdmin(long chatID, User user)
        {
            bool isAdmin = false;
            if (Settings.AdministratorUserIDs.Contains($"{user.ID}")) isAdmin = true;
            isAdmin = isAdmin || DB.GetCollection<Model.Admin>().Find(x => x.ChatID == chatID && x.UserID == user.ID).Any();
            return isAdmin;
        }
    }
}
