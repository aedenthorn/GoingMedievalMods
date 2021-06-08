using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using NSEipix;
using NSEipix.Base;
using NSMedieval;
using NSMedieval.Controllers;
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

namespace CharacterEdit
{
    [BepInPlugin("aedenthorn.CharacterEdit", "Character Edit", "0.2.0")]
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

        [HarmonyPatch(typeof(ClosableUIView), "Update")]
        static class ClosableUIView_Update_Patch
        {
            static void Postfix(ClosableUIView __instance)
            {
                if (!modEnabled.Value || !(__instance is CharactersView) || Input.mouseScrollDelta.y == 0)
                {
                    lastMousePos = Input.mousePosition;
                    return;
                }

                Traverse t = Traverse.Create(__instance);
                List<WorkerInstance> workers = t.Field("workers").GetValue<List<WorkerInstance>>();
                int selected = t.Field("selected").GetValue<int>();
                Traverse tc = Traverse.Create(workers[selected]);
                Traverse ti = Traverse.Create(workers[selected].Info);
                Traverse tg = Traverse.Create(MonoSingleton<WorkerGenerator>.Instance);

                Vector3 mousePos = Input.mousePosition;

                if (lastMousePos == Vector3.zero)
                    lastMousePos = mousePos;

                PointerEventData eventData = new PointerEventData(EventSystem.current)
                {
                    position = lastMousePos
                };

                List<RaycastResult> raycastResults = new List<RaycastResult>();
                EventSystem.current.RaycastAll(eventData, raycastResults);
                foreach (RaycastResult rcr in raycastResults)
                {
                    if (rcr.gameObject.transform.parent.name.StartsWith("CharacterSkill"))
                    {
                        List<SkillLayoutItemView> workerSkills = t.Field("workerSkills").GetValue<List<SkillLayoutItemView>>();
                        string skillName = rcr.gameObject.transform.parent.Find("Name").GetComponentInChildren<TextMeshProUGUI>().text;
                        int skillIdx = workers[selected].Skills.Skills.FindIndex(s => skillName == Singleton<LocalizationController>.Instance.GetText("skill_name_" + s.Id.ToString()));
                        Traverse ts = Traverse.Create(workers[selected].Skills.Skills[skillIdx]);

                        if (Input.GetKey(KeyCode.LeftControl))
                        {
                            int skillPassion = workers[selected].Skills.Skills[skillIdx].PassionLevel;
                            if (Input.mouseScrollDelta.y < 0 && skillPassion > 0)
                            {
                                ts.Field("passionLevel").SetValue(skillPassion - 1);
                            }
                            else if (Input.mouseScrollDelta.y > 0 && skillPassion <= MonoSingleton<GenerationSettingsRepository>.Instance.Settings.Passions.Count)
                                ts.Field("passionLevel").SetValue(skillPassion + 1);
                        }
                        else
                        {
                            int skillLevel = workers[selected].Skills.Skills[skillIdx].Level;
                            float skillExp = workers[selected].Skills.Skills[skillIdx].Experience;
                            if (Input.mouseScrollDelta.y < 0 && skillLevel > 1)
                            {
                                ts.Field("experience").SetValue(skillExp
                                    - (skillExp - MonoSingleton<SkillLevelsRepository>.Instance.GetXpRequirement(workers[selected].Skills.Skills[skillIdx].Id, workers[selected].Skills.Skills[skillIdx].Level))
                                    - (MonoSingleton<SkillLevelsRepository>.Instance.GetXpRequirement(workers[selected].Skills.Skills[skillIdx].Id, workers[selected].Skills.Skills[skillIdx].Level) - MonoSingleton<SkillLevelsRepository>.Instance.GetXpRequirement(workers[selected].Skills.Skills[skillIdx].Id, workers[selected].Skills.Skills[skillIdx].Level - 1))
                                    );
                                ts.Field("level").SetValue(skillLevel - 1);
                            }
                            else if (Input.mouseScrollDelta.y > 0)
                                workers[selected].Skills.Skills[skillIdx].AddLevels(1);
                        }
                        t.Method("ShowWorker", new object[] { selected }).GetValue();
                        t.Method("InitializeGroupSkills").GetValue();
                        return;

                    }
                    else if (rcr.gameObject.transform.parent.name == "Avatar")
                    {
                        
                        if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKey(KeyCode.LeftShift))
                        {
                            WorkerBodyPreview preview = MonoSingleton<WorkerImageRepository>.Instance.GetPrefab(workers[selected].Info.GetPhysicalLookKey()).GetComponentInChildren<WorkerBodyPreview>();
                            if (preview.BodyParts.Count == 0)
                                return; 
                            Transform transform = preview.BodyParts[0];
                            int index = 0;
                            List<string> children = new List<string>();
                            for (int i = 0; i < transform.childCount; i++)
                            {
                                if (transform.GetChild(i).name == workers[selected].Info.PhysicalLook.WorkerBody[0])
                                    index = i;
                                children.Add(transform.GetChild(i).name);
                            }
                            if (Input.mouseScrollDelta.y < 0 && index > 0)
                            {
                                index--;
                            }
                            else if (Input.mouseScrollDelta.y > 0 && index < children.Count - 1)
                            {
                                index++;
                            }
                            else
                                return;
                            workers[selected].Info.PhysicalLook.WorkerBody[0] = children[index];
                        }
                        else if (Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.LeftShift))
                        {
                            WorkerBodyPreview preview = MonoSingleton<WorkerImageRepository>.Instance.GetPrefab(workers[selected].Info.GetPhysicalLookKey()).GetComponentInChildren<WorkerBodyPreview>();
                            if (preview.BodyParts.Count < 2)
                                return;
                            Transform transform = preview.BodyParts[1];
                            int index = 0;
                            List<string> children = new List<string>();
                            for (int i = 0; i < transform.childCount; i++)
                            {
                                if (transform.GetChild(i).name == workers[selected].Info.PhysicalLook.WorkerBody[1])
                                    index = i;
                                children.Add(transform.GetChild(i).name);
                            }
                            if (Input.mouseScrollDelta.y < 0 && index > 0)
                            {
                                index--;
                            }
                            else if (Input.mouseScrollDelta.y > 0 && index < children.Count - 1)
                            {
                                index++;
                            }
                            else
                                return;
                            workers[selected].Info.PhysicalLook.WorkerBody[1] = children[index];
                        }
                        else if (Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.LeftAlt))
                        {
                            WorkerBodyPreview preview = MonoSingleton<WorkerImageRepository>.Instance.GetPrefab(workers[selected].Info.GetPhysicalLookKey()).GetComponentInChildren<WorkerBodyPreview>();
                            if (preview.BodyParts.Count < 3)
                                return;
                            Transform transform = preview.BodyParts[2];
                            int index = 0;
                            List<string> children = new List<string>();
                            for (int i = 0; i < transform.childCount; i++)
                            {
                                if (transform.GetChild(i).name == workers[selected].Info.PhysicalLook.WorkerBody[2])
                                    index = i;
                                children.Add(transform.GetChild(i).name);
                            }
                            if (Input.mouseScrollDelta.y < 0 && index > 0)
                            {
                                index--;
                            }
                            else if (Input.mouseScrollDelta.y > 0 && index < children.Count - 1)
                            {
                                index++;
                            }
                            else
                                return;
                            workers[selected].Info.PhysicalLook.WorkerBody[2] = children[index];
                        }
                        else if (Input.GetKey(KeyCode.LeftControl))
                        {
                            tc.Field("creationID").SetValue(0);
                            if (workers[selected].Info.Gender == Gender.Female)
                                ti.Field("gender").SetValue(Gender.Male);
                            else
                                ti.Field("gender").SetValue(Gender.Female);
                            ti.Field("height").SetValue(tg.Method("GetHeight", new object[] { workers[selected].Info.Gender }).GetValue<float>());
                            ti.Field("weightCoefficient").SetValue(tg.Method("GetWeightCoefficient", new object[] { workers[selected].Info.Height }).GetValue<float>());

                            workers[selected].Info.SetIgnoredTypes(tg.Method("GetPhysicalIgnoreTypes", new object[] { new List<WorkerCharacteristicType>(), workers[selected].Info.Gender, workers[selected].Info.Height, workers[selected].Info.WeightCoefficient }).GetValue<List<WorkerCharacteristicType>>());
                            workers[selected].Info.SetPhysicalLook(tg.Method("GetPhysicalLook", new object[] { workers[selected] }).GetValue<WorkerPhysicalLook>());
                        }
                        else if (Input.GetKey(KeyCode.LeftAlt))
                        {
                            string color = workers[selected].Info.PhysicalLook.BodyColors[workers[selected].Info.PhysicalLook.ShaderParameters[0]];

                            int index = MonoSingleton<WorkerBaseRepository>.Instance.BaseWorker.SkinColor.FindIndex(s => s == color);

                            if (Input.mouseScrollDelta.y < 0 && index > 0)
                            {
                                index--;
                            }
                            else if (Input.mouseScrollDelta.y > 0 && index < MonoSingleton<WorkerBaseRepository>.Instance.BaseWorker.SkinColor.Count - 1)
                            {
                                index++;
                            }
                            else
                                return;
                            workers[selected].Info.PhysicalLook.BodyColors[workers[selected].Info.PhysicalLook.ShaderParameters[0]] = MonoSingleton<WorkerBaseRepository>.Instance.BaseWorker.SkinColor[index];
                        }
                        else if (Input.GetKey(KeyCode.LeftShift))
                        {
                            string color = workers[selected].Info.PhysicalLook.BodyColors[workers[selected].Info.PhysicalLook.ShaderParameters[1]];

                            int index = MonoSingleton<WorkerBaseRepository>.Instance.BaseWorker.HairColor.FindIndex(s => s == color);

                            if (Input.mouseScrollDelta.y < 0 && index > 0)
                            {
                                index--;
                            }
                            else if (Input.mouseScrollDelta.y > 0 && index < MonoSingleton<WorkerBaseRepository>.Instance.BaseWorker.HairColor.Count - 1)
                            {
                                index++;
                            }
                            else
                                return;
                            workers[selected].Info.PhysicalLook.BodyColors[workers[selected].Info.PhysicalLook.ShaderParameters[1]] = MonoSingleton<WorkerBaseRepository>.Instance.BaseWorker.HairColor[index];

                        }
                        else
                            workers[selected].Info.SetPhysicalLook(tg.Method("GetPhysicalLook", new object[] { workers[selected] }).GetValue<WorkerPhysicalLook>());

