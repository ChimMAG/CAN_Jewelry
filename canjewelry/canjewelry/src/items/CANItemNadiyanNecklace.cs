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
    public class CANItemNadiyanNecklace : CANItemWearable, IWearableShapeSupplier, IAttachableToEntity
    {
        private ITextureAtlasAPI curAtlas;
        private ICoreClientAPI capi;
        private float offY;
        private float curOffY;
        public StatModifiers StatModifers;
        public override Size2i AtlasSize => curAtlas.Size;
        public int RequiresBehindSlots { get; set; }
        public string Construction
        {
            get
            {
                return this.Variant["construction"];
            }
        }
        private Dictionary<int, MultiTextureMeshRef> meshrefs
        {

            get
            {
                return ObjectCacheUtil.GetOrCreate<Dictionary<int, MultiTextureMeshRef>>(this.api, "canearringsmeshrefs", () => new Dictionary<int, MultiTextureMeshRef>());
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

            this.AddAllTypesToCreativeInventory();

            string value = Attributes["clothescategory"].AsString();
            EnumCharacterDressType result = EnumCharacterDressType.Unknown;
            Enum.TryParse<EnumCharacterDressType>(value, ignoreCase: true, out result);
            //DressType = result;

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
            List<JsonItemStack> list = new List<JsonItemStack>();
            if (this.Construction == "leather")
            {
                foreach (string arg in this.Attributes["variantGroups"].AsObject<Dictionary<string, string[]>>(null)["leather"])
                {
                    list.Add(this.genJstack(string.Format("{{ leather: \"{0}\"}}", arg)));
                }
            }
            else if(this.Construction == "metal")
            {
                foreach (string arg in this.Attributes["variantGroups"].AsObject<Dictionary<string, string[]>>(null)["metal"])
                {
                    list.Add(this.genJstack(string.Format("{{ metal: \"{0}\"}}", arg)));
                }
            }
            this.CreativeInventoryStacks = new CreativeTabAndStackList[]
            {
            new CreativeTabAndStackList
            {
                Stacks = list.ToArray(),
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
            jsonItemStack.Resolve(this.api.World, "canearrings type", true);
            return jsonItemStack;
        }
        public Shape GetShape(ItemStack stack, Entity forEntity, string texturePrefixCode)
        {
            JsonObject attributes = stack.Collectible.Attributes;
            CompositeShape compositeShape = (attributes["attachShape"].Exists ? attributes["attachShape"].AsObject<CompositeShape>(null, stack.Collectible.Code.Domain) : ((stack.Class == EnumItemClass.Item) ? stack.Item.Shape : stack.Block.Shape));

            AssetLocation assetLocation = compositeShape.Base.CopyWithPath("shapes/" + compositeShape.Base.Path + ".json");
            Shape shape2 = Vintagestory.API.Common.Shape.TryGet(this.api, assetLocation);
            return shape2;
        }
        public void CollectTextures(ItemStack stack, Shape shape, string texturePrefixCode, Dictionary<string, CompositeTexture> intoDict)
        {
            if (this.api.Side is EnumAppSide.Server)
            {
                return;
            }

            FillTextureDict(tmpTextures, stack);

            foreach (var texture in tmpTextures)
            {
                CompositeTexture ctex = new CompositeTexture() { Base = texture.Value };


                AssetLocation armorTexLoc = texture.Value;

                int textureSubId = 0;
                TextureAtlasPosition texpos;

                (this.api as ICoreClientAPI).EntityTextureAtlas.GetOrInsertTexture(armorTexLoc, out textureSubId, out texpos, () =>
                {
                    IAsset texAsset = this.capi.Assets.TryGet(armorTexLoc.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png"));
                    if (texAsset != null)
                    {
                        return texAsset.ToBitmap(capi);
                    }
                    return null;
                });

                ctex.Baked = new BakedCompositeTexture() { BakedName = armorTexLoc, TextureSubId = textureSubId };
                intoDict[texture.Key] = ctex;
            }
        }
        public void FillTextureDict(Dictionary<string, AssetLocation> dict, ItemStack itemStack)
        {
            int maxSocketNumber = EncrustableCB.GetMaxAmountSockets(itemStack);
            if(maxSocketNumber == -1)
            {
                dict["can_necklace_gem_1"] = new AssetLocation("canjewelry:item/gem/emerald.png");
                dict["can_necklace_gem_2"] = new AssetLocation("canjewelry:item/gem/lapislazuli.png");
                dict["can_necklace_gem_3"] = new AssetLocation("canjewelry:item/gem/citrine.png");
            }
            if (itemStack != null && itemStack.Attributes.HasAttribute(CANJWConstants.ITEM_ENCRUSTED_STRING))
            {
                var tree = itemStack.Attributes.GetTreeAttribute(CANJWConstants.ITEM_ENCRUSTED_STRING);
                int possibleGemsNumber = maxSocketNumber;
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
                                dict["gem_" + (i + 1)] = canjewelry.capi.Assets.TryGet(assetPath + ".png").Location;
                            }
                            else
                            {
                                dict["gem_" + (i + 1)] = new AssetLocation("canjewelry:item/gem/notvis.png");
                            }
                        }
                        else
                        {
                            dict["gem_" + (i + 1)] = new AssetLocation("canjewelry:item/gem/notvis.png");
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
                            dict["can_necklace_gem_1"] = canjewelry.capi.Assets.TryGet(assetPath + ".png").Location;
                            dict["can_necklace_gem_4"] = canjewelry.capi.Assets.TryGet(assetPath + ".png").Location;
                        }
                        else
                        {
                            dict["can_necklace_gem_1"] = new AssetLocation("canjewelry:item/gem/notvis.png");
                            dict["can_necklace_gem_4"] = new AssetLocation("canjewelry:item/gem/notvis.png");
                        }
                    }
                    else
                    {
                        dict["can_necklace_gem_1"] = new AssetLocation("canjewelry:item/gem/notvis.png");
                        dict["can_necklace_gem_4"] = new AssetLocation("canjewelry:item/gem/notvis.png");
                    }
                    if (tree.HasAttribute("slot1"))
                    {
                        ITreeAttribute treeSocket = tree.GetTreeAttribute("slot1");
                        string gemType = treeSocket.GetString("gemtype");
                        canjewelry.gems_textures.TryGetValue(gemType, out string assetPath);
                        if (assetPath != null)
                        {
                            dict["can_necklace_gem_2"] = canjewelry.capi.Assets.TryGet(assetPath + ".png").Location;
                        }
                        else
                        {
                            dict["can_necklace_gem_2"] = new AssetLocation("canjewelry:item/gem/notvis.png");
                        }
                    }
                    else
                    {
                        dict["can_necklace_gem_2"] = new AssetLocation("canjewelry:item/gem/notvis.png");
                    }
                    if (tree.HasAttribute("slot2"))
                    {
                        ITreeAttribute treeSocket = tree.GetTreeAttribute("slot2");
                        string gemType = treeSocket.GetString("gemtype");
                        canjewelry.gems_textures.TryGetValue(gemType, out string assetPath);
                        if (assetPath != null)
                        {
                            dict["can_necklace_gem_3"] = canjewelry.capi.Assets.TryGet(assetPath + ".png").Location;
                        }
                        else
                        {
                            dict["can_necklace_gem_3"] = new AssetLocation("canjewelry:item/gem/notvis.png");
                        }
                    }
                    else
                    {
                        dict["can_necklace_gem_3"] = new AssetLocation("canjewelry:item/gem/notvis.png");
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
                            dict["can_necklace_gem_1"] = canjewelry.capi.Assets.TryGet(assetPath + ".png").Location;
                            dict["can_necklace_gem_4"] = canjewelry.capi.Assets.TryGet(assetPath + ".png").Location;
                        }
                        else
                        {
                            dict["can_necklace_gem_1"] = new AssetLocation("canjewelry:item/gem/notvis.png");
                            dict["can_necklace_gem_4"] = new AssetLocation("canjewelry:item/gem/notvis.png");
                        }
                    }
                    else
                    {
                        dict["can_necklace_gem_1"] = new AssetLocation("canjewelry:item/gem/notvis.png");
                        dict["can_necklace_gem_4"] = new AssetLocation("canjewelry:item/gem/notvis.png");
                    }
                    if (tree.HasAttribute("slot1"))
                    {
                        ITreeAttribute treeSocket = tree.GetTreeAttribute("slot1");
                        string gemType = treeSocket.GetString("gemtype");
                        canjewelry.gems_textures.TryGetValue(gemType, out string assetPath);
                        if (assetPath != null)
                        {
                            dict["can_necklace_gem_2"] = canjewelry.capi.Assets.TryGet(assetPath + ".png").Location;
                            dict["can_necklace_gem_3"] = canjewelry.capi.Assets.TryGet(assetPath + ".png").Location;
                        }
                        else
                        {
                            dict["can_necklace_gem_1"] = new AssetLocation("canjewelry:item/gem/notvis.png");
                            dict["can_necklace_gem_4"] = new AssetLocation("canjewelry:item/gem/notvis.png");
                        }
                    }
                    else
                    {
                        dict["can_necklace_gem_1"] = new AssetLocation("canjewelry:item/gem/notvis.png");
                        dict["can_necklace_gem_4"] = new AssetLocation("canjewelry:item/gem/notvis.png");
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
                            dict["can_necklace_gem_1"] = canjewelry.capi.Assets.TryGet(assetPath + ".png").Location;
                            dict["can_necklace_gem_2"] = canjewelry.capi.Assets.TryGet(assetPath + ".png").Location;
                            dict["can_necklace_gem_3"] = canjewelry.capi.Assets.TryGet(assetPath + ".png").Location;
                            dict["can_necklace_gem_4"] = canjewelry.capi.Assets.TryGet(assetPath + ".png").Location;
                        }
                        else
                        {
                            dict["can_necklace_gem_1"] = new AssetLocation("canjewelry:item/gem/notvis.png");
                            dict["can_necklace_gem_2"] = new AssetLocation("canjewelry:item/gem/notvis.png");
                            dict["can_necklace_gem_3"] = new AssetLocation("canjewelry:item/gem/notvis.png");
                            dict["can_necklace_gem_4"] = new AssetLocation("canjewelry:item/gem/notvis.png");
                        }
                    }
                    else
                    {
                        dict["can_necklace_gem_1"] = new AssetLocation("canjewelry:item/gem/notvis.png");
                        dict["can_necklace_gem_2"] = new AssetLocation("canjewelry:item/gem/notvis.png");
                        dict["can_necklace_gem_3"] = new AssetLocation("canjewelry:item/gem/notvis.png");
                        dict["can_necklace_gem_4"] = new AssetLocation("canjewelry:item/gem/notvis.png");
                    }

                }
                else
                {
                    for (int i = 1; i < 5; i++)
                    {
                        dict["gem_" + i] = new AssetLocation("canjewelry:item/gem/notvis.png");
                    }
                }
            }
            else
            {
                dict["can_necklace_gem_1"] = new AssetLocation("canjewelry:item/gem/notvis.png");
                dict["can_necklace_gem_2"] = new AssetLocation("canjewelry:item/gem/notvis.png");
                dict["can_necklace_gem_3"] = new AssetLocation("canjewelry:item/gem/notvis.png");
            }


            if (this.Construction == "leather")
            {
                dict["can_necklace_leather"] = new AssetLocation("block/leather/" + itemStack.Attributes.GetString("leather", "plain") + ".png");
            }
            else if(this.Construction == "metal")
            {
                dict["can_necklace_metal"] = new AssetLocation("block/metal/sheet/" + itemStack.Attributes.GetString("metal", "gold") + "1.png");
            }
            dict["gems"] = new AssetLocation("canjewelry:item/gem/notvis.png");
            dict["plain"] = new AssetLocation("game:block/leather/plain");
        }
        public string GetCategoryCode(ItemStack stack)
        {
            return "canearrings";
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
            return this.GetMeshCacheKey(stack);
        }
        public override string GetMeshCacheKey(ItemStack itemstack)
        {
            string constructon = itemstack.Item.Variant["construction"];
             string materialType = itemstack.Attributes.GetString("leather", "orange");
            var tree = itemstack.Attributes.GetTreeAttribute(CANJWConstants.ITEM_ENCRUSTED_STRING);
            string buildStr = constructon + materialType;
            if (tree != null)
            {
                int slotCount = tree.GetInt(CANJWConstants.SOCKET_ADDED_NUMBER, 0);
                for(int i = 0; i < slotCount; i++)
                {
                    if (tree.HasAttribute("slot" + i.ToString()))
                    {
                        var innerTree = tree.GetTreeAttribute("slot" + i.ToString());
                        if(innerTree.HasAttribute(CANJWConstants.GEM_TYPE_IN_SOCKET))
                        {
                            buildStr += innerTree.GetString(CANJWConstants.GEM_TYPE_IN_SOCKET, "");
                        }
                    }
                }
               
            }
            return buildStr;
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

            string maskMetal = inSlot.Itemstack.Item.Variant.Get("loop", "steel");

            /* if (gem != "none")
            {
                dsc.AppendLine(Lang.Get("canjewelry:necklace-parts-with-gem-held-info", Lang.Get("material-" + loop), Lang.Get("material-" + socket), gem));
            }
            else
            {
                dsc.AppendLine(Lang.Get("canjewelry:necklace-parts-without-gem-held-info", Lang.Get("material-" + loop), Lang.Get("material-" + socket)));
            }*/
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
        public override MeshData genMesh(ICoreClientAPI capi, ItemStack itemstack, ITexPositionSource texSource)
        {
            this.tmpTextures.Clear();
            this.FillTextureDict(tmpTextures, itemstack);
            return base.genMesh(capi, itemstack, texSource);
        }
        public override string GetHeldItemName(ItemStack itemStack)
        {
            return Lang.Get("canjewelry:item-" + itemStack.Collectible.Code.Path.ToString());
        }
        public bool IsAttachable(Entity toEntity, ItemStack itemStack)
        {
            return true;
        }
    }
}
