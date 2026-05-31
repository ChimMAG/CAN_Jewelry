using System.Collections.Generic;
using canjewelry.src.CB;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace canjewelry.src.items
{
    public class CANItemNadiyanNecklace : CANItemWearable
    {
        protected override string MeshrefsCacheName => "cannadiyanmeshrefs";

        public string Construction => this.Variant["construction"];

        protected override void AddAllTypesToCreativeInventory()
        {
            var vg = this.Attributes["variantGroups"].AsObject<Dictionary<string, string[]>>(null);
            if (vg == null) return;
            var stacks = new List<JsonItemStack>();

            if (this.Construction == "leather")
            {
                foreach (string arg in vg["leather"])
                    stacks.Add(GenJstack(string.Format("{{ leather: \"{0}\"}}", arg)));
            }
            else if (this.Construction == "metal")
            {
                foreach (string arg in vg["metal"])
                    stacks.Add(GenJstack(string.Format("{{ metal: \"{0}\"}}", arg)));
            }

            this.CreativeInventoryStacks = new[]
            {
                new CreativeTabAndStackList { Stacks = stacks.ToArray(), Tabs = new[] { "general", "items", "canjewelry" } }
            };
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

        protected override void FillTextureDict(Dictionary<string, AssetLocation> dict, ItemStack itemStack)
        {
            int maxSocketNumber = EncrustableCB.GetMaxAmountSockets(itemStack);
            var notvis = new AssetLocation("canjewelry:item/gem/notvis.png");

            if (maxSocketNumber == -1)
            {
                dict["can_necklace_gem_1"] = new AssetLocation("canjewelry:item/gem/emerald.png");
                dict["can_necklace_gem_2"] = new AssetLocation("canjewelry:item/gem/lapislazuli.png");
                dict["can_necklace_gem_3"] = new AssetLocation("canjewelry:item/gem/citrine.png");
            }

            ITreeAttribute tree = itemStack?.Attributes.HasAttribute(CANJWConstants.ITEM_ENCRUSTED_STRING) == true
                ? itemStack.Attributes.GetTreeAttribute(CANJWConstants.ITEM_ENCRUSTED_STRING)
                : null;

            AssetLocation Resolve(int slotIndex)
            {
                if (tree == null || !tree.HasAttribute("slot" + slotIndex)) return notvis;
                ITreeAttribute treeSocket = tree.GetTreeAttribute("slot" + slotIndex);
                string gemType = treeSocket.GetString("gemtype");
                canjewelry.gems_textures.TryGetValue(gemType, out string assetPath);
                return assetPath != null ? canjewelry.capi.Assets.TryGet(assetPath + ".png")?.Location ?? notvis : notvis;
            }

            if (tree != null)
            {
                if (maxSocketNumber >= 4)
                {
                    for (int i = 0; i < maxSocketNumber; i++) dict["gem_" + (i + 1)] = Resolve(i);
                }
                else if (maxSocketNumber == 3)
                {
                    AssetLocation a0 = Resolve(0);
                    dict["can_necklace_gem_1"] = a0;
                    dict["can_necklace_gem_4"] = a0;
                    dict["can_necklace_gem_2"] = Resolve(1);
                    dict["can_necklace_gem_3"] = Resolve(2);
                }
                else if (maxSocketNumber == 2)
                {
                    AssetLocation a0 = Resolve(0);
                    dict["can_necklace_gem_1"] = a0;
                    dict["can_necklace_gem_4"] = a0;
                    AssetLocation a1 = Resolve(1);
                    dict["can_necklace_gem_2"] = a1;
                    dict["can_necklace_gem_3"] = a1;
                }
                else if (maxSocketNumber == 1)
                {
                    AssetLocation a0 = Resolve(0);
                    for (int i = 1; i < 5; i++) dict["can_necklace_gem_" + i] = a0;
                }
                else if (maxSocketNumber == 0)
                {
                    for (int i = 1; i < 5; i++) dict["gem_" + i] = notvis;
                }
            }
            else
            {
                dict["can_necklace_gem_1"] = notvis;
                dict["can_necklace_gem_2"] = notvis;
                dict["can_necklace_gem_3"] = notvis;
            }

            if (this.Construction == "leather")
                dict["can_necklace_leather"] = new AssetLocation("block/leather/" + itemStack.Attributes.GetString("leather", "plain") + ".png");
            else if (this.Construction == "metal")
                dict["can_necklace_metal"] = new AssetLocation("block/metal/sheet/" + itemStack.Attributes.GetString("metal", "gold") + "1.png");

            dict["gems"] = notvis;
            dict["plain"] = new AssetLocation("game:block/leather/plain");
        }

        public override string GetCategoryCode(ItemStack stack) => "cannadiyannecklace";

        public override string GetMeshCacheKey(ItemStack itemstack)
        {
            string construction = itemstack.Item.Variant["construction"];
            string materialType = construction == "leather"
                ? itemstack.Attributes.GetString("leather", "plain")
                : itemstack.Attributes.GetString("metal", "gold");
            var tree = itemstack.Attributes.GetTreeAttribute(CANJWConstants.ITEM_ENCRUSTED_STRING);
            string buildStr = construction + materialType;
            if (tree != null)
            {
                int slotCount = tree.GetInt(CANJWConstants.SOCKET_ADDED_NUMBER, 0);
                for (int i = 0; i < slotCount; i++)
                {
                    if (tree.HasAttribute("slot" + i))
                    {
                        var innerTree = tree.GetTreeAttribute("slot" + i);
                        if (innerTree.HasAttribute(CANJWConstants.GEM_TYPE_IN_SOCKET))
                            buildStr += innerTree.GetString(CANJWConstants.GEM_TYPE_IN_SOCKET, "");
                    }
                }
            }
            return buildStr;
        }

    }
}
