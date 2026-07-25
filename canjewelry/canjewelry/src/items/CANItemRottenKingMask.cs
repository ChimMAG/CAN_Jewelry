using System.Collections;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace canjewelry.src.items
{
    public class CANItemRottenKingMask : CANItemWearable
    {
        protected override string MeshrefsCacheName => "canrottenkingmaskmeshrefs";

        // Render only the mask's own shape (like CANItemNoseRing) instead of stepparenting
        // it onto the seraph entity shape. The seraph body is normally hidden via a transparent
        // texture, but its shoulder/torso still leaks into the GUI/inventory mesh; tesselating
        // the item shape alone avoids pulling in the entity body at all.
        public override MeshData GenMesh(ItemStack itemstack, ITextureAtlasAPI targetAtlas, BlockPos forBlockPos = null)
        {
            curAtlas = targetAtlas;

            tmpTextures.Clear();
            FillTextureDict(tmpTextures, itemstack);

            capi.Tesselator.TesselateItem(this, out MeshData mesh, this);
            return mesh;
        }

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
