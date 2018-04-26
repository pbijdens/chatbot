using Botje.Core;
using Botje.Core.Commands;
using Botje.Core.Loggers;
using Botje.Core.Utils;
using Botje.DB;
using Botje.Messaging;
using Botje.Messaging.PrivateConversation;
using Botje.Messaging.Telegram;
using Ninject;
using System;
using System.Linq;
using System.Threading;

namespace chatbot
{
    class Program
    {
        static void Main(string[] args)
        {

            // These two settings files are excluded for the GIT solution
#if DEBUG
            var settings = JsonSettings.FromFile("settings.debug.json");
#else
            var settings = JsonSettings.FromFile("settings.release.json");
#endif
            TimeUtils.Initialize(settings.Timezones?.ToArray());

            var kernel = new StandardKernel();
            kernel.Bind<ILoggerFactory>().To<ConsoleLoggerFactory>();
            kernel.Bind<ISettingsService>().ToConstant(settings);

            // Core services
            var database = kernel.Get<Database>();
            database.Setup("Data");
            kernel.Bind<IDatabase>().ToConstant(database);
            kernel.Bind<IPrivateConversationManager>().To<PrivateConversationManager>().InSingletonScope();

            // Set up the messaging client
            CancellationTokenSource source = new CancellationTokenSource();
            TelegramClient client = kernel.Get<ThrottlingTelegramClient>();
            client.Setup(settings.BotKey, source.Token);
            kernel.Bind<IMessagingClient>().ToConstant(client);

            // Set up the console commands
            var helpCommand = new HelpCommand();
            kernel.Bind<IConsoleCommand>().ToConstant(new ExitCommand { TokenSource = source }).InSingletonScope();
            kernel.Bind<IConsoleCommand>().To<PingCommand>().InSingletonScope();
            kernel.Bind<IConsoleCommand>().To<HelpCommand>().InSingletonScope();
            kernel.Bind<IConsoleCommand>().To<LogLevelCommand>().InSingletonScope();
            kernel.Bind<IConsoleCommand>().To<TgCommands.MeCommand>().InSingletonScope();
            kernel.Bind<IConsoleCommand>().To<VerbodenWoord.VwCommand>().InSingletonScope();

            // Simple set-up
            kernel.Bind<IBotModule>().To<TgCommands.WhereAmI>().InSingletonScope();
            kernel.Bind<IBotModule>().To<VerbodenWoord.PrivateChatModule>().InSingletonScope();
            kernel.Bind<IBotModule>().To<VerbodenWoord.ProcessVerbodenWoord>().InSingletonScope();
            kernel.Bind<IBotModule>().To<VerbodenWoord.GeradenWoordCommands>().InSingletonScope();
            kernel.Bind<IBotModule>().To<VerbodenWoord.HuntCommand>().InSingletonScope();
            kernel.Bind<IBotModule>().To<ChatStats.GatherStatistics>().InSingletonScope();
            kernel.Bind<IBotModule>().To<ChatStats.ChatStatsCommands>().InSingletonScope();
            kernel.Bind<IBotModule>().To<ChatMgmt.Admin>().InSingletonScope();
            kernel.Bind<IBotModule>().To<ChatMgmt.Ban>().InSingletonScope();
            kernel.Bind<IBotModule>().To<ChatMgmt.WhoAmI>().InSingletonScope();
            kernel.Bind<IBotModule>().To<ChatMgmt.WhereAmI>().InSingletonScope();

            var modules = kernel.GetAll<IBotModule>().ToList();

            // Start the system
            modules.ForEach(m => m.Startup());
            client.Start();

            // Runt the console loop in the background
            var consoleLoop = kernel.Get<ConsoleLoop>();
            consoleLoop.Run(source.Token);

            // Shut down the modules
            modules.ForEach(m => m.Shutdown());

            Console.WriteLine("Program terminated. Have a nice day.");
        }
    }
}
