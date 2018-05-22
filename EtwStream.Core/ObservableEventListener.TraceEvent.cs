#region Using Statements

using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;

#endregion

// ReSharper disable UnusedMember.Global
// ReSharper disable AccessToDisposedClosure TODO: Try to fix this
namespace EtwStream
{
    public static partial class ObservableEventListener
    {
        private const string ManifestEventName = "ManifestData";

        private const TraceEventID ManifestEventID = (TraceEventID)0xFFFE;

        public static IObservable<TraceEvent> FromTraceEvent(params string[] providerNameOrGuid)
            => FromTraceEvent(providerNameOrGuid.Select(x => new TraceEventProvider(x)).ToArray());

        public static IObservable<TraceEvent> FromTraceEvent(params Guid[] providerGuid)
            => FromTraceEvent(providerGuid.Select(x => new TraceEventProvider(x)).ToArray());

        public static IObservable<TraceEvent> FromTraceEvent(params TraceEventProvider[] providers)
        {
            IDisposable registerManifestSubscription = Disposable.Empty;

            IConnectableObservable<TraceEvent> source;

            var session = new TraceEventSession("ObservableEventListenerFromTraceEventSession." + Guid.NewGuid());

            try
            {
                registerManifestSubscription = Observable
                    .FromEvent<ProviderManifest>(
                        h => session.Source.Dynamic.DynamicProviderAdded += h,
                        h => session.Source.Dynamic.DynamicProviderAdded -= h)
                    .Subscribe(x => TraceEventExtensions.CacheSchema(x));

                source = session.Source.Dynamic.Observe((pName, eName) => EventFilterResponse.AcceptEvent)
                    .Where(x => x.EventName != ManifestEventName && x.ID != ManifestEventID)
                    .Finally(() => session.Dispose())
                    .Publish();

                foreach (var item in providers)
                {
                    session.EnableProvider(item.Guid, item.Level, item.Keywords);
                }
            }
            catch
            {
                session.Dispose();
                registerManifestSubscription.Dispose();

                throw;
            }

            Task.Factory.StartNew(() =>
            {
                using (session)
                {
                    session.Source.Process();
                }

                registerManifestSubscription.Dispose();
            }, TaskCreationOptions.LongRunning);

            return source.RefCount();
        }

        public static IObservable<TraceEvent> FromTraceEvent(string sessionName, params TraceEventProvider[] providers)
        {
            IDisposable registerManifestSubscription = Disposable.Empty;

            IConnectableObservable<TraceEvent> source;

            var session = new TraceEventSession(sessionName);

            try
            {
                registerManifestSubscription = Observable
                    .FromEvent<ProviderManifest>(
                        h => session.Source.Dynamic.DynamicProviderAdded += h,
                        h => session.Source.Dynamic.DynamicProviderAdded -= h)
                    .Subscribe(x => TraceEventExtensions.CacheSchema(x));

                source = session.Source.Dynamic.Observe((pName, eName) => EventFilterResponse.AcceptEvent)
                    .Where(x => x.EventName != ManifestEventName && x.ID != ManifestEventID)
                    .Finally(() => session.Dispose())
                    .Publish();

                foreach (var item in providers)
                {
                    session.EnableProvider(item.Guid, item.Level, item.Keywords);
                }
            }
            catch
            {
                session.Dispose();
                registerManifestSubscription.Dispose();

                throw;
            }

            Task.Factory.StartNew(() =>
            {
                using (session)
                {
                    session.Source.Process();
                }

                registerManifestSubscription.Dispose();
            }, TaskCreationOptions.LongRunning);

            return source.RefCount();
        }

        public static int ClearAllActiveObservableEventListenerSessions()
        {
            var activeSessions = TraceEventSession
                .GetActiveSessionNames()
                .Where(x => x.StartsWith("ObservableEventListener"))
                .Select(TraceEventSession.GetActiveSession);

            var activeCount = 0;

            foreach (var item in activeSessions)
            {
                item.Dispose();

                activeCount++;
            }

            return activeCount;
        }

        public static int ClearActiveObservableEventListenerSession(string sessionName)
        {
            var activeSessions = TraceEventSession
                .GetActiveSessionNames()
                .Where(x => x.Equals(sessionName))
                .Select(TraceEventSession.GetActiveSession);

            var activeCount = 0;

            foreach (var item in activeSessions)
            {
                item.Dispose();

                activeCount++;
            }

            return activeCount;
        }
    }
}
