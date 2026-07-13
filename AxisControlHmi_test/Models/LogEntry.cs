using System;

namespace AxisControlHmi_test.Models
{
    public sealed class LogEntry
    {
        public LogEntry(string level, string message)
        {
            Timestamp = DateTime.Now;
            Level = level;
            Message = message;
        }

        public DateTime Timestamp { get; }
        public string Level { get; }
        public string Message { get; }
    }
}
