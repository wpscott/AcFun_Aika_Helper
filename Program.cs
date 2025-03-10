using System;
using Avalonia;
using Avalonia.ReactiveUI;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace AikaHelper;

internal class Program
{
    public static LoggingLevelSwitch LoggingLevel { get; } = new();

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
#if DEBUG
        LoggingLevel.MinimumLevel = LogEventLevel.Debug;
#endif
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(LoggingLevel)
            .Enrich.WithEnvironmentName()
            .Enrich.WithEnvironmentUserName()
            .Enrich.FromLogContext()
            .WriteTo.Console(LogEventLevel.Debug)
            .WriteTo.Debug(LogEventLevel.Debug)
            .WriteTo.File("output-.log", rollingInterval: RollingInterval.Day, shared: true, retainedFileCountLimit: 10)
            .CreateLogger();

        try
        {
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception e)
        {
            Log.Fatal(e, "Something went wrong");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
    }
}