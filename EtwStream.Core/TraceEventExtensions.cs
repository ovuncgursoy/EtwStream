#region Using Statements

using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

using EtwStream.Json;

using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;

#endregion

namespace EtwStream
{
    public static class TraceEventExtensions
    {
        private static readonly ConcurrentDictionary<Guid, ReadOnlyDictionary<int, string>> Cache
            = new ConcurrentDictionary<Guid, ReadOnlyDictionary<int, string>>();

        internal static void CacheSchema(ProviderManifest manifest)
        {
            var manifestXml = XElement.Parse(manifest.Manifest);
            var manifestNamespace = manifestXml.DescendantsAndSelf().First(x => x.Name.LocalName != "Event").Name.Namespace;

            var unused = manifestXml
                .Descendants(manifestNamespace + "template")
                .ToDictionary(x => x.Attribute("tid")?.Value, x => new ReadOnlyCollection<string>(x.Elements(manifestNamespace + "data")
                    .Select(y => y.Attribute("name")?.Value)
                    .ToArray()));

            var manifestEvents = manifestXml
                .Descendants(manifestNamespace + "event")
                .ToDictionary(
                    x => int.Parse(x.Attribute("value")?.Value ?? throw new InvalidOperationException()),
                    x => x.Attribute("keywords")?.Value ?? "");

            Cache[manifest.Guid] = new ReadOnlyDictionary<int, string>(manifestEvents);
        }

        public static string GetKeywordName(this TraceEvent traceEvent)
            => Cache.TryGetValue(traceEvent.ProviderGuid, out var schema)
                ? schema.TryGetValue((int)traceEvent.ID, out var name) ? name
                : traceEvent.Keywords.ToString()
                : traceEvent.Keywords.ToString();

        public static ConsoleColor? GetColorMap(this TraceEvent traceEvent, bool isBackgroundWhite)
        {
            switch (traceEvent.Level)
            {
                case TraceEventLevel.Critical:
                    return ConsoleColor.Magenta;
                case TraceEventLevel.Error:
                    return ConsoleColor.Red;
                case TraceEventLevel.Informational:
                    return ConsoleColor.Green;
                case TraceEventLevel.Verbose:
                    return ConsoleColor.Gray;
                case TraceEventLevel.Warning:
                    return isBackgroundWhite ? ConsoleColor.DarkRed : ConsoleColor.Yellow;
                case TraceEventLevel.Always:
                    return isBackgroundWhite ? ConsoleColor.Black : ConsoleColor.White;
                default:
                    return null;
            }
        }

        public static string DumpPayload(this TraceEvent traceEvent)
        {
            var names = traceEvent.PayloadNames;

            var stringBuilder = new StringBuilder();

            stringBuilder.Append("{");

            var count = names.Length;

            for (var i = 0; i < count; i++)
            {
                if (i != 0)
                {
                    stringBuilder.Append(", ");
                }

                var name = names[i];
                var value = traceEvent.PayloadString(i);
                stringBuilder.Append(name).Append(": ").Append(value);
            }

            stringBuilder.Append("}");

            return stringBuilder.ToString();
        }

        public static string DumpPayloadOrMessage(this TraceEvent traceEvent)
        {
            var msg = traceEvent.FormattedMessage;

            return string.IsNullOrWhiteSpace(msg) ? traceEvent.DumpPayload() : msg;
        }

        public static string ToJson(this TraceEvent traceEvent)
        {
            var names = traceEvent.PayloadNames;
            var count = names.Length;

            using (var stringWriter = new StringWriter())
            using (var jsonWriter = new TinyJsonWriter(stringWriter))
            {
                jsonWriter.WriteStartObject();

                for (var i = 0; i < count; i++)
                {
                    var name = names[i];
                    var value = traceEvent.PayloadString(i);

                    jsonWriter.WritePropertyName(name);
                    jsonWriter.WriteValue(value);
                }

                jsonWriter.WriteEndObject();

                stringWriter.Flush();

                return stringWriter.ToString();
            }
        }
    }
}
