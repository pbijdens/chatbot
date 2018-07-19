using Botje.Core.Commands;
using Botje.DB;
using Botje.Messaging;
using Ninject;
using System.Linq;

namespace chatbot.TgCommands
{
    /// <summary>
    /// </summary>
    public class RetryCommand : ConsoleCommandBase
    {
        [Inject]
        public IMessagingClient Client { get; set; }

        [Inject]
        public IDatabase DB { get; set; }

        public override CommandInfo Info => new CommandInfo
        {
            Command = "retry",
            Aliases = new string[] { },
            QuickHelp = "Retry message with id",
            DetailedHelp = "Usage: retry <id>."
        };

        /// <summary>
        /// </summary>
        /// <param name="command"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public override bool OnInput(string command, string[] args)
        {
            var coll = DB.GetCollection<Botje.DB.ClientUpdateQueueEntry>();
            foreach (var entry in coll.Find(x => $"{x.Update?.UpdateID}" == args[0]).ToList())
            {
                entry.Failed = false;
                coll.Update(entry);
                System.Console.WriteLine($"{entry.Update.UpdateID} - try again.");
            }
            return true;
        }
    }
}
