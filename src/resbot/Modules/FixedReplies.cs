using Botje.DB;
using Botje.Messaging.Models;
using Ninject;
using resbot.Models;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace resbot.Modules
{
    /// <summary>
    /// Responds to the /Whoami command. Informs the user about the chat they are in.
    /// </summary>
    public class FixedReplies : ChatCommandModuleBase
    {
        [Inject]
        public IDatabase DB { get; set; }

        public override void ProcessCommand(Source source, Message message, string command, string[] args, string argstr)
        {
            var settings = DB.GetCollection<ChannelSettings>().Find(x => x.ChatID == message.Chat.ID).SingleOrDefault();
            settings.Messages = settings.Messages ?? new System.Collections.Generic.Dictionary<string, string>();
            switch (command)
            {
                case "/set":
                    CmdSet(settings, message, args, argstr);
                    break;
                case "/list":
                    CmdList(settings, message, args);
                    break;
                case "/clear":
                    CmdClear(settings, message, args);
                    break;
                default:
                    foreach (var cmd in settings?.Messages ?? new System.Collections.Generic.Dictionary<string, string>())
                    {
                        if ($"/{cmd.Key}" == command)
                        {
                            Reply(settings, message, cmd.Key, cmd.Value, args);
                        }
                    }
                    break;
            }
        }

        private void CmdList(ChannelSettings settings, Message message, string[] args)
        {
            if (message.From.ID != settings.OwnerUserID && !settings.AdminUserIDs.Contains(message.From.ID))
            {
                Client.SendMessageToChat(settings.ChatID, $"You ({message.From.ID}) are not authorized to do this.", "HTML", true, false, message.MessageID);
                return;
            }

            StringBuilder reply = new StringBuilder();
            foreach (var key in settings.Messages.Keys.OrderBy(x => x))
            {
                reply.AppendLine($"<b>/{key}</b>\n{settings.Messages[key]}\n");
            }
            Client.SendMessageToChat(settings.ChatID, $"Messages ({settings.Messages.Keys.Count()}):\n\n{reply}", "HTML", true, false, message.MessageID);
        }

        private void Reply(ChannelSettings settings, Message message, string key, string value, string[] args)
        {
            Client.SendMessageToChat(settings.ChatID, value, "HTML", true, true, message.MessageID);
        }

        private static string[] ReservedWords = new string[] { "list", "set", "clear", "help", "start", "whoami", "claim", "whereami", "admin", "admins" };

        private void CmdSet(ChannelSettings settings, Message message, string[] args, string argstr)
        {
            if (message.From.ID != settings.OwnerUserID && !settings.AdminUserIDs.Contains(message.From.ID))
            {
                Client.SendMessageToChat(settings.ChatID, $"You ({message.From.ID}) are not authorized to do this.", "HTML", true, false, message.MessageID);
                return;
            }

            if (args?.Length < 2)
            {
                Client.SendMessageToChat(settings.ChatID, $"Usage: /set &lt;command&lt; followed by the reply message starting on a newline. You can use telegram HTML formatting and newlines in the reply.", "HTML", true, true, message.MessageID);
                return;
            }

            if (ReservedWords.Contains(args[0]?.ToLowerInvariant()))
            {
                Client.SendMessageToChat(settings.ChatID, "You can't use that command. Try another.", "HTML", true, true, message.MessageID);
                return;
            }

            int wsIndex = Regex.Match(argstr, "\\s").Index;
            string replyText = argstr.Substring(wsIndex).Trim().Replace("<", "_");
            int offset = (message.Text?.Trim() ?? "").Length - replyText.Length;
            foreach (var entity in message.Entities ?? new System.Collections.Generic.List<MessageEntity>())
            {
                entity.Offset -= offset;
            }
            foreach (var entity in message.Entities.OrderBy(x => -1 * x.Offset))
            {
                if (entity.Type == "bold")
                {
                    replyText = InsertAt(replyText, entity.Offset + entity.Length, "</b>");
                    replyText = InsertAt(replyText, entity.Offset, "<b>");
                }
                if (entity.Type == "italic")
                {
                    replyText = InsertAt(replyText, entity.Offset + entity.Length, "</i>");
                    replyText = InsertAt(replyText, entity.Offset, "<i>");
                }
            }
            settings.Messages[args[0]] = replyText;

            DB.GetCollection<ChannelSettings>().Update(settings);
            Client.SendMessageToChat(settings.ChatID, $"<b>/{args[0]}</b>\n{replyText}\n", "HTML", true, false, message.MessageID);
        }

        private string InsertAt(string s, int offset, string v)
        {
            return (offset > 0 ? s.Substring(0, offset) : "") + v + s.Substring(offset);
        }

        private void CmdClear(ChannelSettings settings, Message message, string[] args)
        {
            if (message.From.ID != settings.OwnerUserID && !settings.AdminUserIDs.Contains(message.From.ID))
            {
                Client.SendMessageToChat(settings.ChatID, $"You ({message.From.ID}) are not authorized to do this.", "HTML", true, true, message.MessageID);
                return;
            }

            if (args?.Length != 2)
            {
                Client.SendMessageToChat(settings.ChatID, $"Usage: /clear &lt;command&gt;", "HTML", true, true, message.MessageID);
                return;
            }

            if (settings.Messages.ContainsKey(args[0]))
            {
                settings.Messages.Remove(args[0]);
                DB.GetCollection<ChannelSettings>().Update(settings);
                Client.SendMessageToChat(settings.ChatID, $"<b>/{args[0]}</b> - It's gone.", "HTML", true, false, message.MessageID);
            }
        }
    }
}
