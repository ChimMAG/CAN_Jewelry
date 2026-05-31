using Vintagestory.API.Common;

namespace canjewelry.Tests.Stubs
{
    /// <summary>
    /// Minimal ItemSlot stub. Tracks whether MarkDirty and TakeOut were called.
    /// </summary>
    public class TestItemSlot : ItemSlot
    {
        public bool DirtyMarked { get; private set; }
        public bool ItemWasTaken { get; private set; }

        public TestItemSlot(InventoryBase inv, ItemStack? itemstack = null) : base(inv)
        {
            Itemstack = itemstack;
        }

        public override void MarkDirty()
        {
            DirtyMarked = true;
        }

        public override ItemStack TakeOut(int quantity)
        {
            var result = Itemstack;
            Itemstack = null;
            ItemWasTaken = true;
            return result;
        }
    }
}
