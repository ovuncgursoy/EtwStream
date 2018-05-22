#region Using Statements

using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

using EtwStream.Json;

#endregion

// ReSharper disable UnusedMember.Global
namespace EtwStream
{
    public static class EventWrittenEventArgsExtensions
    {
        private static readonly ConcurrentDictionary<EventSource, ReadOnlyDictionary<int, EventSchemaPortion>> Cache
            = new ConcurrentDictionary<EventSource, ReadOnlyDictionary<int, EventSchemaPortion>>();

        static ReadOnlyDictionary<int, EventSchemaPortion> GetEventSchemaPortions(EventSource source)
        {
            return Cache.GetOrAdd(source, s =>
            {
                var manifest = EventSource.GenerateManifest(s.GetType(), null);

                var manifestXml = XElement.Parse(manifest);
                var manifestNamespace = manifestXml.Name.Namespace;

                var tidReferences = manifestXml
                    .Descendants(manifestNamespace + "template")
                    .ToDictionary(x => x.Attribute("tid")?.Value, x => new ReadOnlyCollection<string>(x.Elements(manifestNamespace + "data")
                        .Select(y => y.Attribute("name")?.Value)
                        .ToArray()));

                var manifestEvents = manifestXml.Descendants(manifestNamespace + "event")
                    .ToDictionary(x => int.Parse(x.Attribute("value")?.Value ?? throw new InvalidOperationException()), x => new EventSchemaPortion(
                        x.Attribute("template")?.Value != null ? tidReferences[x.Attribute("template")?.Value ?? throw new InvalidOperationException()] : new string[0].ToList().AsReadOnly(),
                        x.Attribute("keywords")?.Value ?? "",
                        x.Attribute("task")?.Value ?? x.Attribute("symbol")?.Value));

                return new ReadOnlyDictionary<int, EventSchemaPortion>(manifestEvents);
            });
        }

        public static ReadOnlyCollection<string> GetPayloadNames(this EventWrittenEventArgs eventArgs)
        {
            var source = eventArgs.EventSource;
            var templates = GetEventSchemaPortions(source);

            return templates.TryGetValue(eventArgs.EventId, out var portion)
                ? portion.Payload
                : eventArgs.PayloadNames;
        }

        public static string GetKeywordName(this EventWrittenEventArgs eventArgs)
        {
            var source = eventArgs.EventSource;
            var templates = GetEventSchemaPortions(source);

            return templates.TryGetValue(eventArgs.EventId, out var portion)
                ? portion.KeywordDesciption
                : eventArgs.Keywords.ToString();
        }

        public static string GetTaskName(this EventWrittenEventArgs eventArgs)
        {
            var source = eventArgs.EventSource;
            var templates = GetEventSchemaPortions(source);

            return templates.TryGetValue(eventArgs.EventId, out var portion)
                ? portion.TaskName
                : eventArgs.Task.ToString();
        }

        public static string DumpFormattedMessage(this EventWrittenEventArgs eventArgs)
        {
            var msg = eventArgs.Message;

            return string.IsNullOrWhiteSpace(msg) ? msg : string.Format(msg, eventArgs.Payload.ToArray());
        }

        public static string DumpPayload(this EventWrittenEventArgs eventArgs)
        {
            var names = eventArgs.GetPayloadNames();

            var stringBuilder = new StringBuilder();

            stringBuilder.Append("{");

            var count = eventArgs.Payload.Count;

            for (var i = 0; i < count; i++)
            {
                if (i != 0)
                {
                    stringBuilder.Append(", ");
                }

                var name = names[i];
                var value = eventArgs.Payload[i];
                stringBuilder.Append(name).Append(": ").Append(value);
            }

            stringBuilder.Append("}");

            return stringBuilder.ToString();
        }

        public static string DumpPayloadOrMessage(this EventWrittenEventArgs eventArgs)
        {
            var msg = eventArgs.Message;

            return string.IsNullOrWhiteSpace(msg) ? eventArgs.DumpPayload() : DumpFormattedMessage(eventArgs);
        }

        public static ConsoleColor? GetColorMap(this EventWrittenEventArgs eventArgs, bool isBackgroundWhite)
        {
            switch (eventArgs.Level)
            {
                case EventLevel.Critical:
                    return ConsoleColor.Magenta;
                case EventLevel.Error:
                    return ConsoleColor.Red;
                case EventLevel.Informational:
                    return ConsoleColor.Green;
                case EventLevel.Verbose:
                    return ConsoleColor.Gray;
                case EventLevel.Warning:
                    return isBackgroundWhite ? ConsoleColor.DarkRed : ConsoleColor.Yellow;
                case EventLevel.LogAlways:
                    return isBackgroundWhite ? ConsoleColor.Black : ConsoleColor.White;
                default:
                    return null;
            }
        }

        public static string ToJson(this EventWrittenEventArgs eventArgs)
        {
            var names = eventArgs.PayloadNames;
            var count = names.Count;

            using (var stringWriter = new StringWriter())
            using (var jsonWriter = new TinyJsonWriter(stringWriter))
            {
                jsonWriter.WriteStartObject();

                for (var i = 0; i < count; i++)
                {
                    var name = names[i];
                    var value = eventArgs.Payload[i];

                    jsonWriter.WritePropertyName(name);
                    jsonWriter.WriteValue(value);
                }

                jsonWriter.WriteEndObject();
                stringWriter.Flush();

                return stringWriter.ToString();
            }
        }
    }

    internal class EventSchemaPortion
    {
        internal EventSchemaPortion(ReadOnlyCollection<string> payload, string keywordDescription, string taskName)
        {
            Payload = payload;
            KeywordDesciption = keywordDescription;
            TaskName = taskName;
        }

        internal ReadOnlyCollection<string> Payload { get; }

        internal string KeywordDesciption { get; }

        internal string TaskName { get; }
    }
}
