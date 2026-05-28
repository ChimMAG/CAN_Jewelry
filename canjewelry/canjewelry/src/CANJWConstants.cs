namespace canjewelry.src
{
    public static class CANJWConstants
    {
        public const string SOCKETS_TIERS_STRING = "cansocketstiers";
        public const string SOCKETS_NUMBER_STRING = "canhavesocketsnumber";
        public const string ITEM_ENCRUSTED_STRING = "canencrusted";
        public const string LEVEL_OF_SOSCKET_STRING = "levelOfSocket";
        public const string SOCKET_ADDED_NUMBER = "socketsnumber";
        public const string GEM_TYPE_IN_SOCKET = "gemtype";
        public const string ADDED_SOCKET_TYPE = "sockettype";
        public const string ENCRUSTED_GEM_SIZE = "size";
        public const string GEM_ATTRIBUTE_BUFF = "attributeBuff";
        public const string GEM_ATTRIBUTE_BUFF_VALUE = "attributeBuffValue";
        public const string GEM_BUFF_TYPE = "gembufftype";
        public const string CANDURABILITY_STRING = "candurability";
        public const string CAN_CUSTOM_VARIANTS = "cancustomvariants";
        public const string CAN_CUSTOM_VARIANTS_COMPARE_KEY = "cancustomvariantscomparekey";
        public const string CUTTING_TYPE = "cuttingtype";
        public const string CUT_GEM_BUFF_VALUE = "cutgetmbuffvalue";
        public const string CUT_GEM_TREE = "cutgemtree";
        public const string CUT_GEM_MAIN_STAT_NAME = "cutgemmainstatname";
        public const string ENCRUSTABLE_BUFFS_NAMES = "encrustablebuffsnames";
        public const string ENCRUSTABLE_BUFFS_VALUES = "encrustablebuffsvalues";
        public const string GEM_FULL_PROCESSED = "gemfullprocessed";
        public const string WAS_GROUND_BEFORE = "wasGroundBefore";

        public const string TEMPORALGRASP = "temporalgrasp";
        public const string CUTTING_ROUND = "round";
        public const string CUTTING_BAGUETTE = "baguette";
        public const string CUTTING_PEAR = "pear";
        public const string FALLBACK_GEM_TYPE = "diamond";
        public const string INSCRIPTION = "inscription";
        public const int INSCRIPTION_MAX_LEN = 32;

        // Lapidary Bench — stage identifiers (which side of the gem is exposed)
        public const string STAGE_PAVILION = "pavilion";
        public const string STAGE_CROWN = "crown";

        // Lapidary Bench — per-stage progress within a stage
        public const string PROGRESS_CUTTING = "cutting";
        public const string PROGRESS_POLISHING = "polishing";
        public const string PROGRESS_DONE = "done";

        // Lapidary Bench — lap grit identifiers (mirrors item variant).
        // Simplified per-stage model uses only coarse + polish (no "fine").
        public const string GRIT_COARSE = "coarse";
        public const string GRIT_POLISH = "polish";

        // Lapidary Bench — item attribute keys (defined in itemtype JSONs)
        public const string DOP_CUT_TYPE = "cutType";
        public const string LAP_GRIT = "grit";

        // gem-on-dop attribute tree: top-level key under itemstack.Attributes
        public const string GEM_ON_DOP_TREE = "gemOnDop";

        // gem-on-dop tree fields
        public const string GOD_GEM_TYPE = "gemType";           // e.g. "diamond"
        public const string GOD_ROUGH_QUALITY = "roughQuality"; // chipped/flawed/normal
        public const string GOD_RECIPE_CODE = "recipeCode";     // round/baguette/pear
        public const string GOD_STAGE = "stage";                // pavilion | crown
        public const string GOD_PROGRESS = "progress";          // cutting | polishing | done
        public const string GOD_CUT_FACETS = "cutFacets";       // int[] — recipe-list indices already cut, parallel to facetResults
        public const string GOD_FACET_RESULTS = "facetResults"; // float[] — quality of each cut so far (parallel to cutFacets)
        public const string GOD_PAVILION_SCORE = "pavilionScore"; // float — final score after stage 1

        // Persisted bench state — minimal after refactor. Most state now lives
        // on the assembly itemstack, so the bench only persists the angle/index
        // the player is currently dialled in to.
        public const string LAPIDARY_CURRENT_ANGLE = "lapidaryCurrentAngle";
        public const string LAPIDARY_SELECTED_INDEX = "lapidarySelectedIndex";
    }
}
