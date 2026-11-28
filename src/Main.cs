using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityModManagerNet;
using UnityEngine;

namespace AdofaiEventStudio
{
    public static class Main
    {
        private const string DefaultLevelPath = @"D:\adofai\testlevel\level.adofai";

        private static UnityModManager.ModEntry _entry;
        private static EventProject _project;
        private static Font _uiFont;
        private static Vector2 _mainScroll;
        private static Vector2 _eventScroll;
        private static List<EventRecord> _records = new List<EventRecord>();
        private static string _status = "请载入一个 .adofai 文件。";
        private static bool _statusIsError;

        private static string _inputPath = DefaultLevelPath;
        private static string _outputPath = @"D:\adofai\testlevel\level_edited.adofai";
        private static string _minFloor = "0";
        private static string _maxFloor = "999999";
        private static string _eventType = "";
        private static string _shiftOffset = "0";
        private static string _fieldPath = "intensity";
        private static string _fieldValue = "50";
        private static string _cloneSourceFloor = "2";
        private static string _cloneTargetStart = "16";
        private static string _cloneTargetEnd = "64";
        private static string _cloneInterval = "16";

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            _entry = modEntry;
            SetCallback(modEntry, "OnToggle", "OnToggle");
            SetCallback(modEntry, "OnGUI", "OnGUI");
            SetCallback(modEntry, "OnSaveGUI", "OnSaveGUI");
            LoadSettings();
            EditorIntegration.Initialize(modEntry);
            if (modEntry.Logger != null)
                modEntry.Logger.Log("ADOFAI 事件工坊已加载。");
            return true;
        }

