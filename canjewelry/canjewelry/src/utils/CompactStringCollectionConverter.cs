using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace canjewelry.src.utils
{
    // Short lists of names - buff names a gem may roll, stats shown in the gui, block codes the
    // temporal grasp ignores - cost a line per entry as JSON arrays. Written as "a,b,c" they take
    // one line and stay just as readable. Both forms are accepted on read.
    internal static class CompactStringList
    {
        internal const char SEPARATOR = ',';

        internal static void Write(JsonWriter writer, IEnumerable<string> values)
        {
            writer.WriteValue(string.Join(SEPARATOR.ToString(), values));
        }

        // A name containing the separator could not be split back apart, so such a list keeps
        // the array form rather than being silently mangled.
        internal static bool CanCompact(IEnumerable<string> values)
        {
            return values.All(v => v == null || v.IndexOf(SEPARATOR) < 0);
        }

        internal static List<string> Read(JsonReader reader)
        {
            List<string> result = new List<string>();

            if (reader.TokenType == JsonToken.String)
            {
                foreach (string part in ((string)reader.Value).Split(SEPARATOR))
                {
                    string trimmed = part.Trim();
                    if (trimmed.Length > 0) result.Add(trimmed);
                }
                return result;
            }

            if (reader.TokenType != JsonToken.StartArray)
            {
                throw new JsonException(string.Format(
                    "[canjewelry] expected \"a,b,c\" or [\"a\", \"b\"] at {0}, got {1}", reader.Path, reader.TokenType));
            }

            while (reader.Read() && reader.TokenType != JsonToken.EndArray)
            {
                string value = reader.Value?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(value)) result.Add(value);
            }
            return result;
        }
    }

    public class CompactStringSetConverter : JsonConverter<HashSet<string>>
    {
        public override void WriteJson(JsonWriter writer, HashSet<string> value, JsonSerializer serializer)
        {
            if (value == null) { writer.WriteNull(); return; }

            if (!CompactStringList.CanCompact(value))
            {
                serializer.Serialize(writer, value.ToArray());
                return;
            }
            CompactStringList.Write(writer, value);
        }

        public override HashSet<string> ReadJson(JsonReader reader, Type objectType, HashSet<string> existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;
            return new HashSet<string>(CompactStringList.Read(reader));
        }
    }

    public class CompactStringArrayConverter : JsonConverter<string[]>
    {
        public override void WriteJson(JsonWriter writer, string[] value, JsonSerializer serializer)
        {
            if (value == null) { writer.WriteNull(); return; }

            if (!CompactStringList.CanCompact(value))
            {
                serializer.Serialize(writer, value);
                return;
            }
            CompactStringList.Write(writer, value);
        }

        public override string[] ReadJson(JsonReader reader, Type objectType, string[] existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;
            return CompactStringList.Read(reader).ToArray();
        }
    }
}
