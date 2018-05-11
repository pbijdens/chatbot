using Botje.Core;
using Botje.Core.Utils;
using Botje.DB;
using Botje.Messaging;
using Botje.Messaging.Events;
using Botje.Messaging.Models;
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
    public class PrivateChatModule : IBotModule
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

        private List<ProcessVerbodenWoord> _publicChatModules = new List<ProcessVerbodenWoord>();

        [Inject]
        public IBotModule[] Modules { set { _publicChatModules = value.OfType<ProcessVerbodenWoord>().ToList(); } }

        const string RegexInterpunction = @"\!\.\,\;\:\?\""\(\)\[\]";

        private const string CbqDeleteWoord = "vw.delw";
        private const string CbqStatus = "vw.stat";
        private const string CbqBack = "vw.back";
        private const string CbqAddWord = "vw.addw";

        private const string StateReadWord = "state.read.word";
        private const string StateReadReply = "state.read.reply";

        private int MaxWordsForUser(long userId) => 32;

        public void Shutdown()
        {
            Client.OnPrivateMessage -= Client_OnPrivateMessage;
            Client.OnQueryCallback -= Client_OnQueryCallback;
            _log.Trace($"Shut down {GetType().Name}");
        }

        public void Startup()
        {
            _log.Trace($"Started {GetType().Name}");
            Client.OnPrivateMessage += Client_OnPrivateMessage;
            Client.OnQueryCallback += Client_OnQueryCallback;
        }

        private void Client_OnQueryCallback(object sender, QueryCallbackEventArgs e)
        {
            string query = e.CallbackQuery.Data.Split(':').FirstOrDefault();
            switch (query)
            {
                case CbqBack:
                    Client.AnswerCallbackQuery(e.CallbackQuery.ID);
                    ResetConversation(e.CallbackQuery.From);
                    ShowMainMenu(e.CallbackQuery.From);
                    break;
                case CbqDeleteWoord:
                    Client.AnswerCallbackQuery(e.CallbackQuery.ID);
                    HandleCbqDeleteWoord(e);
                    ResetConversation(e.CallbackQuery.From);
                    ShowMainMenu(e.CallbackQuery.From);
                    break;
                case CbqStatus:
                    Client.AnswerCallbackQuery(e.CallbackQuery.ID);
                    HandleCbqStatus(e);
                    ResetConversation(e.CallbackQuery.From);
                    break;
                case CbqAddWord:
                    Client.AnswerCallbackQuery(e.CallbackQuery.ID);
                    HandleCbqAddWord(e);
                    break;
            }
        }

        private void HandleCbqAddWord(QueryCallbackEventArgs e)
        {
            var user = e.CallbackQuery.From;
            RequestWordFromUser(user);
        }

        private void RequestWordFromUser(User user, string error = "")
        {
            Client.SendMessageToChat(user.ID, $"{error}Stuur me het woord of de woorden die je wil toevoegen.\r\n<i>Meerdere woorden kan je scheiden met de gebruikelijke leestekens (bijvoorbeeld komma's); verder mogen woorden alle geldige unicode karakters bevatten, inclusief emoticons.</i>", "HTML");
            ConversationManager.SetState(user, StateReadWord, null);
        }

        private void HandleCbqDeleteWoord(QueryCallbackEventArgs e)
        {
            var collection = GetVerbodenWoordCollection();
            var args = e.CallbackQuery.Data.Split(':').Skip(1).ToList();
            string id = args[0];
            int deleted = collection.Delete(x => x.OwnerUserId == e.CallbackQuery.From.ID && x.ID == id);

            if (deleted > 0)
            {
                _publicChatModules.ForEach(x => x.OnWordsChanged());
            }

            _log.Info($"Deleted {deleted} words with id {id} for user {e.CallbackQuery.From.DisplayName()}");
        }

        private void HandleCbqStatus(QueryCallbackEventArgs e)
        {

            var woorden = GetVerbodenWoordCollection().Find(x => x.OwnerUserId == e.CallbackQuery.From.ID).OrderBy(x => x.CreationDate).ToList();
            _log.Info($"Returning status for {woorden.Count} words for for user {e.CallbackQuery.From.DisplayName()}");
            if (woorden.Count > 0)
            {
                StringBuilder status = new StringBuilder();
                status.AppendLine($"<b>Status van jouw woorden (oudste eerst):</b>");
                foreach (var woord in woorden.OrderBy(w => w.CreationDate))
                {
                    status.AppendLine($"");
                    status.AppendLine($"<b>Sinds:</b> {TimeUtils.AsReadableTimespan(DateTime.UtcNow - woord.CreationDate)}");
                    status.AppendLine($"<b>Woorden ({woord.Woorden.Count}):</b>");
                    foreach (var w in woord.Woorden)
                    {
                        status.AppendLine($" - {MessageUtils.HtmlEscape(w)}");
                    }
                }
                Client.SendMessageToChat(e.CallbackQuery.From.ID, status.ToString(), "HTML");
                Client.SendMessageToChat(e.CallbackQuery.From.ID, $"Je hebt in totaal {woorden.Count} woord(en) ingesteld, de oudste staat al {TimeUtils.AsReadableTimespan(DateTime.UtcNow - woorden.First().CreationDate)}", "HTML", null, null, null, CreateBackKeyboardMarkup(e.CallbackQuery.From));
            }
            else
            {
                Client.SendMessageToChat(e.CallbackQuery.From.ID, $"🎶 <i>Er zijn geen woorden meer, geen woorden meer, voor jou.</i>", "HTML", null, null, null, CreateBackKeyboardMarkup(e.CallbackQuery.From));
            }
        }

        private void Client_OnPrivateMessage(object sender, PrivateMessageEventArgs e)
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

                    if (commandText == "/start" || commandText == "/vw")
                    {
                        ResetConversation(e.Message.From);
                        ShowMainMenu(e.Message.From);
                    }
                    else if (commandText == "/help")
                    {
                        Client.SendMessageToChat(e.Message.From.ID, System.IO.File.ReadAllText("helpfile.txt"), "HTML");
                    }
                }
            }
            else
            {
                string[] data;
                string state = ConversationManager.GetState(e.Message.From, out data);
                switch (state)
                {
                    case StateReadWord:
                        List<string> woorden = ParseWoordenInputFromMessage(e.Message.Text);

                        if (woorden.Count == 0)
                        {
                            RequestWordFromUser(e.Message.From, "<b>Hier kan ik niks mee. Geef minimaal één woord op.</b>\r\n");
                        }
                        else
                        {
                            string woordenStr = string.Join("", woorden.Select(x => $" - {MessageUtils.HtmlEscape(x)}\r\n"));
                            Client.SendMessageToChat(e.Message.From.ID, $"De {woorden.Count} woord(en):\r\n{woordenStr}\r\nStuur me een bericht om door te sturen naar de chat wanneer een van deze verboden woorden geraden wordt.", "HTML");
                            ConversationManager.SetState(e.Message.From, StateReadReply, new string[] { e.Message.Text });
                        }
                        break;
                    case StateReadReply:
                        ResetConversation(e.Message.From);

                        var record = new Model.VerbodenWoordData();
                        record.ReplyChatID = e.Message.Chat.ID;
                        record.ReplyMessageID = e.Message.MessageID;
                        record.Woorden = ParseWoordenInputFromMessage(data[0]);
                        record.ID = ShortGuid.NewGuid().ToString();
                        record.CreationDate = DateTime.UtcNow;
                        record.OwnerName = e.Message.From.UsernameOrName();
                        record.OwnerUserId = e.Message.From.ID;
                        GetVerbodenWoordCollection().Insert(record);
                        _publicChatModules.ForEach(x => x.OnWordsChanged());

                        Client.SendMessageToChat(e.Message.From.ID, $"Dank je wel. De woorden zijn toegevoegd aan je lijst.", "HTML", null, null, null, CreatePostAddKeyboardMarkup(e.Message.From));

                        string wordStr = string.Join(", ", record.Woorden);

                        _log.Info($"Added {record.Woorden.Count} words with id {record.ID} for user {e.Message.From.DisplayName()}");

                        break;
                }
            }
        }

        private static List<string> ParseWoordenInputFromMessage(string messagae)
        {
            return Regex.Replace(messagae ?? "", $@"[{RegexInterpunction}]", ",", RegexOptions.IgnoreCase).Split(',').Select(x => x.Trim().ToLowerInvariant()).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        }

        private void ShowMainMenu(User user)
        {
            StringBuilder sb = new StringBuilder();
            var verbodenWoordCollection = GetVerbodenWoordCollection();
            var woorden = verbodenWoordCollection.Find(x => x.OwnerUserId == user.ID).OrderBy(x => x.CreationDate).ToList();

            sb.Append($"Je hebt {woorden.Count} woorden(en) ingesteld. ");
            if (woorden.Any())
            {
                var age = DateTime.UtcNow - woorden.First().CreationDate;
                sb.Append($"Je oudste woord is {TimeUtils.AsReadableTimespan(age)} oud. ");
            }

            InlineKeyboardMarkup inlineKeyboard = CreateInlineKeyboardMarkup(user, woorden);

            Client.SendMessageToChat(user.ID, sb.ToString(), "HTML", true, true, null, inlineKeyboard);
        }

        private InlineKeyboardMarkup CreateInlineKeyboardMarkup(User user, List<VerbodenWoordData> woorden)
        {
            InlineKeyboardMarkup result = new InlineKeyboardMarkup();
            result.inline_keyboard = new List<List<InlineKeyboardButton>>();

            List<InlineKeyboardButton> buttons = new List<InlineKeyboardButton>();
            woorden.ForEach(w =>
            {
                buttons.Add(new InlineKeyboardButton
                {
                    text = TrimLength($"🗑 {string.Join(", ", w.Woorden)}"),
                    callback_data = $"{CbqDeleteWoord}:{w.ID}"
                });
            });
            buttons.Add(new InlineKeyboardButton
            {
                text = $"Status",
                callback_data = $"{CbqStatus}"
            });
            if (woorden.Count() < MaxWordsForUser(user.ID))
            {
                buttons.Add(new InlineKeyboardButton
                {
                    text = $"➕ Voeg een woord toe",
                    callback_data = $"{CbqAddWord}"
                });
            }

            var addnRows = SplitButtonsIntoLines(buttons, maxElementsPerLine: 1, maxCharactersPerLine: 30);
            result.inline_keyboard.AddRange(addnRows);

            return result;
        }

        private InlineKeyboardMarkup CreateBackKeyboardMarkup(User user)
        {
            InlineKeyboardMarkup result = new InlineKeyboardMarkup();
            result.inline_keyboard = new List<List<InlineKeyboardButton>>();

            List<InlineKeyboardButton> buttons = new List<InlineKeyboardButton>();
            buttons.Add(new InlineKeyboardButton
            {
                text = $"Terug",
                callback_data = $"{CbqBack}"
            });

            var addnRows = SplitButtonsIntoLines(buttons, maxElementsPerLine: 5, maxCharactersPerLine: 30);
            result.inline_keyboard.AddRange(addnRows);

            return result;
        }

        private InlineKeyboardMarkup CreatePostAddKeyboardMarkup(User user)
        {
            InlineKeyboardMarkup result = new InlineKeyboardMarkup();
            result.inline_keyboard = new List<List<InlineKeyboardButton>>();
            var woorden = GetVerbodenWoordCollection().Find(x => x.OwnerUserId == user.ID).OrderBy(x => x.CreationDate).ToList();

            List<InlineKeyboardButton> buttons = new List<InlineKeyboardButton>();
            if (woorden.Count() < MaxWordsForUser(user.ID))
            {
                buttons.Add(new InlineKeyboardButton
                {
                    text = $"➕ Voeg nog een woord toe",
                    callback_data = $"{CbqAddWord}"
                });
            }
            buttons.Add(new InlineKeyboardButton
            {
                text = $"Naar overzicht",
                callback_data = $"{CbqBack}"
            });

            var addnRows = SplitButtonsIntoLines(buttons, maxElementsPerLine: 1, maxCharactersPerLine: 30);
            result.inline_keyboard.AddRange(addnRows);

            return result;
        }



        private List<List<InlineKeyboardButton>> SplitButtonsIntoLines(List<InlineKeyboardButton> buttons, int maxElementsPerLine, int maxCharactersPerLine)
        {
            var result = new List<List<InlineKeyboardButton>>();
            List<InlineKeyboardButton> currentLine = new List<InlineKeyboardButton>();
            int lineLength = 0;
            foreach (var button in buttons)
            {
                if (currentLine.Count >= 0 && (lineLength + button.text.Length >= maxCharactersPerLine || currentLine.Count + 1 > maxElementsPerLine || button.text.Length > 10))
                {
                    result.Add(currentLine);
                    currentLine = new List<InlineKeyboardButton>();
                    lineLength = 0;
                }

                currentLine.Add(button);
                lineLength += button.text.Length;
            }
            if (currentLine.Count > 0)
            {
                result.Add(currentLine);
            }
            return result;
        }

        private DbSet<Model.VerbodenWoordData> GetVerbodenWoordCollection()
        {
            return DB.GetCollection<Model.VerbodenWoordData>("verbodenwoord");
        }

        private void ResetConversation(User from)
        {
            ConversationManager.SetState(from, null);
        }

        private string TrimLength(string s)
        {
            if (s.Length > 25)
            {
                return s.Substring(0, 22) + "...";
            }
            return s;
        }
    }
}
