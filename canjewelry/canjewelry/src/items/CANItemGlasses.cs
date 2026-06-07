using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace canjewelry.src.items
{
    public class CANItemGlasses : CANItemWearable
    {
        protected override string MeshrefsCacheName => "canglassesmeshrefs";

        protected override void AddAllTypesToCreativeInventory()
        {
            var vg = this.Attributes["variantGroups"].AsObject<Dictionary<string, string[]>>(null);
            if (vg == null) return;
            string[] loops = vg["metal"];
            string[] glassTypes = vg["glass"];

            var stacks = new List<JsonItemStack>();
            foreach (string loop in loops)
                foreach (var glass in glassTypes)
                    stacks.Add(GenJstack(string.Format("{{ metal: \"{0}\", glass: \"{1}\" }}", loop, glass)));

            this.CreativeInventoryStacks = new[]
            {
                new CreativeTabAndStackList { Stacks = stacks.ToArray(), Tabs = new[] { "general", "items", "canjewelry" } }
            };
        }

        protected override void FillTextureDict(Dictionary<string, AssetLocation> dict, ItemStack itemStack)
        {
            dict["can_glasses_metal"] = new AssetLocation("block/metal/sheet/" + itemStack.Attributes.GetString("metal", "steel") + "1.png");
            dict["canglasses_quartz"] = new AssetLocation("block/glass/" + itemStack.Attributes.GetString("glass", "plain") + ".png");
        }

        public override string GetCategoryCode(ItemStack stack) => "canmonocle";

        public override string GetMeshCacheKey(ItemStack itemstack)
        {
            string metal = itemstack.Attributes.GetString("metal", null);
            string glass = itemstack.Attributes.GetString("glass", null);
            return this.Code.ToShortString() + "-" + metal + "-" + glass;
        }

        public override void OnCreatedByCrafting(ItemSlot[] allInputslots, ItemSlot outputSlot, GridRecipe byRecipe)
        {
            if (byRecipe.Name.Path == "can-monocle-change-side")
            {
                ItemSlot monocleSlot = allInputslots.FirstOrDefault(sl => !sl.Empty);
                if (monocleSlot != null)
                {
                    foreach (var attr in monocleSlot.Itemstack.Attributes)
                        outputSlot.Itemstack.Attributes[attr.Key] = attr.Value;

                    if (outputSlot.Itemstack.Attributes.HasAttribute("eye"))
                        outputSlot.Itemstack.Attributes.SetString("eye", outputSlot.Itemstack.Attributes.GetString("eye") == "left" ? "right" : "left");
                    else
                        outputSlot.Itemstack.Attributes.SetString("eye", "right");
                    return;
                }
            }
            base.OnCreatedByCrafting(allInputslots, outputSlot, byRecipe);
        }
    }
}