        private static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            EditorIntegration.SetEnabled(value);
            return true;
        }

        private static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            SaveSettings();
        }

        private static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            PrepareFont();
            _mainScroll = GUILayout.BeginScrollView(_mainScroll, GUILayout.ExpandHeight(true));

            DrawLanguageBar();
            GUILayout.Label(L("ADOFAI 事件工坊 1.0.1", "ADOFAI Event Studio 1.0.1"));
            GUILayout.Label(L("作者：橚", "Author: 橚"));
            GUILayout.Label(L(
                "进入关卡编辑器后，左侧页签可直接批量处理当前关卡。",
                "Open the level editor to batch-process the current level from the left tab."));
            int? nativeCount = EditorIntegration.GetCurrentMatchCount();
            if (nativeCount.HasValue)
                GUILayout.Label(L("编辑器内当前匹配事件: ", "Matching events in editor: ") + nativeCount.Value);
            DrawStatus();

            GUILayout.EndScrollView();
        }

        private static void DrawLanguageBar()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(L("语言", "Language"), GUILayout.Width(110));
            if (GUILayout.Button(L("跟随游戏", "Auto")))
            {
                Localization.SetMode(AppLanguage.Auto);
                SaveSettings();
            }
            if (GUILayout.Button(L("中文", "Chinese")))
            {
                Localization.SetMode(AppLanguage.Chinese);
                SaveSettings();
            }
            if (GUILayout.Button(L("English", "English")))
            {
                Localization.SetMode(AppLanguage.English);
                SaveSettings();
            }
            GUILayout.EndHorizontal();
        }

        private static void DrawStatus()
        {
            GUILayout.Space(5);
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.wordWrap = true;
            style.normal.textColor = new Color(0.72f, 0.9f, 0.72f);
            string message = EditorIntegration.StatusText;
            if (string.IsNullOrEmpty(message))
                message = L("请在关卡编辑器左侧打开“事件工坊”。", "Open Event Studio from the left side of the level editor.");
            GUILayout.Label(message, style);
        }

        private static void DrawFilter()
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label(L("事件筛选", "Filter events"));

            GUILayout.BeginHorizontal();
            GUILayout.Label(L("楼层", "Floor"), GUILayout.Width(36));
            _minFloor = GUILayout.TextField(_minFloor ?? "0", GUILayout.Width(80));
            GUILayout.Label(L("到", "to"), GUILayout.Width(20));
            _maxFloor = GUILayout.TextField(_maxFloor ?? "999999", GUILayout.Width(80));
            GUILayout.Label(L("事件类型", "Event type"), GUILayout.Width(64));
            _eventType = GUILayout.TextField(_eventType ?? string.Empty);
            GUILayout.EndHorizontal();

            string typeHint = _project == null
                ? L("载入关卡后显示", "Shown after loading a level")
                : string.Join(", ", _project.GetEventTypes()
                    .Select(x => Localization.EventName(x))
                    .ToArray());
            GUILayout.Label(L("事件类型留空表示全部。可填写: ", "Leave empty for all. Available: ") + typeHint);

            if (GUI.changed)
                RefreshMatches();

            GUILayout.Label(L("匹配事件数: ", "Matching events: ") + _records.Count);
            GUILayout.EndVertical();
        }

        private static void DrawBatchTools()
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label(L("批量修改匹配事件", "Batch edit matching events"));
            GUILayout.Label(L(
                "操作只修改内存中的事件；保存时输出到新文件。",
                "Operations change in-memory events; save writes to a new file."));

            GUILayout.BeginHorizontal();
            GUILayout.Label(L("楼层平移", "Shift floor"), GUILayout.Width(64));
            _shiftOffset = GUILayout.TextField(_shiftOffset ?? "0", GUILayout.Width(80));
            if (GUILayout.Button(L("应用", "Apply"), GUILayout.Width(70)))
                ApplyShift();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(L("字段路径", "Field path"), GUILayout.Width(64));
            _fieldPath = GUILayout.TextField(_fieldPath ?? string.Empty);
            GUILayout.EndHorizontal();
            GUILayout.Label(L(
                "支持点号路径，例如 filterProperties.filter_Radius 或 positionOffset.0。",
                "Supports dotted paths such as filterProperties.filter_Radius or positionOffset.0."));

            GUILayout.BeginHorizontal();
            GUILayout.Label(L("新值", "New value"), GUILayout.Width(64));
            _fieldValue = GUILayout.TextField(_fieldValue ?? string.Empty);
            if (GUILayout.Button(L("设为该值", "Set value"), GUILayout.Width(90)))
                ApplyFieldValue();
            GUILayout.EndHorizontal();
            GUILayout.Label(L(
                "数字、布尔值、数组和对象按 JSON 解析；其他内容作为字符串。",
                "Numbers, booleans, arrays and objects are parsed as JSON; other text becomes a string."));

            if (GUILayout.Button(L("删除所有匹配事件", "Delete all matching events"), GUILayout.Height(24)))
                DeleteMatched();

            GUILayout.EndVertical();
        }

        private static void DrawGenerationTools()
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label(L("按楼层间隔克隆事件", "Clone events by floor interval"));
            GUILayout.Label(L(
                "来源楼层按当前事件类型筛选；目标楼层会保留现有事件，不自动去重。",
                "Source floor is filtered by event type; destination floors keep existing events."));

            GUILayout.BeginHorizontal();
            GUILayout.Label(L("来源", "Source"), GUILayout.Width(36));
            _cloneSourceFloor = GUILayout.TextField(_cloneSourceFloor ?? "2", GUILayout.Width(62));
            GUILayout.Label(L("目标", "Target"), GUILayout.Width(36));
            _cloneTargetStart = GUILayout.TextField(_cloneTargetStart ?? "16", GUILayout.Width(62));
            GUILayout.Label(L("到", "to"), GUILayout.Width(20));
            _cloneTargetEnd = GUILayout.TextField(_cloneTargetEnd ?? "64", GUILayout.Width(62));
            GUILayout.Label(L("间隔", "Interval"), GUILayout.Width(36));
            _cloneInterval = GUILayout.TextField(_cloneInterval ?? "16", GUILayout.Width(62));
            GUILayout.EndHorizontal();

            if (GUILayout.Button(L("生成克隆事件", "Generate cloned events"), GUILayout.Height(24)))
                CloneEvents();
            GUILayout.EndVertical();
        }

        private static void DrawOutputTools()
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label(L("输出文件", "Output file"));
            _outputPath = GUILayout.TextField(_outputPath ?? string.Empty);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(L("另存为新 .adofai", "Save as new .adofai"), GUILayout.Height(26)))
                SaveProject();

            GUI.enabled = GUI.enabled && _project != null && _project.HasUndo;
            string undoText = _project != null && _project.HasUndo
                ? L("撤销: ", "Undo: ") + _project.UndoLabel
                : L("没有可撤销操作", "Nothing to undo");
            if (GUILayout.Button(undoText, GUILayout.Height(26)))
                UndoLastOperation();
            GUI.enabled = _project != null;
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private static void DrawEventPreview()
        {
            GUILayout.Label(L("匹配事件预览", "Matching event preview"));
            if (_project == null)
            {
                GUILayout.Label(L("尚未载入关卡。", "No level loaded."));
                return;
            }

            _eventScroll = GUILayout.BeginScrollView(
                _eventScroll,
                GUILayout.Height(220),
                GUILayout.ExpandWidth(true));

            int limit = Math.Min(_records.Count, 200);
            for (int i = 0; i < limit; i++)
            {
                EventRecord record = _records[i];
                GUILayout.Label(
                    "[" + record.Index + "]  floor=" + record.Floor + "  " +
                    Localization.EventName(record.EventType) + "  " + record.Summary);
            }

            if (_records.Count > limit)
                GUILayout.Label(L("仅显示前 ", "Showing first ") + limit + L(" 个事件。", " events."));
            if (_records.Count == 0)
                GUILayout.Label(L("没有事件符合当前筛选条件。", "No events match the current filter."));

            GUILayout.EndScrollView();
        }

        private static void LoadProject()
        {
            try
            {
                EventProject project = new EventProject();
                project.Load(CleanPath(_inputPath));
                _project = project;

                if (string.IsNullOrEmpty(_outputPath) ||
                    string.Equals(
                        Path.GetFullPath(CleanPath(_outputPath)),
                        project.SourcePath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    string directory = Path.GetDirectoryName(project.SourcePath);
                    string fileName = Path.GetFileNameWithoutExtension(project.SourcePath);
                    _outputPath = Path.Combine(directory, fileName + "_edited.adofai");
                }

                SetStatus("关卡载入成功。", false);
                RefreshMatches();
                SaveSettings();
            }
            catch (Exception exception)
            {
                _project = null;
                _records.Clear();
                SetStatus("载入失败: " + exception.Message, true);
                LogException(exception);
            }
        }

        private static void ApplyShift()
        {
            try
            {
                int offset = ParseRequiredInt(_shiftOffset, "楼层平移值");
                int min;
                int max;
                GetRange(out min, out max);
                string type = GetFilteredEventType();
                int changed = _project.ShiftFloors(min, max, type, offset);
                RefreshMatches();
                SetStatus("已平移 " + changed + " 个事件。", false);
            }
            catch (Exception exception)
            {
                SetStatus("平移失败: " + exception.Message, true);
                LogException(exception);
            }
        }

        private static void ApplyFieldValue()
        {
            try
            {
                int min;
                int max;
                GetRange(out min, out max);
                string type = GetFilteredEventType();
                int changed = _project.SetField(min, max, type, _fieldPath, _fieldValue);
                RefreshMatches();
                SetStatus("已修改 " + changed + " 个事件的字段 " + _fieldPath + "。", false);
            }
            catch (Exception exception)
            {
                SetStatus("修改字段失败: " + exception.Message, true);
                LogException(exception);
            }
        }

        private static void DeleteMatched()
        {
            try
            {
                int min;
                int max;
                GetRange(out min, out max);
                string type = GetFilteredEventType();
                int changed = _project.DeleteEvents(min, max, type);
                RefreshMatches();
                SetStatus("已删除 " + changed + " 个匹配事件。", false);
            }
            catch (Exception exception)
            {
                SetStatus("删除失败: " + exception.Message, true);
                LogException(exception);
            }
        }

        private static void CloneEvents()
        {
            try
            {
                int source = ParseRequiredInt(_cloneSourceFloor, "来源楼层");
                int start = ParseRequiredInt(_cloneTargetStart, "目标起始楼层");
                int end = ParseRequiredInt(_cloneTargetEnd, "目标结束楼层");
                int interval = ParseRequiredInt(_cloneInterval, "生成间隔");
                string type = GetFilteredEventType();
                int created = _project.CloneAtInterval(source, start, end, interval, type);
                RefreshMatches();
                SetStatus("已生成 " + created + " 个克隆事件。", false);
            }
            catch (Exception exception)
            {
                SetStatus("生成失败: " + exception.Message, true);
                LogException(exception);
            }
        }

        private static void SaveProject()
        {
            try
            {
                _project.Save(CleanPath(_outputPath));
                SetStatus("已保存到: " + _outputPath, false);
                SaveSettings();
            }
            catch (Exception exception)
            {
                SetStatus("保存失败: " + exception.Message, true);
                LogException(exception);
            }
        }

        private static void UndoLastOperation()
        {
            try
            {
                string label = _project.UndoLabel;
                if (!_project.Undo())
                {
                    SetStatus("没有可撤销操作。", true);
                    return;
                }

                _inputPath = _project.SourcePath;
                RefreshMatches();
                SetStatus("已撤销: " + label, false);
            }
            catch (Exception exception)
            {
                SetStatus("撤销失败: " + exception.Message, true);
                LogException(exception);
            }
        }

        private static void RefreshMatches()
        {
            _records.Clear();
            if (_project == null)
                return;

            try
            {
                int min;
                int max;
                GetRange(out min, out max);
                _records = _project.Query(min, max, GetFilteredEventType());
            }
            catch (Exception exception)
            {
                SetStatus("筛选条件无效: " + exception.Message, true);
            }
        }

        private static void GetRange(out int min, out int max)
        {
            min = ParseRequiredInt(string.IsNullOrEmpty(_minFloor) ? "0" : _minFloor, "最小楼层");
            max = ParseRequiredInt(string.IsNullOrEmpty(_maxFloor) ? "999999" : _maxFloor, "最大楼层");
            if (min > max)
                throw new ArgumentException("最小楼层不能大于最大楼层。");
        }

        private static string GetFilteredEventType()
        {
            if (string.IsNullOrEmpty(_eventType))
                return string.Empty;
            return _eventType.Trim();
        }

        private static int ParseRequiredInt(string value, string label)
        {
            int result;
            if (!int.TryParse(value, out result))
                throw new ArgumentException(label + "必须是整数。");
            return result;
        }

        private static string CleanPath(string path)
        {
            if (path == null)
                return string.Empty;
            return path.Trim().Trim('"');
        }

        private static string L(string chinese, string english)
        {
            return Localization.T(chinese, english);
        }

        private static void SetStatus(string message, bool isError)
        {
            _status = message;
            _statusIsError = isError;
        }

        private static void LogException(Exception exception)
        {
            if (_entry != null && _entry.Logger != null)
                _entry.Logger.Log(exception.ToString());
        }

        private static void SetCallback(
            UnityModManager.ModEntry modEntry,
            string fieldName,
            string methodName)
        {
            FieldInfo field = typeof(UnityModManager.ModEntry).GetField(
                fieldName,
                BindingFlags.Public | BindingFlags.Instance);
            MethodInfo method = typeof(Main).GetMethod(
                methodName,
                BindingFlags.NonPublic | BindingFlags.Static);

            if (field == null || method == null)
                throw new MissingMemberException("无法绑定 UMM 回调: " + fieldName);

            Delegate callback = Delegate.CreateDelegate(field.FieldType, method);
            field.SetValue(modEntry, callback);
        }

        private static void PrepareFont()
        {
            if (_uiFont == null)
            {
                try
                {
                    _uiFont = Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 16);
                }
                catch
                {
                    _uiFont = null;
                }
            }

            if (_uiFont != null)
                GUI.skin.font = _uiFont;
        }

        private static void LoadSettings()
        {
            try
            {
                if (_entry == null || string.IsNullOrEmpty(_entry.Path))
                    return;

                string path = Path.Combine(_entry.Path, "settings.json");
                if (!File.Exists(path))
                    return;

                JObject settings = JObject.Parse(File.ReadAllText(path));
                _inputPath = GetString(settings, "inputPath", _inputPath);
                _outputPath = GetString(settings, "outputPath", _outputPath);
                _minFloor = GetString(settings, "minFloor", _minFloor);
                _maxFloor = GetString(settings, "maxFloor", _maxFloor);
                _eventType = GetString(settings, "eventType", _eventType);
                string language = GetString(settings, "language", "Auto");
                Localization.SetMode(ParseLanguage(language));
            }
            catch (Exception exception)
            {
                LogException(exception);
            }
        }

        private static void SaveSettings()
        {
            try
            {
                if (_entry == null || string.IsNullOrEmpty(_entry.Path))
                    return;

                JObject settings = new JObject();
                settings["inputPath"] = _inputPath;
                settings["outputPath"] = _outputPath;
                settings["minFloor"] = _minFloor;
                settings["maxFloor"] = _maxFloor;
                settings["eventType"] = _eventType;
                settings["language"] = Localization.Mode.ToString();
                File.WriteAllText(
                    Path.Combine(_entry.Path, "settings.json"),
                    settings.ToString());
            }
            catch (Exception exception)
            {
                LogException(exception);
            }
        }

        private static string GetString(JObject settings, string name, string fallback)
        {
            JToken token = settings[name];
            if (token == null || token.Type == JTokenType.Null)
                return fallback;
            return token.Value<string>();
        }

        private static AppLanguage ParseLanguage(string value)
        {
            if (string.Equals(value, "Chinese", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "zh", StringComparison.OrdinalIgnoreCase))
            {
                return AppLanguage.Chinese;
            }
            if (string.Equals(value, "English", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "en", StringComparison.OrdinalIgnoreCase))
            {
                return AppLanguage.English;
            }
            return AppLanguage.Auto;
        }
    }
}
