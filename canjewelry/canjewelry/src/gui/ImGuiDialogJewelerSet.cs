using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.RegularExpressions;
using canjewelry.src.CB;
using canjewelry.src.inventories;
using ImGuiNET;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using VSImGui.API;

namespace canjewelry.src.gui
{
    public class ImGuiDialogJewelerSet : ImGuiDialogBase
    {
        private readonly ICoreClientAPI _capi;
        private readonly InventoryJewelerSet _inv;
        private readonly BlockPos _pos;

        private ItemIconAtlas _atlas;
        private ImGuiSlotRenderer _slotRenderer;
        private ImGuiInventoryGrid _grid;
        private JewelerItemPreview _preview;
        private bool _gpuReady;

        // Reused gem display stacks: key = "{gemType}|{gemSize}|{cuttingType}".
        // Tooltip pulls live buffs straight from slotTree, so the cached stack only
        // needs cutting type for naming — buffs are irrelevant to the name/icon.
        private readonly Dictionary<string, ItemStack> _gemDisplayCache = new();

        public bool IsOpen => Opened;

        // ── theme (matches CANJewelryGuideDialog) ────────────────────────────────
        private static readonly Vector4 Col_Header = new(1f,    0.85f, 0.40f, 1f);
        private static readonly Vector4 Col_Gold   = new(0.82f, 0.70f, 0.35f, 1f);
        private static readonly Vector4 Col_T1     = new(0.18f, 0.88f, 0.27f, 1f);
        private static readonly Vector4 Col_T2     = new(0.17f, 0.44f, 0.97f, 1f);
        private static readonly Vector4 Col_T3     = new(0.57f, 0.08f, 0.79f, 1f);

        // dim tints used as slot-background tints (alpha 0.3)
        private static readonly Vector4 Tint_T1     = new(0.18f, 0.88f, 0.27f, 0.30f);
        private static readonly Vector4 Tint_T2     = new(0.17f, 0.44f, 0.97f, 0.30f);
        private static readonly Vector4 Tint_T3     = new(0.57f, 0.08f, 0.79f, 0.30f);
        private static readonly Vector4 Tint_Locked = new(0.15f, 0.15f, 0.15f, 0.45f);
        private static readonly Vector4 Tint_Ok     = new(0.18f, 0.88f, 0.27f, 0.45f);
        private static readonly Vector4 Tint_Bad    = new(0.95f, 0.20f, 0.18f, 0.45f);

        // bronze panel & button palette
        private static readonly Vector4 PanelBg     = new(0.18f, 0.13f, 0.08f, 1.0f);
        private static readonly Vector4 BtnAddSocket    = new(0.18f, 0.45f, 0.20f, 1.0f);
        private static readonly Vector4 BtnAddSocketHv  = new(0.30f, 0.65f, 0.32f, 1.0f);
        private static readonly Vector4 BtnAddGem       = new(0.55f, 0.32f, 0.10f, 1.0f);
        private static readonly Vector4 BtnAddGemHv     = new(0.78f, 0.50f, 0.18f, 1.0f);
        private static readonly Vector4 BtnReplace      = new(0.32f, 0.30f, 0.55f, 1.0f);
        private static readonly Vector4 BtnReplaceHv    = new(0.50f, 0.45f, 0.80f, 1.0f);
        private static readonly Vector4 BtnExtract      = new(0.55f, 0.20f, 0.20f, 1.0f);
        private static readonly Vector4 BtnExtractHv    = new(0.78f, 0.32f, 0.32f, 1.0f);

        // Stored as a field so the same delegate instance can be passed to both += and -=,
        // otherwise the unsubscribe is a no-op and the inventory keeps a reference to us.
        private Action<int> _slotModifiedHandler;

        // ImGui InputText buffer for the inscription field; persists across frames so the user
        // can keep typing. Cleared when the inscribe button fires.
        private string _inscriptionBuffer = "";

        // Cached result of FireCanInscribe — refreshed when slot 0 changes.
        // Defaults to true so vanilla (no companion) behavior is unchanged.
        private bool _canInscribe = true;

        public ImGuiDialogJewelerSet(ICoreClientAPI capi, InventoryJewelerSet inventory, BlockPos pos) : base(capi)
        {
            _capi = capi;
            _inv  = inventory;
            _pos  = pos;
        }

        private void EnsureGpuReady()
        {
            if (_gpuReady) return;
            _atlas        = new ItemIconAtlas(_capi);
            _slotRenderer = new ImGuiSlotRenderer(_capi, slotSize: 52);
            _grid         = new ImGuiInventoryGrid(_capi, _slotRenderer, _atlas, SendInvPacket);
            _grid.SetInventory(_inv);
            _preview      = new JewelerItemPreview(_capi);
            _gpuReady = true;
        }

