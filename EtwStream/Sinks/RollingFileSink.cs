#region Using Statements

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

#if TRACE_EVENT
using Microsoft.Diagnostics.Tracing;
#endif

#endregion

// ReSharper disable UnusedMember.Global
// ReSharper disable once CheckNamespace
namespace EtwStream
{
    public static class RollingFileSink
    {
        public static IDisposable LogToRollingFile(this IObservable<EventWrittenEventArgs> source,
            Func<DateTime, int, string> fileNameSelector,
            Func<DateTime, string> timestampPattern,
            int rollSizeKb,
            Func<EventWrittenEventArgs, string> messageFormatter,
            Encoding encoding,
            bool autoFlush)
        {
            var sink = new EventWrittenEventArgsSink(fileNameSelector, timestampPattern, rollSizeKb, messageFormatter, encoding, autoFlush);

            var subscription = source.Subscribe(sink);

            return sink.CreateLinkedDisposable(subscription);
        }

        public static IDisposable LogToRollingFile(this IObservable<IList<EventWrittenEventArgs>> source,
            Func<DateTime, int, string> fileNameSelector,
            Func<DateTime, string> timestampPattern,
            int rollSizeKb,
            Func<EventWrittenEventArgs, string> messageFormatter,
            Encoding encoding,
            bool autoFlush)
        {
            var sink = new EventWrittenEventArgsSink(fileNameSelector, timestampPattern, rollSizeKb, messageFormatter, encoding, autoFlush);

            var subscription = source.Subscribe(sink);

            return sink.CreateLinkedDisposable(subscription);
        }

        public static IDisposable LogToRollingFile(this IObservable<string> source,
            Func<DateTime, int, string> fileNameSelector,
            Func<DateTime, string> timestampPattern,
            int rollSizeKb,
            Encoding encoding,
            bool autoFlush)
        {
            var sink = new StringSink(fileNameSelector, timestampPattern, rollSizeKb, encoding, autoFlush);

            var subscription = source.Subscribe(sink);

            return sink.CreateLinkedDisposable(subscription);
        }

        public static IDisposable LogToRollingFile(this IObservable<IList<string>> source,
            Func<DateTime, int, string> fileNameSelector,
            Func<DateTime, string> timestampPattern,
            int rollSizeKb,
            Encoding encoding,
            bool autoFlush)
        {
            var sink = new StringSink(fileNameSelector, timestampPattern, rollSizeKb, encoding, autoFlush);

            var subscription = source.Subscribe(sink);

            return sink.CreateLinkedDisposable(subscription);
        }

        abstract class RollingFileSinkBase<T> : SinkBase<T>
        {
            // ReSharper disable once StaticMemberInGenericType
            private static readonly Regex NumberRegex = new Regex("(\\d)+$", RegexOptions.Compiled);

            private readonly bool autoFlush;

            private readonly Encoding encoding;

            private readonly Func<DateTime, int, string> fileNameSelector;

            private readonly Func<T, string> MessageFormatter;

            private readonly object newFileLock = new object();

            private readonly Action<T> onNextCore;

            private readonly long rollSizeInBytes;

            private readonly Func<DateTime, string> timestampPattern;

            private AsyncFileWriter AsyncFileWriter;

            private string currentTimestampPattern;

            protected RollingFileSinkBase(
                Func<DateTime, int, string> fileNameSelector,
                Func<DateTime, string> timestampPattern,
                int rollSizeKB,
                Func<T, string> messageFormatter,
                Encoding encoding,
                bool autoFlush)
            {
                this.MessageFormatter = messageFormatter;
                this.timestampPattern = timestampPattern;
                this.fileNameSelector = fileNameSelector;
                this.rollSizeInBytes = rollSizeKB * 1024;
                this.encoding = encoding;
                this.autoFlush = autoFlush;
                this.onNextCore = OnNextCore;

                ValidateFileNameSelector();
            }

            private void ValidateFileNameSelector()
            {
                var now = DateTime.Now;
                var fileName1 = Path.GetFileNameWithoutExtension(this.fileNameSelector(now, 0));
                var fileName2 = Path.GetFileNameWithoutExtension(this.fileNameSelector(now, 1));

                if (!NumberRegex.IsMatch(fileName1 ?? throw new InvalidOperationException())
                    || !NumberRegex.IsMatch(fileName2 ?? throw new InvalidOperationException()))
                {
                    throw new ArgumentException("fileNameSelector is invalid format, must be int(sequence no) is last.");
                }

                var seqStr1 = NumberRegex.Match(fileName1).Groups[0].Value;
                var seqStr2 = NumberRegex.Match(fileName2).Groups[0].Value;

                if (!int.TryParse(seqStr1, out var seq1) || !int.TryParse(seqStr2, out var seq2))
                {
                    throw new ArgumentException("fileNameSelector is invalid format, must be int(sequence no) is last.");
                }

                if (seq1 == seq2)
                {
                    throw new ArgumentException("fileNameSelector is invalid format, must be int(sequence no) is incremental.");
                }
            }

            private void CheckFileRolling()
            {
                var now = DateTime.Now;

                string ts;

                try
                {
                    ts = this.timestampPattern(now);
                }
                catch (Exception ex)
                {
                    EtwStreamEventSource.Log.SinkError(nameof(RollingFileSink), "timestampPattern convert failed", ex.ToString());
                    return;
                }

                var disposeTarget = this.AsyncFileWriter;

                if (disposeTarget == null || ts != this.currentTimestampPattern || disposeTarget.CurrentStreamLength >= this.rollSizeInBytes)
                {
                    lock (this.newFileLock)
                    {
                        if (this.AsyncFileWriter == disposeTarget)
                        {
                            var sequenceNo = 0;

                            if (disposeTarget != null)
                            {
                                sequenceNo = ExtractCurrentSequence(this.AsyncFileWriter.FileName) + 1;
                            }

                            string fn = null;

                            while (true)
                            {
                                try
                                {
                                    var newFn = this.fileNameSelector(now, sequenceNo);

                                    if (fn == newFn)
                                    {
                                        EtwStreamEventSource.Log.SinkError(nameof(RollingFileSink), "fileNameSelector indicate same filname", "");
                                        return;
                                    }

                                    fn = newFn;
                                }
                                catch (Exception ex)
                                {
                                    EtwStreamEventSource.Log.SinkError(nameof(RollingFileSink), "fileNamemessageFormatter convert failed", ex.ToString());
                                    return;
                                }

                                var fi = new FileInfo(fn);

                                if (fi.Exists)
                                {
                                    if (fi.Length >= this.rollSizeInBytes)
                                    {
                                        sequenceNo++;
                                        continue;
                                    }
                                }

                                break;
                            }

                            string[] safe;

                            try
                            {
                                safe = disposeTarget?.MakeFinal();
                            }
                            catch (Exception ex)
                            {
                                EtwStreamEventSource.Log.SinkError(nameof(RollingFileSink), "Can't dispose fileStream", ex.ToString());

                                return;
                            }

                            try
                            {
                                this.AsyncFileWriter = new AsyncFileWriter(nameof(RollingFileSink), fn, this.encoding, this.autoFlush);

                                this.currentTimestampPattern = ts;

                                if (safe != null)
                                {
                                    foreach (var item in safe)
                                    {
                                        this.AsyncFileWriter.Enqueue(item);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                EtwStreamEventSource.Log.SinkError(nameof(RollingFileSink), "Can't create FileStream", ex.ToString());
                            }
                        }
                    }
                }
            }

            private static int ExtractCurrentSequence(string fileName)
            {
                fileName = Path.GetFileNameWithoutExtension(fileName);

                var sequenceString = NumberRegex.Match(fileName ?? throw new ArgumentNullException(nameof(fileName))).Groups[0].Value;

                return int.TryParse(sequenceString, out var seq) ? seq : 0;
            }

            public override void OnNext(T value)
            {
                CheckFileRolling();

                OnNextCore(value);
            }

            public override void OnNext(IList<T> value)
            {
                CheckFileRolling();

                value.FastForEach(this.onNextCore);
            }

            private void OnNextCore(T value)
            {
                string v;

                try
                {
                    v = this.MessageFormatter(value);
                }
                catch (Exception ex)
                {
                    EtwStreamEventSource.Log.SinkError(nameof(RollingFileSink), "messageFormatter convert failed", ex.ToString());

                    return;
                }

                this.AsyncFileWriter.Enqueue(v);
            }

            public override void Dispose()
            {
                this.AsyncFileWriter?.MakeFinal();
            }
        }

#if TRACE_EVENT
        private class TraceEventSink : RollingFileSinkBase<TraceEvent>
        {
            public TraceEventSink(
                Func<DateTime, int, string> fileNameSelector,
                Func<DateTime, string> timestampPattern,
                int rollSizeKb,
                Func<TraceEvent, string> messageFormatter,
                Encoding encoding,
                bool autoFlush)
                : base(fileNameSelector, timestampPattern, rollSizeKb, messageFormatter, encoding, autoFlush) { }
        }
#endif

        private class EventWrittenEventArgsSink : RollingFileSinkBase<EventWrittenEventArgs>
        {
            public EventWrittenEventArgsSink(
                Func<DateTime, int, string> fileNameSelector,
                Func<DateTime, string> timestampPattern,
                int rollSizeKb,
                Func<EventWrittenEventArgs, string> messageFormatter,
                Encoding encoding,
                bool autoFlush)
                : base(fileNameSelector, timestampPattern, rollSizeKb, messageFormatter, encoding, autoFlush) { }
        }

        private class StringSink : RollingFileSinkBase<string>
        {
            public StringSink(
                Func<DateTime, int, string> fileNameSelector,
                Func<DateTime, string> timestampPattern,
                int rollSizeKb,
                Encoding encoding,
                bool autoFlush)
                : base(fileNameSelector, timestampPattern, rollSizeKb, x => x, encoding, autoFlush) { }
        }

#if TRACE_EVENT
        public static IDisposable LogToRollingFile(this IObservable<TraceEvent> source,
            Func<DateTime, int, string> fileNameSelector,
            Func<DateTime, string> timestampPattern,
            int rollSizeKb,
            Func<TraceEvent, string> messageFormatter,
            Encoding encoding,
            bool autoFlush)
        {
            var sink = new TraceEventSink(fileNameSelector, timestampPattern, rollSizeKb, messageFormatter, encoding, autoFlush);

            var subscription = source.Subscribe(sink);

            return sink.CreateLinkedDisposable(subscription);
        }

        public static IDisposable LogToRollingFile(this IObservable<IList<TraceEvent>> source,
            Func<DateTime, int, string> fileNameSelector,
            Func<DateTime, string> timestampPattern,
            int rollSizeKb,
            Func<TraceEvent, string> messageFormatter,
            Encoding encoding,
            bool autoFlush)
        {
            var sink = new TraceEventSink(fileNameSelector, timestampPattern, rollSizeKb, messageFormatter, encoding, autoFlush);

            var subscription = source.Subscribe(sink);

            return sink.CreateLinkedDisposable(subscription);
        }
#endif
    }
}
