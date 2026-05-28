using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;

namespace canjewelry.src.jewelry
{
    // Loads faceting recipes from `recipes/faceting/*.json` on both client and
    // server. Each side parses the same files independently, so no network sync
    // is needed. Recipes are stored in canjewelry.facetingRecipes.
    //
    // Recipe files can contain either a single JObject or a JArray of recipes.
    public class FacetingRecipeLoader : ModSystem
    {
        public override double ExecuteOrder() => 1.0;

        public override void AssetsLoaded(ICoreAPI api)
        {
            base.AssetsLoaded(api);
            api.Logger.Notification("[lapidary] FacetingRecipeLoader.AssetsLoaded fired (side={0})", api.Side);

            // Build into a LOCAL list — only swap into canjewelry.facetingRecipes
            // at the end. Singleplayer integrates client+server in one process
            // and the static field is shared: a second-firing AssetsLoaded that
            // wipes the list with an empty one would erase the first side's work.
            var newRecipes = new List<FacetingRecipe>();

            Dictionary<AssetLocation, JToken> files;
            try
            {
                files = api.Assets.GetMany<JToken>(api.Logger, "recipes/faceting/", null);
            }
            catch (Exception e)
            {
                api.Logger.Error("[lapidary] Failed to enumerate faceting recipes: {0}", e.Message);
                files = null;
            }

            api.Logger.Notification("[lapidary] Enumerated {0} candidate file(s)", files?.Count ?? 0);
            if (files == null || files.Count == 0)
            {
                try
                {
                    files = api.Assets.GetMany<JToken>(api.Logger, "recipes/faceting", null);
                    api.Logger.Notification("[lapidary] Retry without trailing slash → {0} file(s)", files?.Count ?? 0);
                }
                catch { }
            }

            int nextId = 1;
            if (files != null)
            {
                foreach (var (location, content) in files)
                {
                    api.Logger.Notification("[lapidary]  - {0}", location);
                    List<FacetingRecipe> parsed;
                    try
                    {
                        parsed = Parse(content);
                    }
                    catch (Exception e)
                    {
                        api.Logger.Error("[lapidary] Failed to parse {0}: {1}", location, e.Message);
                        continue;
                    }

                    foreach (var r in parsed)
                    {
                        if (!r.Enabled) { api.Logger.Notification("[lapidary]    disabled, skipped"); continue; }
                        int total = (r.PavilionFacets?.Count ?? 0) + (r.CrownFacets?.Count ?? 0);
                        if (total == 0)
                        {
                            api.Logger.Warning("[lapidary] {0} has no facets — skipped", location);
                            continue;
                        }
                        if (r.Code == null) r.Code = location;
                        r.RecipeId = nextId++;
                        newRecipes.Add(r);
                        api.Logger.Notification("[lapidary]    loaded recipe cutType={0}, pavilion={1}, crown={2}",
                            r.CutType, r.PavilionFacets?.Count ?? 0, r.CrownFacets?.Count ?? 0);
                    }
                }
            }

            // Swap policy:
            //   - If we loaded anything, install it.
            //   - If we found nothing AND there's no existing list, install
            //     an empty one (so reads don't NPE).
            //   - If we found nothing but the field already has content, leave
            //     it alone (the other side already loaded — don't wipe).
            int existing = canjewelry.facetingRecipes?.Count ?? -1;
            if (newRecipes.Count > 0)
            {
                canjewelry.facetingRecipes = newRecipes;
                api.Logger.Event("[lapidary] Installed {0} faceting recipes (overwrote {1})", newRecipes.Count, Math.Max(0, existing));
            }
            else if (existing <= 0)
            {
                canjewelry.facetingRecipes = newRecipes; // empty
                api.Logger.Notification("[lapidary] Installed empty recipe list (no candidates found and no prior list)");
            }
            else
            {
                api.Logger.Notification("[lapidary] Found 0 candidates on side={0}, but {1} recipes already present — keeping existing", api.Side, existing);
            }
        }

        private static List<FacetingRecipe> Parse(JToken content)
        {
            var result = new List<FacetingRecipe>();
            switch (content)
            {
                case JArray arr:
                    foreach (var item in arr)
                    {
                        var r = item.ToObject<FacetingRecipe>();
                        if (r != null) result.Add(r);
                    }
                    break;
                case JObject obj:
                    var single = obj.ToObject<FacetingRecipe>();
                    if (single != null) result.Add(single);
                    break;
            }
            return result;
        }
    }

    public static class FacetingRecipeLookup
    {
        // Find a recipe by its CutType code (the new selection model — player
        // picks the recipe via UI, no dop variants).
        public static FacetingRecipe ByCutType(string cutType)
        {
            if (canjewelry.facetingRecipes == null || cutType == null) return null;
            foreach (var r in canjewelry.facetingRecipes)
            {
                if (r.CutType == cutType) return r;
            }
            return null;
        }

        public static FacetingRecipe ById(int id)
        {
            if (canjewelry.facetingRecipes == null) return null;
            foreach (var r in canjewelry.facetingRecipes)
            {
                if (r.RecipeId == id) return r;
            }
            return null;
        }

        // Enumerate all enabled recipes that can produce a cut from the given
        // rough gem (or all enabled if rough is null).
        public static List<FacetingRecipe> AllForRough(ItemStack roughGem)
        {
            var result = new List<FacetingRecipe>();
            if (canjewelry.facetingRecipes == null) return result;
            foreach (var r in canjewelry.facetingRecipes)
            {
                if (!r.Enabled) continue;
                if (roughGem != null && !r.MatchesRough(roughGem)) continue;
                result.Add(r);
            }
            return result;
        }
    }
}