        private void OnSlotModified(int slotId)
        {
            if (slotId == 0)
            {
                _inv[0].Itemstack?.TempAttributes.RemoveAttribute("meshRefId");
                var ev = new src.integration.CanInscribeEvent
                {
                    Player  = _capi.World.Player,
                    Jewelry = _inv[0].Itemstack,
                };
                canjewelry.Instance?.FireCanInscribe(ev);
                _canInscribe = ev.Allowed;
            }
        }

        protected override bool OnOpen()
        {
            EnsureGpuReady();
            if (_slotModifiedHandler == null)
            {
                _slotModifiedHandler = OnSlotModified;
                _inv.SlotModified += _slotModifiedHandler;
            }
            _capi.World.Player.InventoryManager.OpenInventory(_inv);
            return true;
        }

        protected override bool OnClose()
        {
            UnhookSlotModified();
            _capi.Network.SendBlockEntityPacket(_pos, 1001);
            _capi.World.Player.InventoryManager.CloseInventory(_inv);
            ImGuiInventoryGrid.SuppressMouseDrop = false;
            return true;
        }

        private void UnhookSlotModified()
        {
            if (_slotModifiedHandler == null) return;
            _inv.SlotModified -= _slotModifiedHandler;
            _slotModifiedHandler = null;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                UnhookSlotModified();
                _atlas?.Dispose();
                _slotRenderer?.Dispose();
                _preview?.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override CallbackGUIStatus Draw(float deltaSeconds)
        {
            if (!Opened) return CallbackGUIStatus.Closed;
            _preview?.Render(_inv[0].Itemstack);
            if (!OnDraw()) Close();
            return Opened ? CallbackGUIStatus.GrabMouse : CallbackGUIStatus.Closed;
        }

        protected override bool OnDraw()
        {
            if (ImGui.IsKeyPressed(ImGuiKey.Escape, false)) return false;

            bool open = true;
            ImGui.SetNextWindowSize(new Vector2(760, 520), ImGuiCond.FirstUseEver);

            ImGui.PushStyleColor(ImGuiCol.Border, Col_Gold);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 5f);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);

            if (!ImGui.Begin(Lang.Get("canjewelry:jewelerset-title") + "##canjewelerset", ref open,
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            {
                ImGui.End();
                ImGui.PopStyleVar(2);
                ImGui.PopStyleColor();
                return open;
            }

            DrawContent();

            ImGui.End();
            ImGui.PopStyleVar(2);
            ImGui.PopStyleColor();
            return open;
        }

        // ── header helper (matches guide dialog) ─────────────────────────────────
        private static void H(string title)
        {
            ImGui.TextColored(Col_Header, title);
            ImGui.Separator();
            ImGui.Spacing();
        }

        // ── main layout ──────────────────────────────────────────────────────────

        private void DrawContent()
        {
            int sz = _slotRenderer.SlotSize;
            ItemStack jewelry = _inv[0].Itemstack;

            float previewW = JewelerItemPreview.FboSize;
            float contentW = ImGui.GetContentRegionAvail().X;
            float rightW   = contentW - previewW - ImGui.GetStyle().ItemSpacing.X * 2 - 16f;

            ImGui.BeginGroup();
            DrawPreviewPanel(jewelry, sz, previewW);
            ImGui.EndGroup();

            ImGui.SameLine();

            ImGui.BeginGroup();
            DrawControlsPanel(jewelry, sz, rightW);
            ImGui.EndGroup();
        }

        private void DrawPreviewPanel(ItemStack jewelry, int sz, float previewW)
        {
            var previewSize = new Vector2(previewW, previewW);
            var dl          = ImGui.GetWindowDrawList();
            Vector2 pos     = ImGui.GetCursorScreenPos();

            // dark backdrop + gold border frame
            dl.AddRectFilled(pos, pos + previewSize,
                ImGui.GetColorU32(new Vector4(0.15f, 0.11f, 0.07f, 1f)), 4f);

            if (jewelry?.Collectible != null && _preview != null && _preview.TextureId > 0)
            {
                dl.AddImage((IntPtr)_preview.TextureId, pos, pos + previewSize,
                    new Vector2(0, 0), new Vector2(1, 1));
            }
            else if (jewelry == null)
            {
                var hint = Lang.Get("canjewelry:jewelerset-no-item");
                var ts   = ImGui.CalcTextSize(hint);
                dl.AddText(pos + (previewSize - ts) * 0.5f,
                    ImGui.GetColorU32(new Vector4(0.5f, 0.5f, 0.5f, 1f)), hint);
            }

            dl.AddRect(pos, pos + previewSize, ImGui.GetColorU32(Col_Gold), 4f, ImDrawFlags.None, 1.5f);

            ImGui.InvisibleButton("##preview_drag", previewSize);
            if (ImGui.IsItemActive() && jewelry?.Collectible != null && _preview != null)
            {
                var delta = ImGui.GetIO().MouseDelta;
                _preview.RotationY -= delta.X * 0.5f;
                _preview.RotationX -= delta.Y * 0.5f;
            }

            if (ImGui.IsItemHovered() && jewelry?.Collectible != null)
                ImGui.SetTooltip(Lang.Get("canjewelry:jewelerset-drag-to-rotate"));

            ImGui.Spacing();
            ImGui.TextColored(Col_Header, Lang.Get("canjewelry:jewelerset-item"));
            ImGui.Separator();
            ImGui.Spacing();

            _grid.DrawSingleSlot(0);
            if (jewelry != null)
            {
                ImGui.SameLine();
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (sz - ImGui.GetTextLineHeight()) * 0.5f);
                ImGui.TextColored(Col_Gold, PlainName(jewelry));
            }
            else
            {
                ImGui.SameLine();
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (sz - ImGui.GetTextLineHeight()) * 0.5f);
                ImGui.TextDisabled(Lang.Get("canjewelry:jewelerset-place-item-slot-hint"));
            }

