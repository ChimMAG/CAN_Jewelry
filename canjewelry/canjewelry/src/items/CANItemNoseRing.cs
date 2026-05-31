using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace canjewelry.src.items
{
    public class CANItemNoseRing : CANItemWearable
    {
        protected override string MeshrefsCacheName => "cannoseringmeshrefs";

        protected override void FillTextureDict(Dictionary<string, AssetLocation> dict, ItemStack itemStack)
        {
            string metal = itemStack.Attributes.GetString("metal", "steel");
            dict["metalnosering"] = new AssetLocation("block/metal/sheet/" + metal + "1.png");
        }

        public override string GetCategoryCode(ItemStack stack) => "cannosering";

        public override string GetMeshCacheKey(ItemStack itemstack)
        {
            string metal = itemstack.Item.Variant.Get("loop", "steel");
            return this.Code.ToShortString() + "-" + metal;
        }

        public override MeshData GenMesh(ItemStack itemstack, ITextureAtlasAPI targetAtlas, BlockPos forBlockPos = null)
        {
            curAtlas = targetAtlas;
            string metal = itemstack.Attributes.GetString("metal", null);

            tmpTextures.Clear();
            tmpTextures["metalnosering"] = new AssetLocation("block/metal/sheet/" + metal + "1.png");

            var cnts = new ContainedTextureSource(api as ICoreClientAPI, curAtlas, new Dictionary<string, AssetLocation>(), string.Format("For render in shield {0}", this.Code));
            cnts.Textures["metalnosering"] = new AssetLocation("block/metal/sheet/" + metal + "1.png");

            this.capi.Tesselator.TesselateItem(this, out MeshData mesh, cnts);
            return mesh;
        }

    }
}
