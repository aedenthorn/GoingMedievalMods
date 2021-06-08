using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using NSEipix;
using NSEipix.Base;
using NSMedieval;
using NSMedieval.Controllers;
using NSMedieval.DevConsole;
using NSMedieval.Model;
using NSMedieval.Repository;
using NSMedieval.State;
using NSMedieval.Types;
using NSMedieval.UI;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DevTools
{
    [BepInPlugin("aedenthorn.DevTools", "Dev Tools", "0.1.0")]
    public partial class BepInExPlugin : BaseUnityPlugin
    {
        private static BepInExPlugin context;

        public static ConfigEntry<bool> modEnabled;
        public static ConfigEntry<bool> isDebug;
        public static ConfigEntry<int> nexusID;

        private static Vector3 lastMousePos;

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

        [HarmonyPatch(typeof(TopLeftPanelView), "OnDevToolsActive")]
        static class OnDevToolsActive_Patch
        {
            static bool Prefix()
            {
                if (!modEnabled.Value)
                    return true;
                MonoSingleton<DeveloperToolsView>.Instance.Open();
                return false;
            }
        }
        [HarmonyPatch(typeof(DeveloperToolsView), "SetActive")]
        static class DeveloperToolsView_SetActive_Patch
        {
            static bool Prefix(DeveloperToolsView __instance, GameObject ___mainContainer)
            {
                if (!modEnabled.Value)
                    return true;

                ___mainContainer.SetActive(!___mainContainer.activeSelf);

                return false;
            }
        }
    }
}
