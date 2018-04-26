using Botje.Core;
using Botje.Core.Commands;
using Botje.Core.Utils;
using Botje.DB;
using Botje.Messaging;
using Botje.Messaging.PrivateConversation;
using Ninject;
using System;
using System.Linq;

namespace chatbot.VerbodenWoord
{
    public class VwCommand : IConsoleCommand
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
        public ISettingsService Settings { get; set; }

        private DbSet<Model.VerbodenWoordData> GetVerbodenWoordCollection() => DB.GetCollection<Model.VerbodenWoordData>("verbodenwoord");
        private DbSet<Model.GeradenWoord> GetGeradenWoordCollection() => DB.GetCollection<Model.GeradenWoord>("geradenwoord");

        public CommandInfo Info => new CommandInfo
        {
            Aliases = new string[] { "status" },
            Command = "vw",
            DetailedHelp = "",
            QuickHelp = "Show status of verboden woord data",
        };

        public bool OnInput(string command, string[] args)
        {
            var woorden = GetVerbodenWoordCollection().Find(x => true).OrderBy(x => x.CreationDate).ToList();
            foreach (var w in woorden)
            {
                var str = string.Join(", ", w.Woorden);
                Console.WriteLine($"- {w.ID} : {TimeUtils.AsReadableTimespan(DateTime.UtcNow - w.CreationDate)} oud van {w.OwnerName} [#{w.OwnerUserId}]");
                Console.WriteLine($"  Woorden: {str}");
                Console.WriteLine($"  Reply: {w.ReplyChatID} {w.ReplyMessageID}");
            }
            return true;
        }

        public void OnStart(ILogger logger)
        {
        }
    }
}
