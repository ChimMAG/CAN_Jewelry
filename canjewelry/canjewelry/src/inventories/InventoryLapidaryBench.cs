using System;
using canjewelry.src.items;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace canjewelry.src.inventories
{
    // 3-slot inventory backing the Lapidary Bench.
    // Slot layout:
    //   0 — assembly: gem-on-dop work-in-progress (all per-gem state lives in
    //       its itemstack attrs, so the assembly is portable between sessions)
    //   1 — lap: any grit; the bench checks grit-vs-progress at action time
    //   2 — output: finished cut gem (or completed pavilion-side assembly)
    public class InventoryLapidaryBench : InventoryBase, ISlotProvider
    {
        public const int SLOT_ASSEMBLY = 0;
        public const int SLOT_LAP = 1;
        public const int SLOT_OUTPUT = 2;
        public const int SLOT_COUNT = 3;

        private ItemSlot[] slots;

        public ItemSlot[] Slots => slots;

        public InventoryLapidaryBench(string inventoryID, ICoreAPI api)
            : base(inventoryID, api)
        {
            slots = GenEmptySlots(SLOT_COUNT);
        }

        public InventoryLapidaryBench(string className, string instanceID, ICoreAPI api)
            : base(className, instanceID, api)
        {
            slots = GenEmptySlots(SLOT_COUNT);
        }

        public override int Count => SLOT_COUNT;

        public override ItemSlot this[int slotId]
        {
            get => slotId < 0 || slotId >= Count ? null : slots[slotId];
            set
            {
                if (slotId < 0 || slotId >= Count)
                    throw new ArgumentOutOfRangeException(nameof(slotId));
                slots[slotId] = value ?? throw new ArgumentNullException(nameof(value));
            }
        }

        public override void FromTreeAttributes(ITreeAttribute tree)
            => slots = SlotsFromTreeAttributes(tree, slots);

        public override void ToTreeAttributes(ITreeAttribute tree)
            => SlotsToTreeAttributes(slots, tree);

        protected override ItemSlot NewSlot(int i) => new ItemSlotSurvival(this);

        public override bool CanContain(ItemSlot sinkSlot, ItemSlot sourceSlot)
        {
            if (sourceSlot.Itemstack == null) return false;
            var item = sourceSlot.Itemstack.Item;
            if (item == null) return false;

            int slotIndex = Array.IndexOf(slots, sinkSlot);
            switch (slotIndex)
            {
                case SLOT_ASSEMBLY:
                    return item is CANItemGemOnDop;
                case SLOT_LAP:
                    return item is CANItemFacetingLap;
                case SLOT_OUTPUT:
                    return false; // output slot only filled by bench logic
                default:
                    return base.CanContain(sinkSlot, sourceSlot);
            }
        }

        public override float GetSuitability(ItemSlot sourceSlot, ItemSlot targetSlot, bool isMerge)
            => CanContain(targetSlot, sourceSlot) ? 4f : base.GetSuitability(sourceSlot, targetSlot, isMerge);

        public override ItemSlot GetAutoPushIntoSlot(BlockFacing atBlockFace, ItemSlot fromSlot)
        {
            for (int i = 0; i < SLOT_COUNT; i++)
            {
                if (i == SLOT_OUTPUT) continue;
                if (CanContain(slots[i], fromSlot) && slots[i].Empty) return slots[i];
            }
            return null;
        }

        public override ItemSlot GetAutoPullFromSlot(BlockFacing atBlockFace)
            => slots[SLOT_OUTPUT].Empty ? null : slots[SLOT_OUTPUT];
    }
}
