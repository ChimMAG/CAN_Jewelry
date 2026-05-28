using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using canjewelry.src.CB;
using canjewelry.src.inventories;
using canjewelry.src.items;
using canjewelry.src.jewelry;
using canjewelry.src.gui;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.GameContent.Mechanics;

namespace canjewelry.src.be
{
    // Lapidary Bench — drives a two-stage gem faceting workflow.
    //
    // All per-gem workflow state (stage, progress, recipe code, facet results,
    // pavilion score) lives on the gem-on-dop itemstack's attribute tree
    // (see CANJWConstants.GEM_ON_DOP_TREE). The bench is stateless wrt the
    // gem itself — it only persists the player's current angle/index dial-in
    // and a "pending recipe" choice while the player is browsing options.
    //
    // Stage workflow:
    //   Stage 1 (pavilion): cut all pavilion facets with coarse lap, then
    //   polish; output gem-on-dop with stage=pavilion, progress=done.
    //   Player re-glues the gem onto a fresh dop (game's glue + dop), which
    //   flips stage=crown, progress=cutting.
    //   Stage 2 (crown): cut all crown facets with coarse lap, then polish;
    //   output a fully finished cut gem.
    public class BELapidaryBench : BlockEntityOpenableContainer
    {
        // Packet IDs. Above 1000 to avoid collision with inventory channel.
        public const int PACKET_OPEN_DIALOG    = 5000;
        public const int PACKET_CLOSE_DIALOG   = 1001;
        public const int PACKET_SET_ANGLE      = 2000;
        public const int PACKET_SET_INDEX      = 2001;
        public const int PACKET_CUT_FACET      = 2002;
        public const int PACKET_DO_POLISH      = 2003;
        public const int PACKET_RECIPE_CYCLE   = 2004; // payload: int direction (+1/-1)
        public const int PACKET_RECIPE_COMMIT  = 2005;
        public const int PACKET_RECIPE_CANCEL  = 2006;

        internal InventoryLapidaryBench inventory;

        private ICoreClientAPI capi;
        private ImGuiDialogLapidaryBench imguiGui;
        private BELapidaryBenchRenderer displayRenderer;

        // Block static mesh, tesselated on the client. We add it manually in
        // OnTesselation because BEBehaviorMPConsumer always returns true from
        // its OnTesselation, which suppresses the default block mesh.
        private MeshData benchMesh;

        // Mechanical power consumer behavior — populated from JSON entityBehaviors.
        // Lap spin rate + cut/polish gating read from this when connected.
        public BEBehaviorMPConsumer Mpc { get; private set; }
        public float MpSpeed => Mpc?.TrueSpeed ?? 0f;
        public float MpAngleRad => Mpc?.AngleRad ?? 0f;
        public bool MpConnected => Mpc?.Network != null;

        // Bench-local persisted state. Everything else lives on the assembly.
        public float CurrentAngle { get; private set; } = 0f;
        public int SelectedIndex { get; private set; } = 0;
        public string PendingRecipeCode { get; private set; } = null;

        public override string InventoryClassName => "lapidarybench";
        public override InventoryBase Inventory => inventory;

        public BELapidaryBench()
        {
            inventory = new InventoryLapidaryBench(null, null);
        }

        // Behavior wiring. Grab the MPConsumer reference (added via JSON
        // entityBehaviors) for later use in cut gating and lap spin.
        public override void CreateBehaviors(Block block, IWorldAccessor worldForResolve)
        {
            base.CreateBehaviors(block, worldForResolve);
            Mpc = GetBehavior<BEBehaviorMPConsumer>();
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            inventory.LateInitialize(
                "lapidarybench-" + Pos.X + "/" + Pos.Y + "/" + Pos.Z, api);
            if (api is ICoreClientAPI cAPI)
            {
                capi = cAPI;
                displayRenderer = new BELapidaryBenchRenderer(capi, Pos, this);
                UpdateDisplayText();

                var shape = Vintagestory.API.Common.Shape.TryGet(api, "canjewelry:shapes/block/lapidarybench.json");
                if (shape != null)
                {
                    cAPI.Tesselator.TesselateShape(Block, shape, out benchMesh);
                }
            }
        }

