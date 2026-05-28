using System;
using canjewelry.src.be;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent.Mechanics;

namespace canjewelry.src.blocks
{
    // Lapidary Bench — surface block. Routes player interactions to
    // BELapidaryBench. Front face is a control panel with discrete buttons
    // (see selection-box constants below); the left dop-arm zone keeps a
    // continuous camera-pitch input as a fast alternative for the angle.
    public class BlockLapidaryBench : BlockMPBase
    {
        // Selection-box indices. Order is significant — kept stable so
        // hover-help mappings don't shift when boxes are added.
        public const int BOX_GENERAL = 0;
        public const int BOX_DOP_ARM = 1;

        // Front-panel buttons. Layout (looking at +Z face):
        //   row top    : [<recipe] [>recipe] [OK recipe] [No recipe]
        //   row mid    : [angle-]  [angle+]  [index-]    [index+]
        //   row bottom : [   CUT          ] [   POLISH         ]
        public const int BTN_RECIPE_PREV   = 2;
        public const int BTN_RECIPE_NEXT   = 3;
        public const int BTN_RECIPE_OK     = 4;
        public const int BTN_RECIPE_CANCEL = 5;
        public const int BTN_ANGLE_DOWN    = 6;
        public const int BTN_ANGLE_UP      = 7;
        public const int BTN_INDEX_DOWN    = 8;
        public const int BTN_INDEX_UP      = 9;
        public const int BTN_CUT           = 10;
        public const int BTN_POLISH        = 11;

        // Increment per right-click. Shift = bigger step (5°, 4 indices).
        private const float ANGLE_STEP_SMALL = 1f;
        private const float ANGLE_STEP_BIG   = 5f;
        private const int   INDEX_STEP_SMALL = 1;
        private const int   INDEX_STEP_BIG   = 4;

        private Cuboidf[] cachedBoxes;

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            if (cachedBoxes != null) return cachedBoxes;

            // Helper: front-face button rectangle in voxel-space. z extends
            // just past the block face so the box is clickable from the front.
            static Cuboidf Btn(float x1, float y1, float x2, float y2)
                => new Cuboidf(x1 / 16f, y1 / 16f, 15f / 16f, x2 / 16f, y2 / 16f, 17f / 16f);

