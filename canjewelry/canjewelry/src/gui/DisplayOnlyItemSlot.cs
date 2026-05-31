using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace canjewelry.src.gui
{
    public class DisplayOnlyItemSlot : ItemSlotSurvival
    {
        public DisplayOnlyItemSlot(InventoryBase inv) : base(inv) { }
        public override bool CanTake() => false;
        public override bool CanHold(ItemSlot sourceSlot) => false;
    }
}
