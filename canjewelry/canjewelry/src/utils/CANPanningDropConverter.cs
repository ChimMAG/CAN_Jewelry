using System;
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
        public override void WriteJson(JsonWriter writer, CANPanningDrop value, JsonSerializer serializer)
        {
            if (value == null) { writer.WriteNull(); return; }

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

        public override CANPanningDrop ReadJson(JsonReader reader, Type objectType, CANPanningDrop existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;
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

        private static JToken CI(JObject o, string key) =>
            o.GetValue(key, StringComparison.OrdinalIgnoreCase);
    }
}