        // BEBehaviorMPConsumer suppresses the default block mesh by returning
        // true from its OnTesselation. Re-add the bench mesh ourselves.
        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            base.OnTesselation(mesher, tesselator);
            if (benchMesh != null) mesher.AddMeshData(benchMesh);
            return true;
        }

        // ====================================================================
        // Inventory + assembly accessors
        // ====================================================================

        public ItemSlot AssemblySlot => inventory[InventoryLapidaryBench.SLOT_ASSEMBLY];
        public ItemSlot LapSlot      => inventory[InventoryLapidaryBench.SLOT_LAP];
        public ItemSlot OutputSlot   => inventory[InventoryLapidaryBench.SLOT_OUTPUT];

        public ItemStack AssemblyStack => AssemblySlot.Itemstack;

        public string AssemblyStage    => CANItemGemOnDop.GetStage(AssemblyStack);
        public string AssemblyProgress => CANItemGemOnDop.GetProgress(AssemblyStack);
        public string AssemblyRecipe   => CANItemGemOnDop.GetRecipeCode(AssemblyStack);

        public FacetingRecipe GetRecipe()
            => AssemblyRecipe != null ? FacetingRecipeLookup.ByCutType(AssemblyRecipe) : null;

        public List<FacetTarget> GetStageFacets()
            => AssemblyStack == null ? null : GetRecipe()?.FacetsForStage(AssemblyStage);

        // List of recipe-facet-list indices the player has already cut.
        public int[] GetCutFacets()
        {
            var tree = CANItemGemOnDop.GetTree(AssemblyStack);
            if (tree?[CANJWConstants.GOD_CUT_FACETS] is IntArrayAttribute arr && arr.value != null)
                return arr.value;
            return Array.Empty<int>();
        }

        public float[] GetFacetResults()
        {
            var tree = CANItemGemOnDop.GetTree(AssemblyStack);
            if (tree?[CANJWConstants.GOD_FACET_RESULTS] is FloatArrayAttribute arr && arr.value != null)
                return arr.value;
            return Array.Empty<float>();
        }

        // How many facets done out of the stage's total.
        public int GetCutCount() => GetCutFacets().Length;
        public int GetStageTotal() => GetStageFacets()?.Count ?? 0;

        // Preview which facet the bench would target if the player cut now.
        // Player explicitly selects index (via < >); bench picks the
        // unfinished facet AT THAT INDEX (tie-break by angle closeness).
        public FacetTarget GetCurrentTarget()
        {
            int idx = FindUnsatisfiedFacetAtIndex(SelectedIndex);
            var list = GetStageFacets();
            if (list == null || idx < 0) return null;
            return list[idx];
        }

        // Find an unfinished facet whose recipe Index == ix. If multiple at the
        // same index (rare — only some recipes), pick the one closest to the
        // current angle. Returns the facet's position in the stage list, or -1.
        private int FindUnsatisfiedFacetAtIndex(int ix)
        {
            var list = GetStageFacets();
            if (list == null) return -1;

            var done = new HashSet<int>(GetCutFacets());
            int bestJ = -1;
            float bestAngleDev = float.MaxValue;
            for (int j = 0; j < list.Count; j++)
            {
                if (done.Contains(j)) continue;
                var t = list[j];
                if (t.Index != ix) continue;
                float dev = Math.Abs(CurrentAngle - t.Angle);
                if (dev < bestAngleDev)
                {
                    bestAngleDev = dev;
                    bestJ = j;
                }
            }
            return bestJ;
        }

        // ====================================================================
        // Right-click → insert / take / open dialog
        // ====================================================================

