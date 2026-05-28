using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using canjewelry.src;
using canjewelry.src.be;
using canjewelry.src.inventories;
using canjewelry.src.items;
using canjewelry.src.jewelry;
using ImGuiNET;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using VSImGui.API;

namespace canjewelry.src.gui
{
    // Lapidary Bench dialog. Mirrors the front-panel buttons (recipe picker,
    // angle/index dial, cut/polish actions) and adds a per-facet quality
    // visualisation. State is read live from the assembly itemstack's tree
    // attributes; actions go to server via SendBlockEntityPacket.
    public class ImGuiDialogLapidaryBench : ImGuiDialogBase
    {
        private readonly ICoreClientAPI _capi;
        private readonly BELapidaryBench _be;
        private readonly InventoryLapidaryBench _inv;
        private readonly BlockPos _pos;

        private ItemIconAtlas _atlas;
        private ImGuiSlotRenderer _slotRenderer;
        private ImGuiInventoryGrid _grid;
        private bool _gpuReady;

        private float _angleSliderValue;
        private float _lastSentAngle = float.NaN;
        private int _lastSentIndex = -1;

        public bool IsOpen => Opened;

        public ImGuiDialogLapidaryBench(ICoreClientAPI capi, BELapidaryBench be, BlockPos pos)
            : base(capi)
        {
            _capi = capi;
            _be = be;
            _inv = (InventoryLapidaryBench)be.Inventory;
            _pos = pos;
        }

        private void EnsureGpuReady()
        {
            if (_gpuReady) return;
            _atlas = new ItemIconAtlas(_capi);
            _slotRenderer = new ImGuiSlotRenderer(_capi, slotSize: 52);
            _grid = new ImGuiInventoryGrid(_capi, _slotRenderer, _atlas, SendInvPacket);
            _grid.SetInventory(_inv);
            _gpuReady = true;
        }

        protected override bool OnOpen()
        {
            _capi.World.Player.InventoryManager.OpenInventory(_inv);
            return true;
        }

        protected override bool OnClose()
        {
            _capi.Network.SendBlockEntityPacket(_pos, BELapidaryBench.PACKET_CLOSE_DIALOG);
            _capi.World.Player.InventoryManager.CloseInventory(_inv);
            ImGuiInventoryGrid.SuppressMouseDrop = false;
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _atlas?.Dispose();
                _slotRenderer?.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override CallbackGUIStatus Draw(float deltaSeconds)
        {
            if (!Opened) return CallbackGUIStatus.Closed;
            if (!OnDraw()) Close();
            return Opened ? CallbackGUIStatus.GrabMouse : CallbackGUIStatus.Closed;
        }

        protected override bool OnDraw()
        {
            if (ImGui.IsKeyPressed(ImGuiKey.Escape, false)) return false;

            EnsureGpuReady();

            bool open = true;
            ImGui.SetNextWindowSize(new Vector2(640, 680), ImGuiCond.FirstUseEver);

            if (!ImGui.Begin(Lang.Get("canjewelry:lapidary-title") + "##canlapidary", ref open,
                ImGuiWindowFlags.None))
            {
                ImGui.End();
                return open;
            }

            DrawContent();
            ImGui.End();
            return open;
        }

        private void DrawContent()
        {
            DrawHeader();
            ImGui.Separator();
            DrawSlots();
            ImGui.Separator();

            // Branch on assembly state:
            //   no assembly                  → instruction
            //   assembly, no committed recipe → recipe picker
            //   assembly with recipe         → target panel + cut/polish controls
            var assembly = _be.AssemblyStack;
            if (assembly == null)
            {
                ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f),
                    Lang.Get("canjewelry:lapidary-gui-no-assembly"));
                return;
            }

            if (string.IsNullOrEmpty(_be.AssemblyRecipe))
            {
                DrawRecipePicker();
                return;
            }

