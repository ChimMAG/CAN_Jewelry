using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace canjewelry.src.items.resource
{
    public class CANItemSocket: Item
    {
        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            if(!inSlot.Empty && inSlot.Itemstack.Collectible.Attributes.KeyExists(CANJWConstants.LEVEL_OF_SOSCKET_STRING))
            {
                int level = inSlot.Itemstack.Collectible.Attributes[CANJWConstants.LEVEL_OF_SOSCKET_STRING].AsInt(1);
                dsc.AppendLine(Lang.Get("canjewelry:itemdesc-cansocket-" + level.ToString()));
            }
        }
    }
}
