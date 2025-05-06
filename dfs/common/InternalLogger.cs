using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace common
{
    public static class InternalLogger
    {
        // LoggerFactoryFactoryFactory...
        public static (string logPath, ILoggerFactory factory) CreateLoggerFactory(string logDirectory, LogLevel level)
        {
            System.IO.Directory.CreateDirectory(logDirectory);
            string logPath = System.IO.Path.Combine(logDirectory, $"log_{DateTime.Now:yyyyMMdd_HHmmssfff}.log");

            return (logPath, LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(level);
                builder.AddFile(logPath, minimumLevel: level);
            }));
        }
    }
}
