using System.Collections.Generic;
using System.Text;
using canjewelry.src.CB;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace canjewelry.src.items
{
    public class CANItemRing : CANItemWearable
    {
        protected override string MeshrefsCacheName => "canringsmeshrefs";

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
                dict[prefix + "gem_1"] = new AssetLocation("canjewelry:item/gem/notvis.png");
                dict[prefix + "gem_2"] = new AssetLocation("canjewelry:item/gem/notvis.png");
                dict[prefix + "gem_3"] = new AssetLocation("canjewelry:item/gem/notvis.png");
            }

            dict[prefix + "brass"] = new AssetLocation("block/metal/ingot/" + itemStack.Attributes.GetString("metal", "steel") + ".png");
        }

        public override string GetCategoryCode(ItemStack stack) => "canring";

        public override string GetMeshCacheKey(ItemStack itemstack)
        {
            string metal = itemstack.Attributes.GetString("metal", "steel");
            string armSide = itemstack.Collectible.Variant["side"];
            return this.Code.ToShortString() + "-" + metal + "-" + armSide;
        }

        public override string GetHeldItemName(ItemStack itemStack)
        {
            return Lang.Get("game:material-" + itemStack.Attributes.GetString("metal", "default")) + Lang.Get("canjewelry:item-canring");
        }
    }
}
