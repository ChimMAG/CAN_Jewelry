using Vintagestory.API.Client;
using Vintagestory.GameContent;

namespace canjewelry.src.items.GemChiselMode
{
    public class OneByGemChiselMode: GemChiselMode
    {
        public override DrawSkillIconDelegate DrawAction(ICoreClientAPI capi) => ItemClay.Drawcreate1_svg;
    }
}
