using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using canjewelry.src.CB;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace canjewelry.src.items
{
    public class CANItemMonocle: CANItemWearable, IWearableShapeSupplier, IAttachableToEntity
    {
        private ITextureAtlasAPI curAtlas;
        private ICoreClientAPI capi;
        private float offY;
        private float curOffY;
        public StatModifiers StatModifers;
        public override Size2i AtlasSize => curAtlas.Size;
        public int RequiresBehindSlots { get; set; }
        private Dictionary<int, MultiTextureMeshRef> meshrefs
        {

            get
            {
                return ObjectCacheUtil.GetOrCreate<Dictionary<int, MultiTextureMeshRef>>(this.api, "canmonoclemeshrefs", () => new Dictionary<int, MultiTextureMeshRef>());
            }
        }
        public EnumCharacterDressType DressType { get; private set; }
        private Dictionary<string, AssetLocation> tmpTextures = new Dictionary<string, AssetLocation>();
        protected TextureAtlasPosition getOrCreateTexPos(AssetLocation texturePath)
        {
            ICoreClientAPI capi = api as ICoreClientAPI;
            curAtlas.GetOrInsertTexture(texturePath, out var _, out var texPos, delegate
            {
                IAsset asset = capi.Assets.TryGet(texturePath.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png"));
                if (asset != null)
                {
                    return asset.ToBitmap(capi);
                }

                capi.World.Logger.Warning("Item {0} defined texture {1}, not no such texture found.", Code, texturePath);
                return null;
            }, 0.1f);
            return texPos;
        }
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            this.curOffY = (this.offY = this.FpHandTransform.Translation.Y);
            this.capi = (api as ICoreClientAPI);

            string value = Attributes["clothescategory"].AsString();
            EnumCharacterDressType result = EnumCharacterDressType.Unknown;
            Enum.TryParse<EnumCharacterDressType>(value, ignoreCase: true, out result);
            //DressType = result;
            AddAllTypesToCreativeInventory();
            JsonObject jsonObject = Attributes?["statModifiers"];
            if (jsonObject != null && jsonObject.Exists)
            {
                try
                {
                    StatModifers = jsonObject.AsObject<StatModifiers>();
                }
                catch (Exception ex)
                {
                    api.World.Logger.Error("Failed loading statModifiers for item/block {0}. Will ignore. Exception: {1}", Code, ex);
                    StatModifers = null;
                }
            }

            ProtectionModifiers protectionModifiers = null;
            jsonObject = Attributes?["defaultProtLoss"];
            if (jsonObject != null && jsonObject.Exists)
            {
                try
                {
                    protectionModifiers = jsonObject.AsObject<ProtectionModifiers>();
                }
                catch (Exception ex2)
                {
                    api.World.Logger.Error("Failed loading defaultProtLoss for item/block {0}. Will ignore. Exception: {1}", Code, ex2);
                }
            }
        }
        public void AddAllTypesToCreativeInventory()
        {
            List<JsonItemStack> stacks = new List<JsonItemStack>();
            Dictionary<string, string[]> vg = this.Attributes["variantGroups"].AsObject<Dictionary<string, string[]>>(null);

            Random r = new Random();
            string[] loops = vg["metal"];
            string[] glassTypes = vg["glass"];
            foreach (string loop in loops)
            {
                foreach (var glass in glassTypes)
                {
                    stacks.Add(this.genJstack(string.Format("{{ loop: \"{0}\", glasstype: \"{1}\" }}", loop, glass)));
                }
            }
            this.CreativeInventoryStacks = new CreativeTabAndStackList[]
            {
                new CreativeTabAndStackList
                {
                    Stacks = stacks.ToArray(),
                    Tabs = new string[]
                    {
                        "general",
                        "items",
                        "canjewelry"
                    }
                }
            };
        }
        private JsonItemStack genJstack(string json)
        {
            JsonItemStack jsonItemStack = new JsonItemStack();
            jsonItemStack.Code = this.Code;
            jsonItemStack.Type = EnumItemClass.Item;
            jsonItemStack.Attributes = new JsonObject(JToken.Parse(json));
            jsonItemStack.Resolve(this.api.World, "canmonocle type", true);
            return jsonItemStack;
        }
        public Shape GetShape(ItemStack stack, Entity forEntity, string texturePrefixCode)
        {
            Shape gearShape = null;
            CompositeShape compGearShape = null;
            JsonObject attrObj = stack.Collectible.Attributes;
            compGearShape = ((!attrObj["attachShape"].Exists) ? ((stack.Class == EnumItemClass.Item) ? stack.Item.Shape : stack.Block.Shape) : attrObj["attachShape"].AsObject<CompositeShape>(null, stack.Collectible.Code.Domain));
            string eyeSide = stack.Attributes.GetString("eye", "right");
            AssetLocation shapePath = compGearShape.Base.CopyWithPath("shapes/" + compGearShape.Base.Path + "_" + eyeSide + ".json");
            gearShape = Vintagestory.API.Common.Shape.TryGet(api, shapePath);
            if (gearShape == null)
            {
                api.World.Logger.Warning("Entity armor shape {0} defined in {1} {2} not found or errored, was supposed to be at {3}. Armor piece will be invisible.", new object[]
                {
                        compGearShape.Base,
                        stack.Class,
                        stack.Collectible.Code,
                        shapePath
                });
                return null;
            }
            return gearShape;
        }
        public void CollectTextures(ItemStack stack, Shape shape, string texturePrefixCode, Dictionary<string, CompositeTexture> intoDict)
        {
            if (this.api.Side is EnumAppSide.Server)
            {
                return;
            }

            string carcassus = stack.Attributes.GetString("loop", null);
            tmpTextures["brass"] = new AssetLocation("block/metal/sheet/" + carcassus + "1.png");
            string qurtzType = stack.Attributes.GetString("glasstype", "red");
            tmpTextures["quartzglass"] = new AssetLocation("block/glass/" + qurtzType + ".png");
            FillTextureDict(tmpTextures, stack);

            foreach (var texture in tmpTextures)
            {
                intoDict[texture.Key] = new CompositeTexture() { Base = texture.Value };
                shape.Textures[texture.Key] = texture.Value;
            }
        }
        public string GetCategoryCode(ItemStack stack)
        {
            return "canmonocle";
        }
        public CompositeShape GetAttachedShape(ItemStack stack, string slotCode)
        {
            return this.Shape;
        }
        public string[] GetDisableElements(ItemStack stack)
        {
            return null;
        }
        public string[] GetKeepElements(ItemStack stack)
        {
            return null;
        }
        public string GetTexturePrefixCode(ItemStack stack)
        {
            return "";
        }
        public override string GetMeshCacheKey(ItemStack itemstack)
        {
            string metal = itemstack.Attributes.GetString("metal", null);
            return string.Concat(new string[]
            {
                this.Code.ToShortString(),
                "-",
                metal
            });
        }
        public override TextureAtlasPosition this[string textureCode]
        {
            get
            {

                if (this.tmpTextures.TryGetValue(textureCode, out var res))
                {
                    return this.getOrCreateTexPos(res);
                }

                AssetLocation value = null;
                if (textureCode == "metal")
                {
                    value = this.Textures["metal"].Base;
                }
                if (Textures.TryGetValue(textureCode, out var value2))
                {
                    value = value2.Baked.BakedName;
                }

                if (value == null && Textures.TryGetValue("all", out value2))
                {
                    value = value2.Baked.BakedName;
                }

                if (value == null)
                {
                    nowTesselatingShape?.Textures.TryGetValue(textureCode, out value);
                }

                if (value == null)
                {
                    value = new AssetLocation(textureCode);
                }

                return getOrCreateTexPos(value);
            }
        }
        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            string maskMetal = inSlot.Itemstack.Attributes.GetString("metal", null);

            if ((api as ICoreClientAPI).Settings.Bool["extendedDebugInfo"])
            {
                if (DressType == EnumCharacterDressType.Unknown)
                {
                    dsc.AppendLine(Lang.Get("Cloth Category: Unknown"));
                }
                else
                {
                    dsc.AppendLine(Lang.Get("Cloth Category: {0}", Lang.Get("clothcategory-" + inSlot.Itemstack.ItemAttributes["clothescategory"].AsString())));
                }
            }

        }
        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            if (target == EnumItemRenderTarget.HandTp)
            {
                bool sneak = capi.World.Player.Entity.Controls.Sneak;
                this.curOffY += ((sneak ? 0.4f : this.offY) - this.curOffY) * renderinfo.dt * 8f;
                renderinfo.Transform.Translation.X = this.curOffY;
                renderinfo.Transform.Translation.Y = this.curOffY * 1.2f;
                renderinfo.Transform.Translation.Z = this.curOffY * 1.2f;
            }
            int meshrefid = itemstack.TempAttributes.GetInt("meshRefId", 0);
            if (meshrefid == 0 || !this.meshrefs.TryGetValue(meshrefid, out renderinfo.ModelRef))
            {
                int id = this.meshrefs.Count + 1;
                MultiTextureMeshRef modelref = capi.Render.UploadMultiTextureMesh(this.GenMesh(itemstack, capi.ItemTextureAtlas));
                renderinfo.ModelRef = (this.meshrefs[id] = modelref);
                itemstack.TempAttributes.SetInt("meshRefId", id);
            }
            base.OnBeforeRender(capi, itemstack, target, ref renderinfo);
        }
        public override MeshData GenMesh(ItemStack itemstack, ITextureAtlasAPI targetAtlas, BlockPos forBlockPos = null)
        {
            ICoreClientAPI coreClientAPI = api as ICoreClientAPI;
            curAtlas = targetAtlas;
            if (targetAtlas == coreClientAPI.ItemTextureAtlas)
            {
                ITexPositionSource textureSource = coreClientAPI.Tesselator.GetTextureSource(itemstack.Item);
                return genMesh(coreClientAPI, itemstack, this);
            }

            curAtlas = targetAtlas;
            MeshData meshData = genMesh(api as ICoreClientAPI, itemstack, this);
            meshData.RenderPassesAndExtraBits.Fill((short)1);
            return meshData;
        }
        public void FillTextureDict(Dictionary<string, AssetLocation> dict, ItemStack itemStack)
        {
            if (itemStack != null && itemStack.Attributes.HasAttribute(CANJWConstants.ITEM_ENCRUSTED_STRING))
            {
                var tree = itemStack.Attributes.GetTreeAttribute(CANJWConstants.ITEM_ENCRUSTED_STRING);
                int possibleGemsNumber = EncrustableCB.GetMaxAmountSockets(itemStack);
                if (possibleGemsNumber >= 4)
                {
                    for (int i = 0; i < possibleGemsNumber; i++)
                    {
                        if (tree.HasAttribute("slot" + i))
                        {
                            ITreeAttribute treeSocket = tree.GetTreeAttribute("slot" + i);
                            string gemType = treeSocket.GetString("gemtype");
                            canjewelry.gems_textures.TryGetValue(gemType, out string assetPath);
                            if (assetPath != null)
                            {
                                dict["gems_" + (i + 1)] = canjewelry.capi.Assets.TryGet(assetPath + ".png").Location;
                            }
                            else
                            {
                                dict["gems_" + (i + 1)] = new AssetLocation("canjewelry:item/gem/notvis.png");
                            }
                        }
                        else
                        {
                            dict["gems_" + (i + 1)] = new AssetLocation("canjewelry:item/gem/notvis.png");
                        }
                    }
                }
                else if (possibleGemsNumber == 3)
                {
                    if (tree.HasAttribute("slot0"))
                    {
                        ITreeAttribute treeSocket = tree.GetTreeAttribute("slot0");
                        string gemType = treeSocket.GetString("gemtype");
                        canjewelry.gems_textures.TryGetValue(gemType, out string assetPath);
                        if (assetPath != null)
                        {
                            dict["gems_1"] = canjewelry.capi.Assets.TryGet(assetPath + ".png").Location;
                            dict["gems_4"] = canjewelry.capi.Assets.TryGet(assetPath + ".png").Location;
                        }
                        else
                        {
                            dict["gems_1"] = new AssetLocation("canjewelry:item/gem/notvis.png");
                            dict["gems_4"] = new AssetLocation("canjewelry:item/gem/notvis.png");
                        }
                    }
                    if (tree.HasAttribute("slot1"))
                    {
                        ITreeAttribute treeSocket = tree.GetTreeAttribute("slot1");
                        string gemType = treeSocket.GetString("gemtype");
                        canjewelry.gems_textures.TryGetValue(gemType, out string assetPath);
                        if (assetPath != null)
                        {
                            dict["gems_2"] = canjewelry.capi.Assets.TryGet(assetPath + ".png").Location;
                        }
                        else
                        {
                            dict["gems_2"] = new AssetLocation("canjewelry:item/gem/notvis.png");
                        }
                    }
                    if (tree.HasAttribute("slot2"))
                    {
                        ITreeAttribute treeSocket = tree.GetTreeAttribute("slot2");
                        string gemType = treeSocket.GetString("gemtype");
                        canjewelry.gems_textures.TryGetValue(gemType, out string assetPath);
                        if (assetPath != null)
                        {
                            dict["gems_3"] = canjewelry.capi.Assets.TryGet(assetPath + ".png").Location;
                        }
                        else
                        {
                            dict["gems_3"] = new AssetLocation("canjewelry:item/gem/notvis.png");
                        }
                    }
                }
                else if (possibleGemsNumber == 2)
                {
                    if (tree.HasAttribute("slot0"))
                    {
                        ITreeAttribute treeSocket = tree.GetTreeAttribute("slot0");
                        string gemType = treeSocket.GetString("gemtype");
                        canjewelry.gems_textures.TryGetValue(gemType, out string assetPath);
                        if (assetPath != null)
                        {
                            dict["gems_1"] = canjewelry.capi.Assets.TryGet(assetPath + ".png").Location;
                            dict["gems_4"] = canjewelry.capi.Assets.TryGet(assetPath + ".png").Location;
                        }
                        else
                        {
                            dict["gems_1"] = new AssetLocation("canjewelry:item/gem/notvis.png");
                            dict["gems_4"] = new AssetLocation("canjewelry:item/gem/notvis.png");
                        }
                    }
                    if (tree.HasAttribute("slot1"))
                    {
                        ITreeAttribute treeSocket = tree.GetTreeAttribute("slot1");
                        string gemType = treeSocket.GetString("gemtype");
                        canjewelry.gems_textures.TryGetValue(gemType, out string assetPath);
                        if (assetPath != null)
                        {
                            dict["gems_2"] = canjewelry.capi.Assets.TryGet(assetPath + ".png").Location;
                            dict["gems_3"] = canjewelry.capi.Assets.TryGet(assetPath + ".png").Location;
                        }
                        else
                        {
                            dict["gems_2"] = new AssetLocation("canjewelry:item/gem/notvis.png");
                            dict["gems_3"] = new AssetLocation("canjewelry:item/gem/notvis.png");
                        }
                    }
                }
                else if (possibleGemsNumber == 1)
                {
                    if (tree.HasAttribute("slot0"))
                    {
                        ITreeAttribute treeSocket = tree.GetTreeAttribute("slot0");
                        string gemType = treeSocket.GetString("gemtype");
                        canjewelry.gems_textures.TryGetValue(gemType, out string assetPath);
                        if (assetPath != null)
                        {
                            dict["gems_1"] = canjewelry.capi.Assets.TryGet(assetPath + ".png").Location;
                            dict["gems_2"] = canjewelry.capi.Assets.TryGet(assetPath + ".png").Location;
                            dict["gems_3"] = canjewelry.capi.Assets.TryGet(assetPath + ".png").Location;
                            dict["gems_4"] = canjewelry.capi.Assets.TryGet(assetPath + ".png").Location;
                        }
                        else
                        {
                            dict["gems_1"] = new AssetLocation("canjewelry:item/gem/notvis.png");
                            dict["gems_2"] = new AssetLocation("canjewelry:item/gem/notvis.png");
                            dict["gems_3"] = new AssetLocation("canjewelry:item/gem/notvis.png");
                            dict["gems_4"] = new AssetLocation("canjewelry:item/gem/notvis.png");
                        }
                    }

                }
                else
                {
                    for (int i = 1; i < 5; i++)
                    {
                        dict["gems_" + i] = new AssetLocation("canjewelry:item/gem/notvis.png");
                    }
                }
            }
            else
            {
                for (int i = 1; i < 5; i++)
                {
                    dict["gems_" + i] = new AssetLocation("canjewelry:item/gem/notvis.png");
                }
            }



            //dict["metal"] = itemStack.Item.Textures["metal"].Base;
            //dict["gems"] = new AssetLocation("canjewelry:item/gem/notvis.png");
        }
        public override MeshData genMesh(ICoreClientAPI capi, ItemStack itemstack, ITexPositionSource texSource)
        {
            string carcassus = itemstack.Attributes.GetString("loop", "steel");
            this.tmpTextures.Clear();
            tmpTextures["brass"] = new AssetLocation("block/metal/ingot/" + carcassus + ".png");
            string qurtzType = itemstack.Attributes.GetString("glasstype", "red");
            tmpTextures["quartzglass"] = new AssetLocation("block/glass/" + qurtzType + ".png");
            this.FillTextureDict(tmpTextures, itemstack);
            return base.genMesh(capi, itemstack, texSource);
        }
        public override string GetHeldItemName(ItemStack itemStack)
        {
            string carcassus = itemStack.Attributes.GetString("loop", "steel");
            return Lang.Get("game:material-" + carcassus) + Lang.Get("canjewelry:item-monocle");
        }
        public override void OnCreatedByCrafting(ItemSlot[] allInputslots, ItemSlot outputSlot, GridRecipe byRecipe)
        {
            if(byRecipe.Name.Path == "can-monocle-change-side")
            {
                ItemSlot monocleSlot = allInputslots.FirstOrDefault(sl => !sl.Empty);
                if(monocleSlot != null)
                {
                    foreach(var attr in monocleSlot.Itemstack.Attributes)
                    {
                        outputSlot.Itemstack.Attributes[attr.Key] = attr.Value;
                    }
                    if(outputSlot.Itemstack.Attributes.HasAttribute("eye"))
                    {
                        outputSlot.Itemstack.Attributes.SetString("eye", outputSlot.Itemstack.Attributes.GetString("eye") == "left" ? "right" : "left");
                    }
                    else
                    {
                        outputSlot.Itemstack.Attributes.SetString("eye", "right");
                    }
                    return;
                }
            }
            base.OnCreatedByCrafting(allInputslots, outputSlot, byRecipe);

        }
        public bool IsAttachable(Entity toEntity, ItemStack itemStack)
        {
            return true;
        }
    }
}
