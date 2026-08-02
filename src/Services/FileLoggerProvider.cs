using Microsoft.Extensions.Logging;
using Velopack.Logging;

namespace WikeloContractor.Services;

/// <summary>
/// Routes <see cref="Microsoft.Extensions.Logging"/> output into <see cref="AppLog"/>, so everything
/// the host and the services log reaches the same file the user can find.
/// </summary>
internal sealed class FileLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName);

    public void Dispose()
    {
    }

    private sealed class FileLogger(string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        // Information and up: Debug/Trace from the host and HttpClient would bury the app's own
        // lines in noise, and this file exists to be read by a person, not grepped by a machine.
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                AppLog.Write(logLevel.ToString(), $"{category}: {formatter(state, exception)}", exception);
            }
        }
    }
}

/// <summary>
/// Bridges Velopack's managed-side logging into <see cref="AppLog"/>. Covers the <c>lib-csharp</c>
/// half of an update — locator decisions, "app is out-dated", download progress. The <c>update</c>
/// half comes from the separate <c>Update.exe</c> binary and is picked up by
/// <see cref="AppLog.MirrorUpdaterLog"/> instead.
/// </summary>
internal sealed class VelopackFileLogger : IVelopackLogger
{
    public void Log(VelopackLogLevel logLevel, string? message, Exception? exception) =>
        AppLog.Write($"Velopack/{logLevel}", message ?? string.Empty, exception);
}
