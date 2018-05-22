#region Using Statements

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;

#if TRACE_EVENT
using Microsoft.Diagnostics.Tracing;
#endif

#endregion

// ReSharper disable UnusedMember.Global
// ReSharper disable once CheckNamespace
namespace EtwStream
{
    public static class ConsoleSink
    {
        public static IDisposable LogToConsole(this IObservable<EventWrittenEventArgs> source)
        {
            var sink = new EventWrittenEventArgsSink(x => x.EventName + ": " + x.DumpPayloadOrMessage());

            var subscription = source.Subscribe(sink);

            return sink.CreateLinkedDisposable(subscription);
        }

        public static IDisposable LogToConsole(this IObservable<EventWrittenEventArgs> source, Func<EventWrittenEventArgs, string> messageFormatter)
        {
            var sink = new EventWrittenEventArgsSink(messageFormatter);

            var subscription = source.Subscribe(sink);

            return sink.CreateLinkedDisposable(subscription);
        }

        public static IDisposable LogToConsole(this IObservable<IList<EventWrittenEventArgs>> source)
        {
            var sink = new EventWrittenEventArgsSink(x => x.EventName + ": " + x.DumpPayloadOrMessage());

            var subscription = source.Subscribe(sink);

            return sink.CreateLinkedDisposable(subscription);
        }

        public static IDisposable LogToConsole(this IObservable<IList<EventWrittenEventArgs>> source, Func<EventWrittenEventArgs, string> messageFormatter)
        {
            var sink = new EventWrittenEventArgsSink(messageFormatter);

            var subscription = source.Subscribe(sink);

            return sink.CreateLinkedDisposable(subscription);
        }

        public static IDisposable LogToConsole(this IObservable<string> source)
            => source.Subscribe(x => Console.WriteLine(x));

        public static IDisposable LogToConsole(this IObservable<IList<string>> source)
            => source.Subscribe(x => Console.WriteLine(x));


#if TRACE_EVENT
        private class TraceEventSink : SinkBase<TraceEvent>
        {
            private static readonly object ConsoleColorChangeLock = new object();

            private readonly Func<TraceEvent, string> messageFormatter;

            public TraceEventSink(Func<TraceEvent, string> messageFormatter)
                => this.messageFormatter = messageFormatter;

            public override void Dispose() { }

            public override void OnNext(IList<TraceEvent> value)
            {
                foreach (var item in value)
                {
                    lock (ConsoleColorChangeLock)
                    {
                        var currentColor = Console.ForegroundColor;

                        try
                        {
                            var color = item.GetColorMap(isBackgroundWhite: false);

                            if (color != null)
                            {
                                Console.ForegroundColor = color.Value;
                            }

                            Console.WriteLine(this.messageFormatter(item));
                        }
                        catch (Exception ex)
                        {
                            EtwStreamEventSource.Log.SinkError(nameof(ConsoleSink), "messageFormatter convert failed", ex.ToString());

                            Console.WriteLine(ex);
                        }
                        finally
                        {
                            Console.ForegroundColor = currentColor;
                        }
                    }
                }
            }
        }

#endif

        private class EventWrittenEventArgsSink : SinkBase<EventWrittenEventArgs>
        {
            private static readonly object ConsoleColorChangeLock = new object();

            private readonly Func<EventWrittenEventArgs, string> messageFormatter;

            public EventWrittenEventArgsSink(Func<EventWrittenEventArgs, string> messageFormatter)
                => this.messageFormatter = messageFormatter;

            public override void Dispose() { }

            public override void OnNext(IList<EventWrittenEventArgs> value)
            {
                foreach (var item in value)
                {
                    lock (ConsoleColorChangeLock)
                    {
                        var currentColor = Console.ForegroundColor;

                        try
                        {
                            var color = item.GetColorMap(isBackgroundWhite: false);

                            if (color != null)
                            {
                                Console.ForegroundColor = color.Value;
                            }

                            Console.WriteLine(this.messageFormatter(item));
                        }
                        catch (Exception ex)
                        {
                            EtwStreamEventSource.Log.SinkError(nameof(ConsoleSink), "messageFormatter convert failed", ex.ToString());

                            Console.WriteLine(ex);
                        }
                        finally
                        {
                            Console.ForegroundColor = currentColor;
                        }
                    }
                }
            }
        }

#if TRACE_EVENT
        public static IDisposable LogToConsole(this IObservable<TraceEvent> source)
        {
            var sink = new TraceEventSink(x => x.EventName + ": " + x.DumpPayloadOrMessage());

            var subscription = source.Subscribe(sink);

            return sink.CreateLinkedDisposable(subscription);
        }

        public static IDisposable LogToConsole(this IObservable<TraceEvent> source, Func<TraceEvent, string> messageFormatter)
        {
            var sink = new TraceEventSink(messageFormatter);

            var subscription = source.Subscribe(sink);

            return sink.CreateLinkedDisposable(subscription);
        }

        public static IDisposable LogToConsole(this IObservable<IList<TraceEvent>> source)
        {
            var sink = new TraceEventSink(x => x.EventName + ": " + x.DumpPayloadOrMessage());

            var subscription = source.Subscribe(sink);

            return sink.CreateLinkedDisposable(subscription);
        }

        public static IDisposable LogToConsole(this IObservable<IList<TraceEvent>> source, Func<TraceEvent, string> messageFormatter)
        {
            var sink = new TraceEventSink(messageFormatter);

            var subscription = source.Subscribe(sink);

            return sink.CreateLinkedDisposable(subscription);
        }
#endif
    }
}
