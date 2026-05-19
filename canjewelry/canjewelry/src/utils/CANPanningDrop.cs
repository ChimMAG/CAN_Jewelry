using Newtonsoft.Json;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace canjewelry.src.utils
{
    [JsonConverter(typeof(CANPanningDropConverter))]
    public class CANPanningDrop: JsonItemStack
    {
        public NatFloat Chance;

        public string DropModbyStat;

        // Optional gate — if set, core fires CanPanDropEvent before considering this entry,
        // and skips it unless a subscriber (companion mod) responds with Allowed=true. Empty
        // / null means no gating, entry behaves as before.
        public string requiresPerk;
    }
}
