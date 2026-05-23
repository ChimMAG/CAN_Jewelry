using System.Collections.Generic;
using System.Linq;
using canjewelry.src.CB;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace canjewelry.src.items
{
    public class CANItemMonocle : CANItemWearable
    {
        protected override string MeshrefsCacheName => "canmonoclemeshrefs";

        protected override void AddAllTypesToCreativeInventory()
        {
            var vg = this.Attributes["variantGroups"].AsObject<Dictionary<string, string[]>>(null);
            if (vg == null) return;
            string[] loops = vg["metal"];
            string[] glassTypes = vg["glass"];

            var stacks = new List<JsonItemStack>();
            foreach (string loop in loops)
                foreach (var glass in glassTypes)
                    stacks.Add(GenJstack(string.Format("{{ loop: \"{0}\", glasstype: \"{1}\" }}", loop, glass)));

            this.CreativeInventoryStacks = new[]
            {
                new CreativeTabAndStackList { Stacks = stacks.ToArray(), Tabs = new[] { "general", "items", "canjewelry" } }
            };
        }

        protected override void FillTextureDict(Dictionary<string, AssetLocation> dict, ItemStack itemStack)
        {
            string carcassus = itemStack.Attributes.GetString("loop", "steel");
            dict["brass"] = new AssetLocation("block/metal/sheet/" + carcassus + "1.png");
            string quartzType = itemStack.Attributes.GetString("glasstype", "red");
            dict["quartzglass"] = new AssetLocation("block/glass/" + quartzType + ".png");

            int maxSocketNumber = EncrustableCB.GetMaxAmountSockets(itemStack);
            CANItemCoronet.FillGemPositions4(dict, itemStack, maxSocketNumber);
        }

        public override Shape GetShape(ItemStack stack, Entity forEntity, string texturePrefixCode)
        {
            JsonObject attrObj = stack.Collectible.Attributes;
            CompositeShape compGearShape = (!attrObj["attachShape"].Exists)
                ? ((stack.Class == EnumItemClass.Item) ? stack.Item.Shape : stack.Block.Shape)
                : attrObj["attachShape"].AsObject<CompositeShape>(null, stack.Collectible.Code.Domain);

            string eyeSide = stack.Attributes.GetString("eye", "right");
            AssetLocation shapePath = compGearShape.Base.CopyWithPath("shapes/" + compGearShape.Base.Path + "_" + eyeSide + ".json");
            Shape gearShape = Vintagestory.API.Common.Shape.TryGet(api, shapePath);
            if (gearShape == null)
            {
                api.World.Logger.Warning("Entity armor shape {0} defined in {1} {2} not found or errored, was supposed to be at {3}. Armor piece will be invisible.",
                    compGearShape.Base, stack.Class, stack.Collectible.Code, shapePath);
                return null;
            }
            return gearShape;
        }

        public override string GetCategoryCode(ItemStack stack) => "canmonocle";

        public override string GetMeshCacheKey(ItemStack itemstack)
        {
            string metal = itemstack.Attributes.GetString("metal", null);
            return this.Code.ToShortString() + "-" + metal;
        }

        public override void OnCreatedByCrafting(ItemSlot[] allInputslots, ItemSlot outputSlot, IRecipeBase byRecipe)
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
