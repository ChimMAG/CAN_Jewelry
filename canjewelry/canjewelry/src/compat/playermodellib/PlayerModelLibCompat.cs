using canjewelry.src.harmony;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace canjewelry.src.compat.playermodellib
{
    public class PlayerModelLibCompat : ModSystem
    {
        public static Harmony harmonyInstance;
        public const string harmonyID = "canjewelry.PlayerModelLibCompat.Patches";
        public override double ExecuteOrder()
        {
            return 3;
        }
        public override bool ShouldLoad(ICoreAPI api)
        {
            return true;
            /*if (base.ShouldLoad(api))
            {
                if (api.Side == EnumAppSide.Client || !api.ModLoader.IsModEnabled("rustyshellfork"))
                {
                    return false;
                }
                return true;
            }
            return false;*/
        }
        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            var field = typeof(PlayerModelLib.WearablesTesselatorBehavior).GetField(
                "InventoriesToProcess",
                BindingFlags.Public | BindingFlags.Instance
            );

            var value = (HashSet<string>)field.GetValue(myClassInstance);
        }
        public override void StartServerSide(ICoreServerAPI api)
        {
            harmonyInstance = new Harmony(harmonyID);
           /* harmonyInstance.Patch(typeof(BlastExtensions).GetMethod("CommonBlast"), prefix: new HarmonyMethod(typeof(harmPatch).GetMethod("Prefix_IServerWorldAccessor_CommonBlast")));
            harmonyInstance.Patch(typeof(BlastExtensions).GetMethod("GasBlast"), prefix: new HarmonyMethod(typeof(harmPatch).GetMethod("Prefix_IServerWorldAccessor_GasBlast")));
            harmonyInstance.Patch(typeof(BlastExtensions).GetMethod("IncendiaryBlast"), prefix: new HarmonyMethod(typeof(harmPatch).GetMethod("Prefix_IServerWorldAccessor_IncendiaryBlast")));*/
        }
    }
}
