using System.Collections.Generic;
using System.Text;
using canjewelry.src.CB;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace canjewelry.src.items
{
    public class CANItemEarrings : CANItemWearable
    {
        protected override string MeshrefsCacheName => "canearringsmeshrefs";

        protected override void FillTextureDict(Dictionary<string, AssetLocation> dict, ItemStack itemStack)
        {
            int maxSocketNumber = EncrustableCB.GetMaxAmountSockets(itemStack);
            string side = itemStack.Collectible.Variant["side"];
            string prefix = side == "left" ? "left_" : "";

            if (itemStack != null && itemStack.Attributes.HasAttribute(CANJWConstants.ITEM_ENCRUSTED_STRING))
            {
                var tree = itemStack.Attributes.GetTreeAttribute(CANJWConstants.ITEM_ENCRUSTED_STRING);
                for (int i = 0; i < maxSocketNumber; i++)
                {
                    string key = prefix + "gem_" + (i + 1);
                    if (tree.HasAttribute("slot" + i))
                    {
                        ITreeAttribute treeSocket = tree.GetTreeAttribute("slot" + i);
                        string gemType = treeSocket.GetString("gemtype");
                        canjewelry.gems_textures.TryGetValue(gemType, out string assetPath);
                        dict[key] = assetPath != null
                            ? canjewelry.capi.Assets.TryGet(assetPath + ".png")?.Location ?? NotVisTexture
                            : NotVisTexture;
                    }
                    else
                    {
                        dict[key] = NotVisTexture;
                    }
                }
            }
            else
            {
                dict[prefix + "gem_1"] = new AssetLocation("canjewelry:item/gem/emerald.png");
                dict[prefix + "gem_2"] = new AssetLocation("canjewelry:item/gem/sapphire.png");
                dict[prefix + "gem_3"] = new AssetLocation("canjewelry:item/gem/citrine.png");
            }

            dict[prefix + "metalearrings"] = new AssetLocation("block/metal/ingot/" + itemStack.Attributes.GetString("metal", "steel") + ".png");
            dict[prefix + "gems"] = NotVisTexture;
            dict[prefix + "canearingsbase"] = new AssetLocation("game:block/leather/plain");
            dict[prefix + "plain"] = new AssetLocation("game:block/leather/plain");
        }

        public override string GetCategoryCode(ItemStack stack) => "canearrings";

        public override string GetMeshCacheKey(ItemStack itemstack)
        {
            string metal = itemstack.Attributes.GetString("metal", "steel");
            string side = itemstack.Attributes.GetString("clothescategory", "LeftEarrings");
            return this.Code.ToShortString() + "-" + metal + "-" + side;
        }

        public override string GetHeldItemName(ItemStack itemStack)
        {
            return Lang.Get("game:material-" + itemStack.Attributes.GetString("metal", "default")) + Lang.Get("canjewelry:item-" + itemStack.Collectible.Code.Path);
        }
    }
}