            cachedBoxes = new Cuboidf[]
            {
                // 0 — general fallback (bench body bottom half + dop arm area).
                // Top capped at y=0.5 so downward rays aimed at front-face
                // buttons in the upper half don't get caught by general's top.
                new Cuboidf(0f, 0f, 0f, 1f, 0.5f, 14f / 16f),

                // 1 — dop arm: continuous camera-pitch angle input
                new Cuboidf(0.05f, 0.45f, 0.4f, 0.2f, 0.75f, 0.6f),

                // All buttons in the upper half of the front face (y 8..15)
                // so they sit above general's top and are not eclipsed by it.

                // 2-5 — recipe controls (top row, y 13..15)
                Btn(0,  13, 4,  15),  // < recipe
                Btn(4,  13, 8,  15),  // > recipe
                Btn(8,  13, 12, 15),  // OK
                Btn(12, 13, 16, 15),  // Cancel

                // 6-9 — angle/index (mid row, y 10.5..12.5)
                Btn(0,  10.5f, 4,  12.5f), // angle -
                Btn(4,  10.5f, 8,  12.5f), // angle +
                Btn(8,  10.5f, 12, 12.5f), // index -
                Btn(12, 10.5f, 16, 12.5f), // index +

                // 10-11 — actions (bottom row of upper half, y 8..10)
                Btn(0, 8, 8,  10),    // CUT (wide)
                Btn(8, 8, 16, 10),    // POLISH (wide)
            };
            return cachedBoxes;
        }

        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            return new[] { new Cuboidf(0f, 0f, 0f, 1f, 0.875f, 1f) };
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is not BELapidaryBench be)
                return base.OnBlockInteractStart(world, byPlayer, blockSel);

            if (!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
                return false;

            int box = blockSel.SelectionBoxIndex;
            ItemSlot hand = byPlayer.InventoryManager?.ActiveHotbarSlot;
            bool emptyHand = hand == null || hand.Empty;
            bool shift = byPlayer.Entity.Controls.ShiftKey;

            // Dop arm: empty hand → hold-RC drives camera-pitch angle (see Step).
            if (box == BOX_DOP_ARM && emptyHand)
            {
                return true;
            }

            // Front panel buttons — empty hand only. With item in hand we fall
            // through to general insert so the player can shove e.g. a lap in
            // while looking at the panel.
            if (emptyHand && IsButtonBox(box))
            {
                if (world.Side == EnumAppSide.Server)
                {
                    HandleButton(be, byPlayer, box, shift);
                }
                return true;
            }

            return be.OnPlayerRightClick(byPlayer, blockSel);
        }

        private static bool IsButtonBox(int box)
            => box >= BTN_RECIPE_PREV && box <= BTN_POLISH;

        private static void HandleButton(BELapidaryBench be, IPlayer player, int box, bool shift)
        {
            switch (box)
            {
                case BTN_RECIPE_PREV:   be.CycleRecipe(-1); break;
                case BTN_RECIPE_NEXT:   be.CycleRecipe(+1); break;
                case BTN_RECIPE_OK:     be.CommitRecipe(player); break;
                case BTN_RECIPE_CANCEL: be.CancelRecipe(); break;
                case BTN_ANGLE_DOWN:    be.AdjustAngle(shift ? -ANGLE_STEP_BIG : -ANGLE_STEP_SMALL); break;
                case BTN_ANGLE_UP:      be.AdjustAngle(shift ? +ANGLE_STEP_BIG : +ANGLE_STEP_SMALL); break;
                case BTN_INDEX_DOWN:    be.AdjustIndex(shift ? -INDEX_STEP_BIG : -INDEX_STEP_SMALL); break;
                case BTN_INDEX_UP:      be.AdjustIndex(shift ? +INDEX_STEP_BIG : +INDEX_STEP_SMALL); break;
                case BTN_CUT:           be.TryCutFacet(player, be.SelectedIndex); break;
                case BTN_POLISH:        be.TryDoPolish(player); break;
            }
        }

        // While player holds RC on the dop arm zone, sample camera pitch and
        // forward it to the BE for angle update + snap-to-target.
        public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (blockSel.SelectionBoxIndex != BOX_DOP_ARM) return false;
            if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is not BELapidaryBench be) return false;

            ItemSlot hand = byPlayer.InventoryManager?.ActiveHotbarSlot;
            if (hand != null && !hand.Empty) return false;

            if (world.Side == EnumAppSide.Server)
            {
                float pitch = byPlayer.Entity.Pos.Pitch;
                be.SetAngleFromPitch(pitch);
            }
            return true;
        }

        public override bool OnBlockInteractCancel(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, EnumItemUseCancelReason cancelReason)
            => true;

        public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel) { }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            int box = selection.SelectionBoxIndex;
            WorldInteraction[] specific = box switch
            {
                BOX_DOP_ARM        => Help("canjewelry:blockhelp-lapidary-angle"),
                BTN_RECIPE_PREV    => Help("canjewelry:blockhelp-lapidary-recipe-prev"),
                BTN_RECIPE_NEXT    => Help("canjewelry:blockhelp-lapidary-recipe-next"),
                BTN_RECIPE_OK      => Help("canjewelry:blockhelp-lapidary-recipe-ok"),
                BTN_RECIPE_CANCEL  => Help("canjewelry:blockhelp-lapidary-recipe-cancel"),
                BTN_ANGLE_DOWN     => HelpShift("canjewelry:blockhelp-lapidary-angle-down", "canjewelry:blockhelp-lapidary-angle-down-big"),
                BTN_ANGLE_UP       => HelpShift("canjewelry:blockhelp-lapidary-angle-up", "canjewelry:blockhelp-lapidary-angle-up-big"),
                BTN_INDEX_DOWN     => HelpShift("canjewelry:blockhelp-lapidary-index-down", "canjewelry:blockhelp-lapidary-index-down-big"),
                BTN_INDEX_UP       => HelpShift("canjewelry:blockhelp-lapidary-index-up", "canjewelry:blockhelp-lapidary-index-up-big"),
                BTN_CUT            => Help("canjewelry:blockhelp-lapidary-cut"),
                BTN_POLISH         => Help("canjewelry:blockhelp-lapidary-polish"),
                _ => Help("canjewelry:blockhelp-lapidary-open"),
            };

            var baseHelp = base.GetPlacedBlockInteractionHelp(world, selection, forPlayer);
            return AppendInteractions(specific, baseHelp);
        }

        private static WorldInteraction[] Help(string code) => new[]
        {
            new WorldInteraction { ActionLangCode = code, MouseButton = EnumMouseButton.Right }
        };

        private static WorldInteraction[] HelpShift(string codePlain, string codeShift) => new[]
        {
            new WorldInteraction { ActionLangCode = codePlain, MouseButton = EnumMouseButton.Right },
            new WorldInteraction { ActionLangCode = codeShift, MouseButton = EnumMouseButton.Right, HotKeyCode = "shift" },
        };

        // Mechanical-power wiring. Only the bottom face accepts an axle.
        public override bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face, BlockMPBase forBlock)
            => face == BlockFacing.DOWN;

        public override void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face) { }

        // On placement, try to join a network through the bottom axle.
        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if (!base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode)) return false;
            tryConnect(world, byPlayer, blockSel.Position, BlockFacing.DOWN);
            return true;
        }

        private static WorldInteraction[] AppendInteractions(WorldInteraction[] first, WorldInteraction[] second)
        {
            if (first == null || first.Length == 0) return second ?? Array.Empty<WorldInteraction>();
            if (second == null || second.Length == 0) return first;
            var combined = new WorldInteraction[first.Length + second.Length];
            Array.Copy(first, combined, first.Length);
            Array.Copy(second, 0, combined, first.Length, second.Length);
            return combined;
        }
    }
}
