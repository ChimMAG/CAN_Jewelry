using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using Cairo;
using canjewelry.src.be;
using canjewelry.src.cb;
using canjewelry.src.CB;
using canjewelry.src.eb;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using Vintagestory.Server;

namespace canjewelry.src.harmony
{
    [HarmonyPatch]
    public class harmPatch
    {

        /***
          
         Comment were mostly written after some time after code was added, so at some points I had to guess what I meant before or rewrite some too hideous parts.
         Enter at your own risk.
         
         ***/

        /*
          
         Add or remove buff on player, buff type and value are taken from tree attribute and applied on ep.
          
         */
        public static void applyBuffFromItemStack(ITreeAttribute socketSlot, EntityPlayer ep, bool add)
        {
            if (!socketSlot.HasAttribute(CANJWConstants.GEM_ATTRIBUTE_BUFF))
            {
                return;
            }
            else
            {
                if (socketSlot.HasAttribute(CANJWConstants.GEM_BUFF_TYPE) && (EnumGemBuffType)socketSlot.GetInt(CANJWConstants.GEM_BUFF_TYPE) != EnumGemBuffType.STATS_BUFF)
                {
                    return;
                }
            }
            float additionalValue = socketSlot.GetFloat(CANJWConstants.GEM_ATTRIBUTE_BUFF_VALUE);
            string attributeBuffName = socketSlot.GetString(CANJWConstants.GEM_ATTRIBUTE_BUFF);
            float blendedStatValue = ep.Stats[attributeBuffName].GetBlended();
            canjewelry.config.max_buff_values.TryGetValue(attributeBuffName, out float buffThreshold);

            if (!ep.Stats[attributeBuffName].ValuesByKey.ContainsKey("canencrusted"))
            {
                ep.Stats.Set(attributeBuffName, "canencrusted", 0, true);
            }

            if (add)
            {
                //overflow already present just add to neg part and standard part
                if(ep.Stats[attributeBuffName].ValuesByKey.ContainsKey("canencrustedneg"))
                {
                    ep.Stats.Set(attributeBuffName, "canencrusted", ep.Stats[attributeBuffName].ValuesByKey["canencrusted"].Value + additionalValue, true);
                    if (additionalValue > 0)
                    {
                        ep.Stats.Set(attributeBuffName, "canencrustedneg", ep.Stats[attributeBuffName].ValuesByKey["canencrustedneg"].Value - additionalValue, true);
                    }
                    else
                    {
                        ep.Stats.Set(attributeBuffName, "canencrustedneg", ep.Stats[attributeBuffName].ValuesByKey["canencrustedneg"].Value + additionalValue, true);
                    }
                    //ep.Stats.Set(attributeBuffName, "canencrustedneg", ep.Stats[attributeBuffName].ValuesByKey["canencrustedneg"].Value + additionalValue, true);
                }
                //no neg part, add additional and add neg difference
                else if (buffThreshold != 0 && additionalValue > 0 ?  Math.Abs(ep.Stats[attributeBuffName].ValuesByKey["canencrusted"].Value + additionalValue) > buffThreshold : ep.Stats[attributeBuffName].ValuesByKey["canencrusted"].Value + additionalValue < buffThreshold)
                {
                    ep.Stats.Set(attributeBuffName, "canencrusted", ep.Stats[attributeBuffName].ValuesByKey["canencrusted"].Value + additionalValue, true);
                    if (additionalValue > 0)
                    {
                        ep.Stats.Set(attributeBuffName, "canencrustedneg", -(ep.Stats[attributeBuffName].ValuesByKey["canencrusted"].Value - buffThreshold), true);
                    }
                    else
                    {
                        ep.Stats.Set(attributeBuffName, "canencrustedneg", -(ep.Stats[attributeBuffName].ValuesByKey["canencrusted"].Value - buffThreshold), true);
                    }
                }
                //no neg part and under threshold
                else
                {
                    ep.Stats.Set(attributeBuffName, "canencrusted", ep.Stats[attributeBuffName].ValuesByKey["canencrusted"].Value + additionalValue, true);
                }
            }
            else
            {
                //overflow already present
                if (ep.Stats[attributeBuffName].ValuesByKey.ContainsKey("canencrustedneg"))
                {
                    ep.Stats.Set(attributeBuffName, "canencrusted", ep.Stats[attributeBuffName].ValuesByKey["canencrusted"].Value - additionalValue, true);
                    if (additionalValue > 0 ? ep.Stats[attributeBuffName].ValuesByKey["canencrustedneg"].Value - additionalValue <= 0
                                            : ep.Stats[attributeBuffName].ValuesByKey["canencrustedneg"].Value + additionalValue <= 0)
                    {
                        ep.Stats[attributeBuffName].Remove("canencrustedneg");
                    }
                    else
                    {
                        ep.Stats.Set(attributeBuffName, "canencrustedneg", ep.Stats[attributeBuffName].ValuesByKey["canencrustedneg"].Value - additionalValue, true);
                    }
                }
                else
                {
                    ep.Stats.Set(attributeBuffName, "canencrusted", ep.Stats[attributeBuffName].ValuesByKey["canencrusted"].Value - additionalValue, true);
                }
            }

            //ep.Stats[attributeBuffName].Remove("canencrustedneg");

            //canjewelry.sapi.SendMessage(ep.Player, 0, add.ToString() + ep.Stats[attributeBuffName].GetBlended().ToString() + attributeBuffName, EnumChatType.Notification);
        }