            DrawTargetPanel();
            ImGui.Spacing();
            DrawAngleSlider();
            ImGui.Spacing();
            DrawIndexButtons();
            ImGui.Spacing();
            DrawActionButtons();
            ImGui.Spacing();
            DrawHistoryBars();
            ImGui.Spacing();
            DrawDebugPanel();
        }

        // Live-tuning sliders for the in-world dop arm renderer. Collapsed by
        // default. Values are static on BELapidaryBenchRenderer so changes
        // affect every bench instance immediately, no save/reload.
        private void DrawDebugPanel()
        {
            if (!ImGui.CollapsingHeader("Debug: dop renderer", ImGuiTreeNodeFlags.None)) return;

            // Compact 3-axis layout: XYZ on a single line via SliderFloat3.
            Vector3 spindle = new(BELapidaryBenchRenderer.SpindleX, BELapidaryBenchRenderer.SpindleY, BELapidaryBenchRenderer.SpindleZ);
            if (ImGui.SliderFloat3("Spindle XYZ", ref spindle, 0f, 1.5f, "%.3f"))
            {
                BELapidaryBenchRenderer.SpindleX = spindle.X;
                BELapidaryBenchRenderer.SpindleY = spindle.Y;
                BELapidaryBenchRenderer.SpindleZ = spindle.Z;
            }

            Vector3 arm = new(BELapidaryBenchRenderer.ArmOffsetX, BELapidaryBenchRenderer.ArmOffsetY, BELapidaryBenchRenderer.ArmOffsetZ);
            if (ImGui.SliderFloat3("Arm offset", ref arm, -1f, 1f, "%.3f"))
            {
                BELapidaryBenchRenderer.ArmOffsetX = arm.X;
                BELapidaryBenchRenderer.ArmOffsetY = arm.Y;
                BELapidaryBenchRenderer.ArmOffsetZ = arm.Z;
            }

            ImGui.Separator();
            ImGui.SliderFloat("Dop scale", ref BELapidaryBenchRenderer.DopScale, 0.05f, 2f, "%.2f");
            Vector3 dopOrigin = new(BELapidaryBenchRenderer.DopOriginX, BELapidaryBenchRenderer.DopOriginY, BELapidaryBenchRenderer.DopOriginZ);
            if (ImGui.SliderFloat3("Dop origin", ref dopOrigin, -2f, 2f, "%.2f"))
            {
                BELapidaryBenchRenderer.DopOriginX = dopOrigin.X;
                BELapidaryBenchRenderer.DopOriginY = dopOrigin.Y;
                BELapidaryBenchRenderer.DopOriginZ = dopOrigin.Z;
            }
            Vector3 dopRot = new(BELapidaryBenchRenderer.DopRotX, BELapidaryBenchRenderer.DopRotY, BELapidaryBenchRenderer.DopRotZ);
            if (ImGui.SliderFloat3("Dop rot°", ref dopRot, -180f, 180f, "%.0f"))
            {
                BELapidaryBenchRenderer.DopRotX = dopRot.X;
                BELapidaryBenchRenderer.DopRotY = dopRot.Y;
                BELapidaryBenchRenderer.DopRotZ = dopRot.Z;
            }

            ImGui.Separator();
            ImGui.SliderFloat("Gem scale", ref BELapidaryBenchRenderer.GemScale, 0.05f, 1f, "%.2f");
            Vector3 gemOrigin = new(BELapidaryBenchRenderer.GemOriginX, BELapidaryBenchRenderer.GemOriginY, BELapidaryBenchRenderer.GemOriginZ);
            if (ImGui.SliderFloat3("Gem origin", ref gemOrigin, -3f, 2f, "%.2f"))
            {
                BELapidaryBenchRenderer.GemOriginX = gemOrigin.X;
                BELapidaryBenchRenderer.GemOriginY = gemOrigin.Y;
                BELapidaryBenchRenderer.GemOriginZ = gemOrigin.Z;
            }
            Vector3 gemRot = new(BELapidaryBenchRenderer.GemRotX, BELapidaryBenchRenderer.GemRotY, BELapidaryBenchRenderer.GemRotZ);
            if (ImGui.SliderFloat3("Gem rot°", ref gemRot, -180f, 180f, "%.0f"))
            {
                BELapidaryBenchRenderer.GemRotX = gemRot.X;
                BELapidaryBenchRenderer.GemRotY = gemRot.Y;
                BELapidaryBenchRenderer.GemRotZ = gemRot.Z;
            }

            ImGui.Separator();
            ImGui.SliderFloat("Yaw mul", ref BELapidaryBenchRenderer.YawMul, -2f, 2f, "%.3f");
            ImGui.TextWrapped("Tilt per axis (independent — input × each per-axis multiplier):");
            Vector3 tiltMul = new(BELapidaryBenchRenderer.TiltMulX, BELapidaryBenchRenderer.TiltMulY, BELapidaryBenchRenderer.TiltMulZ);
            if (ImGui.SliderFloat3("Tilt mul X/Y/Z", ref tiltMul, -2f, 2f, "%.3f"))
            {
                BELapidaryBenchRenderer.TiltMulX = tiltMul.X;
                BELapidaryBenchRenderer.TiltMulY = tiltMul.Y;
                BELapidaryBenchRenderer.TiltMulZ = tiltMul.Z;
            }
            ImGui.TextWrapped("Tilt pivot: set Debug tilt° to 30-45° first, then move these sliders until the dop rotates around the desired end (not 'swinging' around a far point).");
            ImGui.Checkbox("Show pivot marker (red cube)", ref BELapidaryBenchRenderer.ShowTiltPivot);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(120);
            ImGui.SliderFloat("size", ref BELapidaryBenchRenderer.PivotMarkerSize, 0.01f, 0.3f, "%.3f");

            ImGui.Separator();
            ImGui.Text("Lap disc:");
            Vector3 lapPos = new(BELapidaryBenchRenderer.LapX, BELapidaryBenchRenderer.LapY, BELapidaryBenchRenderer.LapZ);
            if (ImGui.SliderFloat3("Lap XYZ", ref lapPos, 0f, 1.5f, "%.3f"))
            {
                BELapidaryBenchRenderer.LapX = lapPos.X;
                BELapidaryBenchRenderer.LapY = lapPos.Y;
                BELapidaryBenchRenderer.LapZ = lapPos.Z;
            }
            ImGui.SliderFloat("Lap scale", ref BELapidaryBenchRenderer.LapScale, 0.1f, 2f, "%.2f");
            ImGui.SliderFloat("Lap spin speed (rad/s)", ref BELapidaryBenchRenderer.LapSpinSpeed, 0f, 30f, "%.2f");
            Vector3 tiltPivot = new(BELapidaryBenchRenderer.TiltPivotX, BELapidaryBenchRenderer.TiltPivotY, BELapidaryBenchRenderer.TiltPivotZ);
            if (ImGui.SliderFloat3("Tilt pivot", ref tiltPivot, -3f, 3f, "%.3f"))
            {
                BELapidaryBenchRenderer.TiltPivotX = tiltPivot.X;
                BELapidaryBenchRenderer.TiltPivotY = tiltPivot.Y;
                BELapidaryBenchRenderer.TiltPivotZ = tiltPivot.Z;
            }
            ImGui.Separator();
            ImGui.Text("Preview override (0 = use live game values):");
            ImGui.SliderFloat("Debug tilt°", ref BELapidaryBenchRenderer.DebugTiltDeg, 0f, 90f, "%.1f");
            ImGui.SliderInt("Debug index", ref BELapidaryBenchRenderer.DebugIndexOverride, -1, 15);
            ImGui.Separator();

            if (ImGui.Button("Reset defaults"))
            {
                BELapidaryBenchRenderer.SpindleX = 0.500f;
                BELapidaryBenchRenderer.SpindleY = 0.550f;
                BELapidaryBenchRenderer.SpindleZ = 0.150f;
                BELapidaryBenchRenderer.ArmOffsetX = 0f;
                BELapidaryBenchRenderer.ArmOffsetY = 0f;
                BELapidaryBenchRenderer.ArmOffsetZ = 0.350f;
                BELapidaryBenchRenderer.DopScale = 0.600f;
                BELapidaryBenchRenderer.DopOriginX = -1.430f;
                BELapidaryBenchRenderer.DopOriginY = 0.340f;
                BELapidaryBenchRenderer.DopOriginZ = -0.020f;
                BELapidaryBenchRenderer.GemScale = 0.350f;
                BELapidaryBenchRenderer.GemOriginX = -0.500f;
                BELapidaryBenchRenderer.GemOriginY = -0.790f;
                BELapidaryBenchRenderer.GemOriginZ = -0.500f;
                BELapidaryBenchRenderer.YawMul = 1f;
                BELapidaryBenchRenderer.TiltMulX = 1f;
                BELapidaryBenchRenderer.TiltMulY = 0f;
                BELapidaryBenchRenderer.TiltMulZ = 0f;
                BELapidaryBenchRenderer.DopRotX = 0f;
                BELapidaryBenchRenderer.DopRotY = -92f;
                BELapidaryBenchRenderer.DopRotZ = 0f;
                BELapidaryBenchRenderer.GemRotX = 0f;
                BELapidaryBenchRenderer.GemRotY = -25f;
                BELapidaryBenchRenderer.GemRotZ = 0f;
                BELapidaryBenchRenderer.TiltPivotX = -0.122f;
                BELapidaryBenchRenderer.TiltPivotY = -2.082f;
                BELapidaryBenchRenderer.TiltPivotZ = -0.303f;
            }
            ImGui.SameLine();
            if (ImGui.Button("Copy to clipboard"))
            {
                CopyDebugValuesToClipboard();
            }
        }

