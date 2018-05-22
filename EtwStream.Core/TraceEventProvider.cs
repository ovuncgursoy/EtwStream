#region Using Statements

using System;

using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;

#endregion

namespace EtwStream
{
    public class TraceEventProvider
    {
        public TraceEventProvider(string nameOrGuid, TraceEventLevel level = TraceEventLevel.Verbose, ulong keywords = ulong.MaxValue)
        {
            Guid = !Guid.TryParse(nameOrGuid, out var guid) ? TraceEventProviders.GetEventSourceGuidFromName(nameOrGuid) : guid;
            Level = level;
            Keywords = keywords;
        }

        public TraceEventProvider(Guid guid, TraceEventLevel level = TraceEventLevel.Verbose, ulong keywords = ulong.MaxValue)
        {
            Guid = guid;
            Level = level;
            Keywords = keywords;
        }

        public Guid Guid { get; }

        public TraceEventLevel Level { get; }

        public ulong Keywords { get; }
    }
}
