using Botje.DB;
using Botje.Messaging.Models;
using Ninject;
using resbot.Models;
using System.Linq;

namespace resbot.Modules
{
    /// <summary>
    /// Responds to the /Whoami command. Informs the user about the chat they are in.
    /// </summary>
    public class Claim : ChatCommandModuleBase
    {
        [Inject]
        public IDatabase DB { get; set; }

        public override void ProcessCommand(Source source, Message message, string command, string[] args, string argstr)
        {
            switch (command)
            {
                case "/claim":
                    CmdClaim(message.Chat.ID, message.From);
                    break;
            }
        }

        private void CmdClaim(long chatID, User who)
        {
            var channelSettingsTable = DB.GetCollection<ChannelSettings>();
            var existingChat = channelSettingsTable.Find(x => x.ChatID == chatID).SingleOrDefault();
            if (null == existingChat)
            {
                existingChat = new ChannelSettings
                {
                    ChatID = chatID,
                    AdminUserIDs = new System.Collections.Generic.List<long>()
                };
                channelSettingsTable.Insert(existingChat);
            }

            if (existingChat.OwnerUserID.HasValue)
            {
                Client.SendMessageToChat(chatID, $"You can't do that.");
            }
            else
            {
                existingChat.OwnerUserID = who.ID;
                channelSettingsTable.Update(existingChat);
                Client.SendMessageToChat(chatID, $"This chat is yours now.");
            }
        }
    }
}
