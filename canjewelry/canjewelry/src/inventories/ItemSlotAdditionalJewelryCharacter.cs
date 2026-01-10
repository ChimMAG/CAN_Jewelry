using System;
using Vintagestory.API.Common;

namespace canjewelry.src.inventories
{
    public class ItemSlotAdditionalJewelryCharacter: ItemSlot
    {
        public EnumCharacterAdditionalJewelryDressType Type;

        public override EnumItemStorageFlags StorageType => EnumItemStorageFlags.Outfit;

        public ItemSlotAdditionalJewelryCharacter(EnumCharacterAdditionalJewelryDressType type, InventoryBase inventory)
            : base(inventory)
        {
            Type = type;
        }

        public override bool CanTakeFrom(ItemSlot sourceSlot, EnumMergePriority priority = EnumMergePriority.AutoMerge)
        {
            if (!IsDressType(sourceSlot.Itemstack, Type))
            {
                return false;
            }

            return base.CanTakeFrom(sourceSlot, priority);
        }

        public override bool CanHold(ItemSlot itemstackFromSourceSlot)
        {
            if (!IsDressType(itemstackFromSourceSlot.Itemstack, Type))
            {
                return false;
            }

            return base.CanHold(itemstackFromSourceSlot);
        }

        //
        // Souhrn:
        //     Checks to see what dress type the given item is.
        //
        // Parametry:
        //   itemstack:
        //
        //   dressType:
        public static bool IsDressType(IItemStack itemstack, EnumCharacterAdditionalJewelryDressType dressType)
        {
            if (itemstack == null || itemstack.Collectible.Attributes == null)
            {
                return false;
            }
            string text;
            if (itemstack.Attributes.HasAttribute("clothescategory"))
            {
                text = itemstack.Attributes.GetString("clothescategory");
            }
            else
            {
                text = itemstack.Collectible.Attributes["clothescategory"].AsString() ?? itemstack.Collectible.Attributes["attachableToEntity"]["categoryCode"].AsString();
            }
            if (text != null)
            {
                return dressType.ToString().Equals(text, StringComparison.InvariantCultureIgnoreCase);
            }

            return false;
        }
    }
}
