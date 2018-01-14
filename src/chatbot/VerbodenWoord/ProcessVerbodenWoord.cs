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
using System.Text.RegularExpressions;

namespace chatbot.VerbodenWoord
{
    public class ProcessVerbodenWoord : IBotModule
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
            if (e.Message.ForwardFrom == null && (null == firstEntity || firstEntity.Offset != 0 || firstEntity.Type != "bot_command"))
            {
                bool isduplicate = IsDuplicateInput(e);
                if (!isduplicate)
                {
                    DetectForbiddenWord(e);
                }
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

        private bool IsDuplicateInput(PublicMessageEventArgs e)
        {
            // clean up the input, lowercase it and replace all consecutive non-readable characters to a single _
            string str = Regex.Replace($"{e.Message?.Text}{e.Message?.Caption}".ToLowerInvariant(), "[^a-zA-Z0-9]*", "_");
            if (str.Length < 128) return false;
            var hash = HashUtils.CalculateSHA1Hash(str);
            var hashCollection = DB.GetCollection<MessageHash>();
            var hashRecord = hashCollection.Find(x => x.Hash == hash).FirstOrDefault();
            if (hashRecord != null)
            {
                InsultUserForCopyPasting(e, hashRecord);
                return true;
            }
            else
            {
                hashRecord = new MessageHash
                {
                    Hash = hash,
                    User = e.Message.From,
                    UtcWhen = DateTime.UtcNow,
                    MessageID = e.Message.MessageID,
                    ChatID = e.Message.Chat.ID
                };
                hashCollection.Insert(hashRecord);
                _log.Info($"{e.Message.From.ShortName()} just sent a long message with hash {hash}, which I promptly stored.");
                return false;
            }
        }

        private void InsultUserForCopyPasting(PublicMessageEventArgs e, MessageHash hashRecord)
        {
            string[] allReplies = System.IO.File.ReadAllLines("taunts.duplicate.txt").Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
            string replyFmt = allReplies[_rnd.Next(allReplies.Length)];
            string replyStr = String.Format(replyFmt,
                MessageUtils.HtmlEscape(hashRecord.User.ShortName()),
                TimeUtils.AsReadableTimespan(DateTime.UtcNow - hashRecord.UtcWhen),
                MessageUtils.HtmlEscape(e.Message.From.ShortName()), // {0}
                hashRecord.MessageID, // {1}
                hashRecord.ChatID // {2}
                );
            _log.Info($"{e.Message.From.ShortName()} just duplicated {hashRecord.User.ShortName()}'s message and got abused with reply {replyStr}");
            Client.SendMessageToChat(e.Message.Chat.ID, replyStr, "HTML", true, false, e.Message.MessageID);
        }
    }
}
