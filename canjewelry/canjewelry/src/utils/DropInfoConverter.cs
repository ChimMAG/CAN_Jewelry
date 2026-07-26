using System;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;

namespace canjewelry.src.utils
{
    // The gem drop table is ~100 entries that all say the same thing: one item, one chance,
    // rolled against the canjewelrygemsdroprate stat. Written out as objects that was 900+
    // lines, so an entry that matches those defaults is stored as the single string
    // "code chance". Everything else keeps the full object form and still round-trips.
    //
    // Compact form defaults: Type = Item, var = 0, LastDrop = true, attributes = "",
    // DropModbyStat = "canjewelrygemsdroprate".
    public class DropInfoConverter : JsonConverter<Config.DropInfo>
    {
        private const char COMPACT_SEPARATOR = ' ';
        public const string DEFAULT_DROP_MOD_STAT = "canjewelrygemsdroprate";

        public override void WriteJson(JsonWriter writer, Config.DropInfo value, JsonSerializer serializer)
        {
            if (value == null) { writer.WriteNull(); return; }

            if (TryWriteCompact(writer, value)) return;

            var o = new JObject
            {
                ["TypeCollectable"] = (int)value.TypeCollectable,
                ["NameCollectable"] = value.NameCollectable,
                ["avg"] = value.avg,
                ["var"] = value.var,
                ["LastDrop"] = value.LastDrop,
                ["DropModbyStat"] = value.DropModbyStat,
                ["attributes"] = value.attributes,
            };
            o.WriteTo(writer);
        }

        private static bool TryWriteCompact(JsonWriter writer, Config.DropInfo value)
        {
            if (string.IsNullOrEmpty(value.NameCollectable)) return false;
            if (value.TypeCollectable != EnumItemClass.Item) return false;
            if (value.var != 0f) return false;
            if (!value.LastDrop) return false;
            if (!string.IsNullOrEmpty(value.attributes)) return false;
            if (value.DropModbyStat != DEFAULT_DROP_MOD_STAT) return false;
            // A code with whitespace could not be split back apart.
            if (value.NameCollectable.IndexOf(COMPACT_SEPARATOR) >= 0) return false;

            writer.WriteValue(value.NameCollectable + COMPACT_SEPARATOR + value.avg.ToString("R", CultureInfo.InvariantCulture));
            return true;
        }

        public override Config.DropInfo ReadJson(JsonReader reader, Type objectType, Config.DropInfo existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;
            if (reader.TokenType == JsonToken.String) return ReadCompact((string)reader.Value);

            var o = JObject.Load(reader);

            EnumItemClass type = EnumItemClass.Item;
            JToken typeTok = CI(o, "TypeCollectable");
            if (typeTok != null && typeTok.Type != JTokenType.Null)
            {
                type = typeTok.Type == JTokenType.Integer
                    ? (EnumItemClass)(int)typeTok
                    : Enum.Parse<EnumItemClass>((string)typeTok, ignoreCase: true);
            }

            return new Config.DropInfo(
                type,
                (string)CI(o, "NameCollectable"),
                ReadFloat(CI(o, "avg")),
                ReadFloat(CI(o, "var")),
                CI(o, "LastDrop")?.Value<bool>() ?? true,
                (string)CI(o, "DropModbyStat") ?? "",
                (string)CI(o, "attributes") ?? "");
        }

        /// <summary>Parses the "code chance" short form, e.g. "canjewelry:gem-rough-chipped-malachite 0.005".</summary>
        private static Config.DropInfo ReadCompact(string raw)
        {
            string text = raw?.Trim();
            if (string.IsNullOrEmpty(text)) throw new JsonException("[canjewelry] empty gem drop entry");

            int split = text.LastIndexOf(COMPACT_SEPARATOR);
            if (split <= 0 || split == text.Length - 1)
            {
                throw new JsonException(string.Format(
                    "[canjewelry] gem drop \"{0}\" is not in the \"code chance\" form, e.g. \"canjewelry:gem-rough-chipped-malachite 0.005\"", raw));
            }

            string code = text.Substring(0, split).TrimEnd();
            string chanceText = text.Substring(split + 1).TrimStart();
            if (!float.TryParse(chanceText, NumberStyles.Float, CultureInfo.InvariantCulture, out float chance))
            {
                throw new JsonException(string.Format(
                    "[canjewelry] gem drop \"{0}\" has an unparseable chance \"{1}\"", raw, chanceText));
            }

            return new Config.DropInfo(EnumItemClass.Item, code, chance, 0f, true, DEFAULT_DROP_MOD_STAT);
        }

        private static float ReadFloat(JToken token) =>
            token == null || token.Type == JTokenType.Null ? 0f : (float)token;

        private static JToken CI(JObject o, string key) =>
            o.GetValue(key, StringComparison.OrdinalIgnoreCase);
    }
}
