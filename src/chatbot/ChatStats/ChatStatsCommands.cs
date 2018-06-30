using Botje.Core;
using Botje.Core.Utils;
using Botje.DB;
using Botje.Messaging;
using Botje.Messaging.Events;
using Botje.Messaging.Models;
using chatbot.ChatStats.Model;
using chatbot.Services;
using Ninject;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace chatbot.ChatStats
{
    public class ChatStatsCommands : IBotModule
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

        private DbSet<Model.UserStatistics> GetStatisticsCollection() => DB.GetCollection<Model.UserStatistics>("userstats");

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
                case "/mentions": ShowStatsTopList(args, e, "mentions", x => x.Mentions); break;
                case "/mentioned": ShowStatsTopList(args, e, "times mentioned", x => x.Mentioned); break;
                case "/characters": ShowStatsTopList(args, e, "characters sent", x => x.Characters); break;
                case "/forwarded": ShowStatsTopList(args, e, "times forwarded", x => x.Forwarded); break;
                case "/forwards": ShowStatsTopList(args, e, "forwards", x => x.Forwards); break;
                case "/hashtags": ShowStatsTopList(args, e, "hashtags used", x => x.Hashtags); break;
                case "/joins": ShowStatsTopList(args, e, "joins", x => x.Joined); break;
                case "/quits": ShowStatsTopList(args, e, "rage quits", x => x.Left); break;
                case "/lines": ShowStatsTopList(args, e, "lines sent", x => x.Lines); break;
                case "/repliedto": ShowStatsTopList(args, e, "times replied to", x => x.Replied); break;
                case "/replies": ShowStatsTopList(args, e, "times replied", x => x.Replies); break;
                case "/stickers": ShowStatsTopList(args, e, "stickers sent", x => x.Stickers); break;
                case "/messages": ShowStatsTopList(args, e, "messages sent", x => x.TotalMessages); break;
                case "/urls": ShowStatsTopList(args, e, "URLs sent", x => x.URLs); break;
                case "/words": ShowStatsTopList(args, e, "words sent", x => x.Words); break;

                case "/stats":
                case "/me":
                    ShowStatsForUser(args, e);
                    break;
            }
        }

        private void ShowStatsForUser(string[] args, PublicMessageEventArgs e)
        {
            TimeSpan ago = TimeSpan.FromDays(14);
            string agoParam = "2w";
            if (args.Length >= 1) agoParam = args[0];
            try { ago = TimeUtils.ParseTimeSpan(agoParam); }
            catch (FormatException)
            {
                Client.SendMessageToChat(e.Message.Chat.ID, $"Ik snap er niks van. Wat bedoel je met '{MessageUtils.HtmlEscape(agoParam)}'? Probeer eens '5d', '8u', '4w' of doe gek met '3w2d7u'.", "HTML", true);
                return;
            }

            var who = e.Message.Entities.Where(x => x.User != null).Select(x => x.User).FirstOrDefault();
            if (null == who)
            {
                var entity = e.Message.Entities.Where(x => x.Type == "mention" || x.Type == "text_mention").FirstOrDefault();
                if (null != entity)
                {
                    string userName = e.Message.Text.Substring(entity.Offset, entity.Length).TrimStart('@').ToLower();
                    var user = GetStatisticsCollection().Find((x) => x.UserNameLowerCase == userName && x.ChatID == e.Message.Chat?.ID).FirstOrDefault();
                    if (null != user)
                    {
                        who = new User
                        {
                            ID = user.UserId,
                            Username = user.UserName
                        };
                    }
                }
            }
            who = who ?? e.Message.From;

            var stats = GetStatisticsCollection().Find((x) => x.UserId == who?.ID && x.ChatID == e.Message.Chat?.ID).FirstOrDefault();
            if (null == stats)
            {
                Client.SendMessageToChat(e.Message.Chat.ID, "Geen statistieken gevonden voor deze gebruiker in deze chat.");
            }
            else
            {
                StringBuilder reply = BuildStatsForPlayer((int)ago.TotalDays, stats);
                Client.SendMessageToChat(e.Message.Chat.ID, $"{reply}");
            }
        }

        private StringBuilder BuildStatsForPlayer(int numberOfDays, UserStatistics stats)
        {
            string sep = "; ";

            var sinds = DateTime.UtcNow - TimeSpan.FromDays(numberOfDays);
            var yearDay = (sinds.Year * 1000) + sinds.DayOfYear;
            var totalBucket = new UserStatisticsBucket();
            var buckets = stats.Buckets.Where(x => x.YearDay >= yearDay).ToList();
            buckets.ForEach(x => x.AddTo(totalBucket));

            StringBuilder reply = new StringBuilder();
            reply.Append($"<b>Statistieken voor {numberOfDays} dagen {stats.UserName}:</b> ");
            reply.Append($"{totalBucket.TotalMessages} berichten");
            reply.Append($"{sep}{totalBucket.Characters} letters");
            reply.Append($"{sep}{totalBucket.Words} woorden");
            reply.Append($"{sep}{totalBucket.Lines} regels");
            reply.Append($"{sep}heeft {totalBucket.Forwards} berichten doorgestuurd");
            reply.Append($"{sep}heeft {totalBucket.Mentions} keer gementioned");
            reply.Append($"{sep}heeft {totalBucket.Replies} berichten beantwoord");
            reply.Append($"{sep}is {totalBucket.Forwarded} keer doorgestuurd");
            reply.Append($"{sep}is {totalBucket.Mentioned} keer gementioned");
            reply.Append($"{sep}is {totalBucket.Replied} keer beantwoord");

            if (totalBucket.Joined > 0)
            {
                reply.Append($"{sep}{totalBucket.Joined} keer de groep binnengekomen");
            }
            if (totalBucket.Left > 0)
            {
                reply.Append($"{sep}{totalBucket.Left} ragequits gedaan");
            }
            if (totalBucket.URLs > 0 || totalBucket.Stickers > 0)
            {
                reply.Append($"{sep}{totalBucket.URLs} web links");
            }

            var l = new List<string>();
            foreach (var kvp in _mt)
            {
                if (totalBucket.MessagesByType[(int)kvp.Key] > 0)
                {
                    reply.Append($"{sep}{totalBucket.MessagesByType[(int)kvp.Key]} {kvp.Value}");
                }
            }

            return reply;
        }

        public Dictionary<MessageType, string> _mt = new Dictionary<MessageType, string> {
            { MessageType.Voice, "spraakberichten" },
            { MessageType.Video, "videoboodschappen" },
            { MessageType.VideoNote, "video notities" },
            { MessageType.Location, "locaties" },
            { MessageType.Sticker, "stickers" },
            { MessageType.Document, "documenten" },
            { MessageType.Audio, "audioberichten" },
            { MessageType.TextMessage, "tekstberichten" },
        };

        private void ShowStatsTopList(string[] args, PublicMessageEventArgs e, string what, Func<UserStatisticsBucket, int> countMember)
        {
            TimeSpan ago = TimeSpan.Zero;
            string agoParam = "2w";
            string numParam = "42";

            if (args.Length >= 1) agoParam = args[0];
            if (args.Length >= 2) numParam = args[1];

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

            var collection = GetStatisticsCollection();

            int numberOfDays = (int)Math.Ceiling(ago.TotalDays);

            int grandTotal = 0;
            var sinds = DateTime.UtcNow - TimeSpan.FromDays(numberOfDays);
            var yearDay = (sinds.Year * 1000) + sinds.DayOfYear;
            var allStats = new List<(UserStatistics stats, int total)>();
            foreach (var stats in collection.FindAll())
            {
                int total = 0;
                var buckets = stats.Buckets.Where(x => x.YearDay >= yearDay).ToList();
                buckets.ForEach(x => total += countMember(x));
                if (total > 0)
                {
                    allStats.Add((stats, total));
                    grandTotal += total;
                }
            }

            var sortedAggregatedCollection = allStats.OrderByDescending(x => x.total).Take(number);
            if (sortedAggregatedCollection.Count() < number) { number = sortedAggregatedCollection.Count(); }

            StringBuilder result = new StringBuilder();
            result.AppendLine($"Top <b>{number}</b> {what} vanaf <b>{TimeService.AsDutchString(DateTime.Now - ago)}</b>:");
            int place = 1;
            foreach (var record in sortedAggregatedCollection)
            {
                result.AppendLine($"{place}: {MessageUtils.HtmlEscape(record.stats.UserName.TrimStart().TrimStart('@'))} ({record.total})");
                place++;
            }
            result.AppendLine($"<i>Totaal aantal {what}: {grandTotal} voor {allStats.Count()} personen</i>");

            Client.SendMessageToChat(e.Message.Chat.ID, $"{result}", "HTML", disableWebPagePreview: true, disableNotification: true);
        }

        private class ResultLine
        {
            public string Name { get; set; }
            public int Count { get; set; }
        }

    }
}
