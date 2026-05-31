using canjewelry.src.cb;
using canjewelry.src.CB;
using canjewelry.src.items;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace canjewelry.src.eb
{
    /***
     * Behavior tracks player's armor/cloth slots + active hotbar slot and apply buff for the player.
     */
    public class CANGemBuffAffected : EntityBehavior
    {
        private const string CHARACTER_INV = "character";
        private const string HOTBAR_INV = "hotbar";
        private const string ADDITIONAL_INV = "additionaljewelrycharacter";

        // savedBuffs key layout:
        //   0..15      character inv slots (match EnumCharacterDressType)
        //   100..199   additional jewelry inv slots (offset to avoid collision)
        //   HOTBAR_BUFF_KEY  active hotbar slot (single shared key)
        private const int ADDITIONAL_INV_KEY_OFFSET = 100;
        private static readonly int HOTBAR_BUFF_KEY = 1 + (int)EnumCharacterDressType.ArmorLegs;

        public override string PropertyName() => "cangembuffaffected";

        public Dictionary<int, Dictionary<string, float>> savedBuffs;
        int triesToInit = 0;
        long callbackId = 0;
        public bool initialized = false;

        public CANGemBuffAffected(Entity entity) : base(entity) { }

        public override void OnEntitySpawn()
        {
            base.OnEntitySpawn();
            savedBuffs = new Dictionary<int, Dictionary<string, float>>();
            this.DeserializeBuffs();
        }

        public override void OnEntityLoaded()
        {
            base.OnEntityLoaded();
            savedBuffs = new Dictionary<int, Dictionary<string, float>>();
            this.DeserializeBuffs();
        }

        private IServerPlayer ServerPlayer => (entity as EntityPlayer)?.Player as IServerPlayer;

        // Player inventories may not exist yet on first attempt (login/teleport),
        // so retry every 30s until they do.
        private void EnqueTryAddAndWait()
        {
            this.callbackId = canjewelry.sapi.Event.RegisterCallback(dt => TryToAddSlotModified(), 30 * 1000);
        }

        public bool TryToAddSlotModified()
        {
            IServerPlayer player = ServerPlayer;
            this.triesToInit++;
            canjewelry.sapi.Logger.VerboseDebug(string.Format("[canjewelry] Try #{0} to load behavior for {1}", this.triesToInit, player.PlayerName));
            IInventory characterInv = player.InventoryManager.GetOwnInventory(CHARACTER_INV);
            InventoryBasePlayer playerHotbar = (InventoryBasePlayer)player.InventoryManager.GetOwnInventory(HOTBAR_INV);
            if (characterInv == null || playerHotbar == null)
            {
                canjewelry.sapi.Logger.VerboseDebug(string.Format("[canjewelry] Try #{0} failed to load behavior for {1}", this.triesToInit, player.PlayerName));
                EnqueTryAddAndWait();
                return false;
            }

            characterInv.SlotModified += OnSlotModifiedCharacterInv;
            playerHotbar.SlotModified += OnSlotModifiedHotbarInv;

            var additionalInv = player.InventoryManager.GetOwnInventory(ADDITIONAL_INV);
            if (additionalInv != null)
            {
                additionalInv.SlotModified += OnSlotModifiedAdditionalInv;
            }
            canjewelry.sapi.Logger.VerboseDebug(string.Format("[canjewelry] Try #{0} loaded behavior for {1}", this.triesToInit, player.PlayerName));
            this.callbackId = 0;
            initialized = true;
            return true;
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            IServerPlayer player = ServerPlayer;
            if (player != null)
            {
                IInventory characterInv = player.InventoryManager.GetOwnInventory(CHARACTER_INV);
                if (characterInv != null) characterInv.SlotModified -= OnSlotModifiedCharacterInv;

                InventoryBasePlayer playerHotbar = (InventoryBasePlayer)player.InventoryManager.GetOwnInventory(HOTBAR_INV);
                if (playerHotbar != null) playerHotbar.SlotModified -= OnSlotModifiedHotbarInv;

                IInventory additionalInv = player.InventoryManager.GetOwnInventory(ADDITIONAL_INV);
                if (additionalInv != null) additionalInv.SlotModified -= OnSlotModifiedAdditionalInv;
            }
            if (this.callbackId != 0)
            {
                canjewelry.sapi.Event.UnregisterCallback(this.callbackId);
                this.callbackId = 0;
            }
            this.SerializeBuffs();
            initialized = false;
            base.OnEntityDespawn(despawn);
        }

        private void SerializeBuffs()
        {
            (this.entity as EntityPlayer).Player.WorldData.SetModdata("canjewelrysavedbuffs", SerializerUtil.Serialize(this.savedBuffs));
        }

        private void DeserializeBuffs()
        {
            var loadedBuffs = (this.entity as EntityPlayer).Player.WorldData.GetModdata("canjewelrysavedbuffs");
            if (loadedBuffs != null)
            {
                this.savedBuffs = SerializerUtil.Deserialize<Dictionary<int, Dictionary<string, float>>>(loadedBuffs);
            }
        }

        internal void UpdateBuffsForSlot(int key, ItemStack newItemStack)
        {
            EntityPlayer ep = entity as EntityPlayer;
            Dictionary<string, float> newBuffDict = GetItemStackBuffs(newItemStack);
            if (savedBuffs.TryGetValue(key, out var currentBuffDict))
            {
                if (currentBuffDict == null)
                {
                    canjewelry.sapi.Logger.VerboseDebug(string.Format("[canjewelry] {0} itemslot buff dict was null", key));
                    savedBuffs.Remove(key);
                    return;
                }
                // Skip only when dictionaries are identical: same count AND no differing entries.
                // Count check catches the case where new has additional buffs (gem socketed into
                // item that already has other gems) — Except alone would miss those additions.
                if (currentBuffDict.Count == newBuffDict.Count
                    && !currentBuffDict.Except(newBuffDict).Any()) return;

                ApplyBuffFromItemStack(currentBuffDict, ep, false);
                if (newBuffDict.Count > 0)
                {
                    ApplyBuffFromItemStack(newBuffDict, ep, true);
                    savedBuffs[key] = newBuffDict;
                }
                else
                {
                    savedBuffs.Remove(key);
                }
            }
            else if (newBuffDict.Count > 0)
            {
                ApplyBuffFromItemStack(newBuffDict, ep, true);
                savedBuffs[key] = newBuffDict;
            }
        }

        private void OnSlotModifiedAdditionalInv(int i)
        {
            if (!initialized) return;
            var inv = ServerPlayer?.InventoryManager.GetOwnInventory(ADDITIONAL_INV);
            if (inv == null) return;
            UpdateBuffsForSlot(i + ADDITIONAL_INV_KEY_OFFSET, inv[i].Itemstack);
        }

        private void OnSlotModifiedCharacterInv(int i)
        {
            if (!initialized) return;
            var inv = ServerPlayer?.InventoryManager.GetOwnInventory(CHARACTER_INV);
            if (inv == null) return;
            UpdateBuffsForSlot(i, inv[i].Itemstack);
        }

        public void OnSlotModifiedHotbarInv(int i)
        {
            if (!initialized || i > 10) return;
            IServerPlayer player = ServerPlayer;
            if (player == null || i != player.InventoryManager.ActiveHotbarSlotNumber) return;

            ItemStack itemStack = player.InventoryManager.GetOwnInventory(HOTBAR_INV)[i].Itemstack;

            // Wearables in the active hotbar slot are skipped here: their buffs are already
            // accounted for via CharacterInv/AdditionalInv handlers (otherwise double-counted).
            // Strip any previously applied hotbar buffs.
            if (itemStack == null || itemStack.Item == null || itemStack.Item is ItemWearable || itemStack.Item is CANItemWearable)
            {
                if (savedBuffs.TryGetValue(HOTBAR_BUFF_KEY, out var currentBuffDictD))
                {
                    ApplyBuffFromItemStack(currentBuffDictD, entity as EntityPlayer, false);
                    savedBuffs.Remove(HOTBAR_BUFF_KEY);
                }
                return;
            }

            UpdateBuffsForSlot(HOTBAR_BUFF_KEY, itemStack);
        }

        public void OnActiveSlotSwapped(IServerPlayer player, int from, int to)
        {
            OnSlotModifiedHotbarInv(to);
        }

        public Dictionary<string, float> GetItemStackBuffs(ItemStack itemStack)
        {
            Dictionary<string, float> result = new Dictionary<string, float>();
            if (itemStack == null || !itemStack.Attributes.HasAttribute(CANJWConstants.ITEM_ENCRUSTED_STRING)) return result;

            ITreeAttribute encrustTreeHere = itemStack.Attributes.GetTreeAttribute(CANJWConstants.ITEM_ENCRUSTED_STRING);
            for (int i = 0; i < EncrustableCB.GetMaxAmountSockets(itemStack); i++)
            {
                ITreeAttribute socketSlot = encrustTreeHere.GetTreeAttribute("slot" + i);
                if (socketSlot == null) continue;

                if (socketSlot.HasAttribute(CANJWConstants.GEM_ATTRIBUTE_BUFF))
                {
                    if (socketSlot.HasAttribute(CANJWConstants.GEM_BUFF_TYPE) && (EnumGemBuffType)socketSlot.GetInt(CANJWConstants.GEM_BUFF_TYPE) != EnumGemBuffType.STATS_BUFF)
                        continue;
                    string buffName = socketSlot.GetString(CANJWConstants.GEM_ATTRIBUTE_BUFF);
                    if (buffName == CANJWConstants.CANDURABILITY_STRING || buffName == CANJWConstants.TEMPORALGRASP) continue;

                    float additionalValue = socketSlot.GetFloat(CANJWConstants.GEM_ATTRIBUTE_BUFF_VALUE);
                    string attributeBuffName = socketSlot.GetString(CANJWConstants.GEM_ATTRIBUTE_BUFF);
                    if (result.TryGetValue(attributeBuffName, out float currentResult))
                        result[attributeBuffName] = currentResult + additionalValue;
                    else
                        result[attributeBuffName] = additionalValue;
                }
                else if (socketSlot.HasAttribute(CANJWConstants.ENCRUSTABLE_BUFFS_NAMES))
                {
                    string[] buffNames = (socketSlot[CANJWConstants.ENCRUSTABLE_BUFFS_NAMES] as StringArrayAttribute).value;
                    float[] buffValues = (socketSlot[CANJWConstants.ENCRUSTABLE_BUFFS_VALUES] as FloatArrayAttribute).value;

                    for (int j = 0; j < buffNames.Length; j++)
                    {
                        float additionalValue = buffValues[j];
                        string attributeBuffName = buffNames[j];
                        if (attributeBuffName.Equals(CANJWConstants.CANDURABILITY_STRING) || attributeBuffName.Equals(CANJWConstants.TEMPORALGRASP))
                            continue;
                        if (result.TryGetValue(attributeBuffName, out float currentResult))
                            result[attributeBuffName] = currentResult + additionalValue;
                        else
                            result[attributeBuffName] = additionalValue;
                    }
                }
            }
            return result;
        }

        public static void ApplyBuffFromItemStack(Dictionary<string, float> buffsDict, EntityPlayer ep, bool add)
        {
            if (buffsDict == null) return;

            foreach (var buff in buffsDict)
            {
                string attributeBuffName = buff.Key;
                float additionalValue = buff.Value;

                if (!ep.Stats[attributeBuffName].ValuesByKey.ContainsKey("canencrusted"))
                    ep.Stats.Set(attributeBuffName, "canencrusted", 0, true);

                if (!ep.Stats[attributeBuffName].ValuesByKey.ContainsKey("canencrustedneg"))
                    ep.Stats.Set(attributeBuffName, "canencrustedneg", 0, true);

                float delta = add ? additionalValue : -additionalValue;
                float newValue = ep.Stats[attributeBuffName].ValuesByKey["canencrusted"].Value + delta;
                ep.Stats.Set(attributeBuffName, "canencrusted", newValue, true);

                if (!canjewelry.config.max_buff_values.TryGetValue(attributeBuffName, out float buffThreshold))
                    continue;

                // canencrustedneg is a negative compensation that caps total at buffThreshold.
                // Sign of threshold sets direction: positive caps from above, negative from below.
                bool overflowed = buffThreshold > 0 ? newValue - buffThreshold >= 0 : newValue - buffThreshold <= 0;
                ep.Stats.Set(attributeBuffName, "canencrustedneg", overflowed ? -(newValue - buffThreshold) : 0, true);
            }
        }

        public override void OnEntityRevive()
        {
            base.OnEntityRevive();
            savedBuffs.Clear();
            foreach (KeyValuePair<string, EntityFloatStats> stat in entity.Stats)
            {
                foreach (KeyValuePair<string, EntityStat<float>> keyValuePair in stat.Value.ValuesByKey.ToArray())
                {
                    if (keyValuePair.Key == "canencrusted")
                    {
                        stat.Value.Set(keyValuePair.Key, 0);
                        continue;
                    }
                    if (keyValuePair.Key == "canencrustedneg")
                    {
                        stat.Value.Remove(keyValuePair.Key);
                    }
                }
                entity.WatchedAttributes.MarkPathDirty("stats");
            }

            IInventory playerHotbar = (entity as EntityPlayer).Player.InventoryManager.GetHotbarInventory();
            if (playerHotbar != null)
            {
                ItemSlot activeSlot = (entity as EntityPlayer).Player.InventoryManager.ActiveHotbarSlot;
                var itemStack = activeSlot.Itemstack;
                if (itemStack != null && itemStack.Item is not ItemWearable && itemStack.Item is not CANItemWearable)
                {
                    var newBuffs = GetItemStackBuffs(itemStack);
                    ApplyBuffFromItemStack(newBuffs, entity as EntityPlayer, true);
                    savedBuffs[HOTBAR_BUFF_KEY] = newBuffs;
                }
            }

            IInventory characterInv = (entity as EntityPlayer).Player.InventoryManager.GetOwnInventory(CHARACTER_INV);
            if (characterInv != null)
            {
                for (int i = 0; i < characterInv.Count; ++i)
                {
                    if (characterInv[i] != null)
                    {
                        ItemSlot itemSlot = characterInv[i];
                        ItemStack itemStack = itemSlot.Itemstack;
                        if (itemStack != null)
                        {
                            var newBuffs = GetItemStackBuffs(itemStack);
                            ApplyBuffFromItemStack(newBuffs, entity as EntityPlayer, true);
                            savedBuffs[itemSlot.Inventory.GetSlotId(itemSlot)] = newBuffs;
                        }
                    }
                }
            }

            IInventory additionalInv = (entity as EntityPlayer).Player.InventoryManager.GetOwnInventory(ADDITIONAL_INV);
            if (additionalInv != null)
            {
                for (int i = 0; i < additionalInv.Count; ++i)
                {
                    ItemSlot itemSlot = additionalInv[i];
                    ItemStack itemStack = itemSlot?.Itemstack;
                    if (itemStack != null)
                    {
                        var newBuffs = GetItemStackBuffs(itemStack);
                        ApplyBuffFromItemStack(newBuffs, entity as EntityPlayer, true);
                        savedBuffs[i + ADDITIONAL_INV_KEY_OFFSET] = newBuffs;
                    }
                }
            }
        }
    }
}
