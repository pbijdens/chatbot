using Botje.Core;
using Botje.Core.Commands;
using Botje.Core.Loggers;
using Botje.Core.Utils;
using Botje.DB;
using Botje.Messaging;
using Botje.Messaging.PrivateConversation;
using Botje.Messaging.Telegram;
using Ninject;
using resbot.Modules;
using resbot.Utils;
using System;
using System.Linq;
using System.Threading;

namespace resbot
{
    class Program
    {
        /// <summary>
        /// Arrgs[0] should be the API key, args[1] should be the path to the settings.
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine($"Usage: command \"<api key>\" \"<path to settings file>\"");
                return;
            }

            // Initialize globals
            string apiKey = args[0];
            string settingFilePath = args[1];

            // Create the settings service
            if (!System.IO.File.Exists(settingFilePath))
            {
                Console.WriteLine($"Can't find settings file at {settingFilePath}");
                return;
            }

            var settings = JsonSettings.FromFile(settingFilePath);
            TimeUtils.Initialize(settings.Timezones.ToArray());

            // initialize the kernel
            var kernel = new StandardKernel();
            kernel.Bind<ISettingsService>().ToConstant(settings);
            kernel.Bind<ILoggerFactory>().To<ConsoleLoggerFactory>(); // provided by Botje.Core, replace with your favorite logging framework

            // Set up the database
            var database = kernel.Get<Database>();
            database.Setup(settings.DataDirectory);
            kernel.Bind<IDatabase>().ToConstant(database);
            kernel.Bind<IPrivateConversationManager>().To<PrivateConversationManager>().InSingletonScope(); // helper for storing per-user conversation state in the database

            // Set up the messaging client
            CancellationTokenSource source = new CancellationTokenSource();
            TelegramClient client = kernel.Get<ThrottlingTelegramClient>();
            client.Setup(apiKey, source.Token);
            kernel.Bind<IMessagingClient>().ToConstant(client);

            // Register the console commands
            kernel.Bind<IConsoleCommand>().To<PingCommand>().InSingletonScope(); // provided by the core framework
            kernel.Bind<IConsoleCommand>().To<HelpCommand>().InSingletonScope(); // provided by the core framework
            kernel.Bind<IConsoleCommand>().To<LogLevelCommand>().InSingletonScope(); // provided by the core framework
            kernel.Bind<IConsoleCommand>().ToConstant(new ConsoleCommands.ExitCommand { TokenSource = source }).InSingletonScope();
            kernel.Bind<IConsoleCommand>().To<ConsoleCommands.MeCommand>().InSingletonScope();

            // Register the bot modules
            kernel.Bind<IBotModule>().To<WhereAmI>().InSingletonScope();
            kernel.Bind<IBotModule>().To<WhoAmI>().InSingletonScope();
            kernel.Bind<IBotModule>().To<Claim>().InSingletonScope();
            kernel.Bind<IBotModule>().To<FixedReplies>().InSingletonScope();
            kernel.Bind<IBotModule>().To<SendMessageOnJoin>().InSingletonScope();
            kernel.Bind<IBotModule>().To<Admin>().InSingletonScope();
            kernel.Bind<IBotModule>().To<Help>().InSingletonScope();

            // Register API handlers
            var modules = kernel.GetAll<IBotModule>().ToList();

            // Boot
            modules.ForEach(m => m.Startup());
            client.Start();

            // Run the console loop in the background
            var consoleLoop = kernel.Get<ConsoleLoop>();
            consoleLoop.Run(source.Token);

            // Shut down the modules
            modules.ForEach(m => m.Shutdown());

            // Say goodbye. It's the decent thing to do.
            Console.WriteLine("Bot was terminated. Have a nice day.");
        }
    }
}
