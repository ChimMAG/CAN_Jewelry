using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace canjewelry.src.items
{
    // Dop stick — single-use mounting fixture for a rough gem.
    // The cutType variant (round / baguette / pear) determines which faceting
    // recipe the gem will follow once mounted, so recipe selection happens
    // through item choice rather than a UI dropdown.
    public class CANItemDop : Item
    {
        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            string cutType = inSlot.Itemstack?.Collectible?.Variant?[CANJWConstants.DOP_CUT_TYPE];
            if (!string.IsNullOrEmpty(cutType))
            {
                dsc.AppendLine();
                dsc.AppendLine(Lang.Get("canjewelry:dop-cuttype-" + cutType));
            }
        }
    }
}
