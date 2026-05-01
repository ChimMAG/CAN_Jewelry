using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace canjewelry.src.items
{
    public class CANItemHorusEye : CANItemWearable
    {
        protected override string MeshrefsCacheName => "canhoruseyemeshrefs";

        public string Construction => this.Variant["construction"];

        private Dictionary<string, Dictionary<string, int>> durabilityGains;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            this.durabilityGains = this.Attributes["durabilityGains"].AsObject<Dictionary<string, Dictionary<string, int>>>(null);
        }

        protected override void AddAllTypesToCreativeInventory()
        {
            var vg = this.Attributes["variantGroups"].AsObject<Dictionary<string, string[]>>(null);
            if (vg == null) return;
            string[] loops = vg["metal"];
            string[] sockets = vg["silk"];

            var stacks = new List<JsonItemStack>();
            foreach (string loop in loops)
                foreach (string socket in sockets)
                    stacks.Add(GenJstack(string.Format("{{ metal: \"{0}\", silk: \"{1}\", gem: \"none\" }}", loop, socket)));

            this.CreativeInventoryStacks = new[]
            {
                new CreativeTabAndStackList { Stacks = stacks.ToArray(), Tabs = new[] { "general", "items", "canjewelry" } }
            };
        }

        protected override void FillTextureDict(Dictionary<string, AssetLocation> dict, ItemStack itemStack)
        {
            string loop = itemStack.Attributes.GetString("metal", "steel");
            string socket = itemStack.Attributes.GetString("silk", "red");
            string gem = itemStack.Attributes.GetString("gem", "not");

            dict["metal"] = new AssetLocation("block/metal/sheet/" + loop + "1.png");
            dict["silk"] = new AssetLocation("block/cloth/basic/" + socket + ".png");
            var notvisGem = new AssetLocation("canjewelry:item/gem/notvis.png");
            dict["gem"] = gem != null && canjewelry.gems_textures.TryGetValue(gem, out string assetPath)
                ? canjewelry.capi.Assets.TryGet(assetPath + ".png")?.Location ?? notvisGem
                : notvisGem;
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

        public override string GetCategoryCode(ItemStack stack) => "horuseye";

        public override string GetMeshCacheKey(ItemStack itemstack)
        {
            string loop = itemstack.Attributes.GetString("metal", null);
            string socket = itemstack.Attributes.GetString("silk", null);
            string gem = itemstack.Attributes.GetString("gem", null);
            return this.Code.ToShortString() + "-" + loop + "-" + socket + "-" + gem;
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            string loop = inSlot.Itemstack.Attributes.GetString("metal", null);
            string socket = inSlot.Itemstack.Attributes.GetString("silk", null);
            string gem = inSlot.Itemstack.Attributes.GetString("gem", null);

            if (gem != "none")
                dsc.AppendLine(Lang.Get("canjewelry:horuseyenecklace-parts-with-gem-held-info", Lang.Get("material-" + loop), Lang.Get("material-" + socket), gem));
            else
                dsc.AppendLine(Lang.Get("canjewelry:horuseyenecklace-parts-without-gem-held-info", Lang.Get("material-" + loop), Lang.Get("material-" + socket)));
        }
    }
}
