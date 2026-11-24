using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using ADOFAI;
using UnityEngine;

namespace AdofaiEventStudio
{
    internal sealed class EditorEventResult
    {
        public int Changed { get; private set; }
        public int MatchCount { get; private set; }

        internal EditorEventResult(int changed, int matchCount)
        {
            Changed = changed;
            MatchCount = matchCount;
        }
    }

    internal static class EditorEventTools
    {
        public static int GetMatchCount(scnEditor editor, LevelEvent settings)
        {
            return FindMatches(editor, settings).Count;
        }

        public static EditorEventResult ShiftFloors(scnEditor editor, LevelEvent settings)
        {
            int offset = ReadInt(settings, "shiftAmount", 1);
            List<LevelEvent> matches = FindMatches(editor, settings);
            int maxFloor = Math.Max(0, editor.floors.Count - 1);
            foreach (LevelEvent levelEvent in matches)
            {
                int target = levelEvent.floor + offset;
                if (target < 0 || target > maxFloor)
                    throw new ArgumentException("平移后的楼层超出关卡范围: " + target);
            }

            int changed = Apply(editor, delegate
            {
                foreach (LevelEvent levelEvent in matches)
                    levelEvent.floor += offset;
                return matches.Count;
            });
            return new EditorEventResult(changed, matches.Count);
        }

        public static EditorEventResult SetField(scnEditor editor, LevelEvent settings)
        {
            string path = ReadString(settings, "fieldPath", string.Empty).Trim();
            string value = ReadString(settings, "fieldValue", string.Empty);
            if (path.Length == 0)
                throw new ArgumentException("字段路径为空。");

            List<LevelEvent> matches = FindMatches(editor, settings);
            int changed = Apply(editor, delegate
            {
                foreach (LevelEvent levelEvent in matches)
                    SetEventValue(levelEvent, path, value);
                return matches.Count;
            });
            return new EditorEventResult(changed, matches.Count);
        }

        public static EditorEventResult DeleteEvents(scnEditor editor, LevelEvent settings)
        {
            List<LevelEvent> matches = FindMatches(editor, settings);
            int changed = Apply(editor, delegate
            {
                foreach (LevelEvent levelEvent in matches)
                    editor.events.Remove(levelEvent);
                return matches.Count;
            });
            return new EditorEventResult(changed, Math.Max(0, matches.Count - changed));
        }

        public static EditorEventResult CloneAtInterval(scnEditor editor, LevelEvent settings)
        {
            int sourceFloor = ReadInt(settings, "cloneSource", 0);
            int targetStart = ReadInt(settings, "cloneStart", 0);
            int targetEnd = ReadInt(settings, "cloneEnd", 0);
            int interval = ReadInt(settings, "cloneInterval", 16);
            if (interval <= 0)
                throw new ArgumentException("生成间隔必须大于 0。");
            if (targetStart < 0 || targetEnd < 0)
                throw new ArgumentException("目标楼层不能为负数。");
            if (targetStart > targetEnd)
                throw new ArgumentException("目标起始楼层不能大于结束楼层。");

            int maxFloor = Math.Max(0, editor.floors.Count - 1);
            if (sourceFloor < 0 || sourceFloor > maxFloor)
                throw new ArgumentException("来源楼层超出关卡范围。");
            if (targetEnd > maxFloor)
                throw new ArgumentException("目标结束楼层超出关卡范围。");

            string typeText = ReadString(settings, "eventType", string.Empty);
            string tag = ReadString(settings, "tagFilter", string.Empty);
            List<LevelEvent> templates = FindMatches(
                editor.events,
                sourceFloor,
                sourceFloor,
                typeText,
                tag);

            int created = Apply(editor, delegate
            {
                int count = 0;
                for (long destination = targetStart; destination <= targetEnd; destination += interval)
                {
                    if (destination == sourceFloor)
                        continue;

                    foreach (LevelEvent template in templates)
                    {
                        LevelEvent clone = template.Copy();
                        clone.floor = (int)destination;
                        editor.events.Add(clone);
                        count++;
                    }
                }
                return count;
            });
            return new EditorEventResult(created, FindMatches(editor, settings).Count);
        }

