using Botje.Core;
using Botje.Core.Utils;
using Botje.DB;
using Botje.Messaging;
using Botje.Messaging.Events;
using Botje.Messaging.PrivateConversation;
using chatbot.VerbodenWoord.Model;
using Ninject;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace chatbot.VerbodenWoord
{
    public class PublicChatModule : IBotModule
    {
        private ILogger _log;

        const string RegexInterpunction = @"\!\.\,\;\:\?\""\(\)\[\]";

        [Inject]
        public IMessagingClient Client { get; set; }

        [Inject]
        public IDatabase DB { get; set; }

        [Inject]
        public IPrivateConversationManager ConversationManager { get; set; }

        [Inject]
        public ILoggerFactory LoggerFactory { set { _log = value.Create(GetType()); } }

        [Inject]
        public IBotModule[] Modules { set { } }

        private DbSet<Model.VerbodenWoordData> GetVerbodenWoordCollection() => DB.GetCollection<Model.VerbodenWoordData>("verbodenwoord");
        private DbSet<Model.GeradenWoord> GetGeradenWoordCollection() => DB.GetCollection<Model.GeradenWoord>("geradenwoord");

        private static readonly string[] Maanden = new string[] { "januari", "februari", "maart", "april", "mei", "juni", "juli", "augustus", "september", "oktober", "november", "december" };

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
            else
            {
                DetectForbiddenWord(e);
            }
        }

        private Random _rnd = new Random();

        private object _woordenRegexpLock = new object();
        private Regex _woordenRegexp = null;

        public void OnWordsChanged()
        {
            lock (_woordenRegexpLock)
            {
                _woordenRegexp = null;
            }
        }

        private void DetectForbiddenWord(PublicMessageEventArgs e)
        {
            string input = e.Message.Text + ". " + e.Message.Caption;
            var verbodenWoordCollection = GetVerbodenWoordCollection();

            bool isVerbodenWoordMatch = CheckForPossibleWordMatch(input, verbodenWoordCollection);
            if (isVerbodenWoordMatch)
            {
                // check if the match is one of the eligible forbidden words
                var matchingRecords = new List<VerbodenWoordData>();
#if DEBUG
                var eligibleWoorden = verbodenWoordCollection.Find(x => true);
#else
                var eligibleWoorden = verbodenWoordCollection.Find(x => x.OwnerUserId != e.Message.From.ID);
#endif
                foreach (var record in eligibleWoorden)
                {
                    string expr = string.Join("|", record.Woorden.Select(x => $"{Regex.Escape(x)}").OrderBy(x => x).Distinct());
                    string pattern = $@"(?:^|[{RegexInterpunction}\s])({expr})(?:$|[{RegexInterpunction}\s])";
                    if (Regex.IsMatch(input, expr, RegexOptions.IgnoreCase))
                    {
                        matchingRecords.Add(record);
                    }
                }

                _log.Trace($"Message \"{input}\" matches {matchingRecords.Count} record(s).");
                if (matchingRecords.Count > 0)
                {
                    var selectedIndex = _rnd.Next(matchingRecords.Count);
                    var match = matchingRecords[selectedIndex];

                    verbodenWoordCollection.Delete(x => x.UniqueID == match.UniqueID);

                    var age = DateTime.UtcNow - match.CreationDate;
                    string message = $"Een verboden woord [{string.Join(", ", match.Woorden.Select(w => MessageUtils.HtmlEscape(w)))}] is geraden door {MessageUtils.HtmlEscape(e.Message.From.DisplayName())}. Dit woord was {TimeUtils.AsReadableTimespan(age)} geleden aangewezen door {MessageUtils.HtmlEscape(match.OwnerName)}.";

                    Client.SendMessageToChat(e.Message.Chat.ID, message, "HTML", true, false);
                    Client.SendMessageToChat(match.OwnerUserId, message, "HTML", true, false);
                    Client.ForwardMessageToChat(e.Message.Chat.ID, match.ReplyChatID, match.ReplyMessageID);

                    _log.Info($"Selected match with ID {match.ID} - {message}");

                    var geradenWoord = new GeradenWoord
                    {
                        GuessedByUserId = e.Message.From.ID,
                        GuessedByUserName = e.Message.From.UsernameOrName(),
                        GuessedByUserNameLowerCase = e.Message.From.UsernameOrName().ToLower(),
                        Message = e.Message,
                        OwnerUserId = match.OwnerUserId,
                        OwnerUserName = match.OwnerName,
                        OwnerUserNameLowerCase = match.OwnerName?.ToLower(),
                        VerbodenWoord = match,
                        When = DateTime.UtcNow
                    };
                    GetGeradenWoordCollection().Insert(geradenWoord);
                }
            }
        }

        private bool CheckForPossibleWordMatch(string input, DbSet<VerbodenWoordData> verbodenWoordCollection)
        {
            Regex re = _woordenRegexp;
            if (null == re)
            {
                lock (_woordenRegexpLock)
                {
                    var woorden = verbodenWoordCollection.Find(x => !string.IsNullOrEmpty(x.OwnerName));
                    string wordsExpression = string.Join("|", woorden.SelectMany(x => x.Woorden).Select(x => $"{Regex.Escape(x)}").OrderBy(x => x).Distinct());
                    string pattern = $@"(?:^|[{RegexInterpunction}\s])({wordsExpression})(?:$|[{RegexInterpunction}\s])";
                    _woordenRegexp = re = new Regex(pattern, RegexOptions.IgnoreCase);

                    _log.Trace($"Set regular expression to \"{re}\"");
                }
            }

            var matches = re.IsMatch(input);
            return matches;
        }

        // COMMANDS

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
            int number = 0;
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
                ago = ParseTimeSpan(agoParam);
            }
            catch (FormatException)
            {
                Client.SendMessageToChat(e.Message.Chat.ID, $"Ik snap er niks van. Wat bedoel je met '{MessageUtils.HtmlEscape(agoParam)}'? Probeer eens '5d', '8u', '4w' of doe gek met '3w2d7u'.", "HTML", true);
                return;
            }

            if (!Int32.TryParse(numParam, out number))
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
            result.AppendLine($"Top <b>{number}</b> {whatisit} vanaf <b>{Format(DateTime.Now - ago)}</b>:");
            int place = 1;
            foreach (var record in sortedAggregatedCollection)
            {
                result.AppendLine($"{place}: {record.Name.TrimStart().TrimStart('@')} ({record.Count})");
                place++;
            }
            result.AppendLine($"<i>Totaal aantal records in deze periode: {filteredCollection.Count()} voor {aggregatedCollection.Count()} personen</i>");

            Client.SendMessageToChat(e.Message.Chat.ID, $"{result}", "HTML", disableWebPagePreview: true, disableNotification: true);
        }

        private static string Format(DateTime dt)
        {
            return $"{dt.Day} {Maanden[dt.Month - 1]} {dt.ToString("HH:mm")}";
        }

        private TimeSpan ParseTimeSpan(string v)
        {
            TimeSpan result = TimeSpan.Zero;
            string number = "";
            foreach (char c in v)
            {
                if (char.IsDigit(c))
                {
                    number += c;
                }
                else
                {
                    switch (c)
                    {
                        case 'd':
                            result += TimeSpan.FromDays(Int32.Parse(number));
                            break;
                        case 'w':
                            result += TimeSpan.FromDays(7 * Int32.Parse(number));
                            break;
                        case 'h':
                        case 'u':
                            result += TimeSpan.FromHours(Int32.Parse(number));
                            break;
                    }
                    number = "";
                }
            }
            if (!string.IsNullOrWhiteSpace(number))
            {
                result += TimeSpan.FromDays(Int32.Parse(number));
            }
            return result;
        }

        private class ResultLine
        {
            public string Name { get; set; }
            public int Count { get; set; }
        }

    }
}