        //Active slot item can have canecrusted attribute and buffs player, so we need to know when he change holding item

        /*
        
         Catch active slot changed, also it catches moment when active change and we also flip slots. At that moment try_flip patch called as well
        
         */
        public static void Postfix_TriggerAfterActiveSlotChanged(CoreServerEventManager __instance, IServerPlayer player,
            int fromSlot,
            int toSlot)
        {
            if(player.Entity.HasBehavior<CANGemBuffAffected>())
            {
                player.Entity.GetBehavior<CANGemBuffAffected>().OnActiveSlotSwapped(player, fromSlot, toSlot);
            }
        }
        /*
        
         Used in transplier Transpiler_ComposeSlotOverlays_Add_Socket_Overlays_Not_Draw_ItemDamage for GuiElementItemSlotGridBase.ComposeSlotOverlays()
         to draw color rhombus
        
         */
        public static void addSocketsOverlaysNotDrawItemDamage(ElementBounds[] slotBounds, int slotIndex, ItemSlot slot, LoadedTexture[] slotQuantityTextures, ImageSurface textSurface, Context context)
        {
            var unsSlotSize = GuiElementPassiveItemSlot.unscaledSlotSize;
            if(textSurface == null)
            {
                textSurface = new ImageSurface(0, (int)slotBounds[slotIndex].InnerWidth, (int)slotBounds[slotIndex].InnerHeight);
                context = new Context(textSurface);
            }
            
            ITreeAttribute encrustTree = slot.Itemstack.Attributes.GetTreeAttribute("canencrusted");
            if (encrustTree == null)
            {
                return;
            }

            for (int i = 0; i < EncrustableCB.GetMaxAmountSockets(slot.Itemstack); i++)
            {
                
                ITreeAttribute socketSlot = encrustTree.GetTreeAttribute("slot" + i.ToString());
                if (socketSlot == null || socketSlot.GetString("gemtype").Equals(""))
                {
                    continue;
                }

                if(!canjewelry.gems_textures.TryGetValue(socketSlot.GetString("gemtype"), out string assetPath))
                {
                    continue;
                }
                AssetLocation asset = canjewelry.capi.Assets.TryGet(assetPath + ".png")?.Location;
                /*AssetLocation asset = canjewelry.capi.Assets.TryGet("game:textures/block/stone/gem/" + socketSlot.GetString("gemtype") + ".png")?.Location;*/
                /*if (canjewelry.capi.Assets.TryGet("game:textures/block/stone/gem/" + socketSlot.GetString("gemtype") + ".png") == null)
                {
                    asset = canjewelry.capi.Assets.TryGet("game:textures/item/resource/ungraded/" + socketSlot.GetString("gemtype") + ".png")?.Location;
                }*/



                if (asset == null) { continue; }
                var socketSurface = GuiElement.getImageSurfaceFromAsset(canjewelry.capi, asset, 255);

                double tr = unsSlotSize / 4;
                
                context.NewPath();
                
                context.LineTo((int)GuiElement.scaled(0), (int)GuiElement.scaled(unsSlotSize / 8) + i * (int)GuiElement.scaled(tr));
                context.LineTo((int)GuiElement.scaled(unsSlotSize / 8), (int)GuiElement.scaled(0) + i * (int)GuiElement.scaled(tr));
                context.LineTo((int)GuiElement.scaled(unsSlotSize / 4), (int)GuiElement.scaled(unsSlotSize/ 8) + i * (int)GuiElement.scaled(tr));
                context.LineTo((int)GuiElement.scaled(unsSlotSize / 8), (int)GuiElement.scaled(unsSlotSize / 4) + i * (int)GuiElement.scaled(tr));
               
                context.ClosePath();
                
                context.SetSourceSurface(socketSurface, 0, (int)tr * i);

                context.FillPreserve();
                socketSurface.Dispose();
            }
            canjewelry.capi.Gui.LoadOrUpdateCairoTexture(textSurface, true, ref slotQuantityTextures[slotIndex]);
            context.Dispose();
            textSurface.Dispose();
            return;
        }
       
