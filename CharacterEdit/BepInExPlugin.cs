using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using NSEipix;
using NSEipix.Base;
using NSMedieval;
using NSMedieval.Controllers;
using NSMedieval.Manager;
using NSMedieval.Model;
using NSMedieval.Repository;
using NSMedieval.State;
using NSMedieval.Types;
using NSMedieval.UI;
using NSMedieval.View;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace CharacterEdit
{
    [BepInPlugin("aedenthorn.CharacterEdit", "Character Edit", "0.6.1")]
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
                List<WorkerInstance> workers = MonoSingleton<CharacterEditController>.Instance.Workers;
                int selected = MonoSingleton<CharacterEditController>.Instance.Selected;
                Traverse tc = Traverse.Create(MonoSingleton<CharacterEditController>.Instance.SelectedWorker);
                Traverse ti = Traverse.Create(MonoSingleton<CharacterEditController>.Instance.SelectedWorker.Info);
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
                    if (rcr.gameObject.transform.parent?.name.StartsWith("CharacterSkill") == true)
                    {
                        Dbgl("scrolled on skill");
                        List<EditableSkillLayoutItemView> workerSkills = t.Field("workerSkills").GetValue<List<EditableSkillLayoutItemView>>();
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
                        t.Method("ShowWorker", new object[] { }).GetValue();
                        t.Method("SetGroupSkills").GetValue();
                        return;

                    }
                    else if (rcr.gameObject.transform.parent?.name == "Avatar")
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

                            int index = MonoSingleton<HumanAppearanceRepository>.Instance.GetByID("default").SkinColor.FindIndex(s => s == color);

                            if (Input.mouseScrollDelta.y < 0 && index > 0)
                            {
                                index--;
                            }
                            else if (Input.mouseScrollDelta.y > 0 && index < MonoSingleton<HumanAppearanceRepository>.Instance.GetByID("default").SkinColor.Count - 1)
                            {
                                index++;
                            }
                            else
                                return;
                            workers[selected].Info.PhysicalLook.BodyColors[workers[selected].Info.PhysicalLook.ShaderParameters[0]] = MonoSingleton<HumanAppearanceRepository>.Instance.GetByID("default").SkinColor[index];
                        }
                        else if (Input.GetKey(KeyCode.LeftShift))
                        {
                            string color = workers[selected].Info.PhysicalLook.BodyColors[workers[selected].Info.PhysicalLook.ShaderParameters[1]];

                            int index = MonoSingleton<HumanAppearanceRepository>.Instance.GetByID("default").HairColor.FindIndex(s => s == color);

                            if (Input.mouseScrollDelta.y < 0 && index > 0)
                            {
                                index--;
                            }
                            else if (Input.mouseScrollDelta.y > 0 && index < MonoSingleton<HumanAppearanceRepository>.Instance.GetByID("default").HairColor.Count - 1)
                            {
                                index++;
                            }
                            else
                                return;
                            workers[selected].Info.PhysicalLook.BodyColors[workers[selected].Info.PhysicalLook.ShaderParameters[1]] = MonoSingleton<HumanAppearanceRepository>.Instance.GetByID("default").HairColor[index];

                        }
                        else
                            workers[selected].Info.SetPhysicalLook(tg.Method("GetPhysicalLook", new object[] { workers[selected] }).GetValue<WorkerPhysicalLook>());

                        //tc.Field("equipItemOnSpawn").SetValue(MonoSingleton<GameStartController>.Instance.SelectedScenario.VillagerConstraints.DefaultClothes.GetRandom() ?? "good_linen_winter_clothes");
                        t.Method("ShowWorker", new object[] { }).GetValue();
                        MonoSingleton<WorkerImageController>.Instance.CreatingWorker(workers[selected]);
                        MonoSingleton<TaskController>.Instance.WaitForNextFrame().Then(delegate
                        {
                            MonoSingleton<WorkerImageController>.Instance.CreatingWorker(workers[selected]);
                            t.Method("UpdateTabs", new object[] { selected }).GetValue();
                        });
                        return;
                    }
                    else if (rcr.gameObject.transform.parent?.name == "BackStoryTitle")
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

                        t.Method("ShowWorker", new object[] { }).GetValue();
                        t.Method("SetGroupSkills").GetValue();
                        return;
                    }
                    else if (rcr.gameObject.transform.parent?.name == "PerksList")
                    {
                        if (Input.GetKey(KeyCode.LeftControl))
                        {
                            List<string> perkIds = tc.Field("perkIds").GetValue<List<string>>();
                            List<Perk> perks = tc.Field("perks").GetValue<List<Perk>>();

                            List<Perk> allPerks = Traverse.Create(MonoSingleton<PerkRepository>.Instance).Field("perks").GetValue<List<Perk>>();

                            if (Input.mouseScrollDelta.y < 0 && perks.Count > 1)
                            {
                                perkIds.RemoveAt(perkIds.Count - 1);
                                perks.RemoveAt(perks.Count - 1);
                            }
                            else if (Input.mouseScrollDelta.y > 0 && perks.Count < 10)
                            {
                                int idx = 0;
                                while (perkIds.Contains(allPerks[idx].Name))
                                    idx++;
                                perkIds.Add(allPerks[idx].Name);
                                perks.Add(allPerks[idx]);
                            }

                            t.Method("ShowWorker", new object[] { }).GetValue();
                            t.Method("SetGroupSkills").GetValue();
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
                            t.Method("ShowWorker", new object[] { }).GetValue();
                            t.Method("SetGroupSkills").GetValue();
                        }
                        return;
                    }
                    else if (rcr.gameObject.transform.parent?.name.StartsWith("ReligiousAlig") == true || rcr.gameObject.transform.parent?.parent?.name.StartsWith("ReligiousAlig") == true)
                    {
                        float align = workers[selected].Info.ReligiousAlignment;
                        if (Input.mouseScrollDelta.y < 0 && workers[selected].Info.ReligiousAlignment > 0)
                            align -= Input.GetKey(KeyCode.LeftControl) ? 0.1f : 0.01f;
                        else if (Input.mouseScrollDelta.y > 0 && workers[selected].Info.ReligiousAlignment < 1)
                            align += Input.GetKey(KeyCode.LeftControl) ? 0.1f : 0.01f;

                        ti.Field("religiousAlignment").SetValue(Mathf.Clamp01(align));
                        t.Method("ShowWorker", new object[] { }).GetValue();
                        t.Method("SetGroupSkills").GetValue();
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

        private void Update()
        {
            if (SceneManager.GetActiveScene().name != "MainScene")
                return;
            if (!modEnabled.Value || Input.mouseScrollDelta.y == 0)
            {
                lastMousePos = Input.mousePosition;
                return;
            }
            var selectionPanel = AccessTools.FieldRefAccess<UIController, SelectionPanelManager>(MonoSingleton<UIController>.Instance, "selectionPanel");
            if (selectionPanel == null || !selectionPanel.MainPanel.activeSelf)
                return;
            var workerExtraWindow = AccessTools.FieldRefAccess<SelectionPanelView, SelectionExtraWorker> (selectionPanel.PanelView, "workerExtraWindow");
            if (workerExtraWindow == null || !workerExtraWindow.gameObject.activeSelf)
                return;


            Traverse t = Traverse.Create(workerExtraWindow);
            Traverse tc = Traverse.Create(workerExtraWindow.Worker);
            Traverse ti = Traverse.Create(workerExtraWindow.Worker.Info);
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
                if (rcr.gameObject.layer != LayerMask.NameToLayer("UI") && rcr.gameObject.layer != LayerMask.NameToLayer("Default"))
                    continue;

                if (rcr.gameObject.transform.name == "SkillBar")
                {
                    string skillName = rcr.gameObject.transform.parent.Find("Title").GetComponentInChildren<TextMeshProUGUI>().text;
                    int skillIdx = workerExtraWindow.Worker.Skills.Skills.FindIndex(s => skillName == Singleton<LocalizationController>.Instance.GetText("skill_name_" + s.Id.ToString()));
                    if(skillIdx < 0)
                    {
                        Dbgl($"invalid skill {skillName} {skillIdx}");
                        return;
                    }
                    Traverse ts = Traverse.Create(workerExtraWindow.Worker.Skills.Skills[skillIdx]);

                    if (Input.GetKey(KeyCode.LeftControl))
                    {
                        int skillPassion = workerExtraWindow.Worker.Skills.Skills[skillIdx].PassionLevel;
                        if (Input.mouseScrollDelta.y < 0 && skillPassion > 0)
                        {
                            ts.Field("passionLevel").SetValue(skillPassion - 1);
                        }
                        else if (Input.mouseScrollDelta.y > 0 && skillPassion <= MonoSingleton<GenerationSettingsRepository>.Instance.Settings.Passions.Count)
                            ts.Field("passionLevel").SetValue(skillPassion + 1);
                    }
                    else
                    {
                        int skillLevel = workerExtraWindow.Worker.Skills.Skills[skillIdx].Level;
                        float skillExp = workerExtraWindow.Worker.Skills.Skills[skillIdx].Experience;
                        if (Input.mouseScrollDelta.y < 0 && skillLevel > 1)
                        {
                            ts.Field("experience").SetValue(skillExp
                                - (skillExp - MonoSingleton<SkillLevelsRepository>.Instance.GetXpRequirement(workerExtraWindow.Worker.Skills.Skills[skillIdx].Id, workerExtraWindow.Worker.Skills.Skills[skillIdx].Level))
                                - (MonoSingleton<SkillLevelsRepository>.Instance.GetXpRequirement(workerExtraWindow.Worker.Skills.Skills[skillIdx].Id, workerExtraWindow.Worker.Skills.Skills[skillIdx].Level) - MonoSingleton<SkillLevelsRepository>.Instance.GetXpRequirement(workerExtraWindow.Worker.Skills.Skills[skillIdx].Id, workerExtraWindow.Worker.Skills.Skills[skillIdx].Level - 1))
                                );
                            ts.Field("level").SetValue(skillLevel - 1);
                        }
                        else if (Input.mouseScrollDelta.y > 0)
                            workerExtraWindow.Worker.Skills.Skills[skillIdx].AddLevels(1);
                    }
                    workerExtraWindow.UpdateData();
                    return;
                }
                else if (rcr.gameObject.transform.parent?.name == "Stats" && rcr.gameObject.transform.name == "IconHolder")
                {
                    if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKey(KeyCode.LeftShift))
                    {
                        WorkerBodyPreview preview = MonoSingleton<WorkerImageRepository>.Instance.GetPrefab(workerExtraWindow.Worker.Info.GetPhysicalLookKey()).GetComponentInChildren<WorkerBodyPreview>();
                        if (preview.BodyParts.Count == 0)
                            return;
                        Transform transform = preview.BodyParts[0];
                        int index = 0;
                        List<string> children = new List<string>();
                        for (int i = 0; i < transform.childCount; i++)
                        {
                            if (transform.GetChild(i).name == workerExtraWindow.Worker.Info.PhysicalLook.WorkerBody[0])
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
                        workerExtraWindow.Worker.Info.PhysicalLook.WorkerBody[0] = children[index];
                    }
                    else if (Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.LeftShift))
                    {
                        WorkerBodyPreview preview = MonoSingleton<WorkerImageRepository>.Instance.GetPrefab(workerExtraWindow.Worker.Info.GetPhysicalLookKey()).GetComponentInChildren<WorkerBodyPreview>();
                        if (preview.BodyParts.Count < 2)
                            return;
                        Transform transform = preview.BodyParts[1];
                        int index = 0;
                        List<string> children = new List<string>();
                        for (int i = 0; i < transform.childCount; i++)
                        {
                            if (transform.GetChild(i).name == workerExtraWindow.Worker.Info.PhysicalLook.WorkerBody[1])
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
                        workerExtraWindow.Worker.Info.PhysicalLook.WorkerBody[1] = children[index];
                    }
                    else if (Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.LeftAlt))
                    {
                        WorkerBodyPreview preview = MonoSingleton<WorkerImageRepository>.Instance.GetPrefab(workerExtraWindow.Worker.Info.GetPhysicalLookKey()).GetComponentInChildren<WorkerBodyPreview>();
                        if (preview.BodyParts.Count < 3)
                            return;
                        Transform transform = preview.BodyParts[2];
                        int index = 0;
                        List<string> children = new List<string>();
                        for (int i = 0; i < transform.childCount; i++)
                        {
                            if (transform.GetChild(i).name == workerExtraWindow.Worker.Info.PhysicalLook.WorkerBody[2])
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
                        workerExtraWindow.Worker.Info.PhysicalLook.WorkerBody[2] = children[index];
                    }
                    else if (Input.GetKey(KeyCode.LeftControl))
                    {
                        return;
                        tc.Field("creationID").SetValue(0);
                        if (workerExtraWindow.Worker.Info.Gender == Gender.Female)
                            ti.Field("gender").SetValue(Gender.Male);
                        else
                            ti.Field("gender").SetValue(Gender.Female);
                        ti.Field("height").SetValue(tg.Method("GetHeight", new object[] { workerExtraWindow.Worker.Info.Gender }).GetValue<float>());
                        ti.Field("weightCoefficient").SetValue(tg.Method("GetWeightCoefficient", new object[] { workerExtraWindow.Worker.Info.Height }).GetValue<float>());

                        workerExtraWindow.Worker.Info.SetIgnoredTypes(tg.Method("GetPhysicalIgnoreTypes", new object[] { new List<WorkerCharacteristicType>(), workerExtraWindow.Worker.Info.Gender, workerExtraWindow.Worker.Info.Height, workerExtraWindow.Worker.Info.WeightCoefficient }).GetValue<List<WorkerCharacteristicType>>());
                        workerExtraWindow.Worker.Info.SetPhysicalLook(tg.Method("GetPhysicalLook", new object[] { workerExtraWindow.Worker }).GetValue<WorkerPhysicalLook>());
                    }
                    else if (Input.GetKey(KeyCode.LeftAlt))
                    {
                        string color = workerExtraWindow.Worker.Info.PhysicalLook.BodyColors[workerExtraWindow.Worker.Info.PhysicalLook.ShaderParameters[0]];

                        int index = MonoSingleton<HumanAppearanceRepository>.Instance.GetByID("default").SkinColor.FindIndex(s => s == color);

                        if (Input.mouseScrollDelta.y < 0 && index > 0)
                        {
                            index--;
                        }
                        else if (Input.mouseScrollDelta.y > 0 && index < MonoSingleton<HumanAppearanceRepository>.Instance.GetByID("default").SkinColor.Count - 1)
                        {
                            index++;
                        }
                        else
                            return;
                        workerExtraWindow.Worker.Info.PhysicalLook.BodyColors[workerExtraWindow.Worker.Info.PhysicalLook.ShaderParameters[0]] = MonoSingleton<HumanAppearanceRepository>.Instance.GetByID("default").SkinColor[index];
                    }
                    else if (Input.GetKey(KeyCode.LeftShift))
                    {
                        string color = workerExtraWindow.Worker.Info.PhysicalLook.BodyColors[workerExtraWindow.Worker.Info.PhysicalLook.ShaderParameters[1]];

                        int index = MonoSingleton<HumanAppearanceRepository>.Instance.GetByID("default").HairColor.FindIndex(s => s == color);

                        if (Input.mouseScrollDelta.y < 0 && index > 0)
                        {
                            index--;
                        }
                        else if (Input.mouseScrollDelta.y > 0 && index < MonoSingleton<HumanAppearanceRepository>.Instance.GetByID("default").HairColor.Count - 1)
                        {
                            index++;
                        }
                        else
                            return;
                        workerExtraWindow.Worker.Info.PhysicalLook.BodyColors[workerExtraWindow.Worker.Info.PhysicalLook.ShaderParameters[1]] = MonoSingleton<HumanAppearanceRepository>.Instance.GetByID("default").HairColor[index];

                    }
                    else
                        workerExtraWindow.Worker.Info.SetPhysicalLook(tg.Method("GetPhysicalLook", new object[] { workerExtraWindow.Worker }).GetValue<WorkerPhysicalLook>());

                    MonoSingleton<WorkerImageController>.Instance.CreatingWorker(workerExtraWindow.Worker);

                    MonoSingleton<TaskController>.Instance.WaitForNextFrame().Then(delegate
                    {
                        MonoSingleton<WorkerManager>.Instance.GetView(workerExtraWindow.Worker).BodyPreview.Setup(workerExtraWindow.Worker);
                        AccessTools.Method(typeof(WorkerView), "TakeScreenshot").Invoke(MonoSingleton<WorkerManager>.Instance.GetView(workerExtraWindow.Worker), new object[] { });
                        workerExtraWindow.Worker.SetModified(SelectionPanelBlockType.WorkerStats, true);
                        MonoSingleton<WorkerManager>.Instance.GetView(workerExtraWindow.Worker).BodyPreview.ShowEntity();
                    });
                    return;
                }
                else if (rcr.gameObject.transform.parent?.name == "Background" && rcr.gameObject.transform.parent?.parent?.name == "Infos")
                {
                    int index;
                    if (Input.GetKey(KeyCode.LeftControl))
                    {
                        List<BackStory> backstories = (List<BackStory>)typeof(BackgroundRepositoryBase<BackStoryRepository, BackStory>).GetField("backgrounds", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(MonoSingleton<BackStoryRepository>.Instance);
                        index = backstories.FindIndex(b => b == workerExtraWindow.Worker.Info.BackStory);
                        if (Input.mouseScrollDelta.y < 0 && index > 0)
                        {
                            index--;
                        }
                        else if (Input.mouseScrollDelta.y > 0 && index < backstories.Count - 1)
                        {
                            index++;
                        }
                        workerExtraWindow.Worker.Info.SetBackStory(backstories[index]);
                    }
                    else
                    {
                        List<Background> backgrounds = (List<Background>)typeof(BackgroundRepositoryBase<BackgroundRepository, Background>).GetField("backgrounds", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(MonoSingleton<BackgroundRepository>.Instance);
                        index = backgrounds.FindIndex(b => b == workerExtraWindow.Worker.Info.Background);
                        if (Input.mouseScrollDelta.y < 0 && index > 0)
                        {
                            index--;
                        }
                        else if (Input.mouseScrollDelta.y > 0 && index < backgrounds.Count - 1)
                        {
                            index++;
                        }
                        workerExtraWindow.Worker.Info.SetBackground(backgrounds[index]);
                    }
                    workerExtraWindow.UpdateData();
                    return;
                }
                else if (rcr.gameObject.transform.parent?.parent?.name == "Perks" && rcr.gameObject.transform.name.StartsWith("CharacterPerk"))
                {
                    List<string> perkIds = tc.Field("perkIds").GetValue<List<string>>();
                    List<Perk> perks = tc.Field("perks").GetValue<List<Perk>>();
                    List<Perk> allPerks = MonoSingleton<PerkRepository>.Instance.GetAll().ToList<Perk>();

                    if (Input.GetKey(KeyCode.LeftControl))
                    {
                        if (Input.mouseScrollDelta.y < 0 && perks.Count > 1)
                        {
                            perkIds.RemoveAt(perkIds.Count - 1);
                            perks.RemoveAt(perks.Count - 1);
                        }
                        else if (Input.mouseScrollDelta.y > 0 && perks.Count < 10)
                        {
                            int idx = 0;
                            while (perkIds.Contains(allPerks[idx].Name))
                                idx++;
                            perkIds.Add(allPerks[idx].Name);
                            perks.Add(allPerks[idx]);
                        }
                    }
                    else
                    {
                        PerkTooltipView perkTooltipView = rcr.gameObject.GetComponent<PerkTooltipView>();
                        string perkId = Traverse.Create(perkTooltipView).Field("id").GetValue<string>();

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
                    }
                    var panel = t.Field("panels").GetValue<SelectionExtraPanelBase[]>()[t.Field("selectedPanel").GetValue<int>()];
                    AccessTools.Field(panel.GetType(), "currentWorker").SetValue(panel, null);
                    AccessTools.Method(panel.GetType(), "CreatePerks").Invoke(panel, new object[] { });
                    return;
                }
                else if (rcr.gameObject.transform.parent?.name == ("Religious") || rcr.gameObject.transform.parent?.parent?.name == ("Religious"))
                {
                    float align = workerExtraWindow.Worker.Info.ReligiousAlignment;
                    if (Input.mouseScrollDelta.y < 0 && workerExtraWindow.Worker.Info.ReligiousAlignment > 0)
                        align -= Input.GetKey(KeyCode.LeftControl) ? 0.1f : 0.01f;
                    else if (Input.mouseScrollDelta.y > 0 && workerExtraWindow.Worker.Info.ReligiousAlignment < 1)
                        align += Input.GetKey(KeyCode.LeftControl) ? 0.1f : 0.01f;

                    ti.Field("religiousAlignment").SetValue(Mathf.Clamp01(align));
                    workerExtraWindow.UpdateData();
                    return;
                }
                else if (rcr.gameObject.transform.parent?.name == "Age" && rcr.gameObject.transform.parent?.parent?.name == "Infos")
                {
                    int age = ti.Field("age").GetValue<int>();
                    if (Input.mouseScrollDelta.y < 0 && age > MonoSingleton<GenerationSettingsRepository>.Instance.Settings.AgeRange.Min)
                    {
                        ti.Field("age").SetValue(age - 1);
                    }
                    else if (Input.mouseScrollDelta.y > 0 && age < MonoSingleton<GenerationSettingsRepository>.Instance.Settings.AgeRange.Max)
                        ti.Field("age").SetValue(age + 1);
                    workerExtraWindow.UpdateData();
                    return;
                }
                else if (rcr.gameObject.transform.parent?.name == "Height" && rcr.gameObject.transform.parent?.parent?.name == "Infos")
                {
                    float height = ti.Field("height").GetValue<float>();
                    if (Input.mouseScrollDelta.y < 0 && height > MonoSingleton<GenerationSettingsRepository>.Instance.Settings.HeightRange.Min)
                    {
                        ti.Field("height").SetValue(height - 1);
                    }
                    else if (Input.mouseScrollDelta.y > 0 && height < MonoSingleton<GenerationSettingsRepository>.Instance.Settings.HeightRange.Max)
                        ti.Field("height").SetValue(height + 1);

                    MonoSingleton<WorkerImageController>.Instance.CreatingWorker(workerExtraWindow.Worker);

                    MonoSingleton<TaskController>.Instance.WaitForNextFrame().Then(delegate
                    {
                        MonoSingleton<WorkerManager>.Instance.GetView(workerExtraWindow.Worker).BodyPreview.Setup(workerExtraWindow.Worker);
                        AccessTools.Method(typeof(WorkerView), "TakeScreenshot").Invoke(MonoSingleton<WorkerManager>.Instance.GetView(workerExtraWindow.Worker), new object[] { });
                        workerExtraWindow.Worker.SetModified(SelectionPanelBlockType.WorkerStats, true);
                        MonoSingleton<WorkerManager>.Instance.GetView(workerExtraWindow.Worker).BodyPreview.ShowEntity();
                    });

                    workerExtraWindow.UpdateData();
                    return;
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
