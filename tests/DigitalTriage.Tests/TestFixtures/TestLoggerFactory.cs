using Microsoft.Extensions.Logging;

namespace DigitalTriage.Tests.TestFixtures;

/// <summary>
/// Factory for creating test loggers.
/// </summary>
public static class TestLoggerFactory
{
    /// <summary>
    /// Creates a test logger for the specified type.
    /// </summary>
 /// <typeparam name="T">The type to create a logger for.</typeparam>
    /// <returns>A logger instance for testing.</returns>
 public static ILogger<T> Create<T>()
  {
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
   builder.SetMinimumLevel(LogLevel.Debug);
        });

        return loggerFactory.CreateLogger<T>();
    }
}
