using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using ImGuiNET;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;
using VSImGui.API;

namespace canjewelry.src.gui
{
    public class CANJewelryGuideDialog : ImGuiDialogBase
    {
        public bool IsOpen => Opened;

        private readonly ICoreClientAPI _capi;
        private readonly InventoryGeneric _inv;
        private ItemIconAtlas _atlas;
        private ImGuiSlotRenderer _slotRenderer;
        private ImGuiInventoryGrid _grid;
        private bool _gpuReady     = false;
        private bool _jewelryLoaded = false;
        private bool _gemsLoaded    = false;

        private readonly Dictionary<string, int> _gemSlots = new();

        private record JewelryRow(string Key, string Label, int SocketCount, int[] Tiers);
        private readonly List<JewelryRow> _simpleRows = new();
        private readonly List<(string Key, string Label, Config.CustomVariantSocketsTiers cvst)> _cvstRows = new();

        private static readonly Vector4 Col_Header = new(1f, 0.85f, 0.4f, 1f);
        private static readonly Vector4 Col_Green  = new(0.18f, 0.88f, 0.27f, 1f);
        private static readonly Vector4 Col_Blue   = new(0.17f, 0.44f, 0.97f, 1f);
        private static readonly Vector4 Col_Purple = new(0.57f, 0.08f, 0.79f, 1f);
        private static readonly Vector4 Col_Orange = new(1f, 0.7f, 0.4f, 1f);

        public CANJewelryGuideDialog(ICoreClientAPI api) : base(api)
        {
            _capi = api;
            _inv  = new InventoryGeneric(140, "canguide", null, api, (_, inv) => new DisplayOnlyItemSlot(inv));
            // GPU resources (atlas FBO, cairo textures) are created lazily on first Open()
            // so that the GL context and world are fully initialized.
        }

        private void EnsureGpuReady()
        {
            if (_gpuReady) return;
            _atlas        = new ItemIconAtlas(_capi);
            _slotRenderer = new ImGuiSlotRenderer(_capi, slotSize: 48);
            _grid         = new ImGuiInventoryGrid(_capi, _slotRenderer, _atlas);
            _gpuReady = true;
        }

        protected override bool OnOpen()
        {
            EnsureGpuReady();
            _grid.SetInventory(_inv);
            EnsureJewelryRowsLoaded();   // metadata only, no slot writes
            EnsureGemsLoaded();          // gems still go into slots so the Gems tab can show icons
            _panCacheSource = null;      // force panning tab to rebuild so injected drops are visible
            return true;
        }

        protected override bool OnClose()
        {
            ImGuiInventoryGrid.SuppressMouseDrop = false;
            return true;
        }

