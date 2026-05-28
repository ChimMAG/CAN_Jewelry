using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace canjewelry.src.items
{
    // Faceting lap — abrasive disc with one of three grits (coarse / fine / polish).
    // Each lap is consumed slowly through DamageItem calls from BELapidaryBench
    // during cut operations. The grit variant determines which phase(s) the lap
    // is valid for.
    public class CANItemFacetingLap : Item
    {
        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            string grit = inSlot.Itemstack?.Collectible?.Variant?[CANJWConstants.LAP_GRIT];
            if (!string.IsNullOrEmpty(grit))
            {
                dsc.AppendLine();
                dsc.AppendLine(Lang.Get("canjewelry:lap-grit-" + grit));
            }
        }
    }
}
