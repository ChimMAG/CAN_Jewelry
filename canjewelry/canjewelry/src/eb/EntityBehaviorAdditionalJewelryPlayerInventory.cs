using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace canjewelry.src.eb
{
    public class EntityBehaviorAdditionalJewelryPlayerInventory : CANEntityBehaviorTexturedClothing
    {
        private bool slotModifiedRegistered;
        private float accum;
        private IPlayer Player
        {
            get
            {
                return (this.entity as EntityPlayer).Player;
            }
        }
        public override InventoryBase Inventory
        {
            get
            {
                IPlayer player = this.Player;
                return ((player != null) ? player.InventoryManager.GetOwnInventory("additionaljewelrycharacter") : null) as InventoryBase;
            }
        }
        public override string InventoryClassName
        {
            get
            {
                return "additionalgear";
            }
        }
        public override string PropertyName()
        {
            return "playeradditionaljewelryinventory";
        }
        public EntityBehaviorAdditionalJewelryPlayerInventory(Entity entity)
            : base(entity)
        {
        }
        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);
        }
        protected override Shape addGearToShape(Shape entityShape, ItemStack stack, IAttachableToEntity iatta, string slotCode, string shapePathForLogging, ref string[] willDeleteElements, Dictionary<string, StepParentElementTo> overrideStepParent = null)
        {
            return base.addGearToShape(entityShape, stack, iatta, slotCode, shapePathForLogging, ref willDeleteElements, overrideStepParent);
        }
        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            base.OnEntityDespawn(despawn);
            IPlayer player = this.Player;
            IInventory inv = ((player != null) ? player.InventoryManager.GetOwnInventory("backpack") : null);
            if (inv != null)
            {
                inv.SlotModified -= base.Inventory_SlotModifiedBackpack;
            }
        }
        protected override void loadInv()
        {
        }
        public override void storeInv()
        {
        }
        public override void OnGameTick(float deltaTime)
        {
            if (!this.slotModifiedRegistered)
            {
                this.slotModifiedRegistered = true;
                IPlayer player = this.Player;
                IInventory inv = ((player != null) ? player.InventoryManager.GetOwnInventory("backpack") : null);
                if (inv != null)
                {
                    inv.SlotModified += base.Inventory_SlotModifiedBackpack;
                }
            }
            base.OnGameTick(deltaTime);
            this.accum += deltaTime;
            if (this.accum > 1f)
            {
                TreeAttribute attributes = this.entity.Attributes;
                string text = "hasProtectiveEyeGear";
                bool flag;
                if (this.Inventory != null)
                {
                    flag = this.Inventory.FirstOrDefault(delegate (ItemSlot slot)
                    {
                        if (!slot.Empty)
                        {
                            JsonObject attributes2 = slot.Itemstack.Collectible.Attributes;
                            return attributes2 != null && attributes2.IsTrue("eyeprotective");
                        }
                        return false;
                    }) != null;
                }
                else
                {
                    flag = false;
                }
                attributes.SetBool(text, flag);
            }
        }
        public override void OnTesselation(ref Shape entityShape, string shapePathForLogging, ref bool shapeIsCloned, ref string[] willDeleteElements)
        {
            IPlayer player = this.Player;
            IInventory backPackInv = ((player != null) ? player.InventoryManager.GetOwnInventory("backpack") : null);
            Dictionary<string, ItemSlot> uniqueGear = new Dictionary<string, ItemSlot>();
            int i = 0;
            while (backPackInv != null && i < 4)
            {
                ItemSlot slot = backPackInv[i];
                if (!slot.Empty)
                {
                    Dictionary<string, ItemSlot> dictionary = uniqueGear;
                    JsonObject itemAttributes = slot.Itemstack.ItemAttributes;
                    string text;
                    if (itemAttributes == null)
                    {
                        text = null;
                    }
                    else
                    {
                        JsonObject jsonObject = itemAttributes["attachableToEntity"];
                        if (jsonObject == null)
                        {
                            text = null;
                        }
                        else
                        {
                            JsonObject jsonObject2 = jsonObject["categoryCode"];
                            text = ((jsonObject2 != null) ? jsonObject2.AsString(null) : null);
                        }
                    }
                    dictionary[text ?? (slot.Itemstack.Class.ToString() + slot.Itemstack.Collectible.Id.ToString())] = slot;
                }
                i++;
            }
            foreach (KeyValuePair<string, ItemSlot> val in uniqueGear)
            {
                entityShape = this.addGearToShape(entityShape, val.Value, "default", shapePathForLogging, ref shapeIsCloned, ref willDeleteElements, null);
            }
            
            base.OnTesselation(ref entityShape, shapePathForLogging, ref shapeIsCloned, ref willDeleteElements);
        }
        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            this.Api.Event.EnqueueMainThreadTask(delegate
            {
                EntityServerProperties server = this.entity.Properties.Server;
                bool flag;
                if (server == null)
                {
                    flag = true;
                }
                else
                {
                    ITreeAttribute attributes = server.Attributes;
                    flag = !((attributes != null) ? new bool?(attributes.GetBool("keepContents", false)) : null).GetValueOrDefault();
                }
                if (flag)
                {
                    this.Player.InventoryManager.OnDeath();
                }
                EntityServerProperties server2 = this.entity.Properties.Server;
                bool flag2;
                if (server2 == null)
                {
                    flag2 = false;
                }
                else
                {
                    ITreeAttribute attributes2 = server2.Attributes;
                    flag2 = ((attributes2 != null) ? new bool?(attributes2.GetBool("dropArmorOnDeath", false)) : null).GetValueOrDefault();
                }
                if (flag2)
                {
                    foreach (ItemSlot slot in this.Inventory)
                    {
                        if (!slot.Empty)
                        {
                            JsonObject itemAttributes = slot.Itemstack.ItemAttributes;
                            if (itemAttributes != null && itemAttributes["protectionModifiers"].Exists)
                            {
                                this.Api.World.SpawnItemEntity(slot.Itemstack, this.entity.ServerPos.XYZ, null);
                                slot.Itemstack = null;
                                slot.MarkDirty();
                            }
                        }
                    }
                }
            }, "dropinventoryondeath");
        }
    }
}
