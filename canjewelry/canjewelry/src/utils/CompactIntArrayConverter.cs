using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Newtonsoft.Json;

namespace canjewelry.src.utils
{
    // Socket tier arrays are short (1-4 entries) but the default JSON layout spends a line per
    // number, which is what made items_codes_with_socket_count_and_tiers over a thousand lines
    // long. This writes them as "3,3,3" on a single line and reads both that and the plain
    // array form, so hand edited configs from older versions keep loading.
    public class CompactIntArrayConverter : JsonConverter<int[]>
    {
        private const char SEPARATOR = ',';

        public override void WriteJson(JsonWriter writer, int[] value, JsonSerializer serializer)
        {
            if (value == null) { writer.WriteNull(); return; }

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < value.Length; i++)
            {
                if (i > 0) sb.Append(SEPARATOR);
                sb.Append(value[i].ToString(CultureInfo.InvariantCulture));
            }
            writer.WriteValue(sb.ToString());
        }

        public override int[] ReadJson(JsonReader reader, Type objectType, int[] existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            switch (reader.TokenType)
            {
                case JsonToken.Null:
                    return null;

                case JsonToken.String:
                    return ParseCompact((string)reader.Value);

                // A bare number is accepted as a one socket shorthand.
                case JsonToken.Integer:
                    return new[] { Convert.ToInt32(reader.Value, CultureInfo.InvariantCulture) };

                case JsonToken.StartArray:
                    List<int> values = new List<int>();
                    while (reader.Read() && reader.TokenType != JsonToken.EndArray)
                    {
                        values.Add(Convert.ToInt32(reader.Value, CultureInfo.InvariantCulture));
                    }
                    return values.ToArray();

                default:
                    throw new JsonException(string.Format(
                        "[canjewelry] socket tiers must be \"3,3,3\" or [3,3,3], got {0}", reader.TokenType));
            }
        }

        private static int[] ParseCompact(string raw)
        {
            string text = raw?.Trim();
            if (string.IsNullOrEmpty(text)) return new int[0];

            string[] parts = text.Split(SEPARATOR);
            int[] result = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                if (!int.TryParse(parts[i].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out result[i]))
                {
                    throw new JsonException(string.Format(
                        "[canjewelry] socket tiers \"{0}\" contain a non integer part \"{1}\"", raw, parts[i]));
                }
            }
            return result;
        }
    }
}
