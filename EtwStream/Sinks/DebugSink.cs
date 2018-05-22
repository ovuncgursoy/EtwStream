#region Using Statements

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;

#if TRACE_EVENT
using Microsoft.Diagnostics.Tracing;
#endif

#endregion

// ReSharper disable UnusedMember.Global
// ReSharper disable once CheckNamespace
namespace EtwStream
{
    public static class DebugSink
    {
        public static IDisposable LogToDebug(this IObservable<EventWrittenEventArgs> source)
            => source.Subscribe(x => Debug.WriteLine(x.EventName + ": " + x.DumpPayloadOrMessage()));

        public static IDisposable LogToDebug(this IObservable<EventWrittenEventArgs> source, Func<EventWrittenEventArgs, string> messageFormatter)
            => source.Subscribe(x => Debug.WriteLine(messageFormatter(x)));

        public static IDisposable LogToDebug(this IObservable<IList<EventWrittenEventArgs>> source)
            => source.Subscribe(xs => xs.FastForEach(x => Debug.WriteLine(x.EventName + ": " + x.DumpPayloadOrMessage())));

        public static IDisposable LogToDebug(this IObservable<IList<EventWrittenEventArgs>> source, Func<EventWrittenEventArgs, string> messageFormatter)
            => source.Subscribe(xs => xs.FastForEach(x => Debug.WriteLine(messageFormatter(x))));

        public static IDisposable LogToDebug(this IObservable<string> source)
            => source.Subscribe(x => Debug.WriteLine(x));

        public static IDisposable LogToDebug(this IObservable<IList<string>> source)
            => source.Subscribe(xs => xs.FastForEach(x => Debug.WriteLine(x)));

#if TRACE_EVENT
        public static IDisposable LogToDebug(this IObservable<TraceEvent> source)
            => source.Subscribe(x => Debug.WriteLine(x.EventName + ": " + x.DumpPayloadOrMessage()));

        public static IDisposable LogToDebug(this IObservable<TraceEvent> source, Func<TraceEvent, string> messageFormatter)
            => source.Subscribe(x => Debug.WriteLine(messageFormatter(x)));

        public static IDisposable LogToDebug(this IObservable<IList<TraceEvent>> source)
            => source.Subscribe(xs => xs.FastForEach(x => Debug.WriteLine(x.EventName + ": " + x.DumpPayloadOrMessage())));

        public static IDisposable LogToDebug(this IObservable<IList<TraceEvent>> source, Func<TraceEvent, string> messageFormatter)
            => source.Subscribe(xs => xs.FastForEach(x => Debug.WriteLine(messageFormatter(x))));
#endif
    }
}
