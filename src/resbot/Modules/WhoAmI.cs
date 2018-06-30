using Botje.Messaging.Models;

namespace resbot.Modules
{
    /// <summary>
    /// Responds to the /Whoami command. Informs the user about the chat they are in.
    /// </summary>
    public class WhoAmI : ChatCommandModuleBase
    {
        public override void ProcessCommand(Source source, Message message, string command, string[] args, string argstr)
        {
            switch (command)
            {
                case "/whoami":
                    CmdWhoAmI(message.Chat.ID, message.From);
                    break;
            }
        }

        private void CmdWhoAmI(long conversationID, User who)
        {
            Client.SendMessageToChat(conversationID, $"<b>User:</b> " + _(who.ToString()));
        }
    }
}
