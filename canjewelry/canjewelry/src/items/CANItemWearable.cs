using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace canjewelry.src.items
{
    public class CANItemWearable : Item, IContainedMeshSource, ITexPositionSource
    {
        protected Shape nowTesselatingShape;
        public static AssetLocation NotVisTexture;
        public virtual TextureAtlasPosition this[string textureCode] => throw new NotImplementedException();
        public virtual Size2i AtlasSize => throw new NotImplementedException();
        public virtual MeshData GenMesh(ItemSlot slot, ITextureAtlasAPI targetAtlas, BlockPos atBlockPos)
        {
            throw new NotImplementedException();
        }
        public virtual string GetMeshCacheKey(ItemSlot slot)
        {
            throw new NotImplementedException();
        }
        public virtual MeshData genMesh(ICoreClientAPI capi, ItemStack itemstack, ITexPositionSource texSource)
        {
            JsonObject attributes = itemstack.Collectible.Attributes;
            EntityProperties entityType = capi.World.GetEntityType(new AssetLocation(attributes?["wearerEntityCode"].ToString() ?? "player"));
            Shape loadedShape = entityType.Client.LoadedShape;
            AssetLocation @base = entityType.Client.Shape.Base;
            Shape shape = new Shape
            {
                Elements = loadedShape.CloneElements(),
                Animations = loadedShape.CloneAnimations(),
                AnimationsByCrc32 = loadedShape.AnimationsByCrc32,
                JointsById = loadedShape.JointsById,
                TextureWidth = loadedShape.TextureWidth,
                TextureHeight = loadedShape.TextureHeight,
                Textures = null
            };
            MeshData modeldata;
            if (attributes["wearableInvShape"].Exists)
            {
                AssetLocation shapePath = new AssetLocation("shapes/" + attributes["wearableInvShape"]?.ToString() + ".json");
                Shape shape2 = Vintagestory.API.Common.Shape.TryGet(capi, shapePath);
                capi.Tesselator.TesselateShape(itemstack.Collectible, shape2, out modeldata);
            }
            else
            {
                CompositeShape compositeShape = (attributes["attachShape"].Exists ? attributes["attachShape"].AsObject<CompositeShape>(null, itemstack.Collectible.Code.Domain) : ((itemstack.Class == EnumItemClass.Item) ? itemstack.Item.Shape : itemstack.Block.Shape));
                if (compositeShape == null)
                {
                    capi.World.Logger.Warning("Wearable shape {0} {1} does not define a shape through either the shape property or the attachShape Attribute. Item will be invisible.", itemstack.Class, itemstack.Collectible.Code);
                    return null;
                }

                AssetLocation assetLocation = compositeShape.Base.CopyWithPathPrefixAndAppendixOnce("shapes/", ".json");
                Shape shape3 = Vintagestory.API.Common.Shape.TryGet(capi, assetLocation);
                if (shape3 == null)
                {
                    capi.World.Logger.Warning("Wearable shape {0} defined in {1} {2} not found or errored, was supposed to be at {3}. Item will be invisible.", compositeShape.Base, itemstack.Class, itemstack.Collectible.Code, assetLocation);
                    return null;
                }

                shape.StepParentShape(shape3, assetLocation.ToShortString(), @base.ToShortString(), capi.Logger, delegate
                {
                });
                if (compositeShape.Overlays != null)
                {
                    CompositeShape[] overlays = compositeShape.Overlays;
                    foreach (CompositeShape compositeShape2 in overlays)
                    {
                        Shape shape4 = Vintagestory.API.Common.Shape.TryGet(capi, compositeShape2.Base.CopyWithPathPrefixAndAppendixOnce("shapes/", ".json"));
                        if (shape4 == null)
                        {
                            capi.World.Logger.Warning("Wearable shape {0} overlay {4} defined in {1} {2} not found or errored, was supposed to be at {3}. Item will be invisible.", compositeShape.Base, itemstack.Class, itemstack.Collectible.Code, assetLocation, compositeShape2.Base);
                        }
                        else
                        {
                            shape.StepParentShape(shape4, compositeShape2.Base.ToShortString(), @base.ToShortString(), capi.Logger, delegate
                            {
                            });
                        }
                    }
                }

                nowTesselatingShape = shape;
                capi.Tesselator.TesselateShapeWithJointIds("entity", shape, out modeldata, texSource, new Vec3f());
                nowTesselatingShape = null;
            }

            return modeldata;
        }
    }
}