        // Dump the currently tuned values as a C# block ready to paste into
        // BELapidaryBenchRenderer's static field initialisers. Writes to the
        // OS clipboard via ImGui + also echoes to chat as a backup.
        private void CopyDebugValuesToClipboard()
        {
            string snippet =
                $"public static float SpindleX  = {BELapidaryBenchRenderer.SpindleX:F3}f;\n" +
                $"public static float SpindleY  = {BELapidaryBenchRenderer.SpindleY:F3}f;\n" +
                $"public static float SpindleZ  = {BELapidaryBenchRenderer.SpindleZ:F3}f;\n" +
                $"public static float ArmOffsetX = {BELapidaryBenchRenderer.ArmOffsetX:F3}f;\n" +
                $"public static float ArmOffsetY = {BELapidaryBenchRenderer.ArmOffsetY:F3}f;\n" +
                $"public static float ArmOffsetZ = {BELapidaryBenchRenderer.ArmOffsetZ:F3}f;\n" +
                $"public static float DopScale   = {BELapidaryBenchRenderer.DopScale:F3}f;\n" +
                $"public static float DopOriginX = {BELapidaryBenchRenderer.DopOriginX:F3}f;\n" +
                $"public static float DopOriginY = {BELapidaryBenchRenderer.DopOriginY:F3}f;\n" +
                $"public static float DopOriginZ = {BELapidaryBenchRenderer.DopOriginZ:F3}f;\n" +
                $"public static float GemScale   = {BELapidaryBenchRenderer.GemScale:F3}f;\n" +
                $"public static float GemOriginX = {BELapidaryBenchRenderer.GemOriginX:F3}f;\n" +
                $"public static float GemOriginY = {BELapidaryBenchRenderer.GemOriginY:F3}f;\n" +
                $"public static float GemOriginZ = {BELapidaryBenchRenderer.GemOriginZ:F3}f;\n" +
                $"public static float YawMul     = {BELapidaryBenchRenderer.YawMul:F3}f;\n" +
                $"public static float TiltMulX   = {BELapidaryBenchRenderer.TiltMulX:F3}f;\n" +
                $"public static float TiltMulY   = {BELapidaryBenchRenderer.TiltMulY:F3}f;\n" +
                $"public static float TiltMulZ   = {BELapidaryBenchRenderer.TiltMulZ:F3}f;\n" +
                $"public static float DopRotX    = {BELapidaryBenchRenderer.DopRotX:F2}f;\n" +
                $"public static float DopRotY    = {BELapidaryBenchRenderer.DopRotY:F2}f;\n" +
                $"public static float DopRotZ    = {BELapidaryBenchRenderer.DopRotZ:F2}f;\n" +
                $"public static float GemRotX    = {BELapidaryBenchRenderer.GemRotX:F2}f;\n" +
                $"public static float GemRotY    = {BELapidaryBenchRenderer.GemRotY:F2}f;\n" +
                $"public static float GemRotZ    = {BELapidaryBenchRenderer.GemRotZ:F2}f;\n" +
                $"public static float TiltPivotX = {BELapidaryBenchRenderer.TiltPivotX:F3}f;\n" +
                $"public static float TiltPivotY = {BELapidaryBenchRenderer.TiltPivotY:F3}f;\n" +
                $"public static float TiltPivotZ = {BELapidaryBenchRenderer.TiltPivotZ:F3}f;\n" +
                "// (tilt axis multipliers above)";
            ImGui.SetClipboardText(snippet);
            _capi.ShowChatMessage("Renderer tuning values copied to clipboard.");
        }