            if (jewelry != null)
            {
                ImGui.Spacing();
                DrawStatsSummary(jewelry);
            }
        }

        private void DrawStatsSummary(ItemStack jewelry)
        {
            ImGui.TextColored(Col_Header, Lang.Get("canjewelry:jewelerset-stats"));
            ImGui.Separator();
            ImGui.Spacing();

            int maxDur = jewelry.Collectible.GetMaxDurability(jewelry);
            int curDur = jewelry.Attributes.GetInt("durability", maxDur);
            if (maxDur > 0)
            {
                ImGui.TextColored(Col_Gold, Lang.Get("canjewelry:jewelerset-durability") + ":");
                ImGui.SameLine();
                ImGui.Text($"{curDur} / {maxDur}");
            }

            // Aggregate buff name → summed value across all sockets
            ITreeAttribute encTree = jewelry.Attributes?.GetTreeAttribute(CANJWConstants.ITEM_ENCRUSTED_STRING);
            var totals = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            if (encTree != null)
            {
                int maxSockets = EncrustableCB.GetMaxAmountSockets(jewelry);
                for (int i = 0; i < maxSockets; i++)
                {
                    var slotTree = encTree.GetTreeAttribute("slot" + i);
                    if (slotTree == null) continue;
                    var names  = (slotTree[CANJWConstants.ENCRUSTABLE_BUFFS_NAMES]  as StringArrayAttribute)?.value;
                    var values = (slotTree[CANJWConstants.ENCRUSTABLE_BUFFS_VALUES] as FloatArrayAttribute)?.value;
                    if (names == null || values == null) continue;
                    for (int b = 0; b < names.Length && b < values.Length; b++)
                    {
                        // candurability is already reflected in max durability above; skip dup line
                        if (string.Equals(names[b], "candurability", StringComparison.OrdinalIgnoreCase)) continue;
                        totals.TryGetValue(names[b], out float prev);
                        totals[names[b]] = prev + values[b];
                    }
                }
            }

            if (totals.Count == 0)
            {
                ImGui.TextDisabled(Lang.Get("canjewelry:jewelerset-no-buffs"));
                return;
            }

            foreach (var kv in totals)
            {
                ImGui.TextColored(GemColor(kv.Key), kv.Key);
                ImGui.SameLine();
                ImGui.Text(FormatBuffValue(kv.Key, kv.Value));
            }
        }

        private static string FormatBuffValue(string buffName, float v)
        {
            if (string.Equals(buffName, "maxhealthExtraPoints", StringComparison.OrdinalIgnoreCase))
            {
                return (v >= 0 ? "+" : "") + $"{v:0.###}";
            }
            float pct = (float)Math.Round(v * 100.0, 3);
            return (pct >= 0 ? "+" : "") + $"{pct:0.###}%%";
        }

