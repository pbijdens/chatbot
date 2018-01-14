using Botje.Core.Utils;
using Botje.DB;
using Botje.Messaging.Models;
using System;
using System.Collections.Generic;

namespace pointlessbot.Pointless.Model
{
    public class PointlessCounter : IAtom
    {
        public Guid UniqueID { get; set; }

        public string ID { get; set; }

        public long Value { get; set; }

        public string Text { get; set; }

        internal string ToHtmlMessage()
        {
            if (string.IsNullOrWhiteSpace(Text))
            {
                return $"<b>Pointless counter: </b>{Value}";
            }
            else
            {
                return $"<b>{MessageUtils.HtmlEscape(Text)}</b>: {Value}";
            }
        }

        internal InlineKeyboardMarkup GetMarkup()
        {
            InlineKeyboardMarkup result = new InlineKeyboardMarkup();
            result.inline_keyboard = new List<List<InlineKeyboardButton>>();
            var row = new List<InlineKeyboardButton>();
            row.Add(new InlineKeyboardButton { text = $"-1", callback_data = $"{CounterCommandModule.CcmMinus}:{ID}" });
            // row.Add(new InlineKeyboardButton { text = $"📣", callback_data = $"{CounterCommandModule.CcmRepost}:{ID}" });
            row.Add(new InlineKeyboardButton { text = $"+1", callback_data = $"{CounterCommandModule.CcmPlus}:{ID}" });
            result.inline_keyboard.Add(row);
            return result;
        }
    }
}