        private void DrawHeader()
        {
            var assembly = _be.AssemblyStack;
            if (assembly == null)
            {
                ImGui.Text(Lang.Get("canjewelry:lapidary-display-empty"));
                return;
            }
            string stage = _be.AssemblyStage;
            string progress = _be.AssemblyProgress;
            string recipe = _be.AssemblyRecipe ?? "(none)";
            ImGui.Text(Lang.Get("canjewelry:gemondop-stage-" + stage));
            ImGui.SameLine();
            ImGui.Text(" | " + Lang.Get("canjewelry:gemondop-progress-" + progress));
            ImGui.SameLine();
            ImGui.Text(" | " + Lang.Get("canjewelry:lapidary-recipe", recipe));

            var list = _be.GetStageFacets();
            if (list != null)
            {
                ImGui.Text($"Progress: {_be.GetCutCount()} / {list.Count}");
            }
        }

        private void DrawSlots()
        {
            ImGui.Text("Inventory:");
            int size = _slotRenderer.SlotSize;
            string[] labels = { "Assembly", "Lap", "Output" };
            for (int i = 0; i < InventoryLapidaryBench.SLOT_COUNT; i++)
            {
                if (i > 0) ImGui.SameLine();
                ImGui.BeginGroup();
                ImGui.TextDisabled(labels[i]);
                _grid.DrawSingleSlot(i);
                ImGui.Dummy(new Vector2(size, size));
                ImGui.EndGroup();
            }
        }

