using canjewelry.src.eb;
using canjewelry.src.items;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.CommandAbbr;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace canjewelry.src.commands
{
    public class RegisterCommands
    {
        public static void registerServerCommands(ICoreServerAPI sapi)
        {
            var parsers = sapi.ChatCommands.Parsers;
            sapi.ChatCommands.Create("canjewelry")
                                .RequiresPlayer().RequiresPrivilege(Privilege.controlserver)
                                    .BeginSub("clearbuffs")
                                        .WithDesc("clear cancrusted buffs for player selected by name")
                                        .WithArgs(parsers.Word("playerName"))
                                        .HandleWith(clearCancrustedBuffFromPlayer)
                                    .EndSub()
                                    .BeginSub("reapplybuffs")
                                        .WithDesc("reapply cancrusted buffs for player selected by name")
                                        .WithArgs(parsers.Word("playerName"))
                                        .HandleWith(reapplyCancrustedBuffFromPlayer)
                                    .EndSub()
                                    .BeginSub("setgembuffs")
                                        .WithAlias("sgb")
                                        .WithArgs(parsers.WordRange("cutting", CANJWConstants.CUTTING_ROUND, CANJWConstants.CUTTING_BAGUETTE, CANJWConstants.CUTTING_PEAR), parsers.OptionalAll("buffNamesAndValues"))
                                        .HandleWith(SetGemParams)
                                    .EndSub()
                                    ;
            if (canjewelry.config.debugMode)
            {
                sapi.Logger.VerboseDebug("[canjewelry] " + "Server commands registered");
            }
        }


        public static TextCommandResult clearCancrustedBuffFromPlayer(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            TextCommandResult tcr = new TextCommandResult();
            tcr.Status = EnumCommandStatus.Success;

            string targetPlayerName = (string)args.LastArg;
            IServerPlayer targetPlayer = null;
            foreach (var pl in player.Entity.Api.World.AllOnlinePlayers)
            {
                if (pl.PlayerName.Equals(targetPlayerName))
                {
                    targetPlayer = pl as IServerPlayer;
                }
            }
            if (targetPlayer == null)
            {
                return tcr;
            }
            foreach (KeyValuePair<string, EntityFloatStats> stat in targetPlayer.Entity.Stats)
            {
                foreach (KeyValuePair<string, EntityStat<float>> keyValuePair in stat.Value.ValuesByKey)
                {
                    if (keyValuePair.Key == "canencrusted")
                    {
                        stat.Value.Set(keyValuePair.Key, 0);
                        //stat.Value.Remove(keyValuePair.Key);
                        break;
                    }
                }
                targetPlayer.Entity.WatchedAttributes.MarkPathDirty("stats");
            }
            canjewelry.sapi.SendMessage(player, 0, String.Format("Buffs were cleared for {0}", targetPlayer.PlayerName), EnumChatType.Notification);
            return tcr;
        }

        public static TextCommandResult reapplyCancrustedBuffFromPlayer(TextCommandCallingArgs args)
        {
            var pl = args.Caller.Player as IServerPlayer;
            var beh = pl.Entity.GetBehavior<CANGemBuffAffected>();
            TextCommandResult tcr = new TextCommandResult();
            tcr.Status = EnumCommandStatus.Success;
            if (beh == null)
            {
                return tcr;
            }
            beh.savedBuffs.Clear();
            foreach (KeyValuePair<string, EntityFloatStats> stat in pl.Entity.Stats)
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
                pl.Entity.WatchedAttributes.MarkPathDirty("stats");
            }
            //go through hotbar active slot, character slots and apply all buffs
            IInventory playerBackpacks = (pl.Entity as EntityPlayer).Player.InventoryManager.GetHotbarInventory();
            if (playerBackpacks != null)
            {
                ItemSlot activeSlot = (pl.Entity as EntityPlayer).Player.InventoryManager.ActiveHotbarSlot;
                var itemStack = activeSlot.Itemstack;
                if (itemStack != null && itemStack.Item is not ItemWearable && itemStack.Item is not CANItemWearable)
                {
                    var newBuffs = beh.GetItemStackBuffs(itemStack);
                    CANGemBuffAffected.ApplyBuffFromItemStack(newBuffs, pl.Entity as EntityPlayer, true);
                    beh.savedBuffs[1 + (int)EnumCharacterDressType.ArmorLegs] = newBuffs;
                }
            }

            IInventory charakterInv = (pl.Entity as EntityPlayer).Player.InventoryManager.GetOwnInventory("character");

            //playerBackpacks.Player
            if (charakterInv != null)
            {
                for (int i = 0; i < charakterInv.Count; ++i)
                {
                    if (charakterInv[i] != null)
                    {
                        ItemSlot itemSlot = charakterInv[i];
                        ItemStack itemStack = itemSlot.Itemstack;
                        if (itemStack != null)
                        {
                            var newBuffs = beh.GetItemStackBuffs(itemStack);
                            CANGemBuffAffected.ApplyBuffFromItemStack(newBuffs, pl.Entity as EntityPlayer, true);
                            beh.savedBuffs[itemSlot.Inventory.GetSlotId(itemSlot)] = newBuffs;
                        }
                    }
                }

            }

            canjewelry.sapi.SendMessage(pl, 0, String.Format("Buffs were reapplied for {0}", pl.PlayerName), EnumChatType.Notification);
            return tcr;
        }
        public static TextCommandResult SetGemParams(TextCommandCallingArgs args)
        {
            var pl = args.Caller.Player as IServerPlayer;
            var beh = pl.Entity.GetBehavior<CANGemBuffAffected>();
            TextCommandResult tcr = new TextCommandResult();
            tcr.Status = EnumCommandStatus.Success;
            if (pl.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                return tcr;
            }
            var itemStack = pl.InventoryManager.ActiveHotbarSlot.Itemstack;
            if(itemStack == null)
            {
                return tcr;
            }
            ITreeAttribute tree = new TreeAttribute();
            tree.SetString(CANJWConstants.CUTTING_TYPE, args.Parsers[0].GetValue().ToString());

            var namesAndValues = args.Parsers[1].GetValue().ToString().Split(' ');
            List<string> buffNames = new();
            List<float> buffValues = new();
            for (int i = 0; i < namesAndValues.Length; i+=2)
            {
                try
                {
                    buffNames.Add(namesAndValues[i]);
                    buffValues.Add(float.Parse(namesAndValues[i + 1], CultureInfo.InvariantCulture));
                }
                catch {
                    return tcr;
                }
            }

            tree[CANJWConstants.ENCRUSTABLE_BUFFS_NAMES] = new StringArrayAttribute(buffNames.ToArray());
            tree[CANJWConstants.ENCRUSTABLE_BUFFS_VALUES] = new FloatArrayAttribute(buffValues.ToArray());
            itemStack.Attributes[CANJWConstants.CUT_GEM_TREE] = tree;
            pl.InventoryManager.ActiveHotbarSlot.MarkDirty();
            return tcr;
        }
    }
}
