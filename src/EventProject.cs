using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AdofaiEventStudio
{
    public sealed class EventRecord
    {
        public int Index { get; private set; }
        public int Floor { get; private set; }
        public string EventType { get; private set; }
        public JObject Action { get; private set; }
        public string Summary { get; private set; }

        internal EventRecord(int index, int floor, string eventType, JObject action)
        {
            Index = index;
            Floor = floor;
            EventType = eventType;
            Action = action;
            Summary = BuildSummary(action);
        }

        private static string BuildSummary(JObject action)
        {
            List<string> parts = new List<string>();
            foreach (JProperty property in action.Properties())
            {
                if (property.Name == "floor" || property.Name == "eventType")
                    continue;

                if (parts.Count >= 6)
                    break;

                string value = property.Value.ToString(Formatting.None);
                if (value.Length > 34)
                    value = value.Substring(0, 31) + "...";
                parts.Add(property.Name + "=" + value);
            }
            return string.Join("  ", parts.ToArray());
        }
    }

    public sealed class EventProject
    {
        private JObject _root;
        private JArray _actions;
        private string _undoSnapshot;
        private string _undoLabel;

        public string SourcePath { get; private set; }
        public int SettingsVersion { get; private set; }
        public int AngleTileCount { get; private set; }
        public bool HasUndo { get { return _undoSnapshot != null; } }
        public string UndoLabel { get { return _undoLabel; } }
        public int ActionCount { get { return _actions == null ? 0 : _actions.Count; } }

        public void Load(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("关卡文件路径为空。");

            string fullPath = Path.GetFullPath(path);
            string json = File.ReadAllText(fullPath);
            JObject root = JObject.Parse(json);
            JToken actionsToken = root["actions"];

            if (actionsToken != null && !(actionsToken is JArray))
                throw new FormatException("关卡中的 actions 不是数组。");

            JArray actions = actionsToken as JArray;
            if (actions == null)
            {
                actions = new JArray();
                root["actions"] = actions;
            }

            JObject settings = root["settings"] as JObject;
            JToken versionToken = settings == null ? null : settings["version"];
            int version = versionToken == null ? 0 : versionToken.Value<int>();

            JArray angles = root["angleData"] as JArray;

            SourcePath = fullPath;
            _root = root;
            _actions = actions;
            SettingsVersion = version;
            AngleTileCount = angles == null ? 0 : angles.Count;
            _undoSnapshot = null;
            _undoLabel = null;
        }

        public List<string> GetEventTypes()
        {
            List<string> types = new List<string>();
            if (_actions == null)
                return types;

            foreach (JObject action in _actions.OfType<JObject>())
            {
                string eventType = GetEventType(action);
                if (!string.IsNullOrEmpty(eventType) &&
                    !types.Any(x => string.Equals(x, eventType, StringComparison.OrdinalIgnoreCase)))
                {
                    types.Add(eventType);
                }
            }

            types.Sort(StringComparer.OrdinalIgnoreCase);
            return types;
        }

        public List<EventRecord> Query(int minFloor, int maxFloor, string eventType)
        {
            List<EventRecord> records = new List<EventRecord>();
            if (_actions == null)
                return records;

            string normalizedType = NormalizeEventType(eventType);
            List<JObject> actions = _actions.OfType<JObject>().ToList();

            for (int i = 0; i < actions.Count; i++)
            {
                JObject action = actions[i];
                int floor;
                if (!TryGetFloor(action, out floor))
                    continue;

                if (floor < minFloor || floor > maxFloor)
                    continue;

                string actionType = GetEventType(action);
                if (normalizedType.Length > 0 &&
                    !string.Equals(actionType, normalizedType, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                int actualIndex = _actions.IndexOf(action);
                records.Add(new EventRecord(actualIndex, floor, actionType, action));
            }

            return records;
        }

        public int ShiftFloors(int minFloor, int maxFloor, string eventType, int offset)
        {
            return WithUndo("平移楼层", delegate
            {
                int changed = 0;
                List<EventRecord> records = Query(minFloor, maxFloor, eventType);
                foreach (EventRecord record in records)
                {
                    int targetFloor = record.Floor + offset;
                    if (targetFloor < 0)
                        throw new InvalidOperationException("平移后出现负楼层。");

                    record.Action["floor"] = targetFloor;
                    changed++;
                }
                return changed;
            });
        }

        public int SetField(int minFloor, int maxFloor, string eventType, string fieldPath, string inputValue)
        {
            if (string.IsNullOrEmpty(fieldPath))
                throw new ArgumentException("字段路径为空。");

            JToken value = ParseInputValue(inputValue);
            string[] segments = fieldPath.Split('.');

            return WithUndo("批量修改字段", delegate
            {
                int changed = 0;
                List<EventRecord> records = Query(minFloor, maxFloor, eventType);
                foreach (EventRecord record in records)
                {
                    SetPath(record.Action, segments, value);
                    changed++;
                }
                return changed;
            });
        }

        public int DeleteEvents(int minFloor, int maxFloor, string eventType)
        {
            return WithUndo("删除事件", delegate
            {
                List<EventRecord> records = Query(minFloor, maxFloor, eventType);
                List<int> indexes = records.Select(x => x.Index).OrderByDescending(x => x).ToList();
                foreach (int index in indexes)
                    _actions.RemoveAt(index);
                return indexes.Count;
            });
        }

        public int CloneAtInterval(
            int sourceFloor,
            int targetStart,
            int targetEnd,
            int interval,
            string eventType)
        {
            if (interval <= 0)
                throw new ArgumentException("生成间隔必须大于 0。");
            if (targetStart < 0 || targetEnd < 0)
                throw new ArgumentException("目标楼层不能为负数。");
            if (targetStart > targetEnd)
                throw new ArgumentException("目标起始楼层不能大于结束楼层。");

            return WithUndo("按间隔克隆事件", delegate
            {
                List<EventRecord> records = Query(sourceFloor, sourceFloor, eventType);
                List<JObject> templates = records
                    .Select(x => (JObject)x.Action.DeepClone())
                    .ToList();

                if (templates.Count == 0)
                    return 0;

                int created = 0;
                for (long destination = targetStart; destination <= targetEnd; destination += interval)
                {
                    if (destination == sourceFloor)
                        continue;

                    foreach (JObject template in templates)
                    {
                        JObject clone = (JObject)template.DeepClone();
                        clone["floor"] = (int)destination;
                        _actions.Add(clone);
                        created++;
                    }
                }
                return created;
            });
        }

        public bool Undo()
        {
            if (_undoSnapshot == null)
                return false;

            string snapshot = _undoSnapshot;
            _undoSnapshot = null;
            _undoLabel = null;
            RestoreSnapshot(snapshot);
            return true;
        }

        public void Save(string outputPath)
        {
            if (_root == null)
                throw new InvalidOperationException("尚未载入关卡。");
            if (string.IsNullOrEmpty(outputPath))
                throw new ArgumentException("输出路径为空。");

            string fullOutput = Path.GetFullPath(outputPath);
            if (string.Equals(fullOutput, SourcePath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("输出路径不能与源关卡相同。请另存为新文件。");

            string directory = Path.GetDirectoryName(fullOutput);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string temporary = fullOutput + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                string json = _root.ToString(Formatting.Indented);
                File.WriteAllText(temporary, json, new UTF8Encoding(false));

                if (File.Exists(fullOutput))
                    File.Replace(temporary, fullOutput, null);
                else
                    File.Move(temporary, fullOutput);
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }

        private int WithUndo(string label, Func<int> operation)
        {
            if (_root == null)
                throw new InvalidOperationException("尚未载入关卡。");

            string before = _root.ToString(Formatting.None);
            int changed;
            try
            {
                changed = operation();
            }
            catch
            {
                RestoreSnapshot(before);
                throw;
            }

            if (changed > 0)
            {
                _undoSnapshot = before;
                _undoLabel = label;
            }
            return changed;
        }

        private void RestoreSnapshot(string snapshot)
        {
            JObject restored = JObject.Parse(snapshot);
            _root = restored;
            _actions = restored["actions"] as JArray;
            if (_actions == null)
            {
                _actions = new JArray();
                _root["actions"] = _actions;
            }
        }

        private static JToken ParseInputValue(string input)
        {
            if (input == null)
                return JValue.CreateNull();

            string trimmed = input.Trim();
            if (trimmed.Length == 0)
                return new JValue(string.Empty);

            try
            {
                return JToken.Parse(trimmed);
            }
            catch (JsonReaderException)
            {
                return new JValue(input);
            }
        }

        private static void SetPath(JObject action, string[] segments, JToken value)
        {
            if (segments.Length == 0)
                throw new ArgumentException("字段路径为空。");

            JToken current = action;
            for (int i = 0; i < segments.Length; i++)
            {
                string segment = segments[i];
                bool last = i == segments.Length - 1;

                JObject currentObject = current as JObject;
                if (currentObject != null)
                {
                    if (last)
                    {
                        currentObject[segment] = value.DeepClone();
                        return;
                    }

                    JToken next = currentObject[segment];
                    if (next == null || next.Type == JTokenType.Null)
                    {
                        next = new JObject();
                        currentObject[segment] = next;
                    }
                    current = next;
                    continue;
                }

                JArray currentArray = current as JArray;
                if (currentArray != null)
                {
                    int index;
                    if (!int.TryParse(segment, out index) || index < 0)
                        throw new ArgumentException("数组字段索引无效: " + segment);

                    if (index >= currentArray.Count)
                        throw new ArgumentException("数组字段索引越界: " + segment);

                    if (last)
                    {
                        currentArray[index] = value.DeepClone();
                        return;
                    }

                    JToken next = currentArray[index];
                    if (next == null || next.Type == JTokenType.Null)
                        throw new ArgumentException("不能继续修改 null 数组元素: " + segment);
                    current = next;
                    continue;
                }

                throw new ArgumentException("字段路径包含非对象或非数组节点: " + segment);
            }
        }

        private static int GetFloor(JObject action)
        {
            int floor;
            if (!TryGetFloor(action, out floor))
                throw new FormatException("事件缺少合法 floor。");
            return floor;
        }

        private static bool TryGetFloor(JObject action, out int floor)
        {
            JToken token = action["floor"];
            if (token == null)
            {
                floor = 0;
                return false;
            }

            try
            {
                floor = Convert.ToInt32(token.Value<double>());
                return true;
            }
            catch
            {
                floor = 0;
                return false;
            }
        }

        private static string GetEventType(JObject action)
        {
            JToken token = action["eventType"];
            return token == null ? string.Empty : token.Value<string>();
        }

        private static string NormalizeEventType(string eventType)
        {
            if (string.IsNullOrEmpty(eventType))
                return string.Empty;

            string trimmed = eventType.Trim();
            if (string.Equals(trimmed, "全部", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, "All", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }
            return trimmed;
        }
    }
}
