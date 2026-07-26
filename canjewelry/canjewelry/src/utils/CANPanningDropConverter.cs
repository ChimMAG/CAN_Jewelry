using System;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace canjewelry.src.utils
{
    // CANPanningDrop inherits JsonItemStack, whose Attributes field is a JsonObject wrapping
    // a JToken. Newtonsoft can't iterate a JsonObject whose underlying token is a scalar
    // (string/number), so default serialization throws "Can iterate only over a JObject or
    // JArray". This converter projects to/from a flat DTO shape and stores Attributes as the
    // raw JToken so round-tripping via JsonConvert is safe.
    public class CANPanningDropConverter : JsonConverter<CANPanningDrop>
    {
        // A drop that only says "this item with this chance" is written as the single string
        // "code chance" instead of a nine-line object - the default table is ~136 such entries
        // and the long form made up a third of the whole config file. Anything that sets a
        // stack size, attributes, a stat modifier, a perk gate or a non trivial chance
        // distribution still round-trips through the full object form.
        private const char COMPACT_SEPARATOR = ' ';

        public override void WriteJson(JsonWriter writer, CANPanningDrop value, JsonSerializer serializer)
        {
            if (value == null) { writer.WriteNull(); return; }

            if (TryWriteCompact(writer, value)) return;

            var o = new JObject
            {
                ["code"] = value.Code?.ToString(),
                ["type"] = value.Type.ToString().ToLowerInvariant(),
                ["stackSize"] = value.StackSize,
                ["attributes"] = value.Attributes?.Token,
                ["chance"] = value.Chance == null ? null : JToken.FromObject(value.Chance, serializer),
                ["dropModbyStat"] = value.DropModbyStat,
                ["requiresPerk"] = value.requiresPerk,
            };
            o.WriteTo(writer);
        }

        // True when nothing but code and a flat chance is set, i.e. the entry survives the
        // round trip through the "code chance" string form without losing anything.
        private static bool TryWriteCompact(JsonWriter writer, CANPanningDrop value)
        {
            if (value.Code == null) return false;
            if (value.Type != EnumItemClass.Item) return false;
            if (value.StackSize != 1) return false;
            if (value.Attributes != null) return false;
            if (!string.IsNullOrEmpty(value.DropModbyStat)) return false;
            if (!string.IsNullOrEmpty(value.requiresPerk)) return false;

            NatFloat chance = value.Chance;
            if (chance == null || chance.var != 0f || chance.offset != 0f || chance.dist != EnumDistribution.UNIFORM) return false;

            // A code with whitespace in it could not be split back apart.
            string code = value.Code.ToString();
            if (code.IndexOf(COMPACT_SEPARATOR) >= 0) return false;

            writer.WriteValue(code + COMPACT_SEPARATOR + chance.avg.ToString(CultureInfo.InvariantCulture));
            return true;
        }

        public override CANPanningDrop ReadJson(JsonReader reader, Type objectType, CANPanningDrop existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;
            if (reader.TokenType == JsonToken.String) return ReadCompact((string)reader.Value);

            var o = JObject.Load(reader);

            var drop = new CANPanningDrop();

            var codeStr = (string)CI(o, "code");
            if (codeStr != null) drop.Code = new AssetLocation(codeStr);

            var typeTok = CI(o, "type");
            if (typeTok != null && typeTok.Type != JTokenType.Null)
                drop.Type = typeTok.Type == JTokenType.Integer
                    ? (EnumItemClass)(int)typeTok
                    : Enum.Parse<EnumItemClass>((string)typeTok, ignoreCase: true);

            var ss = CI(o, "stackSize");
            if (ss != null && ss.Type != JTokenType.Null)
                drop.StackSize = (int)ss;

            var attrTok = CI(o, "attributes");
            if (attrTok != null && attrTok.Type != JTokenType.Null)
                drop.Attributes = new JsonObject(attrTok);

            var chanceTok = CI(o, "chance");
            if (chanceTok != null && chanceTok.Type != JTokenType.Null)
                drop.Chance = chanceTok.ToObject<NatFloat>(serializer);

            drop.DropModbyStat = (string)CI(o, "dropModbyStat");
            drop.requiresPerk = (string)CI(o, "requiresPerk");

            return drop;
        }

        /// <summary>Parses the "code chance" short form, e.g. "canjewelry:gem-rough-normal-diamond 0.2".</summary>
        private static CANPanningDrop ReadCompact(string raw)
        {
            string text = raw?.Trim();
            if (string.IsNullOrEmpty(text)) throw new JsonException("[canjewelry] empty panning drop entry");

            int split = text.LastIndexOf(COMPACT_SEPARATOR);
            if (split <= 0 || split == text.Length - 1)
            {
                throw new JsonException(string.Format(
                    "[canjewelry] panning drop \"{0}\" is not in the \"code chance\" form, e.g. \"canjewelry:gem-rough-normal-diamond 0.2\"", raw));
            }

            string code = text.Substring(0, split).TrimEnd();
            string chanceText = text.Substring(split + 1).TrimStart();
            if (!float.TryParse(chanceText, NumberStyles.Float, CultureInfo.InvariantCulture, out float chance))
            {
                throw new JsonException(string.Format(
                    "[canjewelry] panning drop \"{0}\" has an unparseable chance \"{1}\"", raw, chanceText));
            }

            return new CANPanningDrop
            {
                Code = new AssetLocation(code),
                Type = EnumItemClass.Item,
                StackSize = 1,
                Chance = new NatFloat(chance, 0f, EnumDistribution.UNIFORM)
            };
        }

        private static JToken CI(JObject o, string key) =>
            o.GetValue(key, StringComparison.OrdinalIgnoreCase);
    }
}
