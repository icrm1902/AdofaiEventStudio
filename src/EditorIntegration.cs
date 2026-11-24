using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ADOFAI;
using ADOFAI.LevelEditor.Controls;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Events;
using UnityModManagerNet;

namespace AdofaiEventStudio
{
    internal static class EditorIntegration
    {
        private const int StudioTypeValue = 850;
        private const string StudioName = "AdofaiEventStudio";

        private static readonly LevelEventType StudioType = (LevelEventType)StudioTypeValue;
        private static readonly FieldInfo ShowingPanelField =
            AccessTools.Field(typeof(InspectorPanel), "showingPanel");

        private static UnityModManager.ModEntry _entry;
        private static LevelEventInfo _studioInfo;
        private static LevelEvent _settingsEvent;
        private static PropertiesPanel _studioPanel;
        private static bool _initialized;

        public static bool Enabled { get; private set; }

        public static string StatusText { get; private set; }

        public static void Initialize(UnityModManager.ModEntry entry)
        {
            if (_initialized)
                return;

            _entry = entry;
            StatusText = "进入关卡编辑器后，点击左侧“事件工坊”页签。";
            string harmonyId = "AdofaiEventStudio";
            if (entry.Info != null && !string.IsNullOrEmpty(entry.Info.Id))
                harmonyId = entry.Info.Id;
            Harmony harmony = new Harmony(harmonyId);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            Enabled = true;
            _initialized = true;
            Log("编辑器左栏补丁已启用。");
        }

        public static void SetEnabled(bool enabled)
        {
            Enabled = enabled;
        }

        public static int? GetCurrentMatchCount()
        {
            scnEditor editor = scnEditor.instance;
            if (!Enabled || editor == null || _settingsEvent == null)
                return null;
            try
            {
                return EditorEventTools.GetMatchCount(editor, _settingsEvent);
            }
            catch
            {
                return null;
            }
        }

        internal static int GetStudioPropertyCountForTest()
        {
            LevelEventInfo info = BuildStudioInfo();
            return info.propertiesInfo.Count;
        }

        internal static void ReportStatus(string message)
        {
            StatusText = message;
            Log(message);
            scnEditor editor = scnEditor.instance;
            if (editor != null && editor.settingsPanel != null &&
                editor.settingsPanel.title != null)
            {
                editor.settingsPanel.title.text = "ADOFAI 事件工坊 · " + message;
            }
        }

        internal static void ReportDiagnostic(string message)
        {
            Log("[诊断] " + message);
        }

        internal static void ReportException(Exception exception)
        {
            Log("[异常] " + exception);
        }

        private static void EnsureStudioInfo(Dictionary<string, LevelEventInfo> infos)
        {
            if (infos == null)
                return;

            if (_studioInfo == null)
                _studioInfo = BuildStudioInfo();

            string key = StudioTypeValue.ToString();
            if (!infos.ContainsKey(key))
                infos.Add(key, _studioInfo);

            if (EditorConstants.settingsTypes != null)
                EditorConstants.settingsTypes.Add(StudioType);

            if (GCS.levelEventTypeString == null)
                GCS.levelEventTypeString = new Dictionary<LevelEventType, string>();
            GCS.levelEventTypeString[StudioType] = StudioName;

            if (GCS.levelEventIcons == null)
                GCS.levelEventIcons = new Dictionary<LevelEventType, Sprite>();

            if (!GCS.levelEventIcons.ContainsKey(StudioType))
            {
                Sprite icon = GCS.levelEventIcons.Values.FirstOrDefault();
                if (icon == null)
                {
                    icon = Resources.Load<Sprite>("LevelEditor/LevelEvents/SetSpeed");
                    if (icon == null)
                        icon = RDConstants.data.sprIconSameSpeed;
                }
                GCS.levelEventIcons.Add(StudioType, icon);
            }
        }

        private static LevelEventInfo BuildStudioInfo()
        {
            LevelEventInfo info = new LevelEventInfo();
            info.name = StudioName;
            info.type = StudioType;
            info.executionTime = (LevelEventExecutionTime)2;
            info.propertiesInfo = new Dictionary<string, ADOFAI.PropertyInfo>();
            info.categories = new List<LevelEventCategory>();
            info.stretchViewport = false;
            return info;
        }

        private static void AddInt(
            LevelEventInfo info,
            string name,
            string label,
            int defaultValue,
            int min,
            int max)
        {
            Dictionary<string, object> property = new Dictionary<string, object>();
            property["name"] = name;
            property["key"] = name;
            property["type"] = "Int";
            property["default"] = defaultValue;
            property["customLabel"] = label;
            property["min"] = min;
            property["max"] = max;
            info.propertiesInfo.Add(name, new ADOFAI.PropertyInfo(property, info));
        }

