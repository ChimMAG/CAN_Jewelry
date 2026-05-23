using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Util;

namespace canjewelry.src.items
{
    public class CANItemRottenKingMask : CANItemWearable
    {
        protected override string MeshrefsCacheName => "canrottenkingmaskmeshrefs";

        protected override void FillTextureDict(Dictionary<string, AssetLocation> dict, ItemStack itemStack)
        {
            string metal = itemStack.Attributes.GetString("metal", "steel");
            dict["silver1"] = new AssetLocation("block/metal/sheet/" + metal + "1.png");
            dict["canjewelry:canrottenkingmask-normal-silver1"] = new AssetLocation("block/metal/sheet/" + metal + "1.png");
            dict["rotten-king-mask"] = new AssetLocation("canjewelry:item/rottenking.png");
            dict["rotten-king-cloth"] = new AssetLocation("canjewelry:item/rottenkingcloth.png");
        }

        public override string GetCategoryCode(ItemStack stack) => "canrottenkingmask";

        public override string[] GetDisableElements(ItemStack stack)
            => new[] { "Hair tile upper part", "ponytailhigh", "ponytaillow", "Hair", "hideme", "sidehigh", "sidelow", "bangs" };

        public override string GetMeshCacheKey(ItemStack itemstack)
        {
            string metal = itemstack.Item.Variant.Get("loop", "steel");
            return this.Code.ToShortString() + "-" + metal;
        }

    }
}
