using Botje.Core;
using Botje.Core.Utils;
using Botje.DB;
using Botje.Messaging;
using Botje.Messaging.Events;
using Ninject;
using System;
using System.Linq;

namespace pointlessbot.Pointless
{
    public class CounterCommandModule : IBotModule
    {
        internal const string CcmPlus = "ccm.plus";
        internal const string CcmMinus = "ccm.min";
        internal const string CcmRepost = "ccm.rep";

        private ILogger _log;

        [Inject]
        public IMessagingClient Client { get; set; }

        [Inject]
        public IDatabase DB { get; set; }

        [Inject]
        public ILoggerFactory LoggerFactory { set { _log = value.Create(GetType()); } }

        private DbSet<Model.PointlessCounter> GetCounterCollection() => DB.GetCollection<Model.PointlessCounter>();

        public void Shutdown()
        {
            _log.Trace($"Shut down {GetType().Name}");
            Client.OnPublicMessage -= Client_OnPublicMessage;
            Client.OnQueryCallback -= Client_OnQueryCallback;
        }

        public void Startup()
        {
            _log.Trace($"Started {GetType().Name}");
            Client.OnPublicMessage += Client_OnPublicMessage;
            Client.OnQueryCallback += Client_OnQueryCallback;
        }

        private void Client_OnQueryCallback(object sender, QueryCallbackEventArgs e)
        {
            if (null == e.CallbackQuery.Message && null == e.CallbackQuery.Message.Chat)
            {
            }
            var coll = GetCounterCollection();
            string p1 = $"{e.CallbackQuery.Data}".Split(":").FirstOrDefault() ?? String.Empty;
            string p2 = $"{e.CallbackQuery.Data}".Split(":").LastOrDefault() ?? String.Empty;
            var record = coll.Find(x => x.ID == p2).FirstOrDefault();
            Client.AnswerCallbackQuery(e.CallbackQuery.ID, $"Voelt goed he?");
            switch (p1)
            {
                case CcmMinus:
                    record.Value--;
                    coll.Update(record);
                    UpdateMessage(e.CallbackQuery.Message?.Chat?.ID, e.CallbackQuery.Message?.MessageID, e.CallbackQuery.InlineMessageId, record, e.CallbackQuery.Message?.Chat?.Type);
                    break;
                case CcmPlus:
                    record.Value++;
                    coll.Update(record);
                    UpdateMessage(e.CallbackQuery.Message?.Chat?.ID, e.CallbackQuery.Message?.MessageID, e.CallbackQuery.InlineMessageId, record, e.CallbackQuery.Message?.Chat?.Type);
                    break;
                case CcmRepost:
                    if (null != e.CallbackQuery.Message && null != e.CallbackQuery.Message.Chat)
                    {
                        Client.SendMessageToChat(e.CallbackQuery.Message.Chat.ID, ShortGuid.NewGuid().ToString() + "\r\n" + record.ToHtmlMessage(), "HTML", true, false, null, record.GetMarkup());
                    }
                    break;
            }
        }

        private void UpdateMessage(long? chatID, long? messageID, string inlineMessageId, Model.PointlessCounter record, string chatType)
        {
            chatType = "channel";
            if (!string.IsNullOrEmpty(inlineMessageId))
            {
                Client.EditMessageText(null, null, inlineMessageId, record.ToHtmlMessage(), "HTML", true, record.GetMarkup(), chatType);
            }
            else
            {
                Client.EditMessageText($"{chatID}", messageID, null, record.ToHtmlMessage(), "HTML", true, record.GetMarkup(), chatType);
            }
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
                    ProcessCommand(e, commandText, e.Message.Text.Substring(firstEntity.Length));
                }
            }
        }

        private void ProcessCommand(PublicMessageEventArgs e, string command, string argstr)
        {
            command = command?.ToLower() ?? String.Empty;
            string[] args = e.Message.Text.Split(' ').Where(x => !string.IsNullOrWhiteSpace(x)).Skip(1).ToArray();

            switch (command)
            {
                case "/pc":
                    CreateCounter(e, args, argstr);
                    break;
            }
        }

        private void CreateCounter(PublicMessageEventArgs e, string[] args, string rawArgumentString)
        {
            var counter = new Model.PointlessCounter();
            counter.ID = ShortGuid.NewGuid().ToString();
            counter.Value = 0;
            counter.Text = string.IsNullOrWhiteSpace(rawArgumentString) ? "Pointless counter" : rawArgumentString;
            GetCounterCollection().Insert(counter);

            Client.SendMessageToChat(e.Message.Chat.ID, counter.ToHtmlMessage(), "HTML", true, false, null, counter.GetMarkup());
        }
    }
}