        private static void AddString(
            LevelEventInfo info,
            string name,
            string label,
            string defaultValue)
        {
            Dictionary<string, object> property = new Dictionary<string, object>();
            property["name"] = name;
            property["key"] = name;
            property["type"] = "String";
            property["default"] = defaultValue;
            property["customLabel"] = label;
            info.propertiesInfo.Add(name, new ADOFAI.PropertyInfo(property, info));
        }

        private static void AddButton(
            LevelEventInfo info,
            string name,
            string label,
            UnityAction action)
        {
            Dictionary<string, object> property = new Dictionary<string, object>();
            property["name"] = name;
            property["key"] = name;
            property["type"] = "Export";
            property["default"] = action;
            property["customLabel"] = label;
            ADOFAI.PropertyInfo propertyInfo = new ADOFAI.PropertyInfo(property, info);
            propertyInfo.value_default = action;
            info.propertiesInfo.Add(name, propertyInfo);
        }

        private static void ApplyShift()
        {
            ExecuteOperation("平移楼层", delegate(scnEditor editor, LevelEvent settings)
            {
                return EditorEventTools.ShiftFloors(editor, settings);
            });
        }

        private static void ApplySetField()
        {
            ExecuteOperation("修改字段", delegate(scnEditor editor, LevelEvent settings)
            {
                return EditorEventTools.SetField(editor, settings);
            });
        }

        private static void ApplyDelete()
        {
            ExecuteOperation("删除事件", delegate(scnEditor editor, LevelEvent settings)
            {
                return EditorEventTools.DeleteEvents(editor, settings);
            });
        }

        private static void ApplyClone()
        {
            ExecuteOperation("生成事件", delegate(scnEditor editor, LevelEvent settings)
            {
                return EditorEventTools.CloneAtInterval(editor, settings);
            });
        }

        private static void ExecuteOperation(
            string label,
            Func<scnEditor, LevelEvent, EditorEventResult> operation)
        {
            try
            {
                if (!Enabled)
                    throw new InvalidOperationException("事件工坊当前已停用。");
                scnEditor editor = scnEditor.instance;
                if (editor == null || _settingsEvent == null)
                    throw new InvalidOperationException("当前不在关卡编辑器中。");

                EditorEventResult result = operation(editor, _settingsEvent);
                StatusText = label + "完成：" + result.Changed + " 个事件，当前匹配 " +
                    result.MatchCount + " 个。";
                Log(StatusText);
                RefreshTitle(" | 完成 " + result.Changed);
            }
            catch (Exception exception)
            {
                StatusText = label + "失败: " + exception.Message;
                Log(StatusText);
                Log(exception.ToString());
                RefreshTitle(" | 操作失败");
            }
        }

        private static void RefreshTitle(string suffix)
        {
            scnEditor editor = scnEditor.instance;
            if (editor == null || editor.settingsPanel == null ||
                editor.settingsPanel.title == null)
            {
                return;
            }

            int? count = GetCurrentMatchCount();
            editor.settingsPanel.title.text =
                "ADOFAI 事件工坊" + (count.HasValue ? " · 匹配 " + count.Value : string.Empty) + suffix;
        }

        private static PropertiesPanel FindStudioPanel(InspectorPanel inspector)
        {
            if (inspector == null || inspector.panelsList == null)
                return null;
            return inspector.panelsList.FirstOrDefault(
                panel => panel != null && panel.levelEventType == StudioType);
        }

        private static void Log(string message)
        {
            if (_entry != null && _entry.Logger != null)
                _entry.Logger.Log(message);
        }

        [HarmonyPatch(typeof(InspectorPanel), "Init")]
        private static class InspectorPanelInitPatch
        {
            [HarmonyPrefix]
            private static void Prefix(Dictionary<string, LevelEventInfo> levelEventsInfo)
            {
                if (!Enabled || levelEventsInfo == null || GCS.settingsInfo == null ||
                    !object.ReferenceEquals(levelEventsInfo, GCS.settingsInfo))
                {
                    return;
                }
                try
                {
                    EnsureStudioInfo(levelEventsInfo);
                }
                catch (Exception exception)
                {
                    Log("事件工坊页签注册失败: " + exception);
                }
            }

            [HarmonyPostfix]
            private static void Postfix(InspectorPanel __instance)
            {
                if (!Enabled || _studioInfo == null || scnEditor.instance == null ||
                    __instance != scnEditor.instance.settingsPanel)
                {
                    return;
                }

                _studioPanel = FindStudioPanel(__instance);
                if (_studioPanel != null)
                {
                    try
                    {
                        _settingsEvent = new LevelEvent(0, StudioType, _studioInfo);
                        _studioPanel.gameObject.SetActive(false);
                        Log("编辑器左栏“事件工坊”页签已创建。");
                    }
                    catch (Exception exception)
                    {
                        Log("事件工坊设置对象创建失败: " + exception);
                    }
                }
                else
                {
                    Log("编辑器左栏页签创建失败：找不到自定义属性面板。");
                }
            }
        }

