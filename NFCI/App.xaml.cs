using System.IO;
using System.Windows;
using Serilog;
using Serilog.Events;
using Serilog.Configuration;
using Serilog.Sinks.File;
namespace NFCI
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            Directory.CreateDirectory("logs");

            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.File(
                    "logs/nfc-.log",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    outputTemplate: "{Timestamp:O} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Log.CloseAndFlush();
            base.OnExit(e);
        }
    }
}
