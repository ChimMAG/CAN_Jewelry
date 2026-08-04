using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.Server;

namespace canjewelry.src.harmony
{
    public static class ClientPatcher
    {
        public static void ApplyPatches(ICoreClientAPI capi, string harmonyID, ref Harmony harmonyInstance)
        {
            harmonyInstance = new Harmony(harmonyID + "_client");

            harmonyInstance.Patch(typeof(Vintagestory.API.Client.GuiElementItemSlotGridBase).GetMethod("ComposeSlotOverlays", BindingFlags.NonPublic | BindingFlags.Instance), transpiler: new HarmonyMethod(typeof(harmPatch).GetMethod("Transpiler_ComposeSlotOverlays_Add_Socket_Overlays_Not_Draw_ItemDamage")));

            harmonyInstance.Patch(typeof(Vintagestory.API.Common.CollectibleObject).GetMethod("GetHeldItemInfo"), postfix: new HarmonyMethod(typeof(harmPatch).GetMethod("Postfix_GetHeldItemInfo")));

            harmonyInstance.Patch(typeof(CharacterSystem).GetMethod("StartClientSide"), postfix: new HarmonyMethod(typeof(harmPatch).GetMethod("Postfix_CharacterSystem_StartClientSide")));

            harmonyInstance.Patch(typeof(ItemChisel).GetMethod("OnHeldAttackStart"), postfix: new HarmonyMethod(typeof(harmPatch).GetMethod("Postfix_ItemChisel_OnHeldAttackStart")));

            // The DropMouseSlotItems prefix that used to sit here existed only to stop the ImGui
            // inventory grid from throwing the held stack into the world. The native slot grid
            // handles the mouse itself, so the patch is gone with it.
        }
    }
}
