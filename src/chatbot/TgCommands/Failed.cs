using Botje.Core.Commands;
using Botje.DB;
using Botje.Messaging;
using Ninject;
using System;
using System.Linq;

namespace chatbot.TgCommands
{
    /// <summary>
    /// </summary>
    public class FailedCommand : ConsoleCommandBase
    {
        [Inject]
        public IMessagingClient Client { get; set; }

        [Inject]
        public IDatabase DB { get; set; }

        public override CommandInfo Info => new CommandInfo
        {
            Command = "failed",
            Aliases = new string[] { },
            QuickHelp = "Lists failed messages",
            DetailedHelp = "Usage: failed."
        };

        /// <summary>
        /// </summary>
        /// <param name="command"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public override bool OnInput(string command, string[] args)
        {
            var coll = DB.GetCollection<Botje.DB.ClientUpdateQueueEntry>().OrderBy(x => x.Update?.UpdateID).ToList();
            foreach (var entry in coll)
            {
                var f = entry.Failed ? 'X' : ' ';
                Console.WriteLine($"{f} id: {entry.Update?.UpdateID} when: {entry.UpdateDateTime} errors: {entry.Errors?.Count} type: {entry.Update?.GetUpdateType()} from: {entry.Update?.Message?.From} text: {entry.Update?.Message?.Text}");
                if ((entry.Errors?.Count ?? 0) > 0)
                {
                    foreach (var error in entry.Errors)
                    {
                        Console.WriteLine($"  - ERROR: {error}");
                    }
                }
            }
            return true;
        }
    }
}
