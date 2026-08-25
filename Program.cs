using NLog;
using NLog.Layouts;

namespace ZamboniDedicated;

internal static class Program
{
    private static LogLevel _loglevel = LogLevel.Info;
    private const int DefaultTicksPerSecond = 30;
    private const int DefaultMaxClients = 12;
    private const int DefaultPort = 6767;

    static void Main(string[] args)
    {
        var port = DefaultPort;
        var ticksPerSecond = DefaultTicksPerSecond;
        var maxClients = DefaultMaxClients;

        if (args.Length > 0)
        {
            if (!int.TryParse(args[0], out ticksPerSecond) || ticksPerSecond <= 0)
            {
                Console.WriteLine($"ERROR: tick rate must be a positive number, got '{args[0]}'");
                return;
            }
        }

        if (args.Length > 1)
        {
            if (!int.TryParse(args[1], out maxClients) || maxClients <= 0)
            {
                Console.WriteLine($"ERROR: max players must be a positive number, got '{args[1]}'");
                return;
            }
        }

        if (args.Length > 2)
        {
            if (!int.TryParse(args[2], out port) || port is < 1 or > 65535)
            {
                Console.WriteLine($"ERROR: port is not valid, got '{args[2]}'");
                return;
            }
        }

        if (args.Length > 3)
        {
            if (!int.TryParse(args[3], out var intLoglevel) || intLoglevel is < 0 or > 6)
            {
                Console.WriteLine($"ERROR: LogLevel must be 0-6, got '{args[3]}'");
                return;
            }

            _loglevel = LogLevel.FromOrdinal(intLoglevel);
        }

        StartLogger();

        var server = new Dedicated(ticksPerSecond, maxClients, port);

        var shutdownComplete = new ManualResetEventSlim(false);
        server.Stopped += _ => shutdownComplete.Set();
        server.Start();

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            server.Stop();
        };

        shutdownComplete.Wait();
        Console.WriteLine("Shutting down...");
    }

    public static void StartLogger()
    {
        var logLevel = _loglevel;
        var layout = new SimpleLayout("[${longdate}][${callsite-filename:includeSourcePath=false}(${callsite-linenumber})][${level:uppercase=true}]: ${message:withexception=true}");
        LogManager.Setup().LoadConfiguration(builder =>
        {
            builder.ForLogger().FilterMinLevel(logLevel)
                .WriteToConsole(layout)
                .WriteToFile("logs/server-${shortdate}.log", layout);
        });
    }
}