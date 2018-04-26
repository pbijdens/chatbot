using Botje.Core;
using Botje.DB;
using Botje.Messaging;
using Botje.Messaging.Events;
using Botje.Messaging.Models;
using chatbot.ChatMgmt.Model;
using chatbot.Utils;
using Ninject;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace chatbot.ChatMgmt
{
    /// <summary>
    /// Ban all players!
    /// </summary>
    public class Ban : IBotModule
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

        private DbSet<Model.BanRule> GetBanRuleCollection() => DB.GetCollection<Model.BanRule>();


        public void Startup()
        {
            _log.Trace($"Started {GetType().Name}");
            Client.OnPublicMessage += CheckForBanHandler;
            Client.OnPublicMessage += BanCommandHandler;
        }

        public void Shutdown()
        {
            _log.Trace($"Stopped {GetType().Name}");
            Client.OnPublicMessage -= CheckForBanHandler;
            Client.OnPublicMessage -= BanCommandHandler;
        }

        private void BanCommandHandler(object sender, Botje.Messaging.Events.PublicMessageEventArgs e)
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
                case "/bans":
                    OnBansCommand(e.Message);
                    break;
                case "/ban":
                    OnBanCommand(e.Message, command, args);
                    break;
                case "/unban":
                    OnUnBanCommand(e.Message, command, args);
                    break;
            }
        }

        private void OnBanCommand(Message message, string command, IEnumerable<string> args)
        {
            if (!ValidateCallerIsAdmin(message.Chat.ID, message.From))
            {
                Client.SendMessageToChat(message.Chat.ID, "Nee");
                return;
            }

            if (args.Count() == 0)
            {
                Client.SendMessageToChat(message.Chat.ID, $"Gebruik: /ban [-ID &lt;user ID&gt;] [-Expr &lt;regex&gt;] [-Reason &lt;reason&gt;]\nJe kan \" en \\ gebruiken zoals die overal werken.", "HTML", true, true, message.MessageID, null);
            }
            else
            {
                var aargs = args.ToArray();
                BanRule rule = new BanRule();
                for (int index = 0; index < aargs.Length; index++)
                {
                    if (string.Equals(aargs[index], "-id", StringComparison.InvariantCultureIgnoreCase) && index < aargs.Length - 1)
                    {
                        long.TryParse(aargs[index + 1], out long userID);
                        rule.UserID = userID;
                        index++;
                    }
                    else if (string.Equals(aargs[index], "-expr", StringComparison.InvariantCultureIgnoreCase) && index < aargs.Length - 1)
                    {
                        rule.UserNameExpression = string.IsNullOrWhiteSpace(aargs[index + 1]) ? null : aargs[index + 1];
                        index++;
                    }
                    else if (string.Equals(aargs[index], "-reason", StringComparison.InvariantCultureIgnoreCase) && index < aargs.Length - 1)
                    {
                        rule.Reason = string.IsNullOrWhiteSpace(aargs[index + 1]) ? null : aargs[index + 1];
                        index++;
                    }
                }

                rule.ChatID = message.Chat.ID;

                var me = Client.GetMe();
                if (rule.UserID == me.ID || IsRegexUserMatch(rule.UserNameExpression, me))
                {
                    Client.SendMessageToChat(message.Chat.ID, $"Nee. Ik ban mezelf niet. Dat kan je wel proberen, maar dat gaat niet gebeuren.", "HTML", true, true, message.MessageID, null);
                    return;
                }

                GetBanRuleCollection().Insert(rule);
                Client.SendMessageToChat(message.Chat.ID, $"- {rule.UniqueID}\n  Added /ban -ID \"{rule.UserID}\" -Expr \"{_(rule.UserNameExpression ?? "(none)")}\" -Reason \"{_(rule.Reason)}\"", "HTML", true, true, message.MessageID, null);
            }
        }

        private void OnUnBanCommand(Message message, string command, IEnumerable<string> args)
        {
            if (!ValidateCallerIsAdmin(message.Chat.ID, message.From))
            {
                Client.SendMessageToChat(message.Chat.ID, "Nee");
                return;
            }

            if (args.Count() == 0)
            {
                Client.SendMessageToChat(message.Chat.ID, $"Gebruik: /unban &lt;unique-id&gt;\nDe unique-id kan je vinden met het /bans commando.", "HTML", true, true, message.MessageID, null);
            }
            else
            {
                string uniqueID = args.First();
                var coll = GetBanRuleCollection();
                int num = coll.Delete(x => $"{x.UniqueID}" == uniqueID && x.ChatID == message.Chat.ID);
                Client.SendMessageToChat(message.Chat.ID, $"Ik heb {num} ban(s) verwijderd.", "HTML", true, true, message.MessageID, null);
            }
        }

        private void OnBansCommand(Message message)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<b>Bans:</b>");
            GetBanRuleCollection().FindAll().OrderBy(x => x.UniqueID).ToList().ForEach(x =>
            {
                sb.AppendLine($"- {x.UniqueID}: /ban -ID {x.UserID} -Expr {_(x.UserNameExpression)} -Reason {_(x.Reason)}");
            });
            sb.AppendLine("---");
            Client.SendMessageToChat(message.Chat.ID, sb.ToString(), "HTML", true, true, message.MessageID, null);
        }

        private void CheckForBanHandler(object sender, Botje.Messaging.Events.PublicMessageEventArgs e)
        {
            List<UserBan> bannedUsers = new List<UserBan>();
            if (e.Message.NewChatMembers != null)
            {
                e.Message.NewChatMembers.ForEach(x => CheckForBan(e.Message.Chat.ID, x, bannedUsers));
            }
            else if (e.Message.From != null)
            {
                CheckForBan(e.Message.Chat.ID, e.Message.From, bannedUsers);
            }

            bannedUsers.ForEach(bu =>
            {
                Client.SendMessageToChat(e.Message.Chat.ID, $"Gebruiker {bu.User.UsernameOrName()} is hier niet welkom. Er is/zijn {bu.Rules.Count} regels die dat zeggen.");
                // Client.BanUser(e.Message.Chat.ID, bu.User.ID);
            });
        }

        private void CheckForBan(long chatID, User candidate, List<UserBan> bannedUsers)
        {
            if (bannedUsers.Where(x => x.User.ID == candidate.ID).Any()) return; // already banned, enough is enough

            var i = Client.GetMe();
            if (candidate.ID == i.ID) { return; } // not considering banning myself, nice try, Toon.

            var rules = GetBanRuleCollection().Find(x => x.ChatID == chatID && (x.UserID == candidate.ID || IsRegexUserMatch(x.UserNameExpression, candidate)));
            if (rules.Any())
            {
                rules.ToList().ForEach(x =>
                {
                    _log.Info($"User {candidate.ID} / {candidate.UsernameOrName()} matches rule {x.UniqueID} :: userID = '{x.UserID}' regex = '{x.UserNameExpression}'");
                });
                bannedUsers.Add(new UserBan { User = candidate, Rules = rules.ToList() });
            }
        }

        private bool IsRegexUserMatch(string userNameExpression, User candidate)
        {
            if (string.IsNullOrWhiteSpace(userNameExpression)) return false;
            Regex re;
            try { re = new Regex(userNameExpression, RegexOptions.IgnoreCase); }
            catch (Exception ex)
            {
                _log.Error(ex, $"Can't parse expression '{userNameExpression}' as valid regex.");
                return false;
            }
            if (!string.IsNullOrWhiteSpace(candidate.Username) && re.IsMatch(candidate.Username))
            {
                return true;
            }
            string realname = $"{candidate.FirstName} {candidate.LastName}".Trim();
            if (!string.IsNullOrWhiteSpace(realname) && re.IsMatch(realname))
            {
                return true;
            }
            return false;
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