        private void DrawRecipePicker()
        {
            ImGui.Text(Lang.Get("canjewelry:lapidary-gui-pick-recipe"));
            ImGui.Spacing();

            var recipes = FacetingRecipeLookup.AllForRough(null);
            string pending = _be.PendingRecipeCode;

            for (int i = 0; i < recipes.Count; i++)
            {
                if (i > 0) ImGui.SameLine();
                var r = recipes[i];
                bool isSel = r.CutType == pending;
                if (isSel) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.3f, 0.55f, 0.85f, 1f));
                if (ImGui.Button($"{r.CutType}##rcp{i}", new Vector2(96, 32)))
                {
                    int dir = i - IndexOf(recipes, pending);
                    if (dir == 0 && pending == null) dir = 1; // first click — select this one
                    _capi.Network.SendBlockEntityPacket(_pos, BELapidaryBench.PACKET_RECIPE_CYCLE, BitConvert(dir));
                }
                if (isSel) ImGui.PopStyleColor();
            }

            ImGui.Spacing();
            if (ImGui.Button("OK", new Vector2(120, 32)))
            {
                _capi.Network.SendBlockEntityPacket(_pos, BELapidaryBench.PACKET_RECIPE_COMMIT);
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel", new Vector2(120, 32)))
            {
                _capi.Network.SendBlockEntityPacket(_pos, BELapidaryBench.PACKET_RECIPE_CANCEL);
            }
        }

        private static int IndexOf(System.Collections.Generic.List<FacetingRecipe> list, string cutType)
        {
            for (int i = 0; i < list.Count; i++) if (list[i].CutType == cutType) return i;
            return -1;
        }

        private void DrawTargetPanel()
        {
            var target = _be.GetCurrentTarget();
            if (target == null)
            {
                ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "(no active target)");
                return;
            }
            ImGui.TextColored(new Vector4(1f, 0.85f, 0.4f, 1f),
                $"Target: angle {target.Angle}° ±{target.Tolerance}, index {target.Index + 1}");
            float dev = Math.Abs(_be.CurrentAngle - target.Angle);
            string status = dev <= target.Tolerance
                ? "  in tolerance"
                : (dev <= target.Tolerance * 2 ? "  degraded" : "  out of range");
            ImGui.Text($"Current: angle {_be.CurrentAngle:F1}°, index {_be.SelectedIndex + 1}{status}");
        }

        private void DrawAngleSlider()
        {
            bool changed = ImGui.SliderFloat("Angle (deg)", ref _angleSliderValue, 0f, 90f, "%.1f");
            bool dragging = ImGui.IsItemActive();
            if (changed && Math.Abs(_angleSliderValue - _lastSentAngle) > 0.25f)
            {
                SendSetAngle(_angleSliderValue);
                _lastSentAngle = _angleSliderValue;
            }
            if (!dragging && Math.Abs(_be.CurrentAngle - _angleSliderValue) > 0.5f)
            {
                _angleSliderValue = _be.CurrentAngle;
                _lastSentAngle = _be.CurrentAngle;
            }
        }

        private void DrawIndexButtons()
        {
            var recipe = _be.GetRecipe();
            int res = recipe?.IndexResolution ?? 16;
            ImGui.Text("Index:");

            var list = _be.GetStageFacets();
            var doneQuality = new Dictionary<int, float>();
            int previewTarget = -1;
            var upcoming = new HashSet<int>();
            if (recipe != null && list != null)
            {
                int[] cutFacets = _be.GetCutFacets();
                float[] results = _be.GetFacetResults();
                var doneSet = new HashSet<int>(cutFacets);

                // Done — color by quality of the matched cut.
                for (int i = 0; i < cutFacets.Length; i++)
                {
                    int facetListIdx = cutFacets[i];
                    if (facetListIdx < 0 || facetListIdx >= list.Count) continue;
                    int idx = list[facetListIdx].Index;
                    float q = i < results.Length ? results[i] : 0f;
                    if (!doneQuality.TryGetValue(idx, out float existing) || q > existing)
                        doneQuality[idx] = q;
                }

                // Upcoming = every recipe facet not yet matched.
                for (int j = 0; j < list.Count; j++)
                {
                    if (!doneSet.Contains(j)) upcoming.Add(list[j].Index);
                }

                // Preview match — what would be consumed if player cut now.
                var preview = _be.GetCurrentTarget();
                if (preview != null) previewTarget = preview.Index;
            }

            Vector4 colDoneGood = new(0.20f, 0.85f, 0.30f, 1f);
            Vector4 colDoneOk   = new(0.95f, 0.85f, 0.20f, 1f);
            Vector4 colDoneBad  = new(0.95f, 0.25f, 0.20f, 1f);
            Vector4 colTarget   = new(1.00f, 0.70f, 0.20f, 1f);
            Vector4 colUpcoming = new(0.55f, 0.40f, 0.15f, 1f);
            Vector4 colUnused   = new(0.20f, 0.20f, 0.20f, 1f);
            Vector4 colSelBorder = new(0.40f, 0.70f, 1.00f, 1f);

            int currentTarget = previewTarget;

            for (int i = 0; i < res; i++)
            {
                if (i % 8 != 0) ImGui.SameLine();

                Vector4 col;
                if (doneQuality.TryGetValue(i, out float q))
                    col = q >= 0.85f ? colDoneGood : (q >= 0.5f ? colDoneOk : colDoneBad);
                else if (i == currentTarget) col = colTarget;
                else if (upcoming.Contains(i)) col = colUpcoming;
                else col = colUnused;

                bool isSel = i == _be.SelectedIndex;
                ImGui.PushStyleColor(ImGuiCol.Button, col);
                if (isSel)
                {
                    ImGui.PushStyleColor(ImGuiCol.Border, colSelBorder);
                    ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 2f);
                }

                if (ImGui.Button($"{i + 1:D2}##idx{i}", new Vector2(48, 30)))
                {
                    if (i != _lastSentIndex)
                    {
                        SendSetIndex(i);
                        _lastSentIndex = i;
                    }
                }

                if (isSel) { ImGui.PopStyleVar(); ImGui.PopStyleColor(); }
                ImGui.PopStyleColor();
            }
        }

        private void DrawActionButtons()
        {
            ImGui.Separator();
            string progress = _be.AssemblyProgress;
            bool hasRecipe = !string.IsNullOrEmpty(_be.AssemblyRecipe);
            bool canCut = hasRecipe && progress == CANJWConstants.PROGRESS_CUTTING;
            bool canPolish = hasRecipe && progress == CANJWConstants.PROGRESS_POLISHING;

            if (!canCut) ImGui.BeginDisabled();
            if (ImGui.Button("Cut Facet", new Vector2(120, 36)))
                _capi.Network.SendBlockEntityPacket(_pos, BELapidaryBench.PACKET_CUT_FACET);
            if (!canCut) ImGui.EndDisabled();

            ImGui.SameLine();

            if (!canPolish) ImGui.BeginDisabled();
            if (ImGui.Button("Polish", new Vector2(120, 36)))
                _capi.Network.SendBlockEntityPacket(_pos, BELapidaryBench.PACKET_DO_POLISH);
            if (!canPolish) ImGui.EndDisabled();
        }

        private void DrawHistoryBars()
        {
            float[] results = _be.GetFacetResults();
            if (results.Length == 0) return;
            ImGui.Text("Cut history:");
            for (int i = 0; i < results.Length; i++)
            {
                float q = results[i];
                Vector4 col = q >= 0.85f ? new Vector4(0.2f, 0.85f, 0.3f, 1f)
                            : q >= 0.5f ? new Vector4(0.95f, 0.85f, 0.2f, 1f)
                                        : new Vector4(0.95f, 0.25f, 0.2f, 1f);
                ImGui.PushStyleColor(ImGuiCol.PlotHistogram, col);
                ImGui.ProgressBar(q, new Vector2(60, 14), $"{q:F2}");
                ImGui.PopStyleColor();
                if ((i + 1) % 8 != 0 && i != results.Length - 1) ImGui.SameLine();
            }
        }

        private void SendSetAngle(float angle)
        {
            using var ms = new MemoryStream();
            using (var bw = new BinaryWriter(ms)) bw.Write(angle);
            _capi.Network.SendBlockEntityPacket(_pos, BELapidaryBench.PACKET_SET_ANGLE, ms.ToArray());
        }

        private void SendSetIndex(int index)
        {
            using var ms = new MemoryStream();
            using (var bw = new BinaryWriter(ms)) bw.Write(index);
            _capi.Network.SendBlockEntityPacket(_pos, BELapidaryBench.PACKET_SET_INDEX, ms.ToArray());
        }

        private static byte[] BitConvert(int v)
        {
            using var ms = new MemoryStream();
            using (var bw = new BinaryWriter(ms)) bw.Write(v);
            return ms.ToArray();
        }

        private void SendInvPacket(object packet)
            => _capi.Network.SendBlockEntityPacket(_pos.X, _pos.Y, _pos.Z, packet);
    }
}
