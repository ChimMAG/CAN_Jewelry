using System.Collections.Generic;
using Newtonsoft.Json;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace canjewelry.src.jewelry
{
    // One target facet in a faceting recipe. Player must align angle (camera
    // pitch / discrete buttons) and index (front-panel buttons) within tolerance
    // to score full quality. No Phase — the recipe's stage list it lives in
    // determines which side of the gem (pavilion / crown) it's part of.
    public class FacetTarget
    {
        public int Angle;
        public int Index;
        public int Tolerance = 10;
    }

    // A complete faceting recipe for a single cut style. Selected by the player
    // via the bench's recipe-cycle buttons; identified by CutType. Stage 1
    // (pavilion) and stage 2 (crown) cut their own facet lists; player polishes
    // each side after its cutting pass.
    public class FacetingRecipe
    {
        public AssetLocation Code;
        public string CutType = CANJWConstants.CUTTING_ROUND;

        // Wildcard pattern, matched against rough-gem code on stage start.
        // Default accepts any rough gem of any quality and type.
        public string Ingredient = "canjewelry:gem-rough-*-*";

        public int IndexResolution = 16;
        public bool Enabled = true;

        public List<FacetTarget> PavilionFacets = new();
        public List<FacetTarget> CrownFacets = new();

        // Internal — assigned by the loader for stable referencing.
        [JsonIgnore]
        public int RecipeId;

        // Match the rough gem only (player picks cutType separately via UI now,
        // so cutType is no longer part of matching).
        public bool MatchesRough(ItemStack roughGem)
        {
            if (!Enabled) return false;
            if (roughGem?.Collectible?.Code == null) return false;
            return WildcardUtil.Match(new AssetLocation(Ingredient), roughGem.Collectible.Code);
        }

        // Get facet list for the named stage.
        public List<FacetTarget> FacetsForStage(string stage) => stage switch
        {
            CANJWConstants.STAGE_PAVILION => PavilionFacets,
            CANJWConstants.STAGE_CROWN => CrownFacets,
            _ => null,
        };
    }
}
