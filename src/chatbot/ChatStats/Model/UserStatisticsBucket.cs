using System;

namespace chatbot.ChatStats.Model
{
    public class UserStatisticsBucket
    {
        public UserStatisticsBucket()
        {
            MessagesByType = new int[32];
        }

        // 1000 * year + day
        public int YearDay { get; set; }

        public int TotalMessages { get; set; }

        public int Hashtags { get; set; }

        public int Mentions { get; set; }

        public int Mentioned { get; set; }

        public int Forwards { get; set; }

        public int Forwarded { get; set; }

        public int Replies { get; set; }

        public int Replied { get; set; }

        public int Joined { get; set; }

        public int Left { get; set; }

        public int Words { get; set; }

        public int Lines { get; set; }

        public int Characters { get; set; }

        public int Stickers { get; set; }

        public int[] MessagesByType { get; set; }

        public int URLs { get; set; }

        public void AddTo(UserStatisticsBucket total)
        {
            total.TotalMessages += TotalMessages;
            total.Hashtags += Hashtags;
            total.Mentions += Mentions;
            total.Mentioned += Mentioned;
            total.Forwards += Forwards;
            total.Forwarded += Forwarded;
            total.Replies += Replies;
            total.Replied += Replied;
            total.Joined += Joined;
            total.Left += Left;
            total.Words += Words;
            total.Lines += Lines;
            total.Characters += Characters;
            total.Stickers += Stickers;
            total.URLs += URLs;
            int numMBTs = Math.Min(MessagesByType.Length, total.MessagesByType.Length);
            for (int i = 0; i < numMBTs; i++)
            {
                total.MessagesByType[i] += MessagesByType[i];
            }
        }
    }
}
