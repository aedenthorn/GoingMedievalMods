using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using NSEipix.Base;
using NSMedieval.Map;
using NSMedieval.Model.MapNew;
using NSMedieval.Repository;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace DevTools
{
    [BepInPlugin("aedenthorn.CustomMapSizes", "Custom Map Sizes", "0.1.0")]
    public partial class BepInExPlugin : BaseUnityPlugin
    {
        private static BepInExPlugin context;

        public static ConfigEntry<bool> modEnabled;
        public static ConfigEntry<bool> isDebug;
        //public static ConfigEntry<int> nexusID;

        public static void Dbgl(string str = "", bool pref = true)
        {
            if (isDebug.Value)
                Debug.Log((pref ? typeof(BepInExPlugin).Namespace + " " : "") + str);
        }
        private void Awake()
        {

            context = this;
            modEnabled = Config.Bind<bool>("General", "Enabled", true, "Enable this mod");
            isDebug = Config.Bind<bool>("General", "IsDebug", true, "Enable debug logs");
            //nexusID = Config.Bind<int>("General", "NexusID", 1, "Nexus mod ID for updates");

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
        }
        [HarmonyPatch(typeof(MapGeneration), "ExecuteStep")]
        static class ExecuteStep_Patch
        {
            static void Prefix(MapGeneration __instance, ref MapGenerationStep2 step, int ___mapSizeX, int ___mapSizeZ)
            {
                if (!modEnabled.Value)
                    return;
                MapSize defaultSize = MonoSingleton<MapSizeRepository>.Instance.GetByID("default_size");
                float sizeMult = (___mapSizeX * ___mapSizeZ / (float)(defaultSize.Width * defaultSize.Length));
                var t = Traverse.Create(step);
                Vector2 size = t.Field("size").GetValue<Vector2>();
                t.Field("size").SetValue(new Vector2(Mathf.RoundToInt(size.x * ___mapSizeX / defaultSize.Width), Mathf.RoundToInt(size.y * ___mapSizeZ / defaultSize.Length)));

                //t.Field("repeatCountMax").SetValue(Mathf.RoundToInt(t.Field("repeatCountMax").GetValue<int>() * sizeMult));
                //t.Field("repeatCount").SetValue(Mathf.RoundToInt(t.Field("repeatCount").GetValue<int>() * sizeMult));
            }
        }
        [HarmonyPatch(typeof(MapGeneration), "ApplyVoxelTypes")]
        static class ApplyVoxelTypes_Patch
        {
            static void Prefix(int ___mapSizeX, int ___mapSizeZ, List<VoxelTypeDistribution> voxelTypeDistribution)
            {
                if (!modEnabled.Value)
                    return;
                MapSize defaultSize = MonoSingleton<MapSizeRepository>.Instance.GetByID("default_size");
                float sizeMult = (___mapSizeX * ___mapSizeZ / (float)(defaultSize.Width * defaultSize.Length));
                Dbgl($"map scale: {sizeMult}x");
                for (int i = 0; i < voxelTypeDistribution.Count; i++)
                {
                    var t = Traverse.Create(voxelTypeDistribution[i]);
                    t.Field("minCount").SetValue(Mathf.RoundToInt(t.Field("minCount").GetValue<int>() * sizeMult));
                    t.Field("maxCount").SetValue(Mathf.RoundToInt(t.Field("maxCount").GetValue<int>() * sizeMult));
                }
            }
        }
        [HarmonyPatch(typeof(MapGeneration), "GetSmoothValue")]
        static class GetSmoothValue_Patch
        {
            static bool Prefix(ref float __result)
            {
                if (!modEnabled.Value)
                    return true;
                __result = 1f;
                return false;
            }
        }
    }
}
