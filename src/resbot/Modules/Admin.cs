using Botje.DB;
using Botje.Messaging.Models;
using Ninject;
using resbot.Models;
using System.Linq;

namespace resbot.Modules
{
    /// <summary>
    /// Responds to the /admin command
    /// </summary>
    public class Admin : ChatCommandModuleBase
    {
        [Inject]
        public IDatabase DB { get; set; }

        public override void ProcessCommand(Source source, Message message, string command, string[] args, string argstr)
        {
            switch (command)
            {
                case "/admin":
                case "/admins":
                    CmdAdmin(message.Chat.ID, message, args, argstr);
                    break;
            }
        }

        private void CmdAdmin(long conversationID, Message message, string[] args, string argstr)
        {
            var settings = DB.GetCollection<ChannelSettings>().Find(x => x.ChatID == message.Chat.ID).SingleOrDefault();
            if (null == settings || !(settings.OwnerUserID.HasValue))
            {
                Client.SendMessageToChat(settings.ChatID, $"I have not been configured yet to accept any commands for this chat. Did tou /claim the chat?", "HTML", true, false, message.MessageID);
                return;
            }

            if (message.From.ID != settings.OwnerUserID && !settings.AdminUserIDs.Contains(message.From.ID))
            {
                Client.SendMessageToChat(settings.ChatID, $"You ({message.From.ID}) are not authorized to do this.", "HTML", true, false, message.MessageID);
                return;
            }

            string command = (args.FirstOrDefault() ?? "").ToLowerInvariant();
            if (args.Length != 2 || (command != "add" && command != "list" && command != "remove"))
            {
                Client.SendMessageToChat(conversationID, $"Usage: /admin &lt;add|remove|list&gt; &lt;userid&gt;\nMake the user use the /whoami command to obtian their Telegram user ID.", "HTML", true, false, message.MessageID);
                return;
            }

            switch (command)
            {
                case "add":
                    if (long.TryParse(args[1], out long userToAdd))
                    {
                        settings.AdminUserIDs.Add(userToAdd);
                        ListAdminsInReplyTo(message, settings);
                        DB.GetCollection<ChannelSettings>().Update(settings);
                    }
                    else
                    {
                        Client.SendMessageToChat(settings.ChatID, $"That's not a valid user ID, user IDs should be numeric. Make the user do /whoami and use that ID.", "HTML", true, false, message.MessageID);
                    }
                    break;
                case "remove":
                case "delete":
                    if (long.TryParse(args[1], out long userToRemove))
                    {
                        settings.AdminUserIDs.RemoveAll(x => x == userToRemove);
                        ListAdminsInReplyTo(message, settings);
                        DB.GetCollection<ChannelSettings>().Update(settings);
                    }
                    else
                    {
                        Client.SendMessageToChat(settings.ChatID, $"That's not a valid user ID, user IDs should be numeric. Make the user do /whoami and use that ID.", "HTML", true, false, message.MessageID);
                    }
                    break;
                case "list":
                    ListAdminsInReplyTo(message, settings);
                    break;
                default: // no
                    break;
            }
        }

        private void ListAdminsInReplyTo(Message message, ChannelSettings settings)
        {
            string text = $"Admins ({settings.AdminUserIDs.Count}) are: " + string.Join(", ", settings.AdminUserIDs.Select(x => $"{x}"));
            Client.SendMessageToChat(settings.ChatID, text, "HTML", true, false, message.MessageID);
        }
    }
}
