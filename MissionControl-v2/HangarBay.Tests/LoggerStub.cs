using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Unity.ClusterDisplay.MissionControl.HangarBay.Tests
{
    internal class LoggerStub : ILogger
    {
        class DummyDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }

        public class Message
        {
            public LogLevel Level { get; set; }
            public EventId EventId { get; set; }
            public string Content { get; set; } = "";

        };

        public List<Message> Messages { get; set; } = new();

        public IDisposable BeginScope<TState>(TState state)
        {
            return new DummyDisposable();
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = new Message()
            {
                Level = logLevel,
                EventId = eventId,
                Content = formatter(state, exception)
            };
            Messages.Add(message);
        }
    }
}