        private void DrawControlsPanel(ItemStack jewelry, int sz, float rightW)
        {
            ImGui.PushStyleColor(ImGuiCol.ChildBg, PanelBg);
            ImGui.PushStyleColor(ImGuiCol.Border,  Col_Gold);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding,    new Vector2(12, 10));
            ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize,  1f);
            ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding,    5f);

            ImGui.BeginChild("##canjs_controls", new Vector2(rightW, 0), true);

            if (jewelry == null || !jewelry.Collectible.HasBehavior<EncrustableCB>())
            {
                ImGui.Spacing();
                ImGui.TextDisabled(Lang.Get("canjewelry:jewelerset-place-item-hint"));
                EndControlsPanel();
                return;
            }

            int maxSockets = EncrustableCB.GetMaxAmountSockets(jewelry);
            if (maxSockets <= 0)
            {
                ImGui.Spacing();
                ImGui.TextDisabled(Lang.Get("canjewelry:jewelerset-no-sockets"));
                EndControlsPanel();
                return;
            }

            int[] tiers = EncrustableCB.GetSocketsTiers(jewelry);
            ITreeAttribute encTree = jewelry.Attributes?.GetTreeAttribute(CANJWConstants.ITEM_ENCRUSTED_STRING);

            float colW = sz + 4f;
            var tblFlags = ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoPadInnerX
                         | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.RowBg;

            // ── Sockets + Gems (one row per socket) ───────────────────────────────
            H(Lang.Get("canjewelry:jewelerset-sockets"));
            if (ImGui.BeginTable("##sockgems", 4, tblFlags))
            {
                ImGui.TableSetupColumn("sock",  ImGuiTableColumnFlags.WidthFixed,   colW);
                ImGui.TableSetupColumn("gem",   ImGuiTableColumnFlags.WidthFixed,   colW);
                ImGui.TableSetupColumn("input", ImGuiTableColumnFlags.WidthFixed,   colW);
                ImGui.TableSetupColumn("btns",  ImGuiTableColumnFlags.WidthStretch);
                for (int i = 0; i < maxSockets; i++)
                {
                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0); DrawSocketCell(i, encTree, tiers, sz);
                    ImGui.TableSetColumnIndex(1);
                    ImGui.TableSetColumnIndex(2);
                    ImGui.TableSetColumnIndex(3);
                    DrawGemRow(i, encTree, sz);
                }
                ImGui.EndTable();
            }

            ImGui.Spacing();
            ImGui.Spacing();

            // ── Compatible gems ───────────────────────────────────────────────────
            DrawCompatibleGems(jewelry);

            ImGui.Spacing();
            ImGui.Spacing();

            // ── Inscription (permanent) ───────────────────────────────────────────
            DrawInscriptionSection(jewelry);

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // tier legend
            ImGui.TextColored(Col_T1, "T1");
            ImGui.SameLine(); ImGui.TextDisabled(Lang.Get("canjewelry:jewelerset-tier1-desc"));
            ImGui.SameLine(0, 18);
            ImGui.TextColored(Col_T2, "T2");
            ImGui.SameLine(); ImGui.TextDisabled(Lang.Get("canjewelry:jewelerset-tier2-desc"));
            ImGui.SameLine(0, 18);
            ImGui.TextColored(Col_T3, "T3");
            ImGui.SameLine(); ImGui.TextDisabled(Lang.Get("canjewelry:jewelerset-tier3-desc"));

            EndControlsPanel();
        }

        private void DrawCompatibleGems(ItemStack jewelry)
        {
            var types = GetAvailableGemTypes(jewelry);
            if (types.Count == 0) return;

            H(Lang.Get("canjewelry:jewelerset-compatible-gems"));

            float available = ImGui.GetContentRegionAvail().X;
            float lineX = 0f;
            for (int i = 0; i < types.Count; i++)
            {
                string name = Capitalize(types[i]);
                var color = GemColor(types[i]);
                Vector2 ts = ImGui.CalcTextSize(name);
                if (i > 0)
                {
                    if (lineX + ts.X + 12f < available)
                        ImGui.SameLine(0, 12);
                    else
                        lineX = 0f;
                }
                ImGui.TextColored(color, name);
                lineX += ts.X + 12f;
            }
        }

        private static List<string> GetAvailableGemTypes(ItemStack stack)
        {
            var res = new List<string>();
            if (stack?.Collectible == null) return res;
            string itemCode = stack.Collectible.Code.Path;
            foreach (var pair in canjewelry.config.buffNameToPossibleItem)
            {
                foreach (var sub in pair.Value)
                {
                    if (WildcardUtil.Match("*" + sub + "*", itemCode))
                    {
                        if (!res.Contains(pair.Key)) res.Add(pair.Key);
                        break;
                    }
                }
            }
            res.Sort(StringComparer.OrdinalIgnoreCase);
            return res;
        }

        // Hash-derived hue (matches CANJewelryGuideDialog.ColorForStat) so each gem name
        // gets a stable, distinct color across sessions.
        private static Vector4 GemColor(string name)
        {
            if (string.IsNullOrEmpty(name)) return new Vector4(1, 1, 1, 1);
            uint h = 2166136261u;
            foreach (char c in name) h = (h ^ c) * 16777619u;
            float hue = ((h * 2654435761u) % 360u) / 360f;
            ImGui.ColorConvertHSVtoRGB(hue, 0.65f, 1.0f, out float r, out float g, out float b);
            return new Vector4(r, g, b, 1f);
        }

        private static string Capitalize(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..];

        // VS item names sometimes embed rich-text tags (<font>, <strong>, etc.) that ImGui
        // would render literally. Strip them for plain-text rendering.
        private static readonly Regex RichTextTag = new("<[^>]+>", RegexOptions.Compiled);
        private static string PlainName(ItemStack stack) =>
            stack == null ? "" : RichTextTag.Replace(stack.GetName(), "").Trim();

        private static void EndControlsPanel()
        {
            ImGui.EndChild();
            ImGui.PopStyleVar(3);
            ImGui.PopStyleColor(2);
        }

        // ── per-socket cells ─────────────────────────────────────────────────────

        private void DrawSocketCell(int i, ITreeAttribute encTree, int[] tiers, int sz)
        {
            bool hasSocket = encTree != null && encTree.HasAttribute("slot" + i);
            int tier = tiers != null && i < tiers.Length ? tiers[i] : 1;

            // slot tier label centered above the cell
            string tierLabel = $"T{tier}";
            float labelW = ImGui.CalcTextSize(tierLabel).X;
            float startX = ImGui.GetCursorPosX();
            ImGui.SetCursorPosX(startX + (sz - labelW) * 0.5f);
            ImGui.TextColored(TierColor(tier), tierLabel);
            ImGui.SetCursorPosX(startX);

            if (hasSocket)
            {
                int socketTier = encTree.GetTreeAttribute("slot" + i).GetInt(CANJWConstants.ADDED_SOCKET_TYPE);
                string socketCode = FindSocketCodeByTier(socketTier);
                ItemStack socketStack = socketCode != null
                    ? new ItemStack(_capi.World.GetItem(new AssetLocation(socketCode)))
                    : null;
                DrawDisplaySlot(socketStack, sz, TierTint(socketTier));
            }
            else
            {
                _grid.DrawSingleSlot(5 + i, TierTint(tier));
                PushButton(BtnAddSocket, BtnAddSocketHv);
                if (ImGui.Button($"+##sock{i}", new Vector2(sz, 22)))
                    SendAddSocket(i, 5 + i);
                PopButton();
            }
        }

        // Fills columns 1-3 for the horizontal socket layout.
        // labelOffset matches the tier-label height drawn in column 0 so all slots align.
        private void DrawGemRow(int i, ITreeAttribute encTree, int sz)
        {
            float labelOffset = ImGui.GetTextLineHeightWithSpacing();

            bool hasSocket = encTree != null && encTree.HasAttribute("slot" + i);
            ITreeAttribute slotTree = hasSocket ? encTree.GetTreeAttribute("slot" + i) : null;
            string gemType = slotTree?.GetString(CANJWConstants.GEM_TYPE_IN_SOCKET, "") ?? "";
            bool hasGem = gemType != "";

            if (hasGem)
            {
                int gemSizeInt = slotTree.GetInt(CANJWConstants.ENCRUSTED_GEM_SIZE);
                string gemSize = gemSizeInt == 3 ? "exquisite" : gemSizeInt == 2 ? "flawless" : "normal";
                string cutting = slotTree.GetString(CANJWConstants.CUTTING_TYPE, CANJWConstants.CUTTING_ROUND);
                ItemStack gemStack = GetOrCreateGemDisplay(gemType, gemSize, cutting);
                ITreeAttribute capturedSlotTree = slotTree;

                ImGui.TableSetColumnIndex(1);
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + labelOffset);
                DrawDisplaySlot(gemStack, sz, default, () => DrawEncrustedGemTooltip(capturedSlotTree));

                int socketTier = slotTree.GetInt(CANJWConstants.ADDED_SOCKET_TYPE);
                Vector4 inputTint = CompatibilityTint(_inv[1 + i].Itemstack, _inv[0].Itemstack, socketTier);
                ImGui.TableSetColumnIndex(2);
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + labelOffset);
                _grid.DrawSingleSlot(1 + i, inputTint);

                ImGui.TableSetColumnIndex(3);
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + labelOffset);

                string swapLabel    = Lang.Get("canjewelry:jewelerset-swap");
                string extractLabel = Lang.Get("canjewelry:jewelerset-extract");
                float  padding      = ImGui.GetStyle().FramePadding.X * 2 + 16;
                float  swapW        = ImGui.CalcTextSize(swapLabel).X + padding;

                PushButton(BtnReplace, BtnReplaceHv);
                if (ImGui.Button($"{swapLabel}##gem{i}", new Vector2(swapW, 0)))
                    SendAddGem(i, 1 + i);
                PopButton();

                var canEv = new src.integration.CanExtractEvent
                {
                    Player      = _capi.World.Player,
                    Jewelry     = _inv[0].Itemstack,
                    SocketIndex = i,
                };
                canjewelry.Instance?.FireCanExtract(canEv);
                if (canEv.Allowed)
                {
                    float extractW = ImGui.CalcTextSize(extractLabel).X + padding;
                    PushButton(BtnExtract, BtnExtractHv);
                    if (ImGui.Button($"{extractLabel}##gem-ex{i}", new Vector2(extractW, 0)))
                        SendExtract(i, 1 + i);
                    PopButton();
                    if (ImGui.IsItemHovered())
                    {
                        int gemPct   = (int)(canjewelry.config.gemExtractionReturnChance * 100f);
                        int breakPct = (int)(canjewelry.config.jewelryBreakOnExtractionChance * 100f);
                        ImGui.SetTooltip($"{gemPct}%% chance to recover gem\n{breakPct}%% chance to break item");
                    }
                }
            }
            else if (hasSocket)
            {
                int socketTier = encTree.GetTreeAttribute("slot" + i).GetInt(CANJWConstants.ADDED_SOCKET_TYPE);
                Vector4 inputTint = CompatibilityTint(_inv[1 + i].Itemstack, _inv[0].Itemstack, socketTier);
                ImGui.TableSetColumnIndex(2);
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + labelOffset);
                _grid.DrawSingleSlot(1 + i, inputTint);

                ImGui.TableSetColumnIndex(3);
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + labelOffset);
                float addGemW = ImGui.CalcTextSize("+").X + ImGui.GetStyle().FramePadding.X * 2 + 16;
                PushButton(BtnAddGem, BtnAddGemHv);
                if (ImGui.Button($"+##gem{i}", new Vector2(addGemW, 0)))
                    SendAddGem(i, 1 + i);
                PopButton();
            }
        }

        private void DrawGemCell(int i, ITreeAttribute encTree, int sz)
        {
            bool hasSocket = encTree != null && encTree.HasAttribute("slot" + i);
            ITreeAttribute slotTree = hasSocket ? encTree.GetTreeAttribute("slot" + i) : null;
            string gemType = slotTree?.GetString(CANJWConstants.GEM_TYPE_IN_SOCKET, "") ?? "";
            bool hasGem = gemType != "";

            if (hasGem)
            {
                int gemSizeInt = slotTree.GetInt(CANJWConstants.ENCRUSTED_GEM_SIZE);
                string gemSize = gemSizeInt == 3 ? "exquisite" : gemSizeInt == 2 ? "flawless" : "normal";
                string cutting = slotTree.GetString(CANJWConstants.CUTTING_TYPE, CANJWConstants.CUTTING_ROUND);
                ItemStack gemStack = GetOrCreateGemDisplay(gemType, gemSize, cutting);

                ITreeAttribute capturedSlotTree = slotTree;
                DrawDisplaySlot(gemStack, sz, default, () => DrawEncrustedGemTooltip(capturedSlotTree));

                ImGui.Spacing();
                int socketTier = slotTree.GetInt(CANJWConstants.ADDED_SOCKET_TYPE);
                Vector4 inputTint = CompatibilityTint(_inv[1 + i].Itemstack, _inv[0].Itemstack, socketTier);
                _grid.DrawSingleSlot(1 + i, inputTint);
                PushButton(BtnReplace, BtnReplaceHv);
                if (ImGui.Button($"<>##gem{i}", new Vector2(sz * 0.5f - 2, 22)))
                    SendAddGem(i, 1 + i);
                PopButton();
                if (ImGui.IsItemHovered()) ImGui.SetTooltip(Lang.Get("canjewelry:jewelerset-swap"));

                var canEv = new src.integration.CanExtractEvent
                {
                    Player      = _capi.World.Player,
                    Jewelry     = _inv[0].Itemstack,
                    SocketIndex = i,
                };
                canjewelry.Instance?.FireCanExtract(canEv);
                if (canEv.Allowed)
                {
                    ImGui.SameLine(0, 4);
                    PushButton(BtnExtract, BtnExtractHv);
                    if (ImGui.Button($"^##gem-ex{i}", new Vector2(sz * 0.5f - 2, 22)))
                        SendExtract(i, 1 + i);
                    PopButton();
                    if (ImGui.IsItemHovered())
                    {
                        int pct = (int)(canjewelry.config.gemExtractionReturnChance * 100f);
                        ImGui.SetTooltip($"{Lang.Get("canjewelry:jewelerset-extract")} ({pct}%% chance to recover)");
                    }
                }
            }
            else if (hasSocket)
            {
                int socketTier = encTree.GetTreeAttribute("slot" + i).GetInt(CANJWConstants.ADDED_SOCKET_TYPE);
                Vector4 inputTint = CompatibilityTint(_inv[1 + i].Itemstack, _inv[0].Itemstack, socketTier);
                _grid.DrawSingleSlot(1 + i, inputTint);
                PushButton(BtnAddGem, BtnAddGemHv);
                if (ImGui.Button($"+##gem{i}", new Vector2(sz, 22)))
                    SendAddGem(i, 1 + i);
                PopButton();
            }
            else
            {
                DrawDisplaySlot(null, sz, Tint_Locked);
            }
        }

        private void DrawInscriptionSection(ItemStack jewelry)
        {
            H(Lang.Get("canjewelry:jewelerset-inscription"));

            string existing = jewelry?.Attributes?.GetString(CANJWConstants.INSCRIPTION);
            if (!string.IsNullOrEmpty(existing))
            {
                ImGui.TextColored(Col_Gold, "\"" + existing + "\"");
                return;
            }

            if (!_canInscribe)
            {
                ImGui.TextDisabled(Lang.Get("canjewelry:jewelerset-inscribe-locked"));
                return;
            }

            ImGui.SetNextItemWidth(280);
            ImGui.InputText("##inscriptionInput", ref _inscriptionBuffer, (uint)CANJWConstants.INSCRIPTION_MAX_LEN);
            ImGui.SameLine();

            bool valid = !string.IsNullOrWhiteSpace(_inscriptionBuffer);
            if (!valid) ImGui.BeginDisabled();
            PushButton(BtnAddSocket, BtnAddSocketHv);
            if (ImGui.Button(Lang.Get("canjewelry:jewelerset-inscribe-btn") + "##inscribe", new Vector2(120, 22)))
            {
                SendInscribe(_inscriptionBuffer);
                _inscriptionBuffer = "";
            }
            PopButton();
            if (!valid) ImGui.EndDisabled();

            ImGui.TextDisabled(Lang.Get("canjewelry:jewelerset-inscription-hint"));
        }

        private void SendInscribe(string text)
        {
            using var ms = new MemoryStream();
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(text ?? "");
            }
            _capi.Network.SendBlockEntityPacket(_pos, 1007, ms.ToArray());
        }

        // ── draw helpers ─────────────────────────────────────────────────────────

        private void DrawDisplaySlot(ItemStack stack, int sz, Vector4 tint = default, Action customTooltip = null)
        {
            Vector2 pos = ImGui.GetCursorScreenPos();
            var dl = ImGui.GetWindowDrawList();

            _slotRenderer.DrawSlotBackground(pos, dl);

            if (tint.W > 0f)
                dl.AddRectFilled(pos, pos + new Vector2(sz, sz), ImGui.GetColorU32(tint), 2f);

            if (stack?.Collectible != null)
            {
                float iconSz = sz * 0.75f;
                float offset = (sz - iconSz) * 0.5f;
                _atlas.DrawToList(stack, pos + new Vector2(offset, offset), new Vector2(iconSz, iconSz), dl);
            }

            ImGui.SetCursorScreenPos(pos);
            ImGui.InvisibleButton($"disp_{(int)pos.X}_{(int)pos.Y}", new Vector2(sz, sz));

            if (stack?.Collectible != null && ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.TextColored(Col_Gold, PlainName(stack));
                customTooltip?.Invoke();
                ImGui.EndTooltip();
            }
        }

        private void DrawEncrustedGemTooltip(ITreeAttribute slotTree)
        {
            string cutting = slotTree.GetString(CANJWConstants.CUTTING_TYPE, CANJWConstants.CUTTING_ROUND);
            ImGui.Separator();
            ImGui.TextColored(Col_Header, Lang.Get("canjewelry:jewelerset-cut") + ": ");
            ImGui.SameLine();
            ImGui.Text(Lang.Get("canjewelry:cut-gem-cutting-type-" + cutting));

            var names  = (slotTree[CANJWConstants.ENCRUSTABLE_BUFFS_NAMES]  as StringArrayAttribute)?.value;
            var values = (slotTree[CANJWConstants.ENCRUSTABLE_BUFFS_VALUES] as FloatArrayAttribute)?.value;
            if (names == null || values == null || names.Length == 0) return;

            ImGui.Spacing();
            ImGui.TextColored(Col_Header, Lang.Get("canjewelry:jewelerset-buffs") + ":");
            for (int i = 0; i < names.Length && i < values.Length; i++)
            {
                ImGui.TextColored(GemColor(names[i]), "  " + names[i]);
                ImGui.SameLine();
                ImGui.Text(FormatBuffValue(names[i], values[i]));
            }
        }

        private Vector4 CompatibilityTint(ItemStack gemStack, ItemStack jewelry, int socketTier)
        {
            if (gemStack?.Collectible == null) return default;
            return IsGemCompatible(gemStack, jewelry, socketTier) ? Tint_Ok : Tint_Bad;
        }

        private ItemStack GetOrCreateGemDisplay(string gemType, string gemSize, string cuttingType)
        {
            string key = gemType + "|" + gemSize + "|" + cuttingType;
            if (_gemDisplayCache.TryGetValue(key, out var cached)) return cached;

            var item = _capi.World.GetItem(new AssetLocation($"canjewelry:gem-cut-{gemSize}-{gemType}"));
            if (item == null) return null;
            var stack = new ItemStack(item);
            var tree  = new TreeAttribute();
            tree.SetString(CANJWConstants.CUTTING_TYPE, cuttingType);
            stack.Attributes[CANJWConstants.CUT_GEM_TREE] = tree;
            _gemDisplayCache[key] = stack;
            return stack;
        }

        private bool IsGemCompatible(ItemStack gemStack, ItemStack jewelry, int socketTier)
        {
            if (gemStack?.Collectible == null) return false;
            if (!gemStack.Collectible.Attributes?["canGemType"].Exists ?? true) return false;
            int gemTier = gemStack.Collectible.Attributes["canGemType"].AsInt();
            if (socketTier < gemTier) return false;
            if (gemStack.Attributes?.GetTreeAttribute(CANJWConstants.CUT_GEM_TREE) == null) return false;
            string gemType = gemStack.Collectible.Code.Path.Split('-')[^1];
            return EncrustableCB.canItemContainThisGem(gemType, jewelry);
        }

        private static void PushButton(Vector4 normal, Vector4 hovered)
        {
            ImGui.PushStyleColor(ImGuiCol.Button,        normal);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, hovered);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive,  normal);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4f);
        }

        private static void PopButton()
        {
            ImGui.PopStyleVar();
            ImGui.PopStyleColor(3);
        }

        // ── networking ───────────────────────────────────────────────────────────

        private void SendAddSocket(int socketSlot, int invSlot)
        {
            using var ms = new MemoryStream();
            using (var bw = new BinaryWriter(ms))
            {
                var tree = new TreeAttribute();
                tree.SetInt("selectedSocketSlot", socketSlot);
                tree.SetInt("selectedSlotNum", invSlot);
                tree.ToBytes(bw);
            }
            _capi.Network.SendBlockEntityPacket(_pos, 1004, ms.ToArray());
        }

        private void SendAddGem(int socketSlot, int invSlot)
        {
            using var ms = new MemoryStream();
            using (var bw = new BinaryWriter(ms))
            {
                var tree = new TreeAttribute();
                tree.SetInt("selectedSocketSlot", socketSlot);
                tree.SetInt("selectedSlotNum", invSlot);
                tree.ToBytes(bw);
            }
            _capi.Network.SendBlockEntityPacket(_pos, 1005, ms.ToArray());
        }

        private void SendExtract(int socketSlot, int invSlot)
        {
            using var ms = new MemoryStream();
            using (var bw = new BinaryWriter(ms))
            {
                var tree = new TreeAttribute();
                tree.SetInt("selectedSocketSlot", socketSlot);
                tree.SetInt("selectedSlotNum", invSlot);
                tree.ToBytes(bw);
            }
            _capi.Network.SendBlockEntityPacket(_pos, 1006, ms.ToArray());
        }

        private void SendInvPacket(object packet) =>
            _capi.Network.SendBlockEntityPacket(_pos.X, _pos.Y, _pos.Z, packet);

        // ── utilities ────────────────────────────────────────────────────────────

        private string FindSocketCodeByTier(int tier)
        {
            foreach (var kv in canjewelry.config.LevelOfSocketByType)
                if (kv.Value == tier) return kv.Key;
            return null;
        }

        private static Vector4 TierColor(int tier) => tier switch
        {
            1 => Col_T1,
            2 => Col_T2,
            3 => Col_T3,
            _ => Col_T1
        };

        private static Vector4 TierTint(int tier) => tier switch
        {
            1 => Tint_T1,
            2 => Tint_T2,
            3 => Tint_T3,
            _ => Tint_T1
        };
    }
}