        public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
        {
            if (Api.Side != EnumAppSide.Server) return true;
            ItemSlot hand = byPlayer.InventoryManager?.ActiveHotbarSlot;

            if (hand != null && !hand.Empty)
            {
                TryInsertHeldItem(byPlayer);
                return true;
            }

            if (TryTakeWithEmptyHand(byPlayer)) return true;

            // Open dialog: send fresh inventory to client.
            byte[] payload;
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                var tree = new TreeAttribute();
                inventory.ToTreeAttributes(tree);
                tree.ToBytes(bw);
                payload = ms.ToArray();
            }
            ((ICoreServerAPI)Api).Network.SendBlockEntityPacket(
                (IServerPlayer)byPlayer, Pos, PACKET_OPEN_DIALOG, payload);
            byPlayer.InventoryManager.OpenInventory(inventory);
            return true;
        }

        public bool TryInsertHeldItem(IPlayer byPlayer)
        {
            if (Api.Side != EnumAppSide.Server) return false;
            ItemSlot hand = byPlayer.InventoryManager?.ActiveHotbarSlot;
            if (hand == null || hand.Empty) return false;
            var coll = hand.Itemstack.Collectible;
            IWorldAccessor world = Api.World;

            if (coll is CANItemGemOnDop && AssemblySlot.Empty)
            {
                if (hand.TryPutInto(world, AssemblySlot, 1) > 0)
                {
                    MarkDirty(true);
                    return true;
                }
            }

            if (coll is CANItemFacetingLap)
            {
                if (hand.TryPutInto(world, LapSlot, 1) > 0)
                {
                    MarkDirty(true);
                    return true;
                }
            }

            return false;
        }

        public bool TryTakeWithEmptyHand(IPlayer byPlayer)
        {
            if (Api.Side != EnumAppSide.Server) return false;
            if (OutputSlot.Empty) return false;

            if (byPlayer.InventoryManager.TryGiveItemstack(OutputSlot.Itemstack))
            {
                OutputSlot.Itemstack = null;
                OutputSlot.MarkDirty();
                MarkDirty(true);
                return true;
            }
            return false;
        }

        // ====================================================================
        // Recipe selection (Pending → Commit)
        // ====================================================================

        public void CycleRecipe(int direction)
        {
            if (Api.Side != EnumAppSide.Server) return;
            if (AssemblyStack == null) return;
            if (!string.IsNullOrEmpty(AssemblyRecipe)) return; // already committed

            var recipes = FacetingRecipeLookup.AllForRough(null);
            if (recipes.Count == 0) return;

            int idx = -1;
            for (int i = 0; i < recipes.Count; i++)
            {
                if (recipes[i].CutType == PendingRecipeCode) { idx = i; break; }
            }
            int next = ((idx + direction) % recipes.Count + recipes.Count) % recipes.Count;
            PendingRecipeCode = recipes[next].CutType;
            MarkDirty(true);
        }

        public void CommitRecipe(IPlayer byPlayer)
        {
            if (Api.Side != EnumAppSide.Server) return;
            if (PendingRecipeCode == null) return;
            if (AssemblyStack == null) return;
            if (!string.IsNullOrEmpty(AssemblyRecipe)) return;

            var tree = CANItemGemOnDop.EnsureTree(AssemblyStack);
            tree.SetString(CANJWConstants.GOD_RECIPE_CODE, PendingRecipeCode);
            if (string.IsNullOrEmpty(tree.GetString(CANJWConstants.GOD_STAGE)))
                tree.SetString(CANJWConstants.GOD_STAGE, CANJWConstants.STAGE_PAVILION);
            if (string.IsNullOrEmpty(tree.GetString(CANJWConstants.GOD_PROGRESS)))
                tree.SetString(CANJWConstants.GOD_PROGRESS, CANJWConstants.PROGRESS_CUTTING);

            AssemblySlot.MarkDirty();
            PendingRecipeCode = null;
            MarkDirty(true);
        }

        public void CancelRecipe()
        {
            if (Api.Side != EnumAppSide.Server) return;
            PendingRecipeCode = null;
            MarkDirty(true);
        }

        // ====================================================================
        // Angle / Index — discrete and continuous setters
        // ====================================================================

        public void AdjustAngle(float delta)
        {
            // Discrete button input — bypass snap-to-target so successive
            // presses produce exact arithmetic (snap would catch ±2° around
            // target and silently undo small steps).
            CurrentAngle = GameMath.Clamp(CurrentAngle + delta, 0f, 90f);
            MarkDirty(true);
        }

        public void AdjustIndex(int delta)
        {
            int res = GetRecipe()?.IndexResolution ?? 16;
            SelectedIndex = ((SelectedIndex + delta) % res + res) % res;
            MarkDirty(true);
        }

        public void SetAngle(float angle)
        {
            ApplyAngleWithSnap(GameMath.Clamp(angle, 0f, 90f));
            MarkDirty(false);
        }

        public void SetSelectedIndex(int index)
        {
            int res = GetRecipe()?.IndexResolution ?? 16;
            SelectedIndex = ((index % res) + res) % res;
            MarkDirty(false);
        }

        public void SetAngleFromPitch(float pitchRadians)
        {
            float deg = -pitchRadians * 180f / (float)Math.PI;
            ApplyAngleWithSnap(GameMath.Clamp(deg, 0f, 90f));
            MarkDirty(false);
        }

        private void ApplyAngleWithSnap(float angle)
        {
            var target = GetCurrentTarget();
            if (target != null && Math.Abs(angle - target.Angle) <= 2f)
            {
                angle = target.Angle;
            }
            CurrentAngle = angle;
        }

        // ====================================================================
        // Cut / Polish — the actual stage advance
        // ====================================================================

        public bool TryCutFacet(IPlayer byPlayer, int indexPos)
        {
            if (Api.Side != EnumAppSide.Server) return false;
            if (AssemblyStack == null) return false;
            if (AssemblyProgress != CANJWConstants.PROGRESS_CUTTING) return false;

            // Explicit guard: a cut style must be committed before cutting.
            // Without this, FindBestUnsatisfiedFacet silently returns -1 and
            // the player can't tell why nothing happens.
            if (string.IsNullOrEmpty(AssemblyRecipe))
            {
                SendError(byPlayer, "canjewelry:lapidary-no-recipe-selected",
                    Lang.Get("canjewelry:lapidary-no-recipe-selected"));
                return false;
            }

            if (!HasValidLap(CANJWConstants.GRIT_COARSE))
            {
                SendError(byPlayer, "canjewelry:lapidary-wrong-lap",
                    Lang.Get("canjewelry:lap-grit-" + CANJWConstants.GRIT_COARSE));
                return false;
            }

            var list = GetStageFacets();
            if (list == null) return false;

            // Player explicitly picked an index — find the unfinished facet
            // sitting at that index (tie-break by current angle if several).
            int j = FindUnsatisfiedFacetAtIndex(indexPos);
            if (j < 0)
            {
                SendError(byPlayer, "canjewelry:lapidary-no-target-at-index",
                    Lang.Get("canjewelry:lapidary-no-target-at-index", indexPos + 1));
                return false;
            }

            // Score = how close the player's angle is to this facet's angle.
            var target = list[j];
            float angleDev = Math.Abs(CurrentAngle - target.Angle);
            float quality = Math.Max(0f, 1f - angleDev / Math.Max(1, target.Tolerance));

            AppendCutFacet(j, quality);
            DamageLap(byPlayer, 1);

            // Stage complete when all facets done.
            if (GetCutCount() >= list.Count)
            {
                var tree = CANItemGemOnDop.EnsureTree(AssemblyStack);
                tree.SetString(CANJWConstants.GOD_PROGRESS, CANJWConstants.PROGRESS_POLISHING);
            }

            AssemblySlot.MarkDirty();
            MarkDirty(true);
            return true;
        }

        public bool TryDoPolish(IPlayer byPlayer)
        {
            if (Api.Side != EnumAppSide.Server) return false;
            if (AssemblyStack == null) return false;
            if (AssemblyProgress != CANJWConstants.PROGRESS_POLISHING) return false;

            if (!HasValidLap(CANJWConstants.GRIT_POLISH))
            {
                SendError(byPlayer, "canjewelry:lapidary-wrong-lap",
                    Lang.Get("canjewelry:lap-grit-" + CANJWConstants.GRIT_POLISH));
                return false;
            }

            float polishQ = 0.85f + 0.15f * (float)Api.World.Rand.NextDouble();
            DamageLap(byPlayer, 5);

            float facetAvg = AverageFacetResults();
            float stageScore = facetAvg * polishQ;

            string stage = AssemblyStage;
            var tree = CANItemGemOnDop.EnsureTree(AssemblyStack);

            if (stage == CANJWConstants.STAGE_PAVILION)
            {
                // Stage 1 finished — store score, mark done, kick out to output
                // so player can re-glue via game's glue + a fresh dop.
                tree.SetFloat(CANJWConstants.GOD_PAVILION_SCORE, stageScore);
                tree.SetString(CANJWConstants.GOD_PROGRESS, CANJWConstants.PROGRESS_DONE);
                // Reset facet results array so when player returns for crown
                // stage the per-facet history starts fresh.
                tree[CANJWConstants.GOD_FACET_RESULTS] = new FloatArrayAttribute(Array.Empty<float>());
                tree[CANJWConstants.GOD_CUT_FACETS] = new IntArrayAttribute(Array.Empty<int>());

                OutputSlot.Itemstack = AssemblyStack;
                AssemblySlot.Itemstack = null;
            }
            else
            {
                // Crown finished — finalize: combine pavilionScore × crownScore,
                // emit a real cut-gem itemstack into output.
                float pavScore = tree.GetFloat(CANJWConstants.GOD_PAVILION_SCORE, 0f);
                float totalQ = pavScore * stageScore;
                EmitCutGem(tree, totalQ);
                AssemblySlot.Itemstack = null;
            }

            AssemblySlot.MarkDirty();
            OutputSlot.MarkDirty();
            MarkDirty(true);
            return true;
        }

        // ====================================================================
        // Helpers
        // ====================================================================

        private bool HasValidLap(string grit)
        {
            if (LapSlot.Empty) return false;
            return LapSlot.Itemstack.Collectible.Variant?[CANJWConstants.LAP_GRIT] == grit;
        }

        private void DamageLap(IPlayer byPlayer, int amount)
        {
            if (LapSlot.Empty) return;
            LapSlot.Itemstack.Collectible.DamageItem(Api.World, byPlayer?.Entity, LapSlot, amount);
            LapSlot.MarkDirty();
        }

        // Append a completed cut to the parallel cutFacets / facetResults arrays.
        private void AppendCutFacet(int facetListIndex, float quality)
        {
            var tree = CANItemGemOnDop.EnsureTree(AssemblyStack);

            int[] existingIdx = Array.Empty<int>();
            if (tree[CANJWConstants.GOD_CUT_FACETS] is IntArrayAttribute ia && ia.value != null)
                existingIdx = ia.value;
            var updatedIdx = new int[existingIdx.Length + 1];
            Array.Copy(existingIdx, updatedIdx, existingIdx.Length);
            updatedIdx[existingIdx.Length] = facetListIndex;
            tree[CANJWConstants.GOD_CUT_FACETS] = new IntArrayAttribute(updatedIdx);

            float[] existingRes = Array.Empty<float>();
            if (tree[CANJWConstants.GOD_FACET_RESULTS] is FloatArrayAttribute fa && fa.value != null)
                existingRes = fa.value;
            var updatedRes = new float[existingRes.Length + 1];
            Array.Copy(existingRes, updatedRes, existingRes.Length);
            updatedRes[existingRes.Length] = quality;
            tree[CANJWConstants.GOD_FACET_RESULTS] = new FloatArrayAttribute(updatedRes);
        }

        private float AverageFacetResults()
        {
            var arr = GetFacetResults();
            if (arr.Length == 0) return 0f;
            float sum = 0f;
            foreach (var v in arr) sum += v;
            return sum / arr.Length;
        }

        private static int ShortestIndexDist(int a, int b, int resolution)
        {
            int dist = Math.Abs(a - b);
            return Math.Min(dist, resolution - dist);
        }

        private static void SendError(IPlayer byPlayer, string key, string detail)
        {
            if (byPlayer is IServerPlayer sp)
            {
                sp.SendIngameError(key, detail);
            }
        }

        // Emit a finished cut-gem itemstack with quality-scaled buff, place in
        // output. gemType / roughQuality come from the assembly tree.
        private void EmitCutGem(ITreeAttribute assemblyTree, float totalQuality)
        {
            string gemType = assemblyTree.GetString(CANJWConstants.GOD_GEM_TYPE)
                          ?? CANJWConstants.FALLBACK_GEM_TYPE;
            string roughQuality = assemblyTree.GetString(CANJWConstants.GOD_ROUGH_QUALITY) ?? "normal";
            string recipeCode = assemblyTree.GetString(CANJWConstants.GOD_RECIPE_CODE)
                             ?? CANJWConstants.CUTTING_ROUND;

            string cutQuality = roughQuality switch
            {
                "normal" => "exquisite",
                "flawed" => "flawless",
                "chipped" => "normal",
                _ => "normal"
            };

            var outputItem = Api.World.GetItem(
                new AssetLocation("canjewelry:gem-cut-" + cutQuality + "-" + gemType));
            if (outputItem == null) return;

            var outputStack = new ItemStack(outputItem);

            var cutGemTree = new TreeAttribute();
            cutGemTree.SetString(CANJWConstants.CUTTING_TYPE, recipeCode);
            outputStack.Attributes[CANJWConstants.CUT_GEM_TREE] = cutGemTree;

            float qualityMult = 0.5f + 0.5f * GameMath.Clamp(totalQuality, 0f, 1f);
            EncrustableCB.ApplyCuttingBuff(outputStack);
            EncrustableCB.ReduceBuffValueBecauseOfMistakes(outputStack, qualityMult);

            var tree = outputStack.Attributes.GetTreeAttribute(CANJWConstants.CUT_GEM_TREE);
            tree?.SetBool(CANJWConstants.GEM_FULL_PROCESSED, true);

            OutputSlot.Itemstack = outputStack;
        }

        // ====================================================================
        // Network — client packets
        // ====================================================================

        public override void OnReceivedServerPacket(int packetid, byte[] data)
        {
            if (packetid == PACKET_OPEN_DIALOG)
            {
                using (var ms = new MemoryStream(data))
                using (var br = new BinaryReader(ms))
                {
                    var tree = new TreeAttribute();
                    tree.FromBytes(br);
                    Inventory.FromTreeAttributes(tree);
                    Inventory.ResolveBlocksOrItems();
                }
                if (capi != null)
                {
                    if (imguiGui == null)
                        imguiGui = new ImGuiDialogLapidaryBench(capi, this, Pos);
                    if (!imguiGui.IsOpen) imguiGui.Open();
                }
                return;
            }

            if (packetid == PACKET_CLOSE_DIALOG)
            {
                ((IClientWorldAccessor)Api.World).Player.InventoryManager.CloseInventory(Inventory);
                if (imguiGui?.IsOpen == true) imguiGui.Close();
                imguiGui?.Dispose();
                imguiGui = null;
                return;
            }

            base.OnReceivedServerPacket(packetid, data);
        }

        public override void OnReceivedClientPacket(IPlayer player, int packetid, byte[] data)
        {
            switch (packetid)
            {
                case PACKET_CLOSE_DIALOG:
                    player.InventoryManager?.CloseInventory(Inventory);
                    return;

                case PACKET_SET_ANGLE:
                    using (var ms = new MemoryStream(data))
                    using (var br = new BinaryReader(ms))
                        SetAngle(br.ReadSingle());
                    return;

                case PACKET_SET_INDEX:
                    using (var ms = new MemoryStream(data))
                    using (var br = new BinaryReader(ms))
                        SetSelectedIndex(br.ReadInt32());
                    return;

                case PACKET_CUT_FACET:
                    TryCutFacet(player, SelectedIndex);
                    return;

                case PACKET_DO_POLISH:
                    TryDoPolish(player);
                    return;

                case PACKET_RECIPE_CYCLE:
                    using (var ms = new MemoryStream(data))
                    using (var br = new BinaryReader(ms))
                        CycleRecipe(br.ReadInt32());
                    return;

                case PACKET_RECIPE_COMMIT:
                    CommitRecipe(player);
                    return;

                case PACKET_RECIPE_CANCEL:
                    CancelRecipe();
                    return;
            }

            base.OnReceivedClientPacket(player, packetid, data);
        }

        // ====================================================================
        // Lifecycle
        // ====================================================================

        public override void OnBlockRemoved()
        {
            imguiGui?.Dispose();
            imguiGui = null;
            displayRenderer?.Dispose();
            displayRenderer = null;
            base.OnBlockRemoved();
        }

        public override void OnBlockUnloaded()
        {
            imguiGui?.Dispose();
            imguiGui = null;
            displayRenderer?.Dispose();
            displayRenderer = null;
            base.OnBlockUnloaded();
        }

        private void UpdateDisplayText()
        {
            if (displayRenderer == null) return;
            string stageLine;
            if (AssemblyStack == null)
            {
                stageLine = Lang.Get("canjewelry:lapidary-display-empty");
            }
            else if (string.IsNullOrEmpty(AssemblyRecipe))
            {
                string pending = PendingRecipeCode ?? "—";
                stageLine = Lang.Get("canjewelry:lapidary-display-choose-recipe", pending);
            }
            else
            {
                stageLine = Lang.Get("canjewelry:lapidary-display-progress",
                    AssemblyStage, AssemblyProgress, AssemblyRecipe);
            }

            // Hint line: best angle for the selected index (the target the
            // bench will score against if the player cuts now).
            string hintLine;
            var target = GetCurrentTarget();
            if (target != null)
            {
                hintLine = Lang.Get("canjewelry:lapidary-display-target", target.Angle, target.Tolerance);
            }
            else if (AssemblyStack != null && !string.IsNullOrEmpty(AssemblyRecipe))
            {
                hintLine = Lang.Get("canjewelry:lapidary-display-no-target", SelectedIndex + 1);
            }
            else
            {
                hintLine = "";
            }

            string currentLine = Lang.Get("canjewelry:lapidary-display-current",
                CurrentAngle.ToString("F1"), (SelectedIndex + 1).ToString("D2"));

            displayRenderer.SetText(string.IsNullOrEmpty(hintLine)
                ? $"{stageLine}\n{currentLine}"
                : $"{stageLine}\n{hintLine}\n{currentLine}");
        }

        // ====================================================================
        // Persistence
        // ====================================================================

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            CurrentAngle = tree.GetFloat(CANJWConstants.LAPIDARY_CURRENT_ANGLE, 0f);
            SelectedIndex = tree.GetInt(CANJWConstants.LAPIDARY_SELECTED_INDEX, 0);
            PendingRecipeCode = tree.GetString("pendingRecipeCode");
            UpdateDisplayText();
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetFloat(CANJWConstants.LAPIDARY_CURRENT_ANGLE, CurrentAngle);
            tree.SetInt(CANJWConstants.LAPIDARY_SELECTED_INDEX, SelectedIndex);
            if (PendingRecipeCode != null)
                tree.SetString("pendingRecipeCode", PendingRecipeCode);
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);
            if (AssemblyStack == null)
            {
                dsc.AppendLine(Lang.Get("canjewelry:lapidary-info-empty"));
                if (PendingRecipeCode != null)
                    dsc.AppendLine(Lang.Get("canjewelry:lapidary-info-pending-recipe", PendingRecipeCode));
                return;
            }
            dsc.AppendLine(Lang.Get("canjewelry:gemondop-stage-" + AssemblyStage));
            dsc.AppendLine(Lang.Get("canjewelry:gemondop-progress-" + AssemblyProgress));
            if (!string.IsNullOrEmpty(AssemblyRecipe))
                dsc.AppendLine(Lang.Get("canjewelry:lapidary-recipe", AssemblyRecipe));
            else if (PendingRecipeCode != null)
                dsc.AppendLine(Lang.Get("canjewelry:lapidary-info-pending-recipe", PendingRecipeCode));
        }
    }
}
