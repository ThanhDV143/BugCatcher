using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

namespace ThanhDV.BugCatcher
{
    public enum LogKind { Log, Warning, Error }

    /// <summary>One distinct message (message + stack trace). Repeats only bump <see cref="Count"/>.</summary>
    public class LogEntry
    {
        public string Message;
        public string StackTrace;
        public LogType Type;
        public LogKind Kind;
        public DateTime FirstTime;
        public int FirstFrame;
        public int Count;
    }

    /// <summary>One occurrence of a log, used when duplicates are not collapsed.</summary>
    public readonly struct LogRecord
    {
        public readonly LogEntry Entry;
        public readonly DateTime Time;
        public readonly int Frame;

        public LogRecord(LogEntry entry, DateTime time, int frame)
        {
            Entry = entry;
            Time = time;
            Frame = frame;
        }
    }

    /// <summary>
    /// Captures Unity logs from any thread. Logs are queued and folded in on the main thread
    /// by <see cref="Drain"/>, so listeners always run on the main thread.
    /// </summary>
    public class LogStore
    {
        private const int MaxRecords = 10000;

        private readonly struct Pending
        {
            public readonly string Message;
            public readonly string StackTrace;
            public readonly LogType Type;
            public readonly DateTime Time;

            public Pending(string message, string stackTrace, LogType type, DateTime time)
            {
                Message = message;
                StackTrace = stackTrace;
                Type = type;
                Time = time;
            }
        }

        private readonly ConcurrentQueue<Pending> pending = new ConcurrentQueue<Pending>();
        private readonly Dictionary<string, LogEntry> byKey = new Dictionary<string, LogEntry>();
        private readonly List<LogEntry> entries = new List<LogEntry>();
        private readonly List<LogRecord> records = new List<LogRecord>();
        private readonly int[] counts = new int[3];

        public IReadOnlyList<LogEntry> Entries => entries;
        public IReadOnlyList<LogRecord> Records => records;

        /// <summary>Total occurrences of a kind, duplicates included.</summary>
        public int CountOf(LogKind kind) => counts[(int)kind];

        public int TotalCount => counts[0] + counts[1] + counts[2];

        /// <summary>Raised once per frame that received new logs.</summary>
        public event Action OnChanged;

        /// <summary>Raised once per frame that received at least one Error/Exception/Assert.</summary>
        public event Action OnNewError;

        public void Attach()
        {
            Application.logMessageReceivedThreaded -= Receive;
            Application.logMessageReceivedThreaded += Receive;
        }

        public void Detach()
        {
            Application.logMessageReceivedThreaded -= Receive;
        }

        private void Receive(string message, string stackTrace, LogType type)
        {
            pending.Enqueue(new Pending(message ?? string.Empty, stackTrace ?? string.Empty, type, DateTime.Now));
        }

        public void Drain()
        {
            if (pending.IsEmpty) return;

            bool newError = false;
            int frame = Time.frameCount;
            while (pending.TryDequeue(out Pending log))
            {
                LogKind kind = KindOf(log.Type);
                string key = log.Message + "\n" + log.StackTrace;
                if (!byKey.TryGetValue(key, out LogEntry entry))
                {
                    entry = new LogEntry
                    {
                        Message = log.Message,
                        StackTrace = log.StackTrace,
                        Type = log.Type,
                        Kind = kind,
                        FirstTime = log.Time,
                        FirstFrame = frame
                    };
                    byKey.Add(key, entry);
                    entries.Add(entry);
                }

                entry.Count++;
                counts[(int)kind]++;
                records.Add(new LogRecord(entry, log.Time, frame));
                if (kind == LogKind.Error) newError = true;
            }

            if (records.Count > MaxRecords) records.RemoveRange(0, records.Count - MaxRecords);

            OnChanged?.Invoke();
            if (newError) OnNewError?.Invoke();
        }

        public void Clear()
        {
            while (pending.TryDequeue(out _)) { }
            byKey.Clear();
            entries.Clear();
            records.Clear();
            Array.Clear(counts, 0, counts.Length);
            OnChanged?.Invoke();
        }

        public static LogKind KindOf(LogType type)
        {
            switch (type)
            {
                case LogType.Warning: return LogKind.Warning;
                case LogType.Log: return LogKind.Log;
                default: return LogKind.Error;
            }
        }

        public static string TypeLabel(LogType type)
        {
            switch (type)
            {
                case LogType.Exception: return "Exception";
                case LogType.Assert: return "Assert";
                case LogType.Error: return "Error";
                case LogType.Warning: return "Warning";
                default: return "Log";
            }
        }

        /// <summary>Plain-text dump of every record, oldest first.</summary>
        public string Export()
        {
            var sb = new System.Text.StringBuilder(records.Count * 96);
            foreach (LogRecord record in records)
            {
                sb.Append(record.Time.ToString("HH:mm:ss.fff")).Append(" [").Append(TypeLabel(record.Entry.Type)).Append("] ")
                  .AppendLine(record.Entry.Message);
                if (record.Entry.Kind != LogKind.Log && record.Entry.StackTrace.Length > 0)
                    sb.AppendLine(record.Entry.StackTrace.TrimEnd());
            }
            return sb.ToString();
        }
    }
}
