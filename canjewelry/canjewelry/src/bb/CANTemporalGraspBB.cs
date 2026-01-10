using canjewelry.src.CB;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace canjewelry.src.bb
{
    public class CANTemporalGraspBB : BlockBehavior
    {
        public CANTemporalGraspBB(Block block) : base(block)
        {
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, ref float dropChanceMultiplier, ref EnumHandling handling)
        {           
            if(byPlayer == null)
            {
                return base.GetDrops(world, pos, byPlayer, ref dropChanceMultiplier, ref handling);
            }
            ItemSlot activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
            
            if(activeSlot.Empty)
            {
                return base.GetDrops(world, pos, byPlayer, ref dropChanceMultiplier, ref handling);
            }

            ItemStack activeTool = activeSlot.Itemstack;

            if(activeTool.Item != null && (activeTool.Item.Tool == EnumTool.Shovel || activeTool.Item.Tool == EnumTool.Pickaxe || activeTool.Item.Tool == EnumTool.Shears))
            {
                if(!activeTool.Item.HasBehavior<EncrustableCB>())
                {
                    return base.GetDrops(world, pos, byPlayer, ref dropChanceMultiplier, ref handling);
                }

                var eb = activeTool.Item.GetBehavior<EncrustableCB>();

                float graspChance = eb.GetTemporalGraspValue(activeTool);
                if(Config.rand.NextDouble() < graspChance)
                {
                    handling = EnumHandling.PreventSubsequent;

                    return new[] { new ItemStack(block) };
                }               
            }
            return base.GetDrops(world, pos, byPlayer, ref dropChanceMultiplier, ref handling);

        }
    }
}