        public static List<LevelEvent> FindMatches(scnEditor editor, LevelEvent settings)
        {
            if (editor == null)
                throw new InvalidOperationException("当前不在关卡编辑器中。");
            if (editor.playMode)
                throw new InvalidOperationException("请先退出播放模式，再批量修改事件。");

            int minFloor = ReadInt(settings, "minFloor", 0);
            int maxFloor = ReadInt(settings, "maxFloor", 999999);
            if (minFloor > maxFloor)
                throw new ArgumentException("最小楼层不能大于最大楼层。");

            return FindMatches(
                editor.events,
                minFloor,
                maxFloor,
                ReadString(settings, "eventType", string.Empty),
                ReadString(settings, "tagFilter", string.Empty));
        }

        private static List<LevelEvent> FindMatches(
            IEnumerable<LevelEvent> source,
            int minFloor,
            int maxFloor,
            string eventTypeText,
            string tagFilter)
        {
            LevelEventType? filterType = null;
            string trimmedType = string.IsNullOrEmpty(eventTypeText)
                ? string.Empty
                : eventTypeText.Trim();
            if (trimmedType.Length > 0)
            {
                LevelEventType parsed;
                if (!Enum.TryParse<LevelEventType>(trimmedType, true, out parsed) ||
                    !Enum.IsDefined(typeof(LevelEventType), parsed))
                {
                    throw new ArgumentException("未知事件类型: " + trimmedType);
                }
                filterType = parsed;
            }

            string trimmedTag = string.IsNullOrEmpty(tagFilter)
                ? string.Empty
                : tagFilter.Trim();

            List<LevelEvent> matches = new List<LevelEvent>();
            foreach (LevelEvent levelEvent in source)
            {
                if (levelEvent == null)
                    continue;
                if (levelEvent.floor < minFloor || levelEvent.floor > maxFloor)
                    continue;
                if (filterType.HasValue && levelEvent.eventType != filterType.Value)
                    continue;
                if (trimmedTag.Length > 0)
                {
                    string tag = ReadEventDataString(levelEvent, "tag");
                    string eventTag = ReadEventDataString(levelEvent, "eventTag");
                    if (!string.Equals(tag, trimmedTag, StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(eventTag, trimmedTag, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }
                matches.Add(levelEvent);
            }
            return matches;
        }

        private static int Apply(scnEditor editor, Func<int> operation)
        {
            List<LevelEvent> backup = editor.events
                .Select(x => x.Copy())
                .ToList();
            List<LevelEvent> decorationBackup = editor.decorations
                .Select(x => x.Copy())
                .ToList();

            editor.SaveState(true, false);
            try
            {
                int changed = operation();
                if (changed > 0)
                    RefreshEditor(editor);
                return changed;
            }
            catch
            {
                editor.events.Clear();
                foreach (LevelEvent levelEvent in backup)
                    editor.events.Add(levelEvent);
                editor.decorations.Clear();
                foreach (LevelEvent levelEvent in decorationBackup)
                    editor.decorations.Add(levelEvent);
                TryRefreshEditor(editor);
                throw;
            }
        }

        internal static int ApplyTransaction(scnEditor editor, Func<int> operation)
        {
            return Apply(editor, operation);
        }

        private static void RefreshEditor(scnEditor editor)
        {
            SetUnsavedChanges(editor);
            editor.ApplyEventsToFloors();
            editor.OnSelectedFloorChange();
            editor.DecideInspectorTabsAtSelected();
            editor.UpdateDecorationObjects();
        }

        private static void TryRefreshEditor(scnEditor editor)
        {
            try
            {
                editor.ApplyEventsToFloors();
                editor.OnSelectedFloorChange();
            }
            catch
            {
                // The original exception is more useful than a secondary refresh failure.
            }
        }

        private static void SetUnsavedChanges(scnEditor editor)
        {
            System.Reflection.PropertyInfo property = typeof(scnEditor).GetProperty(
                "unsavedChanges",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (property != null && property.CanWrite)
                property.SetValue(editor, true, null);
        }

        private static void SetEventValue(LevelEvent levelEvent, string path, string input)
        {
            if (string.Equals(path, "floor", StringComparison.OrdinalIgnoreCase))
            {
                levelEvent.floor = ParseInt(input);
                return;
            }

            if (string.Equals(path, "eventType", StringComparison.OrdinalIgnoreCase))
            {
                LevelEventType parsed;
                if (!Enum.TryParse<LevelEventType>(input, true, out parsed) ||
                    !Enum.IsDefined(typeof(LevelEventType), parsed))
                {
                    throw new ArgumentException("未知事件类型: " + input);
                }
                levelEvent.eventType = parsed;
                return;
            }

            string[] segments = path.Split('.');
            SetNestedValue(levelEvent.GetData(), segments, input);
        }

        private static void SetNestedValue(object root, string[] segments, string input)
        {
            if (segments.Length == 0 || string.IsNullOrEmpty(segments[0]))
                throw new ArgumentException("字段路径为空。");

            object current = root;
            for (int i = 0; i < segments.Length; i++)
            {
                string segment = segments[i];
                bool last = i == segments.Length - 1;

                IDictionary dictionary = current as IDictionary;
                if (dictionary != null)
                {
                    object existing = dictionary.Contains(segment)
                        ? dictionary[segment]
                        : null;
                    if (last)
                    {
                        dictionary[segment] = ParseValue(input, existing);
                        return;
                    }

                    if (existing == null)
                    {
                        Dictionary<string, object> created = new Dictionary<string, object>();
                        dictionary[segment] = created;
                        current = created;
                    }
                    else
                    {
                        current = existing;
                    }
                    continue;
                }

                IList list = current as IList;
                if (list != null)
                {
                    int index;
                    if (!int.TryParse(segment, out index) || index < 0 || index >= list.Count)
                        throw new ArgumentException("数组字段索引无效: " + segment);
                    if (last)
                    {
                        list[index] = ParseValue(input, list[index]);
                        return;
                    }
                    current = list[index];
                    if (current == null)
                        throw new ArgumentException("不能继续修改 null 数组元素: " + segment);
                    continue;
                }

                throw new ArgumentException("字段路径包含不支持的对象: " + segment);
            }
        }

        private static object ParseValue(string input, object existing)
        {
            if (existing != null)
            {
                Type type = existing.GetType();
                if (type == typeof(string))
                    return input;
                if (type == typeof(int))
                    return ParseInt(input);
                if (type == typeof(float))
                    return ParseFloat(input);
                if (type == typeof(double))
                    return double.Parse(input, NumberStyles.Float, CultureInfo.InvariantCulture);
                if (type == typeof(bool))
                    return ParseBool(input);
                if (type.IsEnum)
                    return Enum.Parse(type, input, true);
                if (type == typeof(Vector2))
                    return ParseVector2(input);
            }

            bool boolValue;
            if (bool.TryParse(input, out boolValue))
                return boolValue;

            int intValue;
            if (int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out intValue))
                return intValue;

            float floatValue;
            if (float.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out floatValue))
                return floatValue;

            if (input.Length >= 2 &&
                ((input[0] == '"' && input[input.Length - 1] == '"') ||
                 (input[0] == '\'' && input[input.Length - 1] == '\'')))
            {
                return input.Substring(1, input.Length - 2);
            }

            return input;
        }

        private static Vector2 ParseVector2(string input)
        {
            string trimmed = input.Trim().Trim('[', ']', '(', ')');
            string[] parts = trimmed.Split(',');
            if (parts.Length != 2)
                throw new ArgumentException("Vector2 字段需要填写 x,y 两个数字。");
            return new Vector2(
                float.Parse(parts[0].Trim(), CultureInfo.InvariantCulture),
                float.Parse(parts[1].Trim(), CultureInfo.InvariantCulture));
        }

        private static int ReadInt(LevelEvent levelEvent, string key, int fallback)
        {
            object value;
            if (!levelEvent.GetData().TryGetValue(key, out value) || value == null)
                return fallback;
            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        private static string ReadString(LevelEvent levelEvent, string key, string fallback)
        {
            object value;
            if (!levelEvent.GetData().TryGetValue(key, out value) || value == null)
                return fallback;
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static string ReadEventDataString(LevelEvent levelEvent, string key)
        {
            object value;
            if (!levelEvent.GetData().TryGetValue(key, out value) || value == null)
                return string.Empty;
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static int ParseInt(string input)
        {
            int value;
            if (!int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                throw new ArgumentException("需要整数，当前值: " + input);
            return value;
        }

        private static float ParseFloat(string input)
        {
            float value;
            if (!float.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                throw new ArgumentException("需要数字，当前值: " + input);
            return value;
        }

        private static bool ParseBool(string input)
        {
            bool value;
            if (!bool.TryParse(input, out value))
                throw new ArgumentException("需要 true 或 false，当前值: " + input);
            return value;
        }
    }
}
