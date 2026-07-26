using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Newtonsoft.Json;

namespace canjewelry.src.utils
{
    // gems_buffs stores one value per gem tier: {"1": 0.02, "2": 0.04, "3": 0.08}. Since the
    // tiers are always a 1..N run, the keys carry no information and the whole thing fits in
    // "0.02,0.04,0.08", position telling the tier.
    //
    // A table whose keys are not a plain 1..N run keeps the object form, so nothing is lost if
    // someone numbers tiers differently. Both forms are read.
    public class TierValuesConverter : JsonConverter<Dictionary<string, float>>
    {
        private const char SEPARATOR = ',';

        public override void WriteJson(JsonWriter writer, Dictionary<string, float> value, JsonSerializer serializer)
        {
            if (value == null) { writer.WriteNull(); return; }

            if (!TryWriteCompact(writer, value))
            {
                writer.WriteStartObject();
                foreach (var tier in value)
                {
                    writer.WritePropertyName(tier.Key);
                    writer.WriteValue(tier.Value);
                }
                writer.WriteEndObject();
            }
        }

        private static bool TryWriteCompact(JsonWriter writer, Dictionary<string, float> value)
        {
            if (value.Count == 0) return false;

            StringBuilder sb = new StringBuilder();
            for (int tier = 1; tier <= value.Count; tier++)
            {
                if (!value.TryGetValue(tier.ToString(CultureInfo.InvariantCulture), out float tierValue)) return false;
                if (tier > 1) sb.Append(SEPARATOR);
                // "R" keeps the value exact through the round trip.
                sb.Append(tierValue.ToString("R", CultureInfo.InvariantCulture));
            }

            writer.WriteValue(sb.ToString());
            return true;
        }

        public override Dictionary<string, float> ReadJson(JsonReader reader, Type objectType, Dictionary<string, float> existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;

            var result = new Dictionary<string, float>();

            if (reader.TokenType == JsonToken.String)
            {
                string[] parts = ((string)reader.Value).Split(SEPARATOR);
                for (int i = 0; i < parts.Length; i++)
                {
                    string text = parts[i].Trim();
                    if (text.Length == 0) continue;
                    if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
                    {
                        throw new JsonException(string.Format(
                            "[canjewelry] tier values at {0} contain a non numeric part \"{1}\"", reader.Path, parts[i]));
                    }
                    result[(i + 1).ToString(CultureInfo.InvariantCulture)] = parsed;
                }
                return result;
            }

            if (reader.TokenType != JsonToken.StartObject)
            {
                throw new JsonException(string.Format(
                    "[canjewelry] expected \"0.02,0.04,0.08\" or a tier object at {0}, got {1}", reader.Path, reader.TokenType));
            }

            while (reader.Read() && reader.TokenType != JsonToken.EndObject)
            {
                string tier = (string)reader.Value;
                reader.Read();
                result[tier] = Convert.ToSingle(reader.Value, CultureInfo.InvariantCulture);
            }
            return result;
        }
    }
}