        public static MethodInfo GetItemStackFromItemSlot = typeof(ItemSlot).GetMethod("get_Itemstack");
        public static MethodInfo GetAttributesFromItemStack = typeof(ItemStack).GetMethod("get_Attributes");
        public static MethodInfo HasAttributeITreeAttribute = typeof(ITreeAttribute).GetMethod("HasAttribute");

        public static FieldInfo ElementBoundsSlotGrid = typeof(GuiElementItemSlotGridBase).GetField("SlotBounds");
        
        public static FieldInfo slotQuantityTexturesSlotGrid = typeof(GuiElementItemSlotGridBase).GetField("slotQuantityTextures");
        
        public static IEnumerable<CodeInstruction> Transpiler_ComposeSlotOverlays_Add_Socket_Overlays_Not_Draw_ItemDamage(IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {
            bool found = false;
            bool foundSec = false;
            var codes = new List<CodeInstruction>(instructions);
            var proxyMethod = AccessTools.Method(typeof(harmPatch), "addSocketsOverlaysNotDrawItemDamage");
            Label returnLabelNoAttribute = il.DefineLabel();
            Label returnLabelNoAttribute2 = il.DefineLabel();
            for (int i = 0; i < codes.Count; i++)
            {
 
                if (!found && 
                        codes[i].opcode == OpCodes.Ldc_I4_1 && codes[i + 1].opcode == OpCodes.Ret && codes[i + 2].opcode == OpCodes.Ldc_I4_0 && codes[i - 1].opcode == OpCodes.Stelem_Ref)
                    {
                        yield return new CodeInstruction(OpCodes.Ldarg_1);
                        yield return new CodeInstruction(OpCodes.Callvirt, GetItemStackFromItemSlot);
                        yield return new CodeInstruction(OpCodes.Callvirt, GetAttributesFromItemStack);
                        yield return new CodeInstruction(OpCodes.Ldstr, "canencrusted");
                        yield return new CodeInstruction(OpCodes.Callvirt, HasAttributeITreeAttribute);
                        yield return new CodeInstruction(OpCodes.Brfalse_S, returnLabelNoAttribute);

                   
                        yield return new CodeInstruction(OpCodes.Ldarg_0);
                        yield return new CodeInstruction(OpCodes.Ldfld, ElementBoundsSlotGrid);
                        yield return new CodeInstruction(OpCodes.Ldarg_3);
                        yield return new CodeInstruction(OpCodes.Ldarg_1);
                        yield return new CodeInstruction(OpCodes.Ldarg_0);
                        yield return new CodeInstruction(OpCodes.Ldfld, slotQuantityTexturesSlotGrid);
                        yield return new CodeInstruction(OpCodes.Ldloc_1);
                        yield return new CodeInstruction(OpCodes.Ldloc_2);
                        yield return new CodeInstruction(OpCodes.Call, proxyMethod);
                        yield return new CodeInstruction(OpCodes.Ldc_I4_1);
                        yield return new CodeInstruction(OpCodes.Ret);
                        codes[i].labels.Add(returnLabelNoAttribute);
                        found = true;
                    }

                if (!foundSec &&
                        codes[i].opcode == OpCodes.Ldloc_2 && codes[i + 1].opcode == OpCodes.Callvirt && codes[i + 2].opcode == OpCodes.Ldloc_1 && codes[i - 1].opcode == OpCodes.Call)
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return new CodeInstruction(OpCodes.Callvirt, GetItemStackFromItemSlot);
                    yield return new CodeInstruction(OpCodes.Callvirt, GetAttributesFromItemStack);
                    yield return new CodeInstruction(OpCodes.Ldstr, "canencrusted");
                    yield return new CodeInstruction(OpCodes.Callvirt, HasAttributeITreeAttribute);
                    yield return new CodeInstruction(OpCodes.Brfalse_S, returnLabelNoAttribute2);

                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, ElementBoundsSlotGrid);
                    yield return new CodeInstruction(OpCodes.Ldarg_3);
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, slotQuantityTexturesSlotGrid);
                    yield return new CodeInstruction(OpCodes.Ldloc_1);
                    yield return new CodeInstruction(OpCodes.Ldloc_2);
                    yield return new CodeInstruction(OpCodes.Call, proxyMethod);
                    yield return new CodeInstruction(OpCodes.Ldc_I4_1);
                    yield return new CodeInstruction(OpCodes.Ret);
                    codes[i].labels.Add(returnLabelNoAttribute2);
                    foundSec = true;
                }
                yield return codes[i];
            }
        }

        public static void Postfix_GetHeldItemInfo(CollectibleObject __instance, ItemSlot inSlot,
        StringBuilder dsc,
        IWorldAccessor world,
        bool withDebugInfo)
        {
            ItemStack itemstack = inSlot.Itemstack;
            if (itemstack.Attributes.HasAttribute("canencrusted"))
            {             
                ITreeAttribute tree = itemstack.Attributes.GetTreeAttribute("canencrusted");
                int maxSocketsNumber = EncrustableCB.GetMaxAmountSockets(itemstack);
                int canHaveNsocketsMore = maxSocketsNumber - tree.GetInt("socketsnumber");
                if (canHaveNsocketsMore > 0)
                {
                    dsc.Append(Lang.Get("canjewelry:item-can-have-n-sockets", canHaveNsocketsMore)).Append("\n");
                }
                if (!canjewelry.config.TurnOffBuffs)
                {
                    for (int i = 0; i < maxSocketsNumber; i++)
                    {
                        var treeSlot = tree.GetTreeAttribute("slot" + i);
                        if (treeSlot == null)
                        {
                            continue;
                        }
                        dsc.Append("<font color=\"#").Append(canjewelry.config.socketTiersColors[treeSlot.GetAsInt("sockettype") - 1]).Append("\"></font>").Append(Lang.Get("canjewelry:item-socket-tier", treeSlot.GetAsInt("sockettype")));
                        // dsc.Append("<font color=\"" + canjewelry.config.socketTiersColors[2] + "\"><icon name=wpCircle></icon></font>" +  Lang.Get("canjewelry:item-socket-tier", treeSlot.GetAsInt("sockettype")));
                        dsc.Append("\n");
                        if (treeSlot.GetString("gemtype") != "")
                        {
                            if (treeSlot.HasAttribute(CANJWConstants.GEM_ATTRIBUTE_BUFF))
                            {
                                if (treeSlot.GetString(CANJWConstants.GEM_ATTRIBUTE_BUFF).Equals("maxhealthExtraPoints"))
                                {
                                    dsc.Append(Lang.Get("canjewelry:socket-has-attribute", i, treeSlot.GetFloat(CANJWConstants.GEM_ATTRIBUTE_BUFF_VALUE))).Append(Lang.Get("canjewelry:buff-name-" + treeSlot.GetString(CANJWConstants.GEM_ATTRIBUTE_BUFF)));
                                }
                                else
                                {
                                    dsc.Append(Lang.Get("canjewelry:socket-has-attribute-percent", i, treeSlot.GetFloat(CANJWConstants.GEM_ATTRIBUTE_BUFF_VALUE) * 100)).Append(Lang.Get("canjewelry:buff-name-" + treeSlot.GetString(CANJWConstants.GEM_ATTRIBUTE_BUFF)));
                                }
                                dsc.AppendLine();
                            }
                            else
                            {
                                string[] buffNames = (treeSlot[CANJWConstants.ENCRUSTABLE_BUFFS_NAMES] as StringArrayAttribute).value;
                                float[] buffValues = (treeSlot[CANJWConstants.ENCRUSTABLE_BUFFS_VALUES] as FloatArrayAttribute).value;

                                for (int j = 0; j < buffNames.Length; j++)
                                {
                                    if (buffNames[j].Equals("maxhealthExtraPoints"))
                                    {
                                        dsc.Append(Lang.Get("canjewelry:buff-name-" + buffNames[j])).Append(" +" + buffValues[j].ToString());
                                        dsc.AppendLine();
                                    }
                                    else
                                    {
                                        if (canjewelry.config.gems_buffs.TryGetValue(buffNames[j], out var buffValuesDict))
                                        {
                                            dsc.Append(Lang.Get("canjewelry:buff-name-" + buffNames[j]));
                                            dsc.Append(buffValues[j] * 100 > 0 ? " +" + Math.Round(buffValues[j] * 100, 3) + "%" : " " + Math.Round(buffValues[j] * 100, 3) + "%");
                                            dsc.AppendLine();
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            //if item has cutom variant handling or just normal canhavenumbersocket parameter, just print it in item's info
            else if(itemstack.ItemAttributes != null && 
                (itemstack.ItemAttributes.KeyExists(CANJWConstants.CAN_CUSTOM_VARIANTS) ||
                itemstack.ItemAttributes.KeyExists("canhavesocketsnumber")))
            {
                int maxSocketsNumber = EncrustableCB.GetMaxAmountSockets(itemstack);
                if (maxSocketsNumber > 0)
                {
                    dsc.AppendLine(Lang.Get("canjewelry:item-can-have-n-sockets", maxSocketsNumber));
                }
            }
        }
        public static void Postfix_CollectibleObject_GetMaxDurability(ref int __result, ItemStack itemstack)
        {
            if(itemstack != null)
            {
                ITreeAttribute tree = itemstack.Attributes.GetTreeAttribute(CANJWConstants.ITEM_ENCRUSTED_STRING);
                if(tree == null)
                {
                    return;
                }

                float valueOrDefault = tree.TryGetFloat(CANJWConstants.CANDURABILITY_STRING).GetValueOrDefault();
                if (valueOrDefault > 0f && __result > 1)
                {
                    __result = (int)(__result * (1f + valueOrDefault));
                }
            }
        }
        public static void TryDropGems(Entity byEntity, ItemSlot itemslot)
        {
            if (byEntity == null || byEntity.Api != null && byEntity.Api.Side != EnumAppSide.Client)
            {
                return;
            }
            /*if(byEntity.Api.Side == EnumAppSide.Client)
            {
                return;
            }*/
            if (itemslot.Itemstack != null && itemslot.Itemstack.Attributes.HasAttribute(CANJWConstants.ITEM_ENCRUSTED_STRING))
            {
                Random r = new Random();
                var tree = itemslot.Itemstack.Attributes.GetTreeAttribute(CANJWConstants.ITEM_ENCRUSTED_STRING);
                for (int i = 0; i < EncrustableCB.GetMaxAmountSockets(itemslot.Itemstack); i++)
                {
                    if(canjewelry.config.chance_gem_drop_on_item_broken == 0 || r.NextDouble() > canjewelry.config.chance_gem_drop_on_item_broken)
                    {
                        continue;
                    }
                    ITreeAttribute socketSlot = tree.GetTreeAttribute("slot" + i.ToString());
                    if (socketSlot != null)
                    {
                        int size = socketSlot.GetInt("size");
                        string gemType = socketSlot.GetString("gemtype");
                        string gemSize;
                        switch (size)
                        {
                            case 1:
                                gemSize = "normal";
                                break;
                            case 2:
                                gemSize = "flawless";
                                break;
                            case 3:
                                gemSize = "exquisite";
                                break;
                            default:
                                return;
                        }

                        string[] buffNames = (socketSlot[CANJWConstants.ENCRUSTABLE_BUFFS_NAMES] as StringArrayAttribute).value;
                        float[] buffValues = (socketSlot[CANJWConstants.ENCRUSTABLE_BUFFS_VALUES] as FloatArrayAttribute).value;
                        ITreeAttribute isTree = new TreeAttribute();
                        tree[CANJWConstants.ENCRUSTABLE_BUFFS_NAMES] = new StringArrayAttribute(buffNames);
                        tree[CANJWConstants.ENCRUSTABLE_BUFFS_VALUES] = new FloatArrayAttribute(buffValues);
                        tree.SetString(CANJWConstants.CUTTING_TYPE, socketSlot.GetString(CANJWConstants.CUTTING_TYPE));
                        /* ITreeAttribute tree = new TreeAttribute();
                         tree.SetString(CANJWConstants.CUTTING_TYPE, newCuttingType);
                         workStack.Attributes[CANJWConstants.CUT_GEM_TREE] = tree;
                         EncrustableCB.ApplyCuttingBuff(workStack);*/


                        Item currentItem = canjewelry.sapi.World.GetItem(new AssetLocation("canjewelry:" + "gem-cut-" + gemSize + "-" + gemType));
                        ItemStack newIS = new ItemStack(currentItem, 1);
                        newIS.Attributes[CANJWConstants.CUT_GEM_TREE] = tree;
                        canjewelry.sapi.World.SpawnItemEntity(newIS, byEntity.Pos.XYZ.Clone().Add(0.5f, 0.25f, 0.5f));
                    }
                }
            }              
        }
        public static IEnumerable<CodeInstruction> Transpiler_CollectibleObject_DamageItem(IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {
            bool found = false;
            var codes = new List<CodeInstruction>(instructions);
            var proxyMethod = AccessTools.Method(typeof(harmPatch), "TryDropGems");
            for (int i = 0; i < codes.Count; i++)
            {

                if (!found &&
                        codes[i].opcode == OpCodes.Ldarg_3 && codes[i + 1].opcode == OpCodes.Ldnull && codes[i + 2].opcode == OpCodes.Callvirt && codes[i - 1].opcode == OpCodes.Bgt)
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_2);
                    yield return new CodeInstruction(OpCodes.Ldarg_3);
                    yield return new CodeInstruction(OpCodes.Call, proxyMethod);
                    found = true;
                }               
                yield return codes[i];
            }
        }

        public static void Postfix_CharacterSystem_StartClientSide(CharacterSystem __instance, ICoreClientAPI api, GuiDialogCharacterBase ___charDlg)
        {
            int lastIndex = ___charDlg.Tabs.Count;
            ___charDlg.Tabs.Add(new GuiTab()
            {
                Name = Lang.Get("canjewelry:stats-tab-name"),
                DataInt = ___charDlg.Tabs.Count
            });
            ___charDlg.RenderTabHandlers.Add(new Action<GuiComposer>(composeStatsTab));

            ___charDlg.Tabs.Add(new GuiTab()
            {
                Name = Lang.Get("canjewelry:additionaljewelry-tab-name"),
                DataInt = ___charDlg.Tabs.Count
            });
            ___charDlg.RenderTabHandlers.Add(new Action<GuiComposer>(composeAdditionalJewelryTab));
        }
        public static void Postfix_ItemChisel_OnHeldAttackStart(ItemChisel __instance, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            if (blockSel != null)
            {
                BlockEntity be = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position);
                BlockEntityGemCuttingTable bea = be as BlockEntityGemCuttingTable;
                if (bea == null)
                {
                    return;
                }
                if (byEntity.World.Side == EnumAppSide.Client)
                {
                    bea.OnUseOver((byEntity as EntityPlayer).Player, blockSel.SelectionBoxIndex);
                    handling = EnumHandHandling.PreventDefault;
                }
            }
        }
        public static int lineCounter = 0;
        public static StringBuilder BuildText()
        {
            StringBuilder sb = new StringBuilder();

            var player = canjewelry.capi.World.Player;
            var playerAttributes = player.Entity.WatchedAttributes;
            lineCounter = 0;
            if (playerAttributes.Keys.Contains("stats"))
            {
                var stats = playerAttributes.GetTreeAttribute("stats");
                foreach(var it in canjewelry.config.buffs_to_show_gui)
                {
                    if(stats.HasAttribute(it))
                    {
                        lineCounter++;
                        sb.AppendLine(Lang.Get("canjewelry:buff-name-" + it) + " " + player.Entity.Stats[it].GetBlended());
                    }
                    
                }
            }

            return sb;
        }
        public static List<string>GetPlayerBuffCellElements()
        {
            List<string> cellElements = new List<string>();
            var player = canjewelry.capi.World.Player;
            var playerAttributes = player.Entity.WatchedAttributes;
            if (playerAttributes.Keys.Contains("stats"))
            {
                var stats = playerAttributes.GetTreeAttribute("stats");
                foreach (var it in canjewelry.config.buffs_to_show_gui)
                {
                    if (stats.HasAttribute(it))
                    {
                        cellElements.Add(Lang.Get("canjewelry:buff-name-" + it) + ": " + player.Entity.Stats[it].GetBlended());                   
                    }
                }
            }
            return cellElements;
        }
        private static void composeStatsTab(GuiComposer compo)
        {
            var mainBounds = ElementBounds.Fixed(0.0, 35.0, 355.0, 250);
            var textBounds = mainBounds.FlatCopy();
            mainBounds.Alignment = EnumDialogArea.LeftTop;
            int insetDepth = 3;
            int insetWidth = (int)mainBounds.fixedWidth;
            int insetHeight = (int)mainBounds.fixedHeight;
            int rowHeight = 25;

            //compo.AddRichtext("hello", CairoFont.WhiteDetailText().WithLineHeightMultiplier(1.15).WithFontSize(16), mainBounds);

            ElementBounds insetBounds = ElementBounds.Fixed(0, GuiStyle.TitleBarHeight, insetWidth, insetHeight);
            ElementBounds scrollbarBounds = insetBounds.RightCopy().WithFixedWidth(20);
            ElementBounds clipBounds = insetBounds.ForkContainingChild(GuiStyle.HalfPadding, GuiStyle.HalfPadding, GuiStyle.HalfPadding, GuiStyle.HalfPadding);
            ElementBounds containerBounds = insetBounds.ForkContainingChild(GuiStyle.HalfPadding, GuiStyle.HalfPadding, GuiStyle.HalfPadding, GuiStyle.HalfPadding);
            ElementBounds containerRowBounds = ElementBounds.Fixed(0, 0, insetWidth, rowHeight);

            compo.BeginChildElements(mainBounds)
               .AddInset(insetBounds, insetDepth)
                    .BeginClip(clipBounds)
                        .AddContainer(containerBounds, "scroll-content")
                    .EndClip()
                    .AddVerticalScrollbar((value)  =>
                    {
                        ElementBounds bounds = compo.GetContainer("scroll-content").Bounds;
                        bounds.fixedY = 5 - value;
                        bounds.CalcWorldBounds();
                    }, scrollbarBounds, "scrollbar")
            .EndChildElements();
            GuiElementContainer scrollArea = compo.GetContainer("scroll-content");
            var li = GetPlayerBuffCellElements();
            foreach (var it in li)
            {
                scrollArea.Add(new GuiElementRichtext(compo.Api, VtmlUtil.Richtextify(compo.Api,it, CairoFont.WhiteMediumText().WithFontSize(20)), containerRowBounds));
                containerRowBounds = containerRowBounds.BelowCopy();
            }
            compo.Compose();

            // After composing dialog, need to set the scrolling area heights to enable scroll behavior
            float scrollVisibleHeight = (float)clipBounds.fixedHeight;
            float scrollTotalHeight = rowHeight * li.Count;
            compo.GetScrollbar("scrollbar").SetHeights(scrollVisibleHeight, scrollTotalHeight);

            return;
        }
        private static void composeAdditionalJewelryTab(GuiComposer compo)
        {
            var mainBounds = ElementBounds.Fixed(0.0, 35.0, 355.0, 10);
            var textBounds = mainBounds.FlatCopy();

            //compo.AddStaticText("hello", CairoFont.WhiteDetailText(), textBounds);
            var invBounds = textBounds.BelowCopy(20, 0).WithFixedSize(350, 250);
            IInventory additionalJewelryInv = canjewelry.capi.World.Player.InventoryManager.GetOwnInventory("additionaljewelrycharacter");
            
            compo.AddItemSlotGrid(additionalJewelryInv, new Action<object>(SendInvPacket), 6, invBounds, "invBounds");
            compo.Compose();
        }
        protected static void SendInvPacket(object packet)
        {
            canjewelry.capi.Network.SendPacketClient(packet);
        }
        public static IEnumerable<CodeInstruction> Transpiler_ServerWorldPlayerData_ToPacketForOtherPlayers(IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {
            bool found = false;
            bool foundSec = false;
            var codes = new List<CodeInstruction>(instructions);
            
            Label returnLabelContinue = il.DefineLabel();

            for (int i = 0; i < codes.Count; i++)
            {

                if (!found &&
                        codes[i].opcode == OpCodes.Ldloc_S && codes[i + 1].opcode == OpCodes.Callvirt && codes[i + 2].opcode == OpCodes.Ldstr && codes[i - 1].opcode == OpCodes.Brtrue_S)
                {
                    if(codes[i + 2].operand as string == "backpack")
                    {
                        yield return new CodeInstruction(OpCodes.Ldloc_S, codes[i].operand);
                        yield return new CodeInstruction(OpCodes.Callvirt, codes[i + 1].operand);
                        yield return new CodeInstruction(OpCodes.Ldstr, "additionaljewelrycharacter");
                        yield return new CodeInstruction(OpCodes.Call, codes[i + 3].operand);
                        yield return new CodeInstruction(OpCodes.Brtrue_S, returnLabelContinue);
                        found = true;
                    }                
                }

                if (!foundSec &&
                       codes[i].opcode == OpCodes.Ldloc_0 && codes[i + 1].opcode == OpCodes.Ldloc_S && codes[i + 2].opcode == OpCodes.Castclass && codes[i - 1].opcode == OpCodes.Brfalse_S)
                {
                    codes[i].labels.Add(returnLabelContinue);
                    foundSec = true;
                }

                yield return codes[i];
            }
        }
    }
}
