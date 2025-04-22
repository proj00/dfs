using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace common
{
    public class InternalLoggerProvider : ILoggerProvider
    {
        private readonly string _path;

        public InternalLoggerProvider(string path)
        {
            _path = path;
        }

        public ILogger CreateLogger(string categoryName) =>
            new InternalLogger(_path);

        public void Dispose() { }

        // LoggerFactoryFactoryFactory...
        public static (string logPath, ILoggerFactory factory) CreateLoggerFactory(string logDirectory)
        {
            Directory.CreateDirectory(logDirectory);
            string logPath = $"{logDirectory}/log_{DateTime.Now:yyyyMMdd_HHmmssfff}.log";
            return (logPath, LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Debug);
                builder.AddProvider(new InternalLoggerProvider(logPath));
            }));
        }
    }

    class InternalLogger : ILogger
    {
        private readonly string _filePath;
        private static readonly Lock _lock = new();

        public InternalLogger(string filePath) => _filePath = filePath;

        IDisposable ILogger.BeginScope<TState>(TState state) => null!;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId,
            TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (_lock)
            {
                File.AppendAllText(_filePath, $"{DateTime.Now}: {formatter(state, exception)}{Environment.NewLine}");
            }
        }
    }
}
