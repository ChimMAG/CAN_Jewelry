using System.Collections.Generic;
using System.Text;
using canjewelry.src.CB;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace canjewelry.src.items
{
    public class CANItemTiara : CANItemWearable
    {
        protected override string MeshrefsCacheName => "cantiarameshrefs";

        protected override void AddAllTypesToCreativeInventory()
        {
            var vg = this.Attributes["variantGroups"].AsObject<Dictionary<string, string[]>>(null);
            if (vg == null) return;
            var stacks = new List<JsonItemStack>();
            foreach (string carcassus in vg["carcassus"])
                stacks.Add(GenJstack(string.Format("{{ carcassus: \"{0}\", gem_1: \"none\", gem_2: \"none\", gem_3: \"none\" }}", carcassus)));

            this.CreativeInventoryStacks = new[]
            {
                new CreativeTabAndStackList { Stacks = stacks.ToArray(), Tabs = new[] { "general", "items", "canjewelry" } }
            };
        }

        protected override void FillTextureDict(Dictionary<string, AssetLocation> dict, ItemStack stack)
        {
            string carcassus = stack.Attributes.GetString("carcassus", "steel");
            dict["carcassus"] = new AssetLocation("block/metal/sheet/" + carcassus + "1.png");

            int maxSocketNumber = EncrustableCB.GetMaxAmountSockets(stack);
            AssetLocation Resolve(string gemAttr)
            {
                string gem = stack.Attributes.GetString(gemAttr, null);
                var notvisGem = new AssetLocation("canjewelry:item/gem/notvis.png");
                return gem != null && canjewelry.gems_textures.TryGetValue(gem, out string assetPath)
                    ? canjewelry.capi.Assets.TryGet(assetPath + ".png")?.Location ?? notvisGem
                    : notvisGem;
            }

            if (maxSocketNumber == 1)
            {
                AssetLocation path = Resolve("gem_1");
                for (int i = 1; i < 4; i++) dict[i + "_gem"] = path;
            }
            else if (maxSocketNumber == 2)
            {
                AssetLocation path = Resolve("gem_1");
                dict["1_gem"] = path;
                dict["3_gem"] = path;
                dict["2_gem"] = Resolve("gem_2");
            }
            else
            {
                for (int i = 1; i < 4; i++) dict[i + "_gem"] = Resolve("gem_" + i);
            }
        }

        public override Shape GetShape(ItemStack stack, Entity forEntity, string texturePrefixCode)
        {
            JsonObject attributes = stack.Collectible.Attributes;
            CompositeShape compositeShape = attributes["attachShape"].Exists
                ? attributes["attachShape"].AsObject<CompositeShape>(null, stack.Collectible.Code.Domain)
                : ((stack.Class == EnumItemClass.Item) ? stack.Item.Shape : stack.Block.Shape);

            AssetLocation assetLocation = compositeShape.Base.CopyWithPath("shapes/" + compositeShape.Base.Path + ".json");
            return Vintagestory.API.Common.Shape.TryGet(this.api, assetLocation);
        }

        public override string GetCategoryCode(ItemStack stack) => "cantiara";

        public override string GetMeshCacheKey(ItemStack itemstack)
        {
            string carcassus = itemstack.Attributes.GetString("carcassus", null);
            string g1 = itemstack.Attributes.GetString("gem_1", null);
            string g2 = itemstack.Attributes.GetString("gem_2", null);
            string g3 = itemstack.Attributes.GetString("gem_3", null);
            return this.Code.ToShortString() + "-" + carcassus + "-" + g1 + "-" + g2 + "-" + g3;
        }

        public override string GetHeldItemName(ItemStack itemStack)
        {
            string variant = itemStack.Attributes.GetString("carcassus", "steel");
            return Lang.Get("game:material-" + variant) + Lang.Get("canjewelry:item-tiara");
        }
    }
}
