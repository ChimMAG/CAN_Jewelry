using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace canjewelry.src.eb
{
    public abstract class CANEntityBehaviorContainer: EntityBehavior
    {
        protected ICoreAPI Api;

        private InWorldContainer container;

        public bool hideClothing;

        private bool eventRegistered;

        private bool dropContentsOnDeath;

        public abstract InventoryBase Inventory { get; }

        public abstract string InventoryClassName { get; }

        protected CANEntityBehaviorContainer(Entity entity)
            : base(entity)
        {
            container = new InWorldContainer(() => Inventory, InventoryClassName);
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            Api = entity.World.Api;
            container.Init(Api, () => entity.Pos.AsBlockPos, delegate
            {
                entity.WatchedAttributes.MarkPathDirty(InventoryClassName);
            });
            if (Api.Side == EnumAppSide.Client)
            {
                entity.WatchedAttributes.RegisterModifiedListener(InventoryClassName, inventoryModified);
            }

            dropContentsOnDeath = attributes?.IsTrue("dropContentsOnDeath") ?? false;
        }

        private void inventoryModified()
        {
            loadInv();
            entity.MarkShapeModified();
        }

        public override void OnGameTick(float deltaTime)
        {
            if (!eventRegistered && Inventory != null)
            {
                eventRegistered = true;
                Inventory.SlotModified += Inventory_SlotModified;
            }
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            if (Inventory != null)
            {
                Inventory.SlotModified -= Inventory_SlotModified;
            }
        }

        protected void Inventory_SlotModifiedBackpack(int slotid)
        {
            if (entity is EntityPlayer entityPlayer && entityPlayer.Player.InventoryManager.GetOwnInventory("backpack")[slotid] is ItemSlotBackpack)
            {
                entity.MarkShapeModified();
            }
        }

        protected virtual void Inventory_SlotModified(int slotid)
        {
            entity.MarkShapeModified();
        }

        public override void OnTesselation(ref Shape entityShape, string shapePathForLogging, ref bool shapeIsCloned, ref string[] willDeleteElements)
        {
            addGearToShape(ref entityShape, shapePathForLogging, ref shapeIsCloned, ref willDeleteElements);
            base.OnTesselation(ref entityShape, shapePathForLogging, ref shapeIsCloned, ref willDeleteElements);
            if (Inventory != null)
            {
                ItemSlot itemSlot = Inventory.MaxBy((ItemSlot slot) => (!slot.Empty) ? slot.Itemstack.Collectible.LightHsv[2] : 0);
                if (!itemSlot.Empty && itemSlot.Itemstack.Collectible.LightHsv[2] > 0)
                {
                    byte[] jewelryLight = itemSlot.Itemstack.Collectible.GetLightHsv(entity.World.BlockAccessor, null, itemSlot.Itemstack);
                    if (entity.LightHsv == null || jewelryLight[2] > entity.LightHsv[2])
                    {
                        entity.LightHsv = jewelryLight;
                    }
                }
            }
        }

        protected Shape addGearToShape(ref Shape entityShape, string shapePathForLogging, ref bool shapeIsCloned, ref string[] willDeleteElements)
        {
            IInventory inventory = Inventory;
            if (inventory == null || (!(entity is EntityPlayer) && inventory.Empty))
            {
                return entityShape;
            }

            for (int i = 0; i < inventory.Count; i++)
            {
                ItemSlot itemSlot = inventory[i];
                if (!itemSlot.Empty && !hideClothing)
                {
                    entityShape = addGearToShape(entityShape, itemSlot, i.ToString(), shapePathForLogging, ref shapeIsCloned, ref willDeleteElements);
                }
            }
            if (shapeIsCloned && Api is ICoreClientAPI coreClientAPI)
            {
                EntityProperties entityType = Api.World.GetEntityType(entity.Code);
                if (entityType != null)
                {
                    foreach (KeyValuePair<string, CompositeTexture> texture in entityType.Client.Textures)
                    {
                        
                        CompositeTexture value = texture.Value;
                        if (value.Baked.TextureFilenames[0].Path.Equals("entity/humanoid/seraph-naked-hairless"))
                        {
                            continue;
                        }
                        value.Bake(Api.Assets);
                        coreClientAPI.EntityTextureAtlas.GetOrInsertTexture(value.Baked.TextureFilenames[0], out var textureSubId, out var _);
                        value.Baked.TextureSubId = textureSubId;
                        entity.Properties.Client.Textures[texture.Key] = texture.Value;
                    }
                }
            }

            return entityShape;
        }

        protected virtual Shape addGearToShape(Shape entityShape, ItemSlot gearslot, string slotCode, string shapePathForLogging, ref bool shapeIsCloned, ref string[] willDeleteElements, Dictionary<string, StepParentElementTo> overrideStepParent = null)
        {
            if (gearslot.Empty || entityShape == null)
            {
                return entityShape;
            }

            IAttachableToEntity attachableToEntity = IAttachableToEntity.FromCollectible(gearslot.Itemstack.Collectible);
            if (attachableToEntity == null || !attachableToEntity.IsAttachable(entity, gearslot.Itemstack))
            {
                return entityShape;
            }

            if (!shapeIsCloned)
            {
                entityShape = entityShape.Clone();
                shapeIsCloned = true;
            }

            return addGearToShape(entityShape, gearslot.Itemstack, attachableToEntity, slotCode, shapePathForLogging, ref willDeleteElements, overrideStepParent);
        }

        protected virtual Shape addGearToShape(Shape entityShape, ItemStack stack, IAttachableToEntity iatta, string slotCode, string shapePathForLogging, ref string[] willDeleteElements, Dictionary<string, StepParentElementTo> overrideStepParent = null)
        {
            if (stack == null || iatta == null)
            {
                return entityShape;
            }

            float damageEffect = 0f;
            JsonObject itemAttributes = stack.ItemAttributes;
            if (itemAttributes != null && itemAttributes["visibleDamageEffect"].AsBool())
            {
                damageEffect = Math.Max(0f, 1f - (float)stack.Collectible.GetRemainingDurability(stack) / (float)stack.Collectible.GetMaxDurability(stack) * 1.1f);
            }

            entityShape.RemoveElements(iatta.GetDisableElements(stack));
            string[] keepElements = iatta.GetKeepElements(stack);
            if (keepElements != null && willDeleteElements != null)
            {
                string[] array = keepElements;
                foreach (string value in array)
                {
                    willDeleteElements = willDeleteElements.Remove(value);
                }
            }

            IDictionary<string, CompositeTexture> textures = entity.Properties.Client.Textures;
            string texturePrefixCode = iatta.GetTexturePrefixCode(stack);
            if (texturePrefixCode != null)
            {
                texturePrefixCode = texturePrefixCode + "-" + slotCode;
            }
            else
            {
                texturePrefixCode = slotCode;
            }

            Shape shape = null;
            AssetLocation assetLocation = null;
            CompositeShape compositeShape = null;
            if (stack.Collectible is IWearableShapeSupplier wearableShapeSupplier)
            {
                shape = wearableShapeSupplier.GetShape(stack, entity, texturePrefixCode);
            }

            if (shape == null)
            {
                compositeShape = iatta.GetAttachedShape(stack, slotCode);
                assetLocation = compositeShape.Base.CopyWithPath("shapes/" + compositeShape.Base.Path + ".json");
                shape = Shape.TryGet(Api, assetLocation);
                if (shape == null)
                {
                    Api.World.Logger.Warning("Entity attachable shape {0} defined in {1} {2} not found or errored, was supposed to be at {3}. Shape will be invisible.", compositeShape.Base, stack.Class, stack.Collectible.Code, assetLocation);
                    return null;
                }

                shape.SubclassForStepParenting(texturePrefixCode, damageEffect);
                shape.ResolveReferences(entity.World.Logger, assetLocation);
            }

            ICoreClientAPI capi = Api as ICoreClientAPI;
            Dictionary<string, CompositeTexture> dictionary = null;
            if (capi != null)
            {
                dictionary = new Dictionary<string, CompositeTexture>();
                iatta.CollectTextures(stack, shape, texturePrefixCode, dictionary);
            }

            applyStepParentOverrides(overrideStepParent, shape);
            
            //fail here
            StepParentShape(entityShape, shape,
                (compositeShape?.Base.ToString() ?? "Custom texture from ItemWearableShapeSupplier") + $" defined in {stack.Class} {stack.Collectible.Code}",
                shapePathForLogging, Api.World.Logger, delegate (string texcode, AssetLocation tloc)
            {
                addTexture(texcode, tloc, textures, texturePrefixCode, capi);
            });
            
            if (compositeShape?.Overlays != null)
            {
                CompositeShape[] overlays = compositeShape.Overlays;
                foreach (CompositeShape compositeShape2 in overlays)
                {
                    Shape shape2 = Shape.TryGet(Api, compositeShape2.Base.CopyWithPath("shapes/" + compositeShape2.Base.Path + ".json"));
                    if (shape2 == null)
                    {
                        Api.World.Logger.Warning("Entity attachable shape {0} overlay {4} defined in {1} {2} not found or errored, was supposed to be at {3}. Shape will be invisible.", compositeShape.Base, stack.Class, stack.Collectible.Code, assetLocation, compositeShape2.Base);
                        continue;
                    }

                    shape2.SubclassForStepParenting(texturePrefixCode, damageEffect);
                    if (capi != null)
                    {
                        iatta.CollectTextures(stack, shape2, texturePrefixCode, dictionary);
                    }

                    applyStepParentOverrides(overrideStepParent, shape2);
                    entityShape.StepParentShape(shape2, compositeShape2.Base.ToShortString(), shapePathForLogging, Api.Logger, delegate (string texcode, AssetLocation tloc)
                    {
                        addTexture(texcode, tloc, textures, texturePrefixCode, capi);
                    });
                }
            }

            if (capi != null)
            {
                foreach (KeyValuePair<string, CompositeTexture> item in dictionary)
                {
                    CompositeTexture compositeTexture2 = (textures[item.Key] = item.Value.Clone());
                    CompositeTexture compositeTexture3 = compositeTexture2;
                    capi.EntityTextureAtlas.GetOrInsertTexture(compositeTexture3, out var textureSubId, out var _);
                    compositeTexture3.Baked.TextureSubId = textureSubId;
                }
            }

            return entityShape;
        }
        public bool StepParentShape(Shape entityShape, Shape childShape, string childLocationForLogging, string parentLocationForLogging, ILogger logger, Action<string, AssetLocation> onTexture)
        {
            return StepParentShape(entityShape, null, childShape.Elements, childShape, childLocationForLogging, parentLocationForLogging, logger, onTexture);
        }

        private bool StepParentShape(Shape entityShape, ShapeElement parentElem, ShapeElement[] elements, Shape childShape, string childLocationForLogging, string parentLocationForLogging, ILogger logger, Action<string, AssetLocation> onTexture)
        {
            bool flag = false;
            foreach (ShapeElement shapeElement in elements)
            {
                if (shapeElement.Children != null)
                {
                    bool flag2 = StepParentShape(entityShape, shapeElement, shapeElement.Children, childShape, childLocationForLogging, parentLocationForLogging, logger, onTexture);
                    flag = flag || flag2;
                }

                if (shapeElement.StepParentName != null)
                {
                    ShapeElement elementByName = GetElementByName(entityShape, shapeElement.StepParentName, StringComparison.InvariantCultureIgnoreCase);
                    if (elementByName == null)
                    {
                        logger.Warning("Step parented shape {0} requires step parent element with name {1}, but no such element was found in parent shape {2}. Will not be visible.", childLocationForLogging, shapeElement.StepParentName, parentLocationForLogging);
                        continue;
                    }

                    if (parentElem != null)
                    {
                        parentElem.Children = parentElem.Children.Remove(shapeElement);
                    }

                    if (elementByName.Children == null)
                    {
                        elementByName.Children = new ShapeElement[1] { shapeElement };
                    }
                    else
                    {
                        elementByName.Children = elementByName.Children.Append(shapeElement);
                    }

                    shapeElement.ParentElement = elementByName;
                    shapeElement.SetJointIdRecursive(elementByName.JointId);
                    flag = true;
                }
                else if (parentElem == null)
                {
                    logger.Warning("Step parented shape {0} did not define a step parent element for parent shape {1}. Will not be visible.", childLocationForLogging, parentLocationForLogging);
                }
            }

            if (!flag)
            {
                return false;
            }

            /*if (childShape.Textures != null)
            {
                foreach (KeyValuePair<string, AssetLocation> texture in childShape.Textures)
                {
                    onTexture(texture.Key, texture.Value);
                }

                foreach (KeyValuePair<string, int[]> textureSize in childShape.TextureSizes)
                {
                    entityShape.TextureSizes[textureSize.Key] = textureSize.Value;
                }

                if (childShape.Textures.Count > 0 && childShape.TextureSizes.Count == 0)
                {
                    foreach (KeyValuePair<string, AssetLocation> texture2 in childShape.Textures)
                    {
                        entityShape.TextureSizes[texture2.Key] = new int[2] { childShape.TextureWidth, childShape.TextureHeight };
                    }
                }
            }*/

            return flag;
        }
        public ShapeElement GetElementByName(Shape entityShape, string name, StringComparison stringComparison = StringComparison.OrdinalIgnoreCase)
        {
            if (entityShape.Elements == null)
            {
                return null;
            }

            return GetElementByName(entityShape, name, entityShape.Elements, isWildcard: false);
        }
        private ShapeElement GetElementByName(Shape entityShape, string name, ShapeElement[] elems, bool isWildcard)
        {
            foreach (ShapeElement shapeElement in elems)
            {
                if (isWildcard ? WildcardUtil.Match(name, shapeElement.Name) : shapeElement.Name.EqualsFastIgnoreCase(name))
                {
                    return shapeElement;
                }

                if (shapeElement.Children != null)
                {
                    ShapeElement elementByName = GetElementByName(entityShape, name, shapeElement.Children, isWildcard);
                    if (elementByName != null)
                    {
                        return elementByName;
                    }
                }
            }

            return null;
        }

        private static void applyStepParentOverrides(Dictionary<string, StepParentElementTo> overrideStepParent, Shape gearShape)
        {
            if (overrideStepParent == null)
            {
                return;
            }

            overrideStepParent.TryGetValue("", out var value);
            ShapeElement[] elements = gearShape.Elements;
            foreach (ShapeElement shapeElement in elements)
            {
                StepParentElementTo value2;
                if (shapeElement.StepParentName == null || shapeElement.StepParentName.Length == 0)
                {
                    shapeElement.StepParentName = value.ElementName;
                }
                else if (overrideStepParent.TryGetValue(shapeElement.StepParentName, out value2))
                {
                    shapeElement.StepParentName = value2.ElementName;
                }
            }
        }

        private void addTexture(string texcode, AssetLocation tloc, IDictionary<string, CompositeTexture> textures, string texturePrefixCode, ICoreClientAPI capi)
        {
            if (capi != null)
            
            {
                CompositeTexture compositeTexture2 = (textures[texturePrefixCode + texcode] = new CompositeTexture(tloc));
                CompositeTexture compositeTexture3 = compositeTexture2;
                compositeTexture3.Bake(Api.Assets);
                capi.EntityTextureAtlas.GetOrInsertTexture(compositeTexture3.Baked.TextureFilenames[0], out var textureSubId, out var _);
                compositeTexture3.Baked.TextureSubId = textureSubId;
            
                
            }
        }

        public override void OnLoadCollectibleMappings(IWorldAccessor worldForNewMappings, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, bool resolveImports)
        {
            container.OnLoadCollectibleMappings(worldForNewMappings, oldBlockIdMapping, oldItemIdMapping, 0, resolveImports);
        }

        public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
        {
            container.OnStoreCollectibleMappings(blockIdMapping, itemIdMapping);
        }

        public override void FromBytes(bool isSync)
        {
            loadInv();
        }

        protected virtual void loadInv()
        {
            if (Inventory != null)
            {
                container.FromTreeAttributes(entity.WatchedAttributes, entity.World);
                entity.MarkShapeModified();
            }
        }

        public override void ToBytes(bool forClient)
        {
            storeInv();
        }

        public virtual void storeInv()
        {
            container.ToTreeAttributes(entity.WatchedAttributes);
            entity.WatchedAttributes.MarkPathDirty(InventoryClassName);
            entity.World.BlockAccessor.GetChunkAtBlockPos(entity.ServerPos.AsBlockPos)?.MarkModified();
        }

        public override bool TryGiveItemStack(ItemStack itemstack, ref EnumHandling handling)
        {
            ItemSlot itemSlot = new DummySlot(null);
            itemSlot.Itemstack = itemstack.Clone();
            ItemStackMoveOperation op = new ItemStackMoveOperation(entity.World, EnumMouseButton.Left, (EnumModifierKey)0, EnumMergePriority.AutoMerge, itemstack.StackSize);
            if (Inventory != null)
            {
                WeightedSlot bestSuitedSlot = Inventory.GetBestSuitedSlot(itemSlot, null, new List<ItemSlot>());
                if (bestSuitedSlot.weight > 0f)
                {
                    itemSlot.TryPutInto(bestSuitedSlot.slot, ref op);
                    itemstack.StackSize -= op.MovedQuantity;
                    entity.WatchedAttributes.MarkAllDirty();
                    return op.MovedQuantity > 0;
                }
            }

            if ((entity as EntityAgent)?.LeftHandItemSlot?.Inventory != null)
            {
                WeightedSlot weightedSlot = (entity as EntityAgent)?.LeftHandItemSlot.Inventory.GetBestSuitedSlot(itemSlot, null, new List<ItemSlot>());
                if (weightedSlot.weight > 0f)
                {
                    itemSlot.TryPutInto(weightedSlot.slot, ref op);
                    itemstack.StackSize -= op.MovedQuantity;
                    entity.WatchedAttributes.MarkAllDirty();
                    return op.MovedQuantity > 0;
                }
            }

            return false;
        }

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            base.OnEntityDeath(damageSourceForDeath);
            if (dropContentsOnDeath)
            {
                Inventory.DropAll(entity.ServerPos.XYZ);
            }
        }
    }
}
