using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Newtonsoft.Json;

namespace canjewelry.src.utils
{
    // Same idea as CompactIntArrayConverter, for the buff value ranges: a two element range
    // costs four lines as a JSON array and one as "0.01,0.03". Both forms are read, so older
    // hand edited configs keep working.
    public class CompactFloatArrayConverter : JsonConverter<float[]>
    {
        private const char SEPARATOR = ',';

        public override void WriteJson(JsonWriter writer, float[] value, JsonSerializer serializer)
        {
            if (value == null) { writer.WriteNull(); return; }

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < value.Length; i++)
            {
                if (i > 0) sb.Append(SEPARATOR);
                // "R" keeps the value exact through the round trip.
                sb.Append(value[i].ToString("R", CultureInfo.InvariantCulture));
            }
            writer.WriteValue(sb.ToString());
        }

        public override float[] ReadJson(JsonReader reader, Type objectType, float[] existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            switch (reader.TokenType)
            {
                case JsonToken.Null:
                    return null;

                case JsonToken.String:
                    return ParseCompact((string)reader.Value);

                // A bare number means a fixed value rather than a range.
                case JsonToken.Float:
                case JsonToken.Integer:
                    return new[] { Convert.ToSingle(reader.Value, CultureInfo.InvariantCulture) };

                case JsonToken.StartArray:
                    List<float> values = new List<float>();
                    while (reader.Read() && reader.TokenType != JsonToken.EndArray)
                    {
                        values.Add(Convert.ToSingle(reader.Value, CultureInfo.InvariantCulture));
                    }
                    return values.ToArray();

                default:
                    throw new JsonException(string.Format(
                        "[canjewelry] a value range must be \"0.01,0.03\" or [0.01, 0.03], got {0}", reader.TokenType));
            }
        }

        private static float[] ParseCompact(string raw)
        {
            string text = raw?.Trim();
            if (string.IsNullOrEmpty(text)) return new float[0];

            string[] parts = text.Split(SEPARATOR);
            float[] result = new float[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                if (!float.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out result[i]))
                {
                    throw new JsonException(string.Format(
                        "[canjewelry] value range \"{0}\" contains a non numeric part \"{1}\"", raw, parts[i]));
                }
            }
            return result;
        }
    }
}
