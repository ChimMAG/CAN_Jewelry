using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace canjewelry.src.items.resource
{
    public class CANCutGemItem: Item, IContainedMeshSource, ITexPositionSource
    {
        private ITextureAtlasAPI targetAtlas;
        private Dictionary<string, AssetLocation> tmpTextures = new Dictionary<string, AssetLocation>();
        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                return getOrCreateTexPos(tmpTextures[textureCode]);
            }
        }
        protected TextureAtlasPosition getOrCreateTexPos(AssetLocation texturePath)
        {
            TextureAtlasPosition texpos = targetAtlas[texturePath];
            if (texpos == null)
            {
                IAsset texAsset = api.Assets.TryGet(texturePath.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png"), true);
                if (texAsset != null)
                {
                    int num;
                    targetAtlas.GetOrInsertTexture(texturePath, out num, out texpos, () => texAsset.ToBitmap(api as ICoreClientAPI), 0f);
                }
                else
                {
                    api.World.Logger.Warning("For render in cut gem {0}, require texture {1}, but no such texture found.", new object[]
                    {
                        Code,
                        texturePath
                    });
                }
            }
            return texpos;
        }
        public Size2i AtlasSize
        {
            get
            {
                return targetAtlas.Size;
            }
        }
        private Dictionary<int, MultiTextureMeshRef> meshrefs
        {
            get
            {
                return ObjectCacheUtil.GetOrCreate(api, "canlongswordsrefs", () => new Dictionary<int, MultiTextureMeshRef>());
            }
        }
        public MeshData GenMesh(ItemSlot slot, ITextureAtlasAPI targetAtlas, BlockPos atBlockPos)
        {
            this.targetAtlas = targetAtlas;
            tmpTextures.Clear();
            var itemstack = slot.Itemstack;
            string cuttingType = CANJWConstants.CUTTING_ROUND;
            if (itemstack.Attributes.HasAttribute(CANJWConstants.CUT_GEM_TREE))
            {
                var cut_tree = itemstack.Attributes.GetTreeAttribute(CANJWConstants.CUT_GEM_TREE);
                cuttingType = cut_tree.GetString(CANJWConstants.CUTTING_TYPE, CANJWConstants.CUTTING_ROUND);
            }
            
            Shape shapeCutGem = null;

            shapeCutGem = (api as ICoreClientAPI).Assets.TryGet("canjewelry:shapes/item/gem/cut/" + Variant["quality"]+  "/gem_" + cuttingType  + ".json").ToObject<Shape>();
            MeshData meshCutGem;


            string gemBase = Variant["gemtype"];
            if (!canjewelry.gems_textures.TryGetValue(gemBase, out string assetPath))
            {
                canjewelry.gems_textures.TryGetValue(CANJWConstants.FALLBACK_GEM_TYPE, out assetPath);
            }
            AssetLocation asset = canjewelry.capi.Assets.TryGet(assetPath + ".png")?.Location;

            tmpTextures["gem"] = asset;
            (api as ICoreClientAPI).Tesselator.TesselateShape("cut gem shape", shapeCutGem, out meshCutGem, this, null, 0, 0, 0, null, null);            
            return meshCutGem;
        }
        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            if (target == EnumItemRenderTarget.HandTp)
            {
                /* bool sneak = capi.World.Player.Entity.Controls.Sneak;
                 this.curOffY += ((sneak ? 0.4f : this.offY) - this.curOffY) * renderinfo.dt * 8f;
                 renderinfo.Transform.Translation.X = this.curOffY;
                 renderinfo.Transform.Translation.Y = this.curOffY * 1.2f;
                 renderinfo.Transform.Translation.Z = this.curOffY * 1.2f;*/
            }
            int meshrefid = itemstack.TempAttributes.GetInt("meshRefId", 0);
            if (meshrefid == 0 || !meshrefs.TryGetValue(meshrefid, out renderinfo.ModelRef))
            {
                int id = meshrefs.Count + 1;
                MultiTextureMeshRef modelref = capi.Render.UploadMultiTextureMesh(GenMesh(itemstack, capi.ItemTextureAtlas, null));
                renderinfo.ModelRef = meshrefs[id] = modelref;
                itemstack.TempAttributes.SetInt("meshRefId", id);
            }
            base.OnBeforeRender(capi, itemstack, target, ref renderinfo);
        }
        public MeshData GenMesh(ItemStack itemstack, ITextureAtlasAPI targetAtlas, BlockPos atBlockPos)
        {
            this.targetAtlas = targetAtlas;
            tmpTextures.Clear();

            string cuttingType = CANJWConstants.CUTTING_ROUND;
            if (itemstack.Attributes.HasAttribute(CANJWConstants.CUT_GEM_TREE))
            {
                var cut_tree = itemstack.Attributes.GetTreeAttribute(CANJWConstants.CUT_GEM_TREE);
                cuttingType = cut_tree.GetString(CANJWConstants.CUTTING_TYPE, CANJWConstants.CUTTING_ROUND);
            }

            Shape shapeCutGem = null;

            shapeCutGem = (api as ICoreClientAPI).Assets.TryGet("canjewelry:shapes/item/gem/cut/" + Variant["quality"] + "/gem_" + cuttingType + ".json").ToObject<Shape>();
            MeshData meshCutGem;


            string gemBase = Variant["gemtype"];
            if (!canjewelry.gems_textures.TryGetValue(gemBase, out string assetPath))
            {
                canjewelry.gems_textures.TryGetValue(CANJWConstants.FALLBACK_GEM_TYPE, out assetPath);
            }
            AssetLocation asset = canjewelry.capi.Assets.TryGet(assetPath + ".png")?.Location;

            tmpTextures["gem"] = asset;
            (api as ICoreClientAPI).Tesselator.TesselateShape("cut gem shape", shapeCutGem, out meshCutGem, this, null, 0, 0, 0, null, null);
            return meshCutGem;
        }
        public override string GetHeldItemName(ItemStack itemStack)
        {
            string bb = base.GetHeldItemName(itemStack);
            if (itemStack.Attributes.HasAttribute(CANJWConstants.CUT_GEM_TREE))
            {
                ITreeAttribute tree = itemStack.Attributes.GetTreeAttribute(CANJWConstants.CUT_GEM_TREE);
                bb += " [" + Lang.Get("canjewelry:cut-gem-cutting-type-" + tree.GetString(CANJWConstants.CUTTING_TYPE, CANJWConstants.CUTTING_ROUND)) + "]";
                //bb += "[" +  +"]";
            }
            return bb;
        }
        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            if (!canjewelry.config.TurnOffBuffs)
            {
                if (inSlot.Itemstack.Attributes.HasAttribute(CANJWConstants.CUT_GEM_TREE))
                {
                    ITreeAttribute tree = inSlot.Itemstack.Attributes.GetTreeAttribute(CANJWConstants.CUT_GEM_TREE);
                    string[] buffNames = (tree[CANJWConstants.ENCRUSTABLE_BUFFS_NAMES] as StringArrayAttribute).value;
                    float[] buffValues = (tree[CANJWConstants.ENCRUSTABLE_BUFFS_VALUES] as FloatArrayAttribute).value;

                    for (int i = 0; i < buffNames.Length; i++)
                    {
                        if (buffNames[i].Equals("maxhealthExtraPoints"))
                        {
                            dsc.Append(Lang.Get("canjewelry:buff-name-" + buffNames[i])).Append(" +" + buffValues[i].ToString());
                            dsc.AppendLine();
                        }
                        else
                        {
                            if (canjewelry.config.gems_buffs.TryGetValue(buffNames[i], out var buffValuesDict))
                            {
                                dsc.Append(Lang.Get("canjewelry:buff-name-" + buffNames[i]));
                                dsc.Append(buffValues[i] * 100 > 0 ? " +" + Math.Round(buffValues[i] * 100, 3) + "%" : " " + Math.Round(buffValues[i] * 100, 3) + "%");
                                dsc.AppendLine();
                            }
                        }
                    }
                }
                else if (inSlot.Itemstack.Collectible.Attributes.KeyExists("canGemTypeToAttribute"))
                {
                    string buffName = inSlot.Itemstack.Collectible.Attributes["canGemTypeToAttribute"].ToString();
                    if (buffName.Equals("maxhealthExtraPoints"))
                    {
                        if (canjewelry.config.gems_buffs.TryGetValue(buffName, out var buffValuesDict))
                        {
                            dsc.Append(Lang.Get("canjewelry:buff-name-" + buffName)).Append(" +" + buffValuesDict[inSlot.Itemstack.Collectible.Attributes["canGemType"].AsInt().ToString()]);
                        }
                    }
                    else if (buffName.Equals("candurability"))
                    {
                        if (canjewelry.config.gems_buffs.TryGetValue(buffName, out var buffValuesDict))
                        {
                            float buffValue = buffValuesDict[inSlot.Itemstack.Collectible.Attributes["canGemType"].AsInt().ToString()] * 100;
                            dsc.Append(Lang.Get("canjewelry:buff-name-" + buffName));
                            dsc.Append(buffValue > 0 ? " +" + Math.Round(buffValue) + "%" : " " + Math.Round(buffValue) + "%");
                        }

                    }
                    else
                    {
                        if (canjewelry.config.gems_buffs.TryGetValue(buffName, out var buffValuesDict))
                        {
                            float buffValue = buffValuesDict[inSlot.Itemstack.Collectible.Attributes["canGemType"].AsInt().ToString()] * 100;
                            dsc.Append(Lang.Get("canjewelry:buff-name-" + buffName));
                            dsc.Append(buffValue > 0 ? " +" + Math.Round(buffValue) + "%" : " " + Math.Round(buffValue) + "%");
                        }


                    }
                }
            }
            if (inSlot?.Itemstack?.Attributes.HasAttribute("cangrindlayerinfo") ?? false)
            {
                var cutGemTree = inSlot.Itemstack.Attributes.GetTreeAttribute("cangrindlayerinfo");
                if (cutGemTree == null)
                {
                    return;
                }
                if (cutGemTree.HasAttribute("grindtype"))
                {
                    dsc.AppendLine(Lang.Get("canjewelry:grinding-process-stage", cutGemTree.GetInt("grindtype")));
                }
                if (cutGemTree.HasAttribute("grindcounter"))
                {
                    dsc.AppendLine(Lang.Get("canjewelry:grinding-process-percent", string.Format("{0:0}", (((20 - cutGemTree.GetInt("grindcounter")) / 20.0) * 100)).ToString()));
                }
            }
        }
        public string GetMeshCacheKey(ItemSlot slot)
        {
            string cuttingType = slot.Itemstack.Attributes.GetString(CANJWConstants.CUTTING_TYPE, "-");

            return string.Concat(new string[]
            {
                Code.ToShortString(),
                "-",
                cuttingType
            });
        }
    }
}
