#region Using Statements

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Text;

#if TRACE_EVENT
using Microsoft.Diagnostics.Tracing;
#endif

#endregion

// ReSharper disable UnusedMember.Global
// ReSharper disable once CheckNamespace
namespace EtwStream
{
    public static class FileSink
    {
        public static IDisposable LogToFile(this IObservable<EventWrittenEventArgs> source,
            string fileName,
            Func<EventWrittenEventArgs, string> messageFormatter,
            Encoding encoding,
            bool autoFlush)
        {
            var sink = new EventWrittenEventArgsSink(fileName, messageFormatter, encoding, autoFlush);

            var subscription = source.Subscribe(sink);

            return sink.CreateLinkedDisposable(subscription);
        }

        public static IDisposable LogToFile(this IObservable<IList<EventWrittenEventArgs>> source,
            string fileName,
            Func<EventWrittenEventArgs, string> messageFormatter,
            Encoding encoding,
            bool autoFlush)
        {
            var sink = new EventWrittenEventArgsSink(fileName, messageFormatter, encoding, autoFlush);

            var subscription = source.Subscribe(sink);

            return sink.CreateLinkedDisposable(subscription);
        }

        public static IDisposable LogToFile(this IObservable<string> source, string fileName, Encoding encoding, bool autoFlush)
        {
            var sink = new StringSink(fileName, encoding, autoFlush);

            var subscription = source.Subscribe(sink);

            return sink.CreateLinkedDisposable(subscription);
        }

        public static IDisposable LogToFile(this IObservable<IList<string>> source,
            string fileName,
            Encoding encoding,
            bool autoFlush)
        {
            var sink = new StringSink(fileName, encoding, autoFlush);

            var subscription = source.Subscribe(sink);

            return sink.CreateLinkedDisposable(subscription);
        }

#if TRACE_EVENT

        private class TraceEventSink : SinkBase<TraceEvent>
        {
            private readonly AsyncFileWriter asyncFileWriter;

            private readonly Func<TraceEvent, string> messageFormatter;

            private readonly Action<TraceEvent> onNext;

            public TraceEventSink(string fileName, Func<TraceEvent, string> messageFormatter, Encoding encoding, bool autoFlush)
            {
                this.asyncFileWriter = new AsyncFileWriter(nameof(FileSink), fileName, encoding, autoFlush);

                this.messageFormatter = messageFormatter;

                this.onNext = OnNext;
            }

            public override void OnNext(TraceEvent value)
            {
                string v;

                try
                {
                    v = this.messageFormatter(value);
                }
                catch (Exception ex)
                {
                    EtwStreamEventSource.Log.SinkError(nameof(FileSink), "messageFormatter convert failed", ex.ToString());
                    return;
                }

                this.asyncFileWriter.Enqueue(v);
            }

            public override void OnNext(IList<TraceEvent> value)
            {
                value.FastForEach(this.onNext);
            }

            public override void Dispose()
            {
                this.asyncFileWriter.MakeFinal();
            }
        }
#endif

        private class EventWrittenEventArgsSink : SinkBase<EventWrittenEventArgs>
        {
            private readonly AsyncFileWriter asyncFileWriter;

            private readonly Func<EventWrittenEventArgs, string> messageFormatter;

            private readonly Action<EventWrittenEventArgs> onNext;

            public EventWrittenEventArgsSink(string fileName, Func<EventWrittenEventArgs, string> messageFormatter, Encoding encoding, bool autoFlush)
            {
                this.asyncFileWriter = new AsyncFileWriter(nameof(FileSink), fileName, encoding, autoFlush);

                this.messageFormatter = messageFormatter;

                this.onNext = OnNext;
            }

            public override void OnNext(EventWrittenEventArgs value)
            {
                string v;

                try
                {
                    v = this.messageFormatter(value);
                }
                catch (Exception ex)
                {
                    EtwStreamEventSource.Log.SinkError(nameof(FileSink), "messageFormatter convert failed", ex.ToString());
                    return;
                }

                this.asyncFileWriter.Enqueue(v);
            }

            public override void OnNext(IList<EventWrittenEventArgs> value)
            {
                value.FastForEach(this.onNext);
            }

            public override void Dispose()
            {
                this.asyncFileWriter.MakeFinal();
            }
        }

        private class StringSink : SinkBase<string>
        {
            private readonly AsyncFileWriter asyncFileWriter;

            private readonly Action<string> onNext;

            public StringSink(string fileName, Encoding encoding, bool autoFlush)
            {
                this.asyncFileWriter = new AsyncFileWriter(nameof(FileSink), fileName, encoding, autoFlush);

                this.onNext = OnNext;
            }

            public override void OnNext(string value)
            {
                this.asyncFileWriter.Enqueue(value);
            }

            public override void OnNext(IList<string> value)
            {
                value.FastForEach(this.onNext);
            }

            public override void Dispose()
            {
                this.asyncFileWriter.MakeFinal();
            }
        }

#if TRACE_EVENT
        public static IDisposable LogToFile(this IObservable<TraceEvent> source,
            string fileName,
            Func<TraceEvent, string> messageFormatter,
            Encoding encoding,
            bool autoFlush)
        {
            var sink = new TraceEventSink(fileName, messageFormatter, encoding, autoFlush);

            var subscription = source.Subscribe(sink);

            return sink.CreateLinkedDisposable(subscription);
        }

        public static IDisposable LogToFile(this IObservable<IList<TraceEvent>> source,
            string fileName,
            Func<TraceEvent, string> messageFormatter,
            Encoding encoding,
            bool autoFlush)
        {
            var sink = new TraceEventSink(fileName, messageFormatter, encoding, autoFlush);

            var subscription = source.Subscribe(sink);

            return sink.CreateLinkedDisposable(subscription);
        }
#endif
    }
}
