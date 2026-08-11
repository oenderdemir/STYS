using Microsoft.Extensions.Logging;

namespace STYS.Agent.Diagnostics;

public sealed class AgentInMemoryLogProvider : ILoggerProvider
{
    private readonly IAgentLogBuffer _buffer;

    public AgentInMemoryLogProvider(IAgentLogBuffer buffer)
    {
        _buffer = buffer;
    }

    public ILogger CreateLogger(string categoryName) => new AgentInMemoryLogger(_buffer, categoryName);

    public void Dispose()
    {
    }

    private sealed class AgentInMemoryLogger : ILogger
    {
        private readonly IAgentLogBuffer _buffer;
        private readonly string _categoryName;

        public AgentInMemoryLogger(IAgentLogBuffer buffer, string categoryName)
        {
            _buffer = buffer;
            _categoryName = categoryName;
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var rendered = formatter(state, exception);
            if (exception is not null)
            {
                rendered = $"{rendered} | {exception.GetType().Name}: {exception.Message}";
            }

            _buffer.Add(_categoryName, logLevel.ToString(), rendered, DateTimeOffset.UtcNow);
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose()
            {
            }
        }
    }
}
