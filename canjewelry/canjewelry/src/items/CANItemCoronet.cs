using System.Collections.Generic;
using canjewelry.src.CB;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace canjewelry.src.items
{
    public class CANItemCoronet : CANItemWearable
    {
        protected override string MeshrefsCacheName => "cancoronetmeshrefs";

        protected override void AddAllTypesToCreativeInventory()
        {
            // Coronet historically does not register creative variants
        }

        protected override void FillTextureDict(Dictionary<string, AssetLocation> dict, ItemStack itemStack)
        {
            int maxSocketNumber = EncrustableCB.GetMaxAmountSockets(itemStack);
            FillGemPositions4(dict, itemStack, maxSocketNumber);
            dict["metal"] = itemStack.Item.Textures["metal"].Base;
            dict["gems"] = new AssetLocation("canjewelry:item/gem/notvis.png");
        }

        public override string GetCategoryCode(ItemStack stack) => "cancoronet";

        public override string GetMeshCacheKey(ItemStack itemstack)
        {
            string metal = itemstack.Item.Variant["loop"];
            return this.Code.ToShortString() + "-" + metal;
        }

        public override string GetHeldItemName(ItemStack itemStack)
        {
            string variant = itemStack.Item.Variant.Get("loop");
            return Lang.Get("game:material-" + variant) + Lang.Get("canjewelry:item-coronet");
        }

        internal static void FillGemPositions4(Dictionary<string, AssetLocation> dict, ItemStack itemStack, int possibleGemsNumber)
        {
            var notvis = new AssetLocation("canjewelry:item/gem/notvis.png");
            void Set(string key, AssetLocation v) => dict[key] = v;

            ITreeAttribute tree = itemStack?.Attributes.HasAttribute(CANJWConstants.ITEM_ENCRUSTED_STRING) == true
                ? itemStack.Attributes.GetTreeAttribute(CANJWConstants.ITEM_ENCRUSTED_STRING)
                : null;

            AssetLocation Resolve(int slotIndex)
            {
                if (tree == null || !tree.HasAttribute("slot" + slotIndex)) return notvis;
                ITreeAttribute treeSocket = tree.GetTreeAttribute("slot" + slotIndex);
                string gemType = treeSocket.GetString("gemtype");
                canjewelry.gems_textures.TryGetValue(gemType, out string assetPath);
                return assetPath != null ? canjewelry.capi.Assets.TryGet(assetPath + ".png").Location : notvis;
            }

            if (tree == null || possibleGemsNumber <= 0)
            {
                for (int i = 1; i < 5; i++) Set("gems_" + i, notvis);
                return;
            }

            if (possibleGemsNumber >= 4)
            {
                for (int i = 0; i < possibleGemsNumber; i++) Set("gems_" + (i + 1), Resolve(i));
            }
            else if (possibleGemsNumber == 3)
            {
                AssetLocation a0 = Resolve(0);
                Set("gems_1", a0); Set("gems_4", a0);
                Set("gems_2", Resolve(1));
                Set("gems_3", Resolve(2));
            }
            else if (possibleGemsNumber == 2)
            {
                AssetLocation a0 = Resolve(0);
                Set("gems_1", a0); Set("gems_4", a0);
                AssetLocation a1 = Resolve(1);
                Set("gems_2", a1); Set("gems_3", a1);
            }
            else // 1
            {
                AssetLocation a0 = Resolve(0);
                for (int i = 1; i < 5; i++) Set("gems_" + i, a0);
            }
        }
    }
}
