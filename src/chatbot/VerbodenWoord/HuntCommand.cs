using Botje.Core;
using Botje.Core.Utils;
using Botje.DB;
using Botje.Messaging;
using Botje.Messaging.Events;
using chatbot.Services;
using Ninject;
using System;
using System.Linq;
using System.Text;

namespace chatbot.VerbodenWoord
{
    public class HuntCommand : IBotModule
    {
        private ILogger _log;

        [Inject]
        public IMessagingClient Client { get; set; }

        [Inject]
        public IDatabase DB { get; set; }

        [Inject]
        public ILoggerFactory LoggerFactory { set { _log = value.Create(GetType()); } }

        [Inject]
        public ITimeService TimeService { get; set; }

        private DbSet<Model.VerbodenWoordData> GetVerbodenWoordCollection() => DB.GetCollection<Model.VerbodenWoordData>("verbodenwoord");

        public void Shutdown()
        {
            _log.Trace($"Shut down {GetType().Name}");
            Client.OnPublicMessage -= Client_OnPublicMessage;
        }

        public void Startup()
        {
            _log.Trace($"Started {GetType().Name}");
            Client.OnPublicMessage += Client_OnPublicMessage;
        }

        private void Client_OnPublicMessage(object sender, Botje.Messaging.Events.PublicMessageEventArgs e)
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
            string[] args = e.Message.Text.Split(' ').Where(x => !string.IsNullOrWhiteSpace(x)).Skip(1).ToArray();

            switch (command)
            {
                case "/hunt":
                    Hunt(e, args);
                    break;
            }
        }

        private void Hunt(PublicMessageEventArgs e, string[] args)
        {
            int number = 5;
            if (args.Length >= 1) int.TryParse(args[0], out number);

            var collection = GetVerbodenWoordCollection();
            var woorden = collection.FindAll().OrderBy(x => x.CreationDate).Take(Math.Max(1, number)).ToList();
            var sb = new StringBuilder();
            sb.AppendLine($"Top {number} langststaande verboden woorden:");
            for (int i = 0; i < woorden.Count; i++)
            {
                var w = woorden[i];
                var lenstr = string.Join(",", w.Woorden.Select(x => $"{x.Length}"));
                string woordwoorden = w.Woorden.Count == 1 ? "woord" : "woorden";
                sb.AppendLine($" {i + 1}. {MessageUtils.HtmlEscape(w.OwnerName)} heeft al {TimeService.AsReadableTimespan(DateTime.UtcNow - w.CreationDate)} {w.Woorden.Count} {woordwoorden} [{lenstr}] staan");
            }
            Client.SendMessageToChat(e.Message.Chat.ID, $"{sb}");
        }
    }
}
