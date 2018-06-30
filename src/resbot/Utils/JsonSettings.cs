using System.Collections.Generic;
using System.IO;

namespace resbot.Utils
{
    /// <summary>
    /// Reads the settings for the bot from a JSON file.
    /// </summary>
    public class JsonSettings : ISettingsService
    {
        /// <summary>
        /// Array of timezones.
        /// </summary>
        public List<string> Timezones { get; set; }

        /// <summary>
        /// Data directory.
        /// </summary>
        public string DataDirectory { get; set; }

        /// <summary>
        /// Reads the settings from a file.
        /// </summary>
        /// <param name="filename">Filename</param>
        public static ISettingsService FromFile(string filename)
        {
            string json = File.ReadAllText(filename);
            return Newtonsoft.Json.JsonConvert.DeserializeObject<JsonSettings>(json);
        }
    }
}
