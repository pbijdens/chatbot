using System.Collections.Generic;
using System.IO;

namespace chatbot
{
    /// <summary>
    /// Technically we would not need an inner-class to implement this interface, because just implementing the interface with getters and setters ad having a static method to deserialize to this type would do.
    /// </summary>
    public class JsonSettings : ISettingsService
    {
        public string BotKey { get; set; }

        /// <summary>
        /// Array of timezones.
        /// </summary>
        public List<string> Timezones { get; set; }

        /// <summary>
        /// Administrators
        /// </summary>
        public List<string> AdministratorUserIDs { get; set; }

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