        /// <summary>
        /// Override the ImGui dialog draw callback so it reports <see cref="VSImGui.API.CallbackGUIStatus.GrabMouse"/>
        /// while the guide is open — this hides the crosshair, frees the cursor for clicking ImGui widgets,
        /// and locks the camera so player input doesn't pass through to the world.
        /// </summary>
        protected override VSImGui.API.CallbackGUIStatus Draw(float deltaSeconds)
        {
            if (!Opened) return VSImGui.API.CallbackGUIStatus.Closed;
            if (!OnDraw()) Close();
            return Opened ? VSImGui.API.CallbackGUIStatus.GrabMouse : VSImGui.API.CallbackGUIStatus.Closed;
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

        // ──────────────────────────── data loading ────────────────────────────

        /// <summary>
        /// Populates _simpleRows / _cvstRows for the Jewelry tab without writing items
        /// into slots — the tab now shows names + sockets/tiers only, no icons.
        /// </summary>
        private void EnsureJewelryRowsLoaded()
        {
            if (_jewelryLoaded) return;
            _jewelryLoaded = true;

            var cfg = canjewelry.config;
            var cvstKeys = new HashSet<string>();
            if (cfg != null)
            {
                foreach (var cvst in cfg.custom_variants_sockets_tiers)
                {
                    string k = ItemCodeToKey(cvst.ItemCode);
                    if (!_cvstRows.Exists(r => r.Key == k))
                        _cvstRows.Add((k, PrettyItemLabel(cvst.ItemCode), cvst));
                    cvstKeys.Add(k);
                }
            }

            var seen = new HashSet<string>();
            foreach (Item item in _capi.World.Items)
            {
                if (item?.Code == null || item.Code.Domain != "canjewelry") continue;
                if (item.Attributes == null) continue;

                int sockets = item.Attributes[CANJWConstants.SOCKETS_NUMBER_STRING].AsInt(0);
                if (sockets <= 0) continue;

                string key = item.Code.FirstCodePart();
                if (cvstKeys.Contains(key)) continue;
                if (!seen.Add(key)) continue;

                int[] tiers = item.Attributes[CANJWConstants.SOCKETS_TIERS_STRING].AsArray<int>(null);
                if (tiers == null || tiers.Length == 0)
                {
                    tiers = new int[sockets];
                    for (int i = 0; i < sockets; i++) tiers[i] = 1;
                }

                _simpleRows.Add(new JewelryRow(key, PrettyItemLabel(item.Code.ToString()), sockets, tiers));
            }

            _simpleRows.Sort((a, b) =>
            {
                int c = a.SocketCount.CompareTo(b.SocketCount);
                return c != 0 ? c : string.Compare(a.Label, b.Label, StringComparison.OrdinalIgnoreCase);
            });
        }

        private static string PrettyKey(string key)
        {
            string s = key.StartsWith("can") ? key[3..] : key;
            return Capitalize(s);
        }

private void EnsureGemsLoaded()
        {
            if (_gemsLoaded) return;
            var cfg = canjewelry.config;
            if (cfg == null) return;
            _gemsLoaded = true;

            var gemItems = _capi.World.SearchItems(new AssetLocation("canjewelry:gem-cut-*"));
            var seenGems = new HashSet<string>();
            int gemSlot  = 0;
            foreach (var item in gemItems)
            {
                if (!item.Variant.ContainsKey("gemtype")) continue;
                string gt = item.Variant["gemtype"];
                if (gt == null || !seenGems.Add(gt)) continue;
                if (!cfg.PossibleGemBuffs.ContainsKey(gt)) continue;
                if (gemSlot >= 80) break;
                var gemStack = new ItemStack(item);
                gemStack.ResolveBlockOrItem(_capi.World);
                _inv[gemSlot].Itemstack = gemStack;
                _gemSlots[gt] = gemSlot++;
            }

            foreach (var cvst in cfg.custom_variants_sockets_tiers)
            {
                string key = ItemCodeToKey(cvst.ItemCode);
                if (!_cvstRows.Exists(r => r.Key == key))
                    _cvstRows.Add((key, FormatCode(cvst.ItemCode), cvst));
            }
        }

        // ──────────────────────────── ImGui draw ────────────────────────────

        protected override bool OnDraw()
        {
            // Close on Escape — VSImGui's own Closed event sometimes loses Esc to the game's main menu,
            // so we guard explicitly here.
            if (ImGui.IsKeyPressed(ImGuiKey.Escape, false))
            {
                return false;
            }

            bool open = true;
            ImGui.SetNextWindowSize(new Vector2(820, 580), ImGuiCond.FirstUseEver);
            if (!ImGui.Begin("CAN Jewelry -Guide##canjguide", ref open,
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            {
                ImGui.End();
                return open;
            }

            PushTabStyle();
            if (ImGui.BeginTabBar("##guidetabs", ImGuiTabBarFlags.Reorderable | ImGuiTabBarFlags.FittingPolicyScroll))
            {
                if (ImGui.BeginTabItem("  Getting Started  "))  { PopTabStyle(); DrawTabGettingStarted(); ImGui.EndTabItem(); PushTabStyle(); }
                if (ImGui.BeginTabItem("  Jewelry & Sockets  ")) { PopTabStyle(); DrawTabJewelry();        ImGui.EndTabItem(); PushTabStyle(); }
                if (ImGui.BeginTabItem("  Gems & Buffs  "))     { PopTabStyle(); DrawTabGems();            ImGui.EndTabItem(); PushTabStyle(); }
                if (ImGui.BeginTabItem("  Cutting Styles  "))   { PopTabStyle(); DrawTabCutting();         ImGui.EndTabItem(); PushTabStyle(); }
                if (ImGui.BeginTabItem("  Panning  "))          { PopTabStyle(); DrawTabPanning();         ImGui.EndTabItem(); PushTabStyle(); }
                ImGui.EndTabBar();
            }
            PopTabStyle();

            ImGui.End();
            return open;
        }

        // ──────────────────────────── Tab 0: Getting Started ────────────────────────────

        private static void DrawTabGettingStarted()
        {
            ImGui.BeginChild("##gs");

            void H(string t) { ImGui.TextColored(Col_Header, t); ImGui.Separator(); ImGui.Spacing(); }
            void B(string t) { ImGui.TextWrapped(t); ImGui.Spacing(); }

            H("Overview");
            B("CAN Jewelry lets you enhance your equipment by socketing cut gems. Each gem provides a primary stat bonus, and baguette-cut gems also roll a random secondary effect.");

            H("Step 1 -Obtain rough gems");
            B("Mine rocks, or pan ore chunks in an iron or steel pan. Rough gems drop as chipped (small), flawed (medium), or normal (large). Different ore types yield different gem varieties.");

            H("Step 2 -Cut the gem");
            B("At the Gem Cutting Table, use a Gem Cutting Chisel to cut the rough gem. Choose a cut style: Round (best main stat after grinding), Baguette (main stat + random secondary), or Pear (strong unground, weaker after full grind).");

            H("Step 3 -Grind the gem (optional but recommended)");
            B("Hold right-click on the Jewel Grinder while holding the cut gem. Three grinding steps apply multipliers that significantly increase the gem's bonus. Use a rough grindlayer for the first two steps, fine grindlayer for the last. The grinder needs mechanical power to spin.");

            H("Step 4 -Forge a socket");
            B("Forge a socket from metal ingots at the smithing anvil. The metal determines the socket tier: bronze = tier 1, gold/silver/iron = tier 2, steel = tier 3.");

            H("Step 5 -Add socket to jewelry");
            B("At the Jeweler's Set, combine a jewelry piece + socket. Each piece has a socket count and a tier ceiling per slot - see the Jewelry & Sockets tab.");

            H("Step 6 -Encrust with gem");
            B("At the Jeweler's Set, combine socketed jewelry + cut gem. The gem size must match the socket tier. The gem grants its stat bonus while worn.");

            ImGui.Separator(); ImGui.Spacing();
            ImGui.TextColored(Col_Header, "Gem size vs socket tier");
            ImGui.Spacing();
            ImGui.TextColored(Col_Green,  "  Tier 1 (green)  = bronze socket    =>  small (chipped) gem");
            ImGui.TextColored(Col_Blue,   "  Tier 2 (blue)   = gold/iron socket  =>  medium (flawed) gem");
            ImGui.TextColored(Col_Purple, "  Tier 3 (purple) = steel socket      =>  large (normal) gem");

            ImGui.EndChild();
        }

        // ──────────────────────────── Tab 1: Jewelry & Sockets ────────────────────────────

        private void DrawTabJewelry()
        {
            var   cfg     = canjewelry.config;

            var   avail   = ImGui.GetContentRegionAvail();
            float tableH  = avail.Y - 84f;

            var tblFlags = ImGuiTableFlags.BordersOuter | ImGuiTableFlags.RowBg
                         | ImGuiTableFlags.ScrollY      | ImGuiTableFlags.SizingFixedFit;
            if (ImGui.BeginTable("##jtbl", 3, tblFlags, new Vector2(0, tableH)))
            {
                ImGui.TableSetupScrollFreeze(0, 1);
                ImGui.TableSetupColumn("Name",       ImGuiTableColumnFlags.WidthFixed,   200);
                ImGui.TableSetupColumn("Sockets",    ImGuiTableColumnFlags.WidthFixed,   70);
                ImGui.TableSetupColumn("Slot tiers", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableHeadersRow();

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                ImGui.TextColored(Col_Header, "Simple Jewelry");

                foreach (var row in _simpleRows)
                {
                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);
                    ImGui.Text(row.Label);
                    ImGui.TableSetColumnIndex(1);
                    ImGui.Text(row.SocketCount.ToString());
                    ImGui.TableSetColumnIndex(2);
                    ImGui.Text(FormatTiers(row.Tiers));
                }

                if (cfg != null && _cvstRows.Count > 0)
                {
                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);
                    ImGui.TextColored(Col_Header, "Metal-scaled Jewelry");

                    foreach (var (key, label, cvst) in _cvstRows)
                    {
                        ImGui.TableNextRow();
                        ImGui.TableSetColumnIndex(0);
                        ImGui.Text(label);
                        ImGui.TableSetColumnIndex(2);
                        foreach (var kv in cvst.SocketTiers)
                            ImGui.Text($"{Capitalize(kv.Key)}: {kv.Value.Length} socket(s) -{FormatTiers(kv.Value)}");
                    }
                }

                ImGui.EndTable();
            }

            ImGui.Spacing();
            ImGui.TextColored(Col_Green,  "  T1 = green  (bronze socket, small gem)");
            ImGui.TextColored(Col_Blue,   "  T2 = blue   (gold/iron socket, medium gem)");
            ImGui.TextColored(Col_Purple, "  T3 = purple (steel socket, large gem)");
        }

        // ──────────────────────────── Tab 2: Gems & Buffs ────────────────────────────

        private void DrawTabGems()
        {
            int   slotSz  = _slotRenderer.SlotSize;
            float rowH    = slotSz + 4;
            float textOff = (slotSz - ImGui.GetTextLineHeight()) * 0.5f;
            var   cfg     = canjewelry.config;
            if (cfg == null) return;

            ImGui.TextWrapped("Each cut gem grants its primary buff when socketed and worn. Baguette gems add a small random secondary on top.");
            ImGui.Spacing();

            float tableH = ImGui.GetContentRegionAvail().Y - 28f;
            var tblFlags = ImGuiTableFlags.BordersOuter | ImGuiTableFlags.RowBg
                         | ImGuiTableFlags.ScrollY      | ImGuiTableFlags.SizingFixedFit;
            if (ImGui.BeginTable("##gtbl", 7, tblFlags, new Vector2(0, tableH)))
            {
                ImGui.TableSetupScrollFreeze(0, 1);
                ImGui.TableSetupColumn("",          ImGuiTableColumnFlags.WidthFixed,   slotSz + 4);
                ImGui.TableSetupColumn("Gem",       ImGuiTableColumnFlags.WidthFixed,   120);
                ImGui.TableSetupColumn("Main stat", ImGuiTableColumnFlags.WidthFixed,   220);
                ImGui.TableSetupColumn("Tier 1",    ImGuiTableColumnFlags.WidthFixed,   110);
                ImGui.TableSetupColumn("Tier 2",    ImGuiTableColumnFlags.WidthFixed,   110);
                ImGui.TableSetupColumn("Tier 3",    ImGuiTableColumnFlags.WidthFixed,   110);
                ImGui.TableSetupColumn("Secondary", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableHeadersRow();

                foreach (var kv in cfg.PossibleGemBuffs)
                {
                    string gemName = kv.Key;
                    if (!_gemSlots.TryGetValue(gemName, out int sid)) continue;

                    foreach (string statName in kv.Value)
                    {
                        // ── main stat row ──
                        ImGui.TableNextRow(ImGuiTableRowFlags.None, rowH);
                        ImGui.TableSetColumnIndex(0);
                        _grid.DrawSingleSlot(sid);
                        ImGui.TableSetColumnIndex(1);
                        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + textOff);
                        ImGui.Text(Capitalize(gemName));
                        ImGui.TableSetColumnIndex(2);
                        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + textOff);
                        ImGui.TextColored(ColorForStat(statName), statName);

                        if (cfg.BuffAttributesDict.TryGetValue(statName, out var ba))
                        {
                            ImGui.TableSetColumnIndex(3);
                            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + textOff);
                            ImGui.Text(FmtRange(ba.MainStatValueRange, 1));
                            ImGui.TableSetColumnIndex(4);
                            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + textOff);
                            ImGui.Text(FmtRange(ba.MainStatValueRange, 2));
                            ImGui.TableSetColumnIndex(5);
                            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + textOff);
                            ImGui.Text(FmtRange(ba.MainStatValueRange, 3));

                            // ── secondary stats column: colored list with tier-range tooltip ──
                            ImGui.TableSetColumnIndex(6);
                            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 4);
                            if (ba.PossibleSecondaryStats != null && ba.PossibleSecondaryStats.Count > 0)
                            {
                                bool first = true;
                                foreach (var secName in ba.PossibleSecondaryStats)
                                {
                                    if (!first) ImGui.SameLine(0, 6);
                                    first = false;
                                    ImGui.TextColored(ColorForStat(secName), secName);
                                }
                                // Tooltip with tier ranges, applies to the whole cell
                                if (ImGui.IsItemHovered() && ba.SecondaryStatValueRange != null)
                                {
                                    ImGui.BeginTooltip();
                                    ImGui.Text("Secondary value range:");
                                    ImGui.Separator();
                                    ImGui.TextColored(Col_Green,  $"  T1: {FmtRange(ba.SecondaryStatValueRange, 1)}");
                                    ImGui.TextColored(Col_Blue,   $"  T2: {FmtRange(ba.SecondaryStatValueRange, 2)}");
                                    ImGui.TextColored(Col_Purple, $"  T3: {FmtRange(ba.SecondaryStatValueRange, 3)}");
                                    ImGui.EndTooltip();
                                }
                            }
                        }
                    }
                }

