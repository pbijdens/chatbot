using Botje.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace chatbot.ChatStats.Model
{
    public class UserStatistics : IAtom
    {
        public UserStatistics()
        {
            Buckets = new List<UserStatisticsBucket>();
        }

        public Guid UniqueID { get; set; }

        public long UserId { get; set; }

        public string UserName { get; set; }

        public string UserNameLowerCase { get; set; }

        public long ChatID { get; set; }

        public List<UserStatisticsBucket> Buckets { get; set; }

        public UserStatisticsBucket GetOrCreateBucket(DateTime when)
        {
            int yearDay = (1000 * when.Year) + when.DayOfYear;
            UserStatisticsBucket result = Buckets.Where(x => x.YearDay == yearDay).FirstOrDefault();
            if (null == result)
            {
                result = new UserStatisticsBucket()
                {
                    YearDay = yearDay
                };
                Buckets.Insert(0, result);
            }
            return result;
        }
    }
}
