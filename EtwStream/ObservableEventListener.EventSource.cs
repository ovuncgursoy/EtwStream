#region Using Statements

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Reactive.Linq;
using System.Reactive.Subjects;

#endregion

// ReSharper disable UnusedMember.Global
namespace EtwStream
{
    public static partial class ObservableEventListener
    {
        public static IObservable<EventWrittenEventArgs> FromEventSource(EventSource eventSource,
            EventLevel level = EventLevel.Verbose,
            EventKeywords matchAnyKeyword = EventKeywords.None,
            IDictionary<string, string> arguments = null)
        {
            if (eventSource == null)
            {
                throw new ArgumentNullException(nameof(eventSource));
            }

            var listener = new SystemEventSourceListener();
            listener.EnableEvents(eventSource, level, matchAnyKeyword, arguments);

            return listener.Finally(() => listener.DisableEvents(eventSource));
        }

        public static IObservable<EventWrittenEventArgs> FromEventSource(string eventSourceName,
            EventLevel level = EventLevel.Verbose,
            EventKeywords matchAnyKeyword = EventKeywords.None,
            IDictionary<string, string> arguments = null)
        {
            if (eventSourceName == null)
            {
                throw new ArgumentNullException(nameof(eventSourceName));
            }

            foreach (var item in EventSource.GetSources())
            {
                if (item.Name.Equals(eventSourceName, StringComparison.Ordinal))
                {
                    return FromEventSource(item, level);
                }
            }

            var listener = new SystemEventSourceListener();

            listener.RegisterDelay(new SystemArgs
            {
                EventSourceName = eventSourceName,
                Level = level,
                MatchAnyKeyword = matchAnyKeyword,
                Arguments = arguments
            });

            return listener;
        }

        private class SystemEventSourceListener : EventListener, IObservable<EventWrittenEventArgs>
        {
            private readonly LinkedList<SystemArgs> delayedRegisters = new LinkedList<SystemArgs>();

            private readonly object delayLock = new object();

            private readonly Subject<EventWrittenEventArgs> subject = new Subject<EventWrittenEventArgs>();

            public IDisposable Subscribe(IObserver<EventWrittenEventArgs> observer)
                => this.subject.Subscribe(observer);

            public void RegisterDelay(SystemArgs args)
            {
                lock (this.delayLock)
                {
                    this.delayedRegisters.AddFirst(args);
                }
            }

            protected override void OnEventSourceCreated(EventSource eventSource)
            {
                base.OnEventSourceCreated(eventSource);

                lock (this.delayLock)
                {
                    var node = this.delayedRegisters.First;

                    while (node != null)
                    {
                        var currentNode = node;
                        node = currentNode.Next;

                        if (!eventSource.Name.Equals(currentNode.Value.EventSourceName, StringComparison.Ordinal))
                        {
                            continue;
                        }


                        EnableEvents(eventSource, currentNode.Value.Level, currentNode.Value.MatchAnyKeyword, currentNode.Value.Arguments);

                        this.delayedRegisters.Remove(currentNode);
                    }
                }
            }

            protected override void OnEventWritten(EventWrittenEventArgs eventData)
            {
                this.subject.OnNext(eventData);
            }
        }

        private class SystemArgs
        {
            public IDictionary<string, string> Arguments;

            public string EventSourceName;

            public EventLevel Level;

            public EventKeywords MatchAnyKeyword;
        }
    }
}