                ImGui.EndTable();
            }

            ImGui.Spacing();
            ImGui.TextDisabled("Note: negative values are beneficial for hungerrate / armorDurabilityLoss / animalSeekingRange.");
        }

        // ──────────────────────────── Tab 3: Cutting Styles ────────────────────────────

        private static void DrawTabCutting()
        {
            ImGui.BeginChild("##cut");

            (string title, Vector4 color, string desc)[] cuts = {
                ("Round",    Col_Green,
                 "Primary stat only. After FULL processing on the jewel grinder this gives the highest main-stat value of any cut. Best long-term choice."),
                ("Baguette", Col_Blue,
                 "Primary stat + a small random secondary buff from a fixed pool for that stat. Trades some main-stat power for a bonus second effect."),
                ("Pear",     Col_Orange,
                 "Primary stat only, but the gem starts much stronger BEFORE grinding. If both gems are fully ground, Round overtakes it. Good for un-ground gems."),
            };

            var cutFlags = ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchSame;
            if (ImGui.BeginTable("##cutstyles", cuts.Length, cutFlags))
            {
                // Title row
                ImGui.TableNextRow();
                for (int i = 0; i < cuts.Length; i++)
                {
                    ImGui.TableSetColumnIndex(i);
                    ImGui.TextColored(cuts[i].color, cuts[i].title);
                }
                // Description row
                ImGui.TableNextRow();
                for (int i = 0; i < cuts.Length; i++)
                {
                    ImGui.TableSetColumnIndex(i);
                    ImGui.TextWrapped(cuts[i].desc);
                }
                ImGui.EndTable();
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.TextColored(Col_Header, "Gem size reminder");
            ImGui.Spacing();

            var tblFlags = ImGuiTableFlags.BordersOuter | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit;
            if (ImGui.BeginTable("##sizetbl", 3, tblFlags))
            {
                ImGui.TableSetupColumn("Rough gem grade",  ImGuiTableColumnFlags.WidthFixed, 200);
                ImGui.TableSetupColumn("Socket metal",     ImGuiTableColumnFlags.WidthFixed, 270);
                ImGui.TableSetupColumn("Slot tier",        ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableHeadersRow();

                (string grade, string metal, string tier, Vector4 col)[] rows = {
                    ("Chipped (small)",  "Bronze (tin/bismuth/black)",          "Tier 1", Col_Green),
                    ("Flawed (medium)",  "Gold / Silver / Iron / Meteoric",     "Tier 2", Col_Blue),
                    ("Normal (large)",   "Steel",                               "Tier 3", Col_Purple),
                };
                foreach (var (grade, metal, tier, col) in rows)
                {
                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0); ImGui.Text(grade);
                    ImGui.TableSetColumnIndex(1); ImGui.Text(metal);
                    ImGui.TableSetColumnIndex(2); ImGui.TextColored(col, tier);
                }
                ImGui.EndTable();
            }

            ImGui.EndChild();
        }

        // ──────────────────────────── Tab 4: Panning ────────────────────────────

        // Matches "@(ore|crystalizedore)-{quality}-{ore}-.*" keys
        private static readonly Regex _panKeyRegex = new Regex(
            @"^@\([^)]+\)-(\w+)-(.+?)-\.\*$", RegexOptions.Compiled);

        // Cached parse result — rebuilt on every guide open (cheap) so companion mods that inject
        // into panningDrops after the first open are always reflected without a game restart.
        private static Dictionary<string, utils.CANPanningDrop[]> _panCacheSource;
        private static Dictionary<string, Dictionary<string, utils.CANPanningDrop[]>> _panByOre;
        private static List<(string key, utils.CANPanningDrop[] drops)> _panSpecial     = new();
        private static List<(string key, utils.CANPanningDrop[] drops)> _panPerkSpecial = new();

        private static readonly string[] _qualityOrder = { "bountiful", "rich", "medium", "poor" };

        private static readonly Dictionary<string, string> _oreDisplayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "hematite",           "Hematite" },
            { "malachite",          "Malachite" },
            { "bismuthinite",       "Bismuthinite" },
            { "cassiterite",        "Cassiterite" },
            { "sphalerite",         "Sphalerite" },
            { "quartz_nativesilver","Quartz (Native Silver)" },
            { "quartz_nativegold",  "Quartz (Native Gold)" },
            { "limonite",           "Limonite" },
            { "ilmenite",           "Ilmenite" },
            { "nativecopper",       "Native Copper" },
            { "magnetite",          "Magnetite" },
            { "uranium",            "Uranium" },
            { "galena",             "Galena" },
        };

        private static readonly Dictionary<string, string> _gemDisplayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "diamond",    "Diamond" },
            { "emerald",    "Emerald" },
            { "corundum",   "Corundum" },
            { "malachite",  "Malachite" },
            { "lapislazuli","Lapis Lazuli" },
            { "olivine",    "Olivine" },
            { "fluorite",   "Fluorite" },
            { "quartz",     "Quartz" },
            { "uranium",    "Uranium" },
            { "ruby",       "Ruby" },
            { "citrine",    "Citrine" },
            { "amethyst",   "Amethyst" },
        };

        private static void DrawTabPanning()
        {
            ImGui.BeginChild("##pan");

            ImGui.TextWrapped("Use an iron or steel pan while holding ore chunks (right-click the pan block). Different ore types yield different gem varieties.");
            ImGui.Spacing();

            var drops = canjewelry.config?.panningDrops;
            if (drops == null || drops.Count == 0)
            {
                ImGui.TextDisabled("No panning drop data available.");
                ImGui.EndChild();
                return;
            }

            // ── Group entries (cached) ──
            if (!ReferenceEquals(drops, _panCacheSource))
            {
                _panCacheSource = drops;
                _panByOre       = new Dictionary<string, Dictionary<string, utils.CANPanningDrop[]>>(StringComparer.OrdinalIgnoreCase);
                _panSpecial     = new List<(string, utils.CANPanningDrop[])>();
                _panPerkSpecial = new List<(string, utils.CANPanningDrop[])>();

                foreach (var kv in drops)
                {
                    var m = _panKeyRegex.Match(kv.Key);
                    if (m.Success)
                    {
                        string quality = m.Groups[1].Value;
                        string ore     = m.Groups[2].Value;
                        if (!_panByOre.TryGetValue(ore, out var qualMap))
                            _panByOre[ore] = qualMap = new Dictionary<string, utils.CANPanningDrop[]>(StringComparer.OrdinalIgnoreCase);
                        qualMap[quality] = kv.Value;
                    }
                    else
                    {
                        bool perkGated = kv.Value.Any(d => !string.IsNullOrEmpty(d?.requiresPerk));
                        if (perkGated) _panPerkSpecial.Add((kv.Key, kv.Value));
                        else           _panSpecial.Add((kv.Key, kv.Value));
                    }
                }

                // Companion runtime drops (registered via RegisterPanDrops) — always perk-gated.
                var runtimeDrops = canjewelry.Instance?.runtimeExtraPanDrops;
                if (runtimeDrops != null)
                {
                    foreach (var kv in runtimeDrops)
                        _panPerkSpecial.Add((kv.Key, kv.Value));
                }
            }

            var byOre      = _panByOre;
            var special    = _panSpecial;
            var perkSpecial = _panPerkSpecial;

            var tblFlags = ImGuiTableFlags.BordersOuter | ImGuiTableFlags.BordersInnerV
                         | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit;

            // ── Special entries (suevite etc.) ──
            foreach (var (key, spDrops) in special)
            {
                string label = key.Contains(':') ? key.Split(':')[1] : key;
                label = char.ToUpperInvariant(label[0]) + label.Substring(1).Replace('-', ' ');
                ImGui.TextColored(Col_Header, label);
                ImGui.Spacing();
                if (ImGui.BeginTable("##sp_" + key, 2, tblFlags))
                {
                    ImGui.TableSetupColumn("Gem",    ImGuiTableColumnFlags.WidthFixed,   200);
                    ImGui.TableSetupColumn("Chance", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableHeadersRow();
                    foreach (var d in spDrops)
                    {
                        if (d?.Code == null) continue;
                        ImGui.TableNextRow();
                        ImGui.TableSetColumnIndex(0); ImGui.Text(FormatGemCode(d.Code.Path));
                        ImGui.TableSetColumnIndex(1); ImGui.Text(d.Chance != null ? $"{d.Chance.avg * 100:0}%" : "-");
                    }
                    ImGui.EndTable();
                }
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
            }

            // ── Prospector-perk special entries ──
            if (perkSpecial.Count > 0)
            {
                ImGui.Spacing();
                ImGui.TextColored(Col_Orange, "Prospector perk (Lapidary skill)");
                ImGui.Separator();
                ImGui.Spacing();

                foreach (var (key, spDrops) in perkSpecial)
                {
                    string label = key.Contains(':') ? key.Split(':')[1] : key;
                    label = char.ToUpperInvariant(label[0]) + label.Substring(1).Replace('-', ' ');
                    ImGui.TextColored(Col_Orange, label);
                    ImGui.Spacing();
                    if (ImGui.BeginTable("##perk_" + key, 2, tblFlags))
                    {
                        ImGui.TableSetupColumn("Gem",    ImGuiTableColumnFlags.WidthFixed,   200);
                        ImGui.TableSetupColumn("Chance", ImGuiTableColumnFlags.WidthStretch);
                        ImGui.TableHeadersRow();
                        foreach (var d in spDrops)
                        {
                            if (d?.Code == null) continue;
                            ImGui.TableNextRow();
                            ImGui.TableSetColumnIndex(0); ImGui.Text(FormatGemCode(d.Code.Path));
                            ImGui.TableSetColumnIndex(1); ImGui.Text(d.Chance != null ? $"{d.Chance.avg * 100:0}%" : "-");
                        }
                        ImGui.EndTable();
                    }
                    ImGui.Spacing();
                }
                ImGui.Separator();
                ImGui.Spacing();
            }

            // ── Per-ore tables ──
            foreach (var ore in byOre.Keys.OrderBy(o => o))
            {
                var qualMap = byOre[ore];
                string oreLabel = _oreDisplayNames.TryGetValue(ore, out var dn) ? dn : ore;

                // Collect unique gem types for this ore to build columns
                var gemTypes = new List<string>();
                foreach (var q in _qualityOrder)
                {
                    if (!qualMap.TryGetValue(q, out var qdrops)) continue;
                    foreach (var d in qdrops)
                    {
                        if (d?.Code == null) continue;
                        string gt = ExtractGemType(d.Code.Path);
                        if (gt != null && !gemTypes.Contains(gt)) gemTypes.Add(gt);
                    }
                }
                if (gemTypes.Count == 0) continue;

                ImGui.TextColored(Col_Header, oreLabel);
                ImGui.Spacing();

                // Columns: Quality | for each gemType: Chipped | Flawed | Normal
                int cols = 1 + gemTypes.Count * 3;
                if (ImGui.BeginTable("##ore_" + ore, cols, tblFlags))
                {
                    ImGui.TableSetupColumn("Quality", ImGuiTableColumnFlags.WidthFixed, 90);
                    foreach (var gt in gemTypes)
                    {
                        string gemLabel = _gemDisplayNames.TryGetValue(gt, out var gn) ? gn : gt;
                        ImGui.TableSetupColumn(gemLabel + " (chipped)", ImGuiTableColumnFlags.WidthFixed,   100);
                        ImGui.TableSetupColumn(gemLabel + " (flawed)",  ImGuiTableColumnFlags.WidthFixed,   100);
                        ImGui.TableSetupColumn(gemLabel + " (normal)",  ImGuiTableColumnFlags.WidthStretch);
                    }
                    ImGui.TableHeadersRow();

                    foreach (var quality in _qualityOrder)
                    {
                        if (!qualMap.ContainsKey(quality)) continue;
                        var qdrops = qualMap[quality];

                        // Index drops by gemtype+size for quick lookup
                        var lookup = new Dictionary<string, float>();
                        foreach (var d in qdrops)
                        {
                            if (d?.Code == null) continue;
                            string gt   = ExtractGemType(d.Code.Path);
                            string size = ExtractGemSize(d.Code.Path);
                            if (gt != null && size != null)
                                lookup[gt + ":" + size] = d.Chance?.avg ?? 0f;
                        }

                        ImGui.TableNextRow();
                        ImGui.TableSetColumnIndex(0);
                        ImGui.Text(char.ToUpperInvariant(quality[0]) + quality.Substring(1));

                        int col = 1;
                        foreach (var gt in gemTypes)
                        {
                            string chipped = lookup.TryGetValue(gt + ":chipped", out float c) ? $"{c * 100:0}%" : "-";
                            string flawed  = lookup.TryGetValue(gt + ":flawed",  out float f) ? $"{f * 100:0}%" : "-";
                            string normal  = lookup.TryGetValue(gt + ":normal",  out float n) ? $"{n * 100:0}%" : "-";
                            ImGui.TableSetColumnIndex(col++); ImGui.Text(chipped);
                            ImGui.TableSetColumnIndex(col++); ImGui.Text(flawed);
                            ImGui.TableSetColumnIndex(col++); ImGui.Text(normal);
                        }
                    }
                    ImGui.EndTable();
                }
                ImGui.Spacing();
            }

            ImGui.EndChild();
        }

        private static string ExtractGemType(string codePath)
        {
            // "gem-rough-{size}-{gemtype}" or full "canjewelry:gem-rough-..."
            var parts = codePath.Split('-');
            return parts.Length >= 4 ? parts[parts.Length - 1] : null;
        }

        private static string ExtractGemSize(string codePath)
        {
            var parts = codePath.Split('-');
            return parts.Length >= 4 ? parts[parts.Length - 2] : null;
        }

        private static string FormatGemCode(string codePath)
        {
            string gt   = ExtractGemType(codePath);
            string size = ExtractGemSize(codePath);
            if (gt == null || size == null) return codePath;
            string gemLabel  = _gemDisplayNames.TryGetValue(gt, out var gn) ? gn : gt;
            string sizeLabel = size == "normal" ? "Large" : size == "flawed" ? "Medium" : "Small";
            return $"{gemLabel} ({sizeLabel})";
        }

        // ──────────────────────────── helpers ────────────────────────────

        /// <summary>
        /// Deterministic color per stat name — hashes the string into a hue so the same stat
        /// always gets the same color across rows, and different stats are visually distinct.
        /// </summary>
        private static Vector4 ColorForStat(string name)
        {
            if (string.IsNullOrEmpty(name)) return new Vector4(1, 1, 1, 1);
            // FNV-1a hash → hue, with saturation/value tuned for readable yet distinct colors on dark bg
            uint h = 2166136261u;
            foreach (char c in name) { h = (h ^ c) * 16777619u; }
            // Use a step-skip multiplier so close strings get well-separated hues (golden-ratio trick)
            float hue = ((h * 2654435761u) % 360u) / 360f;
            ImGui.ColorConvertHSVtoRGB(hue, 0.75f, 1.0f, out float r, out float g, out float b);
            return new Vector4(r, g, b, 1f);
        }

        private static string FormatTiers(int[] tiers)
        {
            var parts = new string[tiers.Length];
            for (int i = 0; i < tiers.Length; i++)
                parts[i] = tiers[i] switch { 1 => "T1", 2 => "T2", 3 => "T3", _ => $"T{tiers[i]}" };
            return string.Join(", ", parts);
        }

        private static string FmtRange(Dictionary<int, float[]> dict, int tier)
        {
            if (dict == null || !dict.TryGetValue(tier, out float[] r)) return "--";
            if (r.Length == 1) return r[0].ToString("G3");
            if (r[0] == r[1]) return r[0].ToString("G3");
            return $"{r[0]:G3}/{r[1]:G3}";
        }

        private static string FormatCode(string code)
        {
            int colon = code.IndexOf(':');
            string path = colon >= 0 ? code[(colon + 1)..] : code;
            return Capitalize(path.Replace('-', ' ').Replace('*', ' ').Trim());
        }

        private static string ItemCodeToKey(string code)
        {
            int colon = code.IndexOf(':');
            string path = colon >= 0 ? code[(colon + 1)..] : code;
            int dash = path.IndexOf('-');
            return dash >= 0 ? path[..dash] : path;
        }

        // ── Tab styling ────────────────────────────────────────────────────────────────
        // Bronze/gold jewelry-themed tab look. Push before BeginTabBar / each TabItem block,
        // pop before drawing tab content (so content uses the dialog's normal style),
        // re-push before the next TabItem, and pop one final time after EndTabBar.

        private static readonly Vector4 TabIdle           = new(0.10f, 0.08f, 0.05f, 1.0f); // very dark brown
        private static readonly Vector4 TabHovered        = new(0.45f, 0.31f, 0.12f, 1.0f); // muted bronze
        private static readonly Vector4 TabActive         = new(0.82f, 0.58f, 0.18f, 1.0f); // saturated gold (selected)
        private static readonly Vector4 TabBarBackground  = new(0.06f, 0.04f, 0.03f, 1.0f);
        private static readonly Vector4 TabBorder         = new(1.00f, 0.82f, 0.35f, 1.0f); // bright gold rim

        private int _tabStylePushDepth;

        private void PushTabStyle()
        {
            ImGui.PushStyleColor(ImGuiCol.Tab,                TabIdle);
            ImGui.PushStyleColor(ImGuiCol.TabHovered,         TabHovered);
            ImGui.PushStyleColor(ImGuiCol.TabActive,          TabActive);
            ImGui.PushStyleColor(ImGuiCol.TabUnfocused,       TabIdle);
            ImGui.PushStyleColor(ImGuiCol.TabUnfocusedActive, TabActive);
            ImGui.PushStyleColor(ImGuiCol.Border,             TabBorder);
            ImGui.PushStyleColor(ImGuiCol.ChildBg,            TabBarBackground);
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding,   new Vector2(14, 7));
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing,    new Vector2(6, 6));
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding,  6f);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);
            _tabStylePushDepth++;
        }

        private void PopTabStyle()
        {
            if (_tabStylePushDepth <= 0) return;
            _tabStylePushDepth--;
            ImGui.PopStyleVar(4);  // FramePadding, ItemSpacing, FrameRounding, FrameBorderSize
            ImGui.PopStyleColor(7); // Tab, TabHovered, TabActive, TabUnfocused, TabUnfocusedActive, Border, ChildBg
        }

        private static string Capitalize(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..];
    }
}
