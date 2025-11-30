using System;
using System.Collections.Generic;
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
    public class CANItemTiara: CANItemWearable, IWearableShapeSupplier, IAttachableToEntity
    {
        public override Size2i AtlasSize => curAtlas.Size;
        public int RequiresBehindSlots { get; set; }
        private Dictionary<int, MultiTextureMeshRef> meshrefs
        {
            get
            {
                return ObjectCacheUtil.GetOrCreate<Dictionary<int, MultiTextureMeshRef>>(this.api, "cantiarameshrefs", () => new Dictionary<int, MultiTextureMeshRef>());
            }
        }
        private ITextureAtlasAPI curAtlas;
        public EnumCharacterDressType DressType { get; private set; }
        public StatModifiers StatModifers;
        private float offY;
        private float curOffY;
        private ICoreClientAPI capi;
        private Dictionary<string, AssetLocation> tmpTextures = new Dictionary<string, AssetLocation>();
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
            DressType = result;

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

            foreach (string carcassus in vg["carcassus"])
            {
                stacks.Add(this.genJstack(string.Format("{{ carcassus: \"{0}\", gem_1: \"none\", gem_2: \"none\", gem_3: \"none\" }}", carcassus)));              
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
            jsonItemStack.Resolve(this.api.World, "cantiara type", true);
            return jsonItemStack;
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

        public override MeshData genMesh(ICoreClientAPI capi, ItemStack itemstack, ITexPositionSource texSource)
        {
            this.tmpTextures.Clear();
            this.FillTextureDict(tmpTextures, itemstack);
            return base.genMesh(capi, itemstack, texSource);
        }

        public override string GetHeldItemName(ItemStack itemStack)
        {
            string variant = itemStack.Attributes.GetString("carcassus", "steel");
            return Lang.Get("game:material-" + variant) + Lang.Get("canjewelry:item-tiara");
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

           /* string loop = inSlot.Itemstack.Attributes.GetString("loop", null);
            string socket = inSlot.Itemstack.Attributes.GetString("socket", null);
            string gem = inSlot.Itemstack.Attributes.GetString("gem", null);*/

            /*if (gem != "none")
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

        public override MeshData GenMesh(ItemStack itemstack, ITextureAtlasAPI targetAtlas, BlockPos forBlockPos = null)
        {
            ICoreClientAPI coreClientAPI = api as ICoreClientAPI;
            curAtlas = targetAtlas;
            if (targetAtlas == coreClientAPI.ItemTextureAtlas)
            {
                ITexPositionSource textureSource = coreClientAPI.Tesselator.GetTextureSource(itemstack.Item);
                return genMesh(coreClientAPI, itemstack, this);
                /* ITexPositionSource textureSource = coreClientAPI.Tesselator.GetTextureSource(itemstack.Item);
                MeshData meshData1 =  genMesh(coreClientAPI, itemstack, this);
                MeshData meshData2 = genMesh(coreClientAPI, itemstack, this);
                meshData2.Rotate(new Vec3f(0.5f, 0.5f, 0.6f), 40, 40, 40);
                meshData1.AddMeshData(meshData2);
                return meshData1;*/
            }

            curAtlas = targetAtlas;
            MeshData meshData = genMesh(api as ICoreClientAPI, itemstack, this);
            meshData.RenderPassesAndExtraBits.Fill((short)1);
            return meshData;
        }

        public override string GetMeshCacheKey(ItemStack itemstack)
        {
            string carcassus = itemstack.Attributes.GetString("carcassus", null);
            string gem_1 = itemstack.Attributes.GetString("gem_1", null);
            string gem_2 = itemstack.Attributes.GetString("gem_2", null);
            string gem_3 = itemstack.Attributes.GetString("gem_3", null);

            return string.Concat(new string[]
            {
                this.Code.ToShortString(),
                "-",
                carcassus,
                "-",
                gem_1,
                "-",
                gem_2,
                "-",
                gem_3
            });
        }
        public void FillTextureDict(Dictionary<string, AssetLocation> newdict, ItemStack stack)
        {
            string carcassus = stack.Attributes.GetString("carcassus", "steel");
            newdict["carcassus"] = new AssetLocation("block/metal/sheet/" + carcassus + "1.png");
            int maxSocketNumber = EncrustableCB.GetMaxAmountSockets(stack);
            if (maxSocketNumber == 1)
            {
                AssetLocation path;
                if (!canjewelry.gems_textures.TryGetValue(stack.Attributes.GetString("gem_1", "none"), out string assetPath))
                {
                    path = new AssetLocation("canjewelry:item/gem/notvis.png");
                }
                else
                {
                    path = canjewelry.capi.Assets.TryGet(assetPath + ".png").Location;
                }
                for (int i = 1; i < 4; i++)
                {
                    newdict[i.ToString() + "_gem"] = path;
                }
            }
            else if (maxSocketNumber == 2)
            {
                AssetLocation path;
                if (!canjewelry.gems_textures.TryGetValue(stack.Attributes.GetString("gem_1", "none"), out string assetPath))
                {
                    path = new AssetLocation("canjewelry:item/gem/notvis.png");
                }
                else
                {
                    path = canjewelry.capi.Assets.TryGet(assetPath + ".png").Location;
                }
                newdict["1_gem"] = path;
                newdict["3_gem"] = path;

                if (!canjewelry.gems_textures.TryGetValue(stack.Attributes.GetString("gem_2", "none"), out assetPath))
                {
                    path = new AssetLocation("canjewelry:item/gem/notvis.png");
                }
                else
                {
                    path = canjewelry.capi.Assets.TryGet(assetPath + ".png").Location;
                }
                newdict["2_gem"] = path;
            }
            else
            {
                for (int i = 1; i < 4; i++)
                {
                    if (!canjewelry.gems_textures.TryGetValue(stack.Attributes.GetString("gem_" + i.ToString(), "none"), out string assetPath))
                    {
                        newdict[i.ToString() + "_gem"] = new AssetLocation("canjewelry:item/gem/notvis.png");
                    }
                    else
                    {
                        newdict[i.ToString() + "_gem"] = canjewelry.capi.Assets.TryGet(assetPath + ".png").Location;
                    }
                }
            }
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

            string carcassus = stack.Attributes.GetString("carcassus", null);
            tmpTextures["carcassus"] = new AssetLocation("block/metal/sheet/" + carcassus + "1.png");

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

        public string GetCategoryCode(ItemStack stack)
        {
            return "cantiara";
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
        public bool IsAttachable(Entity toEntity, ItemStack itemStack)
        {
            return true;
        }
    }
}
