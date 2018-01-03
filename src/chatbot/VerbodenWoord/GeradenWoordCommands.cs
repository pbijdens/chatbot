using Botje.Core;
using Botje.Core.Utils;
using Botje.DB;
using Botje.Messaging;
using Botje.Messaging.Events;
using chatbot.VerbodenWoord.Model;
using Ninject;
using System;
using System.Linq;
using System.Text;

namespace chatbot.VerbodenWoord
{
    public class GeradenWoordCommands : IBotModule
    {
        private ILogger _log;

        [Inject]
        public IMessagingClient Client { get; set; }

        [Inject]
        public IDatabase DB { get; set; }

        [Inject]
        public ILoggerFactory LoggerFactory { set { _log = value.Create(GetType()); } }

        private DbSet<Model.GeradenWoord> GetGeradenWoordCollection() => DB.GetCollection<Model.GeradenWoord>("geradenwoord");

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
                case "/geraden":
                    ProcessGeraden(args, e, "raders van een verboden woord", x => x.GuessedByUserNameLowerCase, x => x.When, x => x.GuessedByUserName, x => x.OwnerUserId != x.GuessedByUserId);
                    break;
                case "/geradenvan":
                    ProcessGeraden(args, e, "makers van een geraden verboden woord", x => x.OwnerUserNameLowerCase, x => x.When, x => x.OwnerUserName, x => x.OwnerUserId != x.GuessedByUserId);
                    break;

            }
        }

        private void ProcessGeraden(string[] args, PublicMessageEventArgs e, string whatisit, Func<GeradenWoord, string> groupByMember, Func<GeradenWoord, DateTime> whenMember, Func<GeradenWoord, string> nameMember, Func<GeradenWoord, bool> filter)
        {
            var collection = GetGeradenWoordCollection();

            TimeSpan ago = TimeSpan.Zero;
            string agoParam = "4w";
            string numParam = "42";

            if (args.Length >= 1)
            {
                agoParam = args[0];
            }

            if (args.Length >= 2)
            {
                numParam = args[1];
            }

            try
            {
                ago = TimeUtils.ParseTimeSpan(agoParam);
            }
            catch (FormatException)
            {
                Client.SendMessageToChat(e.Message.Chat.ID, $"Ik snap er niks van. Wat bedoel je met '{MessageUtils.HtmlEscape(agoParam)}'? Probeer eens '5d', '8u', '4w' of doe gek met '3w2d7u'.", "HTML", true);
                return;
            }

            if (!Int32.TryParse(numParam, out int number))
            {
                Client.SendMessageToChat(e.Message.Chat.ID, $"Ik snap er niks van. Wat bedoel je met '{MessageUtils.HtmlEscape(agoParam)}'? Hoe moet ik daar een getal van maken?", "HTML", true);
                return;
            }

            if (number == 0 || ago == TimeSpan.Zero)
            {
                Client.SendMessageToChat(e.Message.Chat.ID, $"Haha, erg grappig 👏 Hier zijn je nul resultaten, malloot.", "HTML", true);
                return;
            }

            var filteredCollection = collection.Find(x => filter(x) && whenMember(x) >= (DateTime.UtcNow - ago));
            var aggregatedCollection = filteredCollection.GroupBy(groupByMember).Select(cl => new ResultLine { Name = nameMember(cl.First()), Count = cl.Count() });
            var sortedAggregatedCollection = aggregatedCollection.OrderByDescending(x => x.Count).Take(number);
            if (sortedAggregatedCollection.Count() < number) { number = sortedAggregatedCollection.Count(); }
            StringBuilder result = new StringBuilder();
            result.AppendLine($"Top <b>{number}</b> {whatisit} vanaf <b>{TimeUtils.AsDutchString(DateTime.Now - ago)}</b>:");
            int place = 1;
            foreach (var record in sortedAggregatedCollection)
            {
                result.AppendLine($"{place}: {record.Name.TrimStart().TrimStart('@')} ({record.Count})");
                place++;
            }
            result.AppendLine($"<i>Totaal aantal records in deze periode: {filteredCollection.Count()} voor {aggregatedCollection.Count()} personen</i>");

            Client.SendMessageToChat(e.Message.Chat.ID, $"{result}", "HTML", disableWebPagePreview: true, disableNotification: true);
        }

        private class ResultLine
        {
            public string Name { get; set; }
            public int Count { get; set; }
        }

    }
}
