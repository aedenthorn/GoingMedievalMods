using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NSEipix.FileReader;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace DevTools
{
    [BepInPlugin("aedenthorn.DevTools", "Data Mod Framework", "0.1.0")]
    public partial class BepInExPlugin : BaseUnityPlugin
    {
        private static BepInExPlugin context;

        public static ConfigEntry<bool> modEnabled;
        public static ConfigEntry<bool> isDebug;
        public static string assetPath;
        public static ConfigEntry<int> nexusID;

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
           
            assetPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), typeof(BepInExPlugin).Namespace);
            if (!Directory.Exists(assetPath))
                Directory.CreateDirectory(assetPath);

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
        }

        [HarmonyPatch(typeof(DefaultFileReader), new Type[] { })]
        static class ReadFile_Patch
        {
            static void Postfix(ref string ___persistantPath)
            {
                if (!modEnabled.Value)
                    return;
                ___persistantPath = assetPath;
            }
        }
    }
}
