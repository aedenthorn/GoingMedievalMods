using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using NSEipix.Base;
using NSMedieval.DevConsole;
using NSMedieval.UI;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace DevTools
{
    [BepInPlugin("aedenthorn.DevTools", "Dev Tools", "0.3.0")]
    public partial class BepInExPlugin : BaseUnityPlugin
    {
        private static BepInExPlugin context;

        public static ConfigEntry<bool> modEnabled;
        public static ConfigEntry<bool> isDebug;
        public static ConfigEntry<bool> showButton;
        public static ConfigEntry<string> hotKey;
        public static ConfigEntry<string> hotKeyMod;
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
            showButton = Config.Bind<bool>("General", "ShowButton", true, "Show DevTools Button");
            hotKey = Config.Bind<string>("General", "HotKey", "`", "Hotkey to toggle dev tools");
            hotKeyMod = Config.Bind<string>("General", "HotKeyMod", "", "Hotkey modifier ");
            //nexusID = Config.Bind<int>("General", "NexusID", 1, "Nexus mod ID for updates");

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
        }

        private void Update()
        {
            if (AedenthornUtils.CheckKeyDown(hotKey.Value) && AedenthornUtils.CheckKeyHeld(hotKeyMod.Value, false))
                Traverse.Create(MonoSingleton<DeveloperToolsView>.Instance).Method("SetActive").GetValue();

        }

        [HarmonyPatch(typeof(TopLeftPanelView), "OnDevToolsActive")]
        static class OnDevToolsActive_Patch
        {
            static bool Prefix()
            {
                if (!modEnabled.Value)
                    return true;
                MonoSingleton<DeveloperToolsView>.Instance.Open();
                return !showButton.Value;
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
        [HarmonyPatch(typeof(DeveloperPanelView), "FlushMenuItems")]
        static class DeveloperPanelView_ReparentMenuItems_Patch
        {
            static void Prefix(DeveloperPanelView __instance, ref GameObject ___contentParent)
            {
                if (!modEnabled.Value)
                    return;
                ___contentParent.GetComponent<GridLayoutGroup>().constraint = GridLayoutGroup.Constraint.Flexible;
                if(___contentParent.GetComponent<ContentSizeFitter>() == null)
                    ___contentParent.AddComponent<ContentSizeFitter>();
                ___contentParent.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                ___contentParent.GetComponent<RectTransform>().pivot = new Vector2(0, 1);
                ___contentParent.GetComponent<RectTransform>().pivot = new Vector2(0, 1);

            }
        }
    }
}
