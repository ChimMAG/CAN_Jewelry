using Vintagestory.API.Client;

namespace canjewelry.src.items.GemChiselMode
{
    public class VerticalLineGemChiselMode: GemChiselMode
    {
        public override DrawSkillIconDelegate DrawAction(ICoreClientAPI capi) => capi.Gui.Icons.Drawrepeat_svg;
    }
}
