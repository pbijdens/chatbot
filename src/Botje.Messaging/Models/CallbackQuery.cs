﻿using RestSharp.Deserializers;

namespace Botje.Messaging.Models
{
    public class CallbackQuery
    {
        [DeserializeAs(Name = "id")]
        public string ID { get; set; }

        [DeserializeAs(Name = "from")]
        public User From { get; set; }

        // Optional. Message with the callback button that originated the query. Note that message content and message date will not be available if the message is too old
        [DeserializeAs(Name = "message")]
        public Message Message { get; set; }

        // Optional. Identifier of the message sent via the bot in inline mode, that originated the query.
        [DeserializeAs(Name = "inline_message_id")]
        public string InlineMessageId { get; set; }

        // Global identifier, uniquely corresponding to the chat to which the message with the callback button was sent. Useful for high scores in games.
        [DeserializeAs(Name = "chat_instance")]
        public string ChatInstance { get; set; }

        [DeserializeAs(Name = "data")]
        public string Data { get; set; }

        // game_short_name	String
    }
}