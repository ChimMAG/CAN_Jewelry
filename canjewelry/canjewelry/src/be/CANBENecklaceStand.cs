using System;
using System.Collections.Generic;
using canjewelry.src.blocks;
using canjewelry.src.inventories;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace canjewelry.src.be
{
    public class CANBENecklaceStand : CANBEBasePSContainer
    {
        public override string[] AttributeCheck => ["canNeckware"];
        public override int[] SectionSegmentCounts => [1];
        public string clothType = "black";
        public string woodType = "oak";
        public CANBENecklaceStand() {
            inv = new InventoryGeneric(SlotCount, InventoryClassName + "-0", Api, (_, inv) => new ItemSlotPSUniversal(inv, AttributeCheck));
        }
        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
        }
        public override void updateMeshes()
        {
            if (Api == null || Api.Side == EnumAppSide.Server)
            {
                return;
            }

            int displayedItems = DisplayedItems;
            if (displayedItems != 0)
            {
                for (int i = 0; i < displayedItems; i++)
                {
                    updateMesh(i);
                }

                tfMatrices = genTransformationMatrices();
            }
        }
        protected override void updateMesh(int index)
        {
            if (Api != null && Api.Side != EnumAppSide.Server)
            {
                ItemStack itemstack = Inventory[index].Itemstack;
                if (itemstack != null && !(itemstack.Collectible?.Code == null))
                {
                    getOrCreateMesh(Inventory[index].Itemstack, index);
                }
            }
        }
        protected override bool TryPut(IPlayer byPlayer, ItemSlot slot, BlockSelection blockSel)
        {
            if (slot.Itemstack?.Collectible?.Code.Path.EndsWith("marketeer") == true) // Marketeer backlisted
                return false;

            return base.TryPut(byPlayer, slot, blockSel);
        }
        protected override float[][] genTransformationMatrices()
        {
            float[][] tfMatrices = new float[SlotCount][];
            int index = 0 * ItemsPerSegment;

            float x = -0.05f;
            float y = 0 / 3 * 0.43525f;
            float z = 0 % 3 * 0.275f;
            tfMatrices[0] = new Matrixf()
                .Translate(0.5f, 0, 0.5f)
                .RotateYDeg(block.Shape.rotateY + 90)
                .Translate(x - 0.65f, y - 2.085f, z - 0.65f)
                                .Scale(1.3f, 1.8f, 1.3f)
                .Values;
                //.Translate(-0.28f, -1.2225f, -0.78f)
                //.Scale(1.5f, 1.8f, 1.5f)
                    //.Translate(0.5f, 0, 0.5f)
                //.RotateYDeg(block.Shape.rotateY + 90)
                
                //.Translate(-0.28f, -1.2225f, -0.78f)
                //.Values;
            /*for (int segment = 0; segment < SectionSegmentCounts[0]; segment++)
            {
                int index = segment * ItemsPerSegment;

                float x = -0.1f;
                float y = segment / 3 * 0.43525f;
                float z = segment % 3 * 0.275f;

                tfMatrices[index] = new Matrixf()
                    .Translate(0.5f, 0, 0.5f)
                    .RotateYDeg(block.Shape.rotateY + 90)
                    .Translate(x - 0.28f, y - 1.2225f, z - 0.78f)
                    .Values;
            }*/

            return tfMatrices;
        }
        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            for (int i = 0; i < DisplayedItems; i++)
            {
                ItemSlot itemSlot = Inventory[i];
                if (!itemSlot.Empty && tfMatrices != null && !(itemSlot.Itemstack.Collectible?.Code == null))
                {
                    mesher.AddMeshData(getMesh(itemSlot.Itemstack), tfMatrices[i]);
                }
            }

            return base.OnTesselation(mesher, tessThreadTesselator);
        }
    }
}