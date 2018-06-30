using Botje.Core.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace chatbot.Services
{
    public class TimeService : ITimeService
    {
        /// <summary>
        /// Convert a datetime to justa short time.
        /// </summary>
        /// <param name="utc"></param>
        /// <returns></returns>
        public string AsShortTime(DateTime utc)
        {
            DateTime converted = TimeZoneInfo.ConvertTimeFromUtc(utc, TimeUtils.TzInfo);
            return converted.ToString("HH:mm");
        }

        public string AsFullTime(DateTime utc)
        {
            DateTime converted = TimeZoneInfo.ConvertTimeFromUtc(utc, TimeUtils.TzInfo);
            return converted.ToString("HH:mm:ss.fff");
        }

        /// <summary>
        /// Human readable timespan.
        /// </summary>
        /// <param name="ts"></param>
        /// <returns></returns>
        public string AsReadableTimespan(TimeSpan ts)
        {
            // formats and its cutoffs based on totalseconds
            var cutoff = new SortedList<long, string> {
               {60, "{3:S}" },
               {60*60-1, "{2:M}, {3:S}"},
               {60*60, "{1:H}"},
               {24*60*60-1, "{1:H}, {2:M}"},
               {24*60*60, "{0:D}"},
               {Int64.MaxValue , "{0:D}, {1:H}"}
             };

            // find nearest best match
            var find = cutoff.Keys.ToList().BinarySearch((long)ts.TotalSeconds);

            // negative values indicate a nearest match
            var near = find < 0 ? Math.Abs(find) - 1 : find;

            // use custom formatter to get the string
            return String.Format(new LocalizedHMSFormatter(), cutoff[cutoff.Keys[near]], ts.Days, ts.Hours, ts.Minutes, ts.Seconds);
        }


        public string AsDutchString(DateTime dt)
        {
            string[] months = new string[] {
                ("January"),
                ("February"),
                ("March"),
                ("April"),
                ("May"),
                ("June"),
                ("July"),
                ("August"),
                ("September"),
                ("October"),
                ("November"),
                ("December")
            };

            return $"{dt.Day} {months[dt.Month - 1]} {dt.ToString(("HH:mm"))}";
        }

        public string AsLocalShortTime(DateTime dt)
        {
            DateTime converted = TimeZoneInfo.ConvertTimeFromUtc(dt, TimeUtils.TzInfo);
            return dt.ToString(("MM-dd-yy HH:mm"));
        }

        public class LocalizedHMSFormatter : ICustomFormatter, IFormatProvider
        {
            public LocalizedHMSFormatter()
            {
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="format"></param>
            /// <param name="arg"></param>
            /// <param name="formatProvider"></param>
            /// <returns></returns>
            public string Format(string format, object arg, IFormatProvider formatProvider)
            {
                var TimeFormats = new Dictionary<string, string> {
                    {"S", "{0:P:"+("seconds")+":"+("second")+"}"},
                    {"M", "{0:P:"+("minutes")+":"+("minute")+"}"},
                    {"H","{0:P:"+("hours")+":"+("hour")+"}"},
                    {"D", "{0:P:"+("days")+":"+("day")+"}"}
                };

                return String.Format(new PluralFormatter(), TimeFormats[format], arg);
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="formatType"></param>
            /// <returns></returns>
            public object GetFormat(Type formatType)
            {
                return formatType == typeof(ICustomFormatter) ? this : null;
            }
        }
    }
}
