using System;
using System.Collections.Generic;
using System.Text;
using canjewelry.src.items.resource;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace canjewelry.src.items
{
    // gem-on-dop — assembly of a rough gem glued to a dop stick. All workflow
    // state (which side is being cut, how far along, per-facet scores, the
    // gem identity) lives in the itemstack's attribute tree under
    // CANJWConstants.GEM_ON_DOP_TREE. The bench reads/writes this tree
    // directly, so the assembly is fully portable between sessions.
    public class CANItemGemOnDop : Item
    {
        // Read the gem-on-dop tree off an itemstack, or null if missing.
        public static ITreeAttribute GetTree(ItemStack stack)
            => stack?.Attributes?.GetTreeAttribute(CANJWConstants.GEM_ON_DOP_TREE);

        // Create + attach a fresh tree to the given stack. Caller fills the
        // fields and the stack is ready to use.
        public static ITreeAttribute EnsureTree(ItemStack stack)
        {
            var tree = stack.Attributes.GetTreeAttribute(CANJWConstants.GEM_ON_DOP_TREE);
            if (tree == null)
            {
                tree = new TreeAttribute();
                stack.Attributes[CANJWConstants.GEM_ON_DOP_TREE] = tree;
            }
            return tree;
        }

        // Convenience getters used by the bench and the GUI.
        public static string GetStage(ItemStack stack)
            => GetTree(stack)?.GetString(CANJWConstants.GOD_STAGE) ?? CANJWConstants.STAGE_PAVILION;
        public static string GetProgress(ItemStack stack)
            => GetTree(stack)?.GetString(CANJWConstants.GOD_PROGRESS) ?? CANJWConstants.PROGRESS_CUTTING;
        public static string GetGemType(ItemStack stack)
            => GetTree(stack)?.GetString(CANJWConstants.GOD_GEM_TYPE);
        public static string GetRoughQuality(ItemStack stack)
            => GetTree(stack)?.GetString(CANJWConstants.GOD_ROUGH_QUALITY);
        public static string GetRecipeCode(ItemStack stack)
            => GetTree(stack)?.GetString(CANJWConstants.GOD_RECIPE_CODE);

        // Grid-recipe hook. When the output of a crafting recipe is gem-on-dop
        // (rough + dop + glue), seed its attrs from the rough gem's code so
        // the assembly carries the gem identity through faceting.
        public override void OnCreatedByCrafting(ItemSlot[] allInputSlots, ItemSlot outputSlot, IRecipeBase byRecipe)
        {
            base.OnCreatedByCrafting(allInputSlots, outputSlot, byRecipe);
            if (outputSlot.Itemstack == null) return;

            // Re-glue path: input had a gem-on-dop with stage=pavilion, progress=done.
            // copyAttributesFrom in the recipe already moved gemType/roughQuality/
            // recipeCode/pavilionScore to the output — we just flip the work-state
            // fields so cutting resumes on the crown side.
            foreach (var slot in allInputSlots)
            {
                if (slot?.Itemstack?.Item is not CANItemGemOnDop) continue;
                if (GetStage(slot.Itemstack) != CANJWConstants.STAGE_PAVILION) continue;
                if (GetProgress(slot.Itemstack) != CANJWConstants.PROGRESS_DONE) continue;

                var rtree = EnsureTree(outputSlot.Itemstack);
                rtree.SetString(CANJWConstants.GOD_STAGE, CANJWConstants.STAGE_CROWN);
                rtree.SetString(CANJWConstants.GOD_PROGRESS, CANJWConstants.PROGRESS_CUTTING);
                rtree[CANJWConstants.GOD_CUT_FACETS] = new IntArrayAttribute(Array.Empty<int>());
                rtree[CANJWConstants.GOD_FACET_RESULTS] = new FloatArrayAttribute(Array.Empty<float>());
                return;
            }

            // Initial mount path: input had a rough gem.
            foreach (var slot in allInputSlots)
            {
                if (slot?.Itemstack?.Item is not CANRoughGemItem) continue;

                // rough code format: canjewelry:gem-rough-{quality}-{gemtype}
                string path = slot.Itemstack.Collectible?.Code?.Path ?? "";
                string[] parts = path.Split('-');
                if (parts.Length < 4) continue;

                string roughQuality = parts[2];
                string gemType = parts[3];

                var tree = EnsureTree(outputSlot.Itemstack);
                tree.SetString(CANJWConstants.GOD_GEM_TYPE, gemType);
                tree.SetString(CANJWConstants.GOD_ROUGH_QUALITY, roughQuality);
                tree.SetString(CANJWConstants.GOD_STAGE, CANJWConstants.STAGE_PAVILION);
                tree.SetString(CANJWConstants.GOD_PROGRESS, CANJWConstants.PROGRESS_CUTTING);
                tree[CANJWConstants.GOD_CUT_FACETS] = new IntArrayAttribute(Array.Empty<int>());
                tree[CANJWConstants.GOD_FACET_RESULTS] = new FloatArrayAttribute(Array.Empty<float>());
                return;
            }
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            var tree = GetTree(inSlot.Itemstack);
            if (tree == null)
            {
                dsc.AppendLine(Lang.Get("canjewelry:gemondop-empty"));
                return;
            }

            string stage = GetStage(inSlot.Itemstack);
            string progress = GetProgress(inSlot.Itemstack);
            string gem = GetGemType(inSlot.Itemstack) ?? "?";
            string quality = GetRoughQuality(inSlot.Itemstack) ?? "?";
            string recipe = GetRecipeCode(inSlot.Itemstack) ?? "?";

            dsc.AppendLine(Lang.Get("canjewelry:gemondop-info", gem, quality, recipe));
            dsc.AppendLine(Lang.Get("canjewelry:gemondop-stage-" + stage));
            dsc.AppendLine(Lang.Get("canjewelry:gemondop-progress-" + progress));

            if (tree.HasAttribute(CANJWConstants.GOD_PAVILION_SCORE))
            {
                float pav = tree.GetFloat(CANJWConstants.GOD_PAVILION_SCORE);
                dsc.AppendLine(Lang.Get("canjewelry:gemondop-pavilion-score", pav.ToString("0.00")));
            }
        }
    }
}
