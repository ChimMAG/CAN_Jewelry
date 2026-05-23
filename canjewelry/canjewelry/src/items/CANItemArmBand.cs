using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace canjewelry.src.items
{
    public class CANItemArmBand : CANItemWearable
    {
        protected override string MeshrefsCacheName => "canarmbandmeshrefs";

        protected override short MeshDefaultRenderPass => (short)EnumChunkRenderPass.OpaqueNoCull;

        protected override void AddAllTypesToCreativeInventory()
        {
            var vg = this.Attributes["variantGroups"].AsObject<Dictionary<string, string[]>>(null);
            if (vg == null || !vg.TryGetValue("metal", out string[] metals)) return;

            var stacks = new List<JsonItemStack>();
            foreach (string metal in metals)
            {
                stacks.Add(GenJstack(string.Format("{{ loop: \"{0}\"}}", metal)));
            }
            this.CreativeInventoryStacks = new[]
            {
                new CreativeTabAndStackList
                {
                    Stacks = stacks.ToArray(),
                    Tabs = new[] { "general", "items", "canjewelry" }
                }
            };
        }

        protected override void FillTextureDict(Dictionary<string, AssetLocation> dict, ItemStack itemStack)
        {
            string loop = itemStack.Attributes.GetString("loop", "steel");
            dict["bracelets1"] = new AssetLocation("block/metal/ingot/" + loop + ".png");
            dict["gems"] = new AssetLocation("canjewelry:item/gem/notvis.png");
        }

        public override string GetCategoryCode(ItemStack stack) => "canarmband";

        public override string GetMeshCacheKey(ItemStack itemstack)
        {
            string loop = itemstack.Attributes.GetString("loop", null);
            return this.Code.ToShortString() + "-" + loop;
        }

    }
}
