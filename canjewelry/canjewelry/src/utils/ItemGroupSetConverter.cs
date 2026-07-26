using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace canjewelry.src.utils
{
    // buffNameToPossibleItem lists, for every gem, every item code substring it may be set into.
    // Most gems accept whole families (all armor, all melee weapons, all jewelry), so writing
    // them out per gem repeated the same ~90 entries thirty times over - a sixth of the config.
    //
    // On write, a set that fully contains one of Config.ActiveItemGroups is folded into "$armor".
    // On read the "$armor" entries are kept verbatim: expanding them here would mean reading
    // item_groups out of the file that is still being deserialized, so that is done afterwards
    // by Config.ExpandItemGroups, where the whole config is already in memory.
    public class ItemGroupSetConverter : JsonConverter<HashSet<string>>
    {
        private const char GROUP_PREFIX = '$';

        public override void WriteJson(JsonWriter writer, HashSet<string> value, JsonSerializer serializer)
        {
            if (value == null) { writer.WriteNull(); return; }

            // Every fully contained group is decided against the untouched set, so overlapping
            // groups cannot cannibalize each other and the output does not depend on order.
            List<string> matched = new List<string>();
            foreach (var group in Config.ActiveItemGroups)
            {
                if (group.Value != null && group.Value.Count > 0 && group.Value.IsSubsetOf(value)) matched.Add(group.Key);
            }
            matched.Sort(StringComparer.Ordinal);

            HashSet<string> covered = new HashSet<string>();
            foreach (string name in matched) covered.UnionWith(Config.ActiveItemGroups[name]);

            List<string> literals = value.Where(v => !covered.Contains(v)).ToList();
            literals.Sort(StringComparer.Ordinal);

            writer.WriteStartArray();
            foreach (string name in matched) writer.WriteValue(GROUP_PREFIX + name);
            foreach (string literal in literals) writer.WriteValue(literal);
            writer.WriteEndArray();
        }

        public override HashSet<string> ReadJson(JsonReader reader, Type objectType, HashSet<string> existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;

            HashSet<string> result = new HashSet<string>();

            if (reader.TokenType == JsonToken.String)
            {
                Add(result, (string)reader.Value);
                return result;
            }

            if (reader.TokenType != JsonToken.StartArray)
            {
                throw new JsonException(string.Format(
                    "[canjewelry] expected a list of item codes at {0}, got {1}", reader.Path, reader.TokenType));
            }

            while (reader.Read() && reader.TokenType != JsonToken.EndArray)
            {
                Add(result, reader.Value?.ToString());
            }
            return result;
        }

        private static void Add(HashSet<string> into, string entry)
        {
            if (string.IsNullOrWhiteSpace(entry)) return;
            into.Add(entry.Trim());
        }
    }
}