                        //tc.Field("equipItemOnSpawn").SetValue(MonoSingleton<GameStartController>.Instance.SelectedScenario.VillagerConstraints.DefaultClothes.GetRandom() ?? "good_linen_winter_clothes");
                        t.Method("ShowWorker", new object[] { selected }).GetValue();
                        MonoSingleton<WorkerImageController>.Instance.CreatingWorker(workers[selected]);
                        MonoSingleton<TaskController>.Instance.WaitForNextFrame().Then(delegate
                        {
                            MonoSingleton<WorkerImageController>.Instance.CreatingWorker(workers[selected]);
                            t.Method("UpdateTabs", new object[] { selected }).GetValue();
                        });
                        return;
                    }
                    else if (rcr.gameObject.transform.parent.name == "BackStoryTitle")
                    {
                        int index;
                        if (Input.GetKey(KeyCode.LeftControl))
                        {
                            List<BackStory> backstories = (List<BackStory>)typeof(BackgroundRepositoryBase<BackStoryRepository, BackStory>).GetField("backgrounds", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(MonoSingleton<BackStoryRepository>.Instance);
                            index = backstories.FindIndex(b => b == workers[selected].Info.BackStory);
                            if (Input.mouseScrollDelta.y < 0 && index > 0)
                            {
                                index--;
                            }
                            else if (Input.mouseScrollDelta.y > 0 && index < backstories.Count - 1)
                            {
                                index++;
                            }
                            workers[selected].Info.SetBackStory(backstories[index]);
                        }
                        else
                        {
                            List<Background> backgrounds = (List<Background>)typeof(BackgroundRepositoryBase<BackgroundRepository, Background>).GetField("backgrounds", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(MonoSingleton<BackgroundRepository>.Instance);
                            index = backgrounds.FindIndex(b => b == workers[selected].Info.Background);
                            if (Input.mouseScrollDelta.y < 0 && index > 0)
                            {
                                index--;
                            }
                            else if (Input.mouseScrollDelta.y > 0 && index < backgrounds.Count - 1)
                            {
                                index++;
                            }
                            workers[selected].Info.SetBackground(backgrounds[index]);
                        }

                        t.Method("ShowWorker", new object[] { selected }).GetValue();
                        t.Method("InitializeGroupSkills").GetValue();
                        return;
                    }
                    else if (rcr.gameObject.transform.parent.name == "PerksList")
                    {
                        if (Input.GetKey(KeyCode.LeftControl))
                        {
                            List<string> perkIds = tc.Field("perkIds").GetValue<List<string>>();
                            List<Perk> perks = tc.Field("perks").GetValue<List<Perk>>();

                            List<Perk> allPerks = Traverse.Create(MonoSingleton<PerkRepository>.Instance).Field("perks").GetValue<List<Perk>>();

                            int totalPerkCount = rcr.gameObject.transform.parent.childCount;
                            if (Input.mouseScrollDelta.y < 0 && perks.Count > 0)
                            {
                                perkIds.RemoveAt(perkIds.Count - 1);
                                perks.RemoveAt(perks.Count - 1);
                            }
                            else if (Input.mouseScrollDelta.y > 0 && perks.Count < 4)
                            {
                                int idx = 0;
                                while (perkIds.Contains(allPerks[idx].Name))
                                    idx++;
                                perkIds.Add(allPerks[idx].Name);
                                perks.Add(allPerks[idx]);
                            }

                            t.Method("ShowWorker", new object[] { selected }).GetValue();
                            t.Method("InitializeGroupSkills").GetValue();
                        }
                        else
                        {
                            PerkTooltipView perkTooltipView = rcr.gameObject.GetComponent<PerkTooltipView>();
                            List<string> perkIds = tc.Field("perkIds").GetValue<List<string>>();
                            List<Perk> perks = tc.Field("perks").GetValue<List<Perk>>();
                            string perkId = Traverse.Create(perkTooltipView).Field("id").GetValue<string>();

                            List<Perk> allPerks = Traverse.Create(MonoSingleton<PerkRepository>.Instance).Field("perks").GetValue<List<Perk>>();
                            int allPerksIndex = allPerks.FindIndex(p => p.Name == perkId);

                            int perkIndex = rcr.gameObject.transform.GetSiblingIndex();
                            int perkInt = int.Parse(perkId.Split('_')[1]);
                            if (Input.mouseScrollDelta.y < 0 && allPerksIndex > 0)
                            {
                                while (allPerksIndex >= 0 && perkIds.Contains(allPerks[allPerksIndex].Name))
                                    allPerksIndex--;
                                if (allPerksIndex < 0)
                                    return;
                            }
                            else if (Input.mouseScrollDelta.y > 0 && allPerksIndex < allPerks.Count - 1)
                            {
                                while (allPerksIndex < allPerks.Count - 1 && perkIds.Contains(allPerks[allPerksIndex].Name))
                                    allPerksIndex++;
                                if (allPerksIndex >= allPerks.Count)
                                    return;
                            }

                            perkIds[perkIndex] = allPerks[allPerksIndex].Name;
                            perks[perkIndex] = allPerks[allPerksIndex];
                            t.Method("ShowWorker", new object[] { selected }).GetValue();
                            t.Method("InitializeGroupSkills").GetValue();
                        }
                        return;
                    }
                    else if (rcr.gameObject.transform.parent.name.StartsWith("ReligiousAlig") || rcr.gameObject.transform.parent.parent.name.StartsWith("ReligiousAlig"))
                    {
                        float align = workers[selected].Info.ReligiousAlignment;
                        if (Input.mouseScrollDelta.y < 0 && workers[selected].Info.ReligiousAlignment > 0)
                            align -= Input.GetKey(KeyCode.LeftControl) ? 0.1f : 0.01f;
                        else if (Input.mouseScrollDelta.y > 0 && workers[selected].Info.ReligiousAlignment < 1)
                            align += Input.GetKey(KeyCode.LeftControl) ? 0.1f : 0.01f;

                        ti.Field("religiousAlignment").SetValue(Mathf.Clamp01(align));
                        t.Method("ShowWorker", new object[] { selected }).GetValue();
                        t.Method("InitializeGroupSkills").GetValue();
                        return;
                    }
                    else if (rcr.gameObject.transform.name == "Age")
                    {
                        int age = ti.Field("age").GetValue<int>();
                        if (Input.mouseScrollDelta.y < 0 && age > MonoSingleton<GenerationSettingsRepository>.Instance.Settings.AgeRange.Min)
                        {
                            ti.Field("age").SetValue(age - 1);
                        }
                        else if (Input.mouseScrollDelta.y > 0 && age < MonoSingleton<GenerationSettingsRepository>.Instance.Settings.AgeRange.Max)
                            ti.Field("age").SetValue(age + 1);
                        t.Method("ShowWorker", new object[] { selected }).GetValue();
                        return;
                    }
                    else if (rcr.gameObject.transform.name == "Height")
                    {
                        float height = ti.Field("height").GetValue<float>();
                        if (Input.mouseScrollDelta.y < 0 && height > MonoSingleton<GenerationSettingsRepository>.Instance.Settings.HeightRange.Min)
                        {
                            ti.Field("height").SetValue(height - 1);
                        }
                        else if (Input.mouseScrollDelta.y > 0 && height < MonoSingleton<GenerationSettingsRepository>.Instance.Settings.HeightRange.Max)
                            ti.Field("height").SetValue(height + 1);
                        MonoSingleton<WorkerImageController>.Instance.CreatingWorker(workers[selected]);
                        t.Method("ShowWorker", new object[] { selected }).GetValue();
                        return;
                    }
                }
            }
        }
        //[HarmonyPatch(typeof(HumanoidBodyPreview), "ShowBody")]
        static class Test_Patch
        {
            static void Postfix(HumanoidBodyPreview __instance, List<Transform> ___bodyParts)
            {
                if (!modEnabled.Value)
                    return;
                Dbgl("WorkerBody");
                foreach (string s in __instance.GetInfo().PhysicalLook.WorkerBody)
                    Dbgl("\t"+s);
                Dbgl("bodyParts");
                foreach (Transform t in ___bodyParts)
                {
                    Dbgl("\t" + t.name);
                    for (int i = 0; i < t.childCount; i++)
                    {
                        Dbgl("\t\t" + t.GetChild(i).name);
                    }
                }
            }
        }
        //[HarmonyPatch(typeof(CharactersView), "SetWorkerInfo")]
        static class CharactersView_SetWorkerInfo_Patch
        {
            static void Postfix(CharactersView __instance, int index, TMP_InputField ___workerName, List<WorkerInstance> ___workers, int ___selected)
            {
                if (!modEnabled.Value)
                    return;
                ___workerName.text = ___workers[___selected].Info.GetFullName();
            }
        }
        //[HarmonyPatch(typeof(CharactersView), "OnWorkerNameInput")]
        static class CharactersView_OnWorkerNameInput_Patch
        {
            static bool Prefix(CharactersView __instance, ref string newName, TMP_InputField ___workerName, List<WorkerInstance> ___workers, int ___selected)
            {
                if (!modEnabled.Value)
                    return true;
                if(newName.Contains(" "))
                {
                    string text = newName.TrimStart();
                    if (string.CompareOrdinal(newName, text) != 0)
                    {
                        ___workerName.text = text;
                    }
                    text = text.TrimEnd();
                    var parts = newName.Split(' ');
                    typeof(CharacterInfoBase).GetField("lastName", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(___workers[___selected].Info, parts[parts.Length - 1]);
                    typeof(CharacterInfoBase).GetField("firstName", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(___workers[___selected].Info, string.Join(" ", parts.Take(parts.Length - 1)));
                    return false;
                }
                return true;
            }
        }
    }
}
