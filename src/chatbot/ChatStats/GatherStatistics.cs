using Botje.Core;
using Botje.DB;
using Botje.Messaging;
using Botje.Messaging.Events;
using Botje.Messaging.Models;
using chatbot.ChatStats.Model;
using Ninject;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace chatbot.ChatStats
{
    public class GatherStatistics : IBotModule
    {
        private ILogger _log;

        [Inject]
        public IMessagingClient Client { get; set; }

        [Inject]
        public IDatabase DB { get; set; }

        [Inject]
        public ILoggerFactory LoggerFactory { set { _log = value.Create(GetType()); } }

        [Inject]
        public IBotModule[] Modules { set { } }

        private DbSet<Model.UserStatistics> GetStatisticsCollection() => DB.GetCollection<Model.UserStatistics>("userstats");

        private object _statsLock = new object();

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
            if (null == firstEntity || firstEntity.Offset != 0 || firstEntity.Type != "bot_command")
            {
                UpdateStats(e);
            }
        }

        private void UpdateStats(PublicMessageEventArgs e)
        {
            if (e.Message.Chat == null) return;

            lock (_statsLock)
            {
                var collection = GetStatisticsCollection();

                var message = e.Message;

                if (message.From != null)
                {
                    GetAndUpdate(collection, message.Chat.ID, message.From, (bucket) =>
                    {
                        bucket.TotalMessages++;
                        bucket.MessagesByType[(int)message.Type]++;

                        if (message.ForwardFrom != null) bucket.Forwards++;
                        if (message.Sticker != null) bucket.Stickers++;
                        if (message.ReplyToMessage != null) bucket.Replies++;

                        message.Entities.ForEach((x) =>
                        {
                            // 	Type of the entity. Can be mention (@username), hashtag, bot_command, url, email, bold (bold text), italic (italic text), code (monowidth string), pre (monowidth block), text_link (for clickable text URLs), text_mention (for users without usernames)
                            switch (x.Type)
                            {
                                case "hashtag":
                                    bucket.Hashtags++;
                                    break;
                                case "mention":
                                    bucket.Mentions++;
                                    break;
                                case "text_mention":
                                    bucket.Mentions++;
                                    break;
                                case "text_link":
                                case "url":
                                    bucket.URLs++;
                                    break;
                            }
                        });

                        string messageText = $"{message.Text}{message.Caption}";
                        messageText = Regex.Replace(messageText, @"\s+", " ").Trim();

                        if (message.Type == MessageType.TextMessage && !string.IsNullOrWhiteSpace(messageText) && null == message.ForwardFrom)
                        {
                            bucket.Characters += messageText.Length;
                            bucket.Words += Regex.Split(message.Text, @"\s").Where(x => !string.IsNullOrWhiteSpace(x)).Count(); // use original
                            bucket.Lines += message.Text.Split("\n").Where(x => !string.IsNullOrWhiteSpace(x)).Count(); // use original
                        }
                    });
                }

                if (message.ForwardFrom != null)
                {
                    GetAndUpdate(collection, message.Chat.ID, message.ForwardFrom, (bucket) => { bucket.Forwarded++; });
                }

                if (message.ReplyToMessage != null && message.ReplyToMessage.From != null)
                {
                    GetAndUpdate(collection, message.Chat.ID, message.ReplyToMessage.From, (bucket) => { bucket.Replied++; });
                }

                if (message.LeftChatMember != null)
                {
                    GetAndUpdate(collection, message.Chat.ID, message.LeftChatMember, (bucket) => { bucket.Left++; });
                }

                if (message.NewChatMembers != null)
                {
                    message.NewChatMembers.ForEach(x => GetAndUpdate(collection, message.Chat.ID, x, (bucket) => { bucket.Joined++; }));
                }

                if (message.Entities.Count > 0)
                {
                    int index = 0;
                    message.Entities.ForEach(entity =>
                    {
                        if (null != entity.User)
                        {
                            GetAndUpdate(collection, message.Chat.ID, entity.User, (bucket) => { bucket.Mentioned++; });
                        }
                        else if (entity.Type == "text_mention" || entity.Type == "mention")
                        {
                            string userName = message.Text.Substring(entity.Offset, entity.Length).TrimStart('@')?.ToLower();
                            var user = collection.Find((x) => x.UserNameLowerCase == userName && x.ChatID == e.Message.Chat?.ID).FirstOrDefault();
                            if (null != user)
                            {
                                var bucket = user.GetOrCreateBucket(DateTime.UtcNow);
                                bucket.Mentioned++;
                                collection.Update(user);
                            }
                        }
                        index++;
                    });
                }
            }
        }

        private void GetAndUpdate(DbSet<UserStatistics> collection, long chatID, User user, Action<UserStatisticsBucket> action)
        {
            var userRecord = collection.Find((x) => x.UserId == user.ID && x.ChatID == chatID).FirstOrDefault();
            if (null == userRecord)
            {
                userRecord = new UserStatistics()
                {
                    UserId = user.ID,
                    UserName = user.UsernameOrName(),
                    UserNameLowerCase = user.UsernameOrName().ToLowerInvariant(),
                    ChatID = chatID,
                };
                collection.Insert(userRecord);
            }
            else
            {
                userRecord.UserName = user.UsernameOrName();
                userRecord.UserNameLowerCase = user.UsernameOrName().ToLowerInvariant();
            }
            var userRecordBucket = userRecord.GetOrCreateBucket(DateTime.UtcNow);

            action(userRecordBucket);

            collection.Update(userRecord);
        }
    }
}
