using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace canjewelry.src.inventories
{
    public class InventoryCharacterAdditionalJewelry: InventoryBasePlayer
    {
        public InventoryCharacterAdditionalJewelry(string className, string playerUID, ICoreAPI api)
            : base(className, playerUID, api)
        {
            this.slots = base.GenEmptySlots(18);
            this.baseWeight = 2.5f;
        }
        public InventoryCharacterAdditionalJewelry(string inventoryId, ICoreAPI api)
            : base(inventoryId, api)
        {
            this.slots = base.GenEmptySlots(18);
            this.baseWeight = 2.5f;
        }
        public override void OnItemSlotModified(ItemSlot slot)
        {
            base.OnItemSlotModified(slot);
        }
        public override int Count
        {
            get
            {
                return this.slots.Length;
            }
        }
        public override ItemSlot this[int slotId]
        {
            get
            {
                if (slotId < 0 || slotId >= this.Count)
                {
                    return null;
                }
                return this.slots[slotId];
            }
            set
            {
                if (slotId < 0 || slotId >= this.Count)
                {
                    throw new ArgumentOutOfRangeException("slotId");
                }
                if (value == null)
                {
                    throw new ArgumentNullException("value");
                }
                this.slots[slotId] = value;
            }
        }
        public override void FromTreeAttributes(ITreeAttribute tree)
        {
            this.slots = this.SlotsFromTreeAttributes(tree, null, null);
            ItemSlot[] prevSlots2 = this.slots;
            this.slots = base.GenEmptySlots(18);
            for (int j = 0; j < prevSlots2.Length; j++)
            {
                this.slots[j] = prevSlots2[j];
            }
            
        }
        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.SlotsToTreeAttributes(this.slots, tree);
            this.ResolveBlocksOrItems();
        }
        protected override ItemSlot NewSlot(int slotId)
        {
            ItemSlotAdditionalJewelryCharacter slot = new ItemSlotAdditionalJewelryCharacter((EnumCharacterAdditionalJewelryDressType)slotId, this);
            this.iconByDressType.TryGetValue((EnumCharacterAdditionalJewelryDressType)slotId, out slot.BackgroundIcon);
            return slot;
        }
        public override void DiscardAll()
        {
        }
        public override void OnOwningEntityDeath(Vec3d pos)
        {
        }
        public override WeightedSlot GetBestSuitedSlot(ItemSlot sourceSlot, ItemStackMoveOperation op, List<ItemSlot> skipSlots = null)
        {
            return new WeightedSlot();
        }
        private ItemSlot[] slots;
        private Dictionary<EnumCharacterAdditionalJewelryDressType, string> iconByDressType = new Dictionary<EnumCharacterAdditionalJewelryDressType, string>
        {
            {
                EnumCharacterAdditionalJewelryDressType.Nose,
                "canjewelry:nose"
            },
            {
                EnumCharacterAdditionalJewelryDressType.LeftEarrings,
                "canjewelry:earrings-left"
            },
            {
                EnumCharacterAdditionalJewelryDressType.RightEarrings,
                "canjewelry:earrings-right"
            },
            {
                EnumCharacterAdditionalJewelryDressType.RightEye,
                "canjewelry:eye-right"
            },
            {
                EnumCharacterAdditionalJewelryDressType.LeftEye,
                "canjewelry:eye-left"
            },
            {
                EnumCharacterAdditionalJewelryDressType.RightPalm,
                "canjewelry:palm-right"
            },
            {
                EnumCharacterAdditionalJewelryDressType.LeftPalm,
                "canjewelry:palm-left"
            }
        };
    }
}
