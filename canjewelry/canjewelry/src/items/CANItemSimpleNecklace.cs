using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace canjewelry.src.items
{
    public class CANItemSimpleNecklace : CANItemWearable
    {
        protected override string MeshrefsCacheName => "canneckmeshrefs";

        public string Construction => this.Variant["construction"];

        protected override void AddAllTypesToCreativeInventory()
        {
            var vg = this.Attributes["variantGroups"].AsObject<Dictionary<string, string[]>>(null);
            if (vg == null) return;
            string[] loops = vg["loop"];
            string[] sockets = vg["socket"];

            var stacks = new List<JsonItemStack>();
            foreach (string loop in loops)
                foreach (string socket in sockets)
                    stacks.Add(GenJstack(string.Format("{{ loop: \"{0}\", socket: \"{1}\", gem: \"none\" }}", loop, socket)));

            this.CreativeInventoryStacks = new[]
            {
                new CreativeTabAndStackList { Stacks = stacks.ToArray(), Tabs = new[] { "general", "items", "canjewelry" } }
            };
        }

        protected override void FillTextureDict(Dictionary<string, AssetLocation> dict, ItemStack itemStack)
        {
            string loop = itemStack.Attributes.GetString("loop", "gold");
            string socket = itemStack.Attributes.GetString("socket", "gold");
            string gem = itemStack.Attributes.GetString("gem", null);

            dict["loop"] = new AssetLocation("block/metal/ingot/" + loop + ".png");
            dict["socket"] = new AssetLocation("block/metal/ingot/" + socket + ".png");
            dict["gem"] = gem != null && canjewelry.gems_textures.TryGetValue(gem, out string assetPath)
                ? canjewelry.capi.Assets.TryGet(assetPath + ".png")?.Location ?? new AssetLocation("canjewelry:item/gem/notvis.png")
                : new AssetLocation("canjewelry:item/gem/notvis.png");
        }

        public override void CollectTextures(ItemStack stack, Shape shape, string texturePrefixCode, Dictionary<string, CompositeTexture> intoDict)
        {
            if (this.api.Side is EnumAppSide.Server) return;

            string loop = stack.Attributes.GetString("loop", "gold");
            string socket = stack.Attributes.GetString("socket", "gold");
            string gem = stack.Attributes.GetString("gem", null);

            tmpTextures.Clear();
            tmpTextures["loop"] = new AssetLocation("block/metal/sheet/" + loop + "1.png");
            tmpTextures["socket"] = new AssetLocation("block/metal/plate/" + socket + ".png");
            tmpTextures["gem"] = gem != null && canjewelry.gems_textures.TryGetValue(gem, out string assetPath)
                ? canjewelry.capi.Assets.TryGet(assetPath + ".png")?.Location ?? new AssetLocation("canjewelry:item/gem/notvis.png")
                : new AssetLocation("canjewelry:item/gem/notvis.png");

            foreach (var texture in tmpTextures)
            {
                CompositeTexture ctex = new CompositeTexture { Base = texture.Value };
                AssetLocation armorTexLoc = texture.Value;
                int textureSubId;
                (this.api as ICoreClientAPI).EntityTextureAtlas.GetOrInsertTexture(armorTexLoc, out textureSubId, out _, () =>
                {
                    IAsset texAsset = this.capi.Assets.TryGet(armorTexLoc.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png"));
                    return texAsset?.ToBitmap(capi);
                });
                ctex.Baked = new BakedCompositeTexture { BakedName = armorTexLoc, TextureSubId = textureSubId };
                intoDict[texture.Key] = ctex;
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

        public override string GetCategoryCode(ItemStack stack) => "cansimplenecklace";

        public override string GetMeshCacheKey(ItemStack itemstack)
        {
            string loop = itemstack.Attributes.GetString("loop", null);
            string socket = itemstack.Attributes.GetString("socket", null);
            string gem = itemstack.Attributes.GetString("gem", null);
            return this.Code.ToShortString() + "-" + loop + "-" + socket + "-" + gem;
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            string loop = inSlot.Itemstack.Attributes.GetString("loop", null);
            string socket = inSlot.Itemstack.Attributes.GetString("socket", null);
            string gem = inSlot.Itemstack.Attributes.GetString("gem", null);

            if (gem != "none")
                dsc.AppendLine(Lang.Get("canjewelry:necklace-parts-with-gem-held-info", Lang.Get("material-" + loop), Lang.Get("material-" + socket), gem));
            else
                dsc.AppendLine(Lang.Get("canjewelry:necklace-parts-without-gem-held-info", Lang.Get("material-" + loop), Lang.Get("material-" + socket)));
        }
    }
}
