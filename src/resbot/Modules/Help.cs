using Botje.DB;
using Botje.Messaging.Models;
using Ninject;

namespace resbot.Modules
{
    /// <summary>
    /// Responds to the /admin command
    /// </summary>
    public class Help : ChatCommandModuleBase
    {
        [Inject]
        public IDatabase DB { get; set; }

        public override void ProcessCommand(Source source, Message message, string command, string[] args, string argstr)
        {
            switch (command)
            {
                case "/start":
                case "/help":
                    CmdHelp(message.Chat.ID, message, args, argstr);
                    break;
            }
        }

        private void CmdHelp(long conversationID, Message message, string[] args, string argstr)
        {
            Client.SendMessageToChat(conversationID, System.IO.File.ReadAllText("helpfile.txt"), "HTML", true, false, message.MessageID);
        }
    }
}
