using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace canjewelry.Tests.Stubs
{
    /// <summary>
    /// Minimal InventoryBase stub. Used only so TryAddSocket has something to set TakeLocked on.
    /// </summary>
    public class TestInventory : InventoryBase
    {
        public TestInventory() : base("test", "test", null) { }

        public override ItemSlot this[int slotId] { get => null; set { } }
        public override int Count => 0;
        public override void FromTreeAttributes(ITreeAttribute tree) { }
        public override void ToTreeAttributes(ITreeAttribute tree) { }
    }
}