        [HarmonyPatch(typeof(InspectorPanel), "ShowPanel")]
        private static class InspectorPanelShowPanelPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(
                InspectorPanel __instance,
                LevelEventType eventType,
                int eventIndex)
            {
                if (!Enabled || eventType != StudioType ||
                    scnEditor.instance == null ||
                    __instance != scnEditor.instance.settingsPanel)
                {
                    return true;
                }

                PropertiesPanel panel = FindStudioPanel(__instance);
                if (panel == null || _settingsEvent == null)
                {
                    StatusText = "事件工坊面板尚未初始化，请重新进入编辑器。";
                    return false;
                }

                if (ShowingPanelField != null)
                    ShowingPanelField.SetValue(__instance, true);
                try
                {
                    foreach (PropertiesPanel other in __instance.panelsList)
                    {
                        if (other != null)
                            other.gameObject.SetActive(other == panel);
                    }

                    _studioPanel = panel;
                    panel.gameObject.SetActive(true);
                    __instance.selectedEvent = _settingsEvent;
                    __instance.selectedEventType = StudioType;
                    __instance.titleCanvas.SetActive(true);
                    panel.SetProperties(_settingsEvent, true);
                    EditorPanelUI.Show(panel, scnEditor.instance);

                    foreach (Transform child in __instance.tabs)
                    {
                        InspectorTab tab = child.GetComponent<InspectorTab>();
                        if (tab != null)
                            tab.SetSelected(tab.levelEventType == StudioType);
                    }

                    __instance.title.text = "ADOFAI 事件工坊";
                    Log("编辑器左栏“事件工坊”页签已打开。");
                }
                catch (Exception exception)
                {
                    StatusText = "打开事件工坊失败: " + exception.Message;
                    Log(exception.ToString());
                }
                finally
                {
                    if (ShowingPanelField != null)
                        ShowingPanelField.SetValue(__instance, false);
                }
                return false;
            }
        }

        [HarmonyPatch(typeof(EditorConstants), "IsSetting")]
        private static class EditorConstantsIsSettingPatch
        {
            [HarmonyPostfix]
            private static void Postfix(LevelEventType type, ref bool __result)
            {
                if (Enabled && type == StudioType)
                    __result = true;
            }
        }

        [HarmonyPatch(typeof(PropertiesPanel), "RenderControl")]
        private static class PropertiesPanelRenderControlPatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(
                PropertiesPanel __instance,
                string propertyKey,
                ADOFAI.PropertyInfo propertyInfo)
            {
                if (!Enabled || propertyInfo == null ||
                    propertyInfo.levelEventInfo != _studioInfo ||
                    propertyInfo.type != PropertyType.Export)
                {
                    return true;
                }

                try
                {
                    GameObject propertyObject = UnityEngine.Object.Instantiate(
                        RDConstants.data.prefab_property);
                    propertyObject.name = propertyKey;
                    propertyObject.transform.SetParent(__instance.content, false);

                    ADOFAI.Property property = propertyObject.GetComponent<ADOFAI.Property>();
                    property.key = propertyKey;
                    property.info = propertyInfo;
                    if (property.label != null)
                        property.label.text = propertyInfo.customLabel;

                    GameObject controlObject = UnityEngine.Object.Instantiate(
                        RDConstants.data.prefab_controlExport);
                    controlObject.transform.SetParent(property.controlContainer, false);
                    PropertyControl_Export control =
                        controlObject.GetComponent<PropertyControl_Export>();
                    control.propertyInfo = propertyInfo;
                    control.propertiesPanel = __instance;
                    control.propertyTransform = control.GetComponent<RectTransform>();
                    control.Setup(true);

                    UnityAction action = propertyInfo.value_default as UnityAction;
                    if (action == null)
                        throw new InvalidOperationException("按钮缺少 UnityAction: " + propertyKey);
                    control.exportButton.onClick.RemoveAllListeners();
                    control.exportButton.onClick.AddListener(action);
                    if (control.buttonText != null)
                        control.buttonText.text = propertyInfo.customLabel;

                    property.control = control;
                    __instance.properties.Add(propertyInfo.name, property);
                    return false;
                }
                catch (Exception exception)
                {
                    Log("按钮控件渲染失败，改用游戏默认渲染: " + exception);
                    return true;
                }
            }
        }
    }
}
