using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ADOFAI;
using ADOFAI.LevelEditor.Controls;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace AdofaiEventStudio
{
    internal enum PanelMode
    {
        Root,
        GenerateEventMenu,
        GenerateForm,
        DecorationMenu,
        DecorationForm,
        PropagateForm,
        AdjustMenu,
        MoveEventMenu,
        MoveForm,
        ParamEventMenu,
        ParamKeyMenu,
        ParamForm,
        DecorationAdjustMenu,
        DecorationAdjustForm,
        DeleteEventMenu,
        DeleteForm
    }

    internal sealed class InputRow
    {
        public string Key;
        public TMP_InputField Input;
        public PropertyType PropertyType;
        private Func<string> _getText;

        internal InputRow(string key, TMP_InputField input, PropertyType propertyType)
            : this(
                key,
                input,
                propertyType,
                delegate
                {
                    return input == null ? string.Empty : input.text;
                })
        {
        }

        internal InputRow(
            string key,
            TMP_InputField input,
            PropertyType propertyType,
            Func<string> getText)
        {
            Key = key;
            Input = input;
            PropertyType = propertyType;
            _getText = getText;
        }

        public string GetText()
        {
            if (_getText == null)
                return string.Empty;
            return _getText() ?? string.Empty;
        }
    }

    internal static class EditorPanelUI
    {
        private static readonly LevelEventType[] EventTypes = new LevelEventType[]
        {
            LevelEventType.MoveDecorations,
            LevelEventType.SetFilter,
            LevelEventType.MoveCamera,
            LevelEventType.SetFilterAdvanced,
            LevelEventType.MoveTrack,
            LevelEventType.RecolorTrack,
            LevelEventType.SetObject,
            LevelEventType.SetText,
            LevelEventType.EmitParticle,
            LevelEventType.SetParticle
        };

        private static readonly LevelEventType[] DecorationTypes = new LevelEventType[]
        {
            LevelEventType.AddDecoration,
            LevelEventType.AddText,
            LevelEventType.AddObject,
            LevelEventType.AddParticle
        };

        private static PropertiesPanel _panel;
        private static scnEditor _editor;
        private static System.Reflection.MethodInfo _addEventMethod;
        private static PanelMode _mode = PanelMode.Root;
        private static LevelEventType _selectedEventType;
        private static string _selectedParameter;
        private static string _formKey = "root";
        private static List<InputRow> _rows = new List<InputRow>();
        private static Dictionary<string, string> _saved = new Dictionary<string, string>();

        public static void Show(PropertiesPanel panel, scnEditor editor)
        {
            _panel = panel;
            _editor = editor;
            _mode = PanelMode.Root;
            Rebuild();
        }

        private static void Rebuild()
        {
            CaptureInputs();
            _rows.Clear();
            ClearContent();

            switch (_mode)
            {
                case PanelMode.Root:
                    BuildRoot();
                    break;
                case PanelMode.GenerateEventMenu:
                    BuildGenerateEventMenu();
                    break;
                case PanelMode.GenerateForm:
                    BuildGenerateForm();
                    break;
                case PanelMode.DecorationMenu:
                    BuildDecorationMenu();
                    break;
                case PanelMode.DecorationForm:
                    BuildDecorationForm();
                    break;
                case PanelMode.PropagateForm:
                    BuildPropagateForm();
                    break;
                case PanelMode.AdjustMenu:
                    BuildAdjustMenu();
                    break;
                case PanelMode.MoveEventMenu:
                    BuildMoveEventMenu();
                    break;
                case PanelMode.MoveForm:
                    BuildMoveForm();
                    break;
                case PanelMode.ParamEventMenu:
                    BuildParamEventMenu();
                    break;
                case PanelMode.ParamKeyMenu:
                    BuildParamKeyMenu();
                    break;
                case PanelMode.ParamForm:
                    BuildParamForm();
                    break;
                case PanelMode.DecorationAdjustMenu:
                    BuildDecorationAdjustMenu();
                    break;
                case PanelMode.DecorationAdjustForm:
                    BuildDecorationAdjustForm();
                    break;
                case PanelMode.DeleteEventMenu:
                    BuildDeleteEventMenu();
                    break;
                case PanelMode.DeleteForm:
                    BuildDeleteForm();
                    break;
            }
        }

        private static void BuildRoot()
        {
            _formKey = "root";
            AddHeader(L("ADOFAI 事件工坊", "ADOFAI Event Studio"));
            AddButton(L("批量生成", "Batch Generate"), delegate { Goto(PanelMode.GenerateEventMenu); });
            AddButton(L("批量装饰", "Batch Decorate"), delegate { Goto(PanelMode.DecorationMenu); });
            AddButton(L("推广", "Propagate"), delegate { Goto(PanelMode.PropagateForm); });
            AddButton(L("批量调整", "Batch Adjust"), delegate { Goto(PanelMode.AdjustMenu); });
            AddButton(L("批量删除", "Batch Delete"), delegate { Goto(PanelMode.DeleteEventMenu); });
            AddNote(L(
                "按菜单顺序选择事件类型和参数，底部按钮执行最终操作。",
                "Follow the menus to select an event type and parameters, then run the final action."));
        }

        private static void BuildGenerateEventMenu()
        {
            _formKey = "gen-menu";
            AddBackButton(PanelMode.Root, L("返回", "Back"));
            AddHeader(L("批量生成：选择事件类型", "Batch Generate: Select Event Type"));
            foreach (LevelEventType eventType in EventTypes)
            {
                LevelEventType captured = eventType;
                AddButton(EventTitle(eventType), delegate
                {
                    _selectedEventType = captured;
                    _formKey = "gen:" + captured;
                    Goto(PanelMode.GenerateForm);
                });
            }
        }

        private static void BuildGenerateForm()
        {
            LevelEventInfo info = GetEventInfo(_selectedEventType);
            AddBackButton(PanelMode.GenerateEventMenu, L("返回事件列表", "Back to event list"));
            AddHeader(L("批量生成：", "Batch Generate: ") + EventTitle(_selectedEventType));
            AddNote(L(
                "在指定楼层生成指定次数；参数按序号计算 inc 和 random。",
                "Generate the given count at one floor; inc and random are evaluated per index."));
            AddInputRow(
                "genFloor",
                L("生成楼层", "Floor"),
                Saved("genFloor", "0"),
                PropertyType.Int);
            AddInputRow(
                "genCount",
                L("生成次数", "Count"),
                Saved("genCount", "1"),
                PropertyType.Int);
            AddInputRow(
                "textReadCount",
                L("[]读取次数", "Bracket read count"),
                Saved("textReadCount", "1"),
                PropertyType.Int);

            AddParameterInputs(info);

            AddButton(L("生成", "Generate"), ExecuteGenerate);
        }

        private static void BuildDecorationMenu()
        {
            _formKey = "deco-menu";
            AddBackButton(PanelMode.Root, L("返回", "Back"));
            AddHeader(L("批量装饰：选择装饰类型", "Batch Decorate: Select Decoration Type"));
            foreach (LevelEventType eventType in DecorationTypes)
            {
                LevelEventType captured = eventType;
                AddButton(EventTitle(eventType), delegate
                {
                    _selectedEventType = captured;
                    _formKey = "deco:" + captured;
                    Goto(PanelMode.DecorationForm);
                });
            }
        }

        private static void BuildDecorationForm()
        {
            LevelEventInfo info = GetEventInfo(_selectedEventType);
            AddBackButton(PanelMode.DecorationMenu, L("返回装饰列表", "Back to decoration list"));
            AddHeader(L("批量装饰：", "Batch Decorate: ") + EventTitle(_selectedEventType));
            AddNote(L(
                "在指定楼层生成指定次数；参数表达式与批量生成相同。",
                "Generate the given count at one floor; expressions work like Batch Generate."));
            AddInputRow(
                "decoFloor",
                L("生成楼层", "Floor"),
                Saved("decoFloor", "0"),
                PropertyType.Int);
            AddInputRow(
                "decoCount",
                L("生成次数", "Count"),
                Saved("decoCount", "1"),
                PropertyType.Int);
            AddInputRow(
                "textReadCount",
                L("[]读取次数", "Bracket read count"),
                Saved("textReadCount", "1"),
                PropertyType.Int);
            AddParameterInputs(info);
            AddButton(L("生成", "Generate"), ExecuteDecorationGenerate);
        }

        private static void BuildPropagateForm()
        {
            _formKey = "propagate";
            AddBackButton(PanelMode.Root, L("返回", "Back"));
            AddHeader(L("推广移动装饰动画", "Propagate MoveDecorations Animation"));
            AddNote(L(
                "复制源楼层的 MoveDecorations，仅改变标签和角度偏移。",
                "Copies MoveDecorations from the source floor, changing only tag and angle offset."));
            AddInputRow(
                "sourceFloor",
                L("源楼层", "Source floor"),
                Saved("sourceFloor", "0"),
                PropertyType.Int);
            AddInputRow(
                "targetFloor",
                L("目标楼层", "Target floor"),
                Saved("targetFloor", "0"),
                PropertyType.Int);
            AddInputRow(
                "sourceTag",
                L("源标签（留空复制全部）", "Source tag (empty = all)"),
                Saved("sourceTag", string.Empty),
                PropertyType.String);
            AddInputRow(
                "propagateCount",
                L("次数", "Count"),
                Saved("propagateCount", "1"),
                PropertyType.Int);
            AddInputRow(
                "tagExpr",
                L("新标签表达式", "Tag expression"),
                Saved("tagExpr", "a[1]"),
                PropertyType.String);
            AddInputRow(
                "angleExpr",
                L("角度偏移增量", "Angle offset delta"),
                Saved("angleExpr", "inc(0,5)"),
                PropertyType.Int);
            AddInputRow(
                "textReadCount",
                L("[]读取次数", "Bracket read count"),
                Saved("textReadCount", "1"),
                PropertyType.Int);
            AddButton(L("推广", "Propagate"), ExecutePropagate);
        }

        private static void AddParameterInputs(LevelEventInfo info)
        {
            foreach (PropertyInfo propertyInfo in info.propertiesInfo.Values)
            {
                if (propertyInfo.type == PropertyType.Vector2)
                {
                    AddInputRow(
                        propertyInfo.name + ".x",
                        PropertyLabel(propertyInfo) + " X",
                        Saved(
                            propertyInfo.name + ".x",
                            string.Empty),
                        PropertyType.Float);
                    AddInputRow(
                        propertyInfo.name + ".y",
                        PropertyLabel(propertyInfo) + " Y",
                        Saved(
                            propertyInfo.name + ".y",
                            string.Empty),
                        PropertyType.Float);
                }
                else if (propertyInfo.type == PropertyType.File)
                {
                    AddFileInputRow(
                        propertyInfo.name,
                        PropertyLabel(propertyInfo),
                        Saved(propertyInfo.name, string.Empty),
                        propertyInfo.fileType);
                }
                else if (propertyInfo.type == PropertyType.Enum)
                {
                    AddEnumInputRow(
                        propertyInfo.name,
                        PropertyLabel(propertyInfo),
                        Saved(propertyInfo.name, string.Empty),
                        propertyInfo);
                }
                else if (propertyInfo.type == PropertyType.Bool)
                {
                    AddInputRow(
                        propertyInfo.name,
                        PropertyLabel(propertyInfo),
                        Saved(propertyInfo.name, string.Empty),
                        PropertyType.Bool);
                    AddNote(L("可填写：true, false", "Allowed: true, false"));
                }
                else if (propertyInfo.type == PropertyType.Color)
                {
                    AddColorInputRow(
                        propertyInfo.name,
                        PropertyLabel(propertyInfo),
                        Saved(propertyInfo.name, string.Empty),
                        propertyInfo);
                }
                else
                {
                    AddInputRow(
                        propertyInfo.name,
                        PropertyLabel(propertyInfo),
                        Saved(propertyInfo.name, string.Empty),
                        propertyInfo.type);
                }
            }
        }

        private static void BuildAdjustMenu()
        {
            _formKey = "adjust-menu";
            AddBackButton(PanelMode.Root, L("返回", "Back"));
            AddHeader(L("批量调整", "Batch Adjust"));
            AddButton(L("事件移动", "Move Events"), delegate { Goto(PanelMode.MoveEventMenu); });
            AddButton(L("参数批量调整", "Adjust Parameters"), delegate { Goto(PanelMode.ParamEventMenu); });
            AddButton(L("批量调整装饰", "Adjust Decorations"), delegate { Goto(PanelMode.DecorationAdjustMenu); });
        }

        private static void BuildMoveEventMenu()
        {
            _formKey = "move-menu";
            AddBackButton(PanelMode.AdjustMenu, L("返回", "Back"));
            AddHeader(L("事件移动：选择事件类型", "Move Events: Select Event Type"));
            foreach (LevelEventType eventType in EventTypes)
            {
                LevelEventType captured = eventType;
                AddButton(EventTitle(eventType), delegate
                {
                    _selectedEventType = captured;
                    _formKey = "move:" + captured;
                    Goto(PanelMode.MoveForm);
                });
            }
        }

        private static void BuildMoveForm()
        {
            AddBackButton(PanelMode.MoveEventMenu, L("返回事件列表", "Back to event list"));
            AddHeader(L("事件移动：", "Move Events: ") + EventTitle(_selectedEventType));
            AddNote(L(
                "输入例如 +10、-5；作用于事件楼层。",
                "Enter +10, -5, etc.; applies to the event floor."));
            AddRangeInputs();
            AddInputRow("operation", L("楼层运算", "Floor operation"), Saved("operation", "+0"), PropertyType.String);
            AddButton(L("调整", "Adjust"), ExecuteMove);
        }

        private static void BuildParamEventMenu()
        {
            _formKey = "param-menu";
            AddBackButton(PanelMode.AdjustMenu, L("返回", "Back"));
            AddHeader(L("参数批量调整：选择事件类型", "Adjust Parameters: Select Event Type"));
            foreach (LevelEventType eventType in EventTypes)
            {
                LevelEventType captured = eventType;
                AddButton(EventTitle(eventType), delegate
                {
                    _selectedEventType = captured;
                    _formKey = "param-keys:" + captured;
                    Goto(PanelMode.ParamKeyMenu);
                });
            }
        }

        private static void BuildParamKeyMenu()
        {
            LevelEventInfo info = GetEventInfo(_selectedEventType);
            AddBackButton(PanelMode.ParamEventMenu, L("返回事件列表", "Back to event list"));
            AddHeader(L("选择要调整的参数", "Select a parameter"));
            foreach (PropertyInfo propertyInfo in info.propertiesInfo.Values)
            {
                if (propertyInfo.type != PropertyType.Int &&
                    propertyInfo.type != PropertyType.Float &&
                    propertyInfo.type != PropertyType.Vector2)
                {
                    continue;
                }

                if (propertyInfo.type == PropertyType.Vector2)
                {
                    string capturedX = propertyInfo.name + ".x";
                    string capturedY = propertyInfo.name + ".y";
                    AddButton(PropertyLabel(propertyInfo) + " X", delegate
                    {
                        _selectedParameter = capturedX;
                        _formKey = "param:" + _selectedEventType + ":" + capturedX;
                        Goto(PanelMode.ParamForm);
                    });
                    AddButton(PropertyLabel(propertyInfo) + " Y", delegate
                    {
                        _selectedParameter = capturedY;
                        _formKey = "param:" + _selectedEventType + ":" + capturedY;
                        Goto(PanelMode.ParamForm);
                    });
                }
                else
                {
                    string capturedKey = propertyInfo.name;
                    AddButton(PropertyLabel(propertyInfo), delegate
                    {
                        _selectedParameter = capturedKey;
                        _formKey = "param:" + _selectedEventType + ":" + capturedKey;
                        Goto(PanelMode.ParamForm);
                    });
                }
            }
        }

        private static void BuildParamForm()
        {
            AddBackButton(PanelMode.ParamKeyMenu, L("返回参数列表", "Back to parameter list"));
            AddHeader(
                L("参数调整：", "Adjust Parameter: ") + EventTitle(_selectedEventType) +
                " / " + _selectedParameter);
            AddNote(L(
                "输入例如 +10、-5、*2、/2；在事件当前值基础上计算。",
                "Enter +10, -5, *2, /2; applied to each event's current value."));
            AddRangeInputs();
            AddInputRow("operation", L("参数运算", "Parameter operation"), Saved("operation", "+0"), PropertyType.String);
            AddButton(L("调整", "Adjust"), ExecuteParamAdjust);
        }

        private static void BuildDecorationAdjustMenu()
        {
            _formKey = "deco-adjust-menu";
            AddBackButton(PanelMode.AdjustMenu, L("返回", "Back"));
            AddHeader(L("批量调整装饰：选择类型", "Adjust Decorations: Select Type"));
            foreach (LevelEventType eventType in DecorationTypes)
            {
                LevelEventType captured = eventType;
                AddButton(EventTitle(eventType), delegate
                {
                    _selectedEventType = captured;
                    _formKey = "deco-adjust:" + captured;
                    Goto(PanelMode.DecorationAdjustForm);
                });
            }
        }

        private static void BuildDecorationAdjustForm()
        {
            LevelEventInfo info = GetEventInfo(_selectedEventType);
            AddBackButton(PanelMode.DecorationAdjustMenu, L("返回装饰列表", "Back to decoration list"));
            AddHeader(L("批量调整装饰：", "Adjust Decorations: ") + EventTitle(_selectedEventType));
            AddNote(L(
                "只修改填写了内容的参数；v 表示当前值，i 表示匹配序号。",
                "Only filled parameters are changed; v is the current value and i is the match index."));
            AddInputRow(
                "decoAdjustFloor",
                L("装饰所在楼层", "Decoration floor"),
                Saved("decoAdjustFloor", "0"),
                PropertyType.Int);
            AddInputRow(
                "textReadCount",
                L("[]读取次数", "Bracket read count"),
                Saved("textReadCount", "1"),
                PropertyType.Int);
            AddParameterInputs(info);
            AddButton(L("调整", "Adjust"), ExecuteDecorationAdjust);
        }

        private static void BuildDeleteEventMenu()
        {
            _formKey = "delete-menu";
            AddBackButton(PanelMode.Root, L("返回", "Back"));
            AddHeader(L("批量删除：选择事件类型", "Batch Delete: Select Event Type"));
            foreach (LevelEventType eventType in EventTypes)
            {
                LevelEventType captured = eventType;
                AddButton(EventTitle(eventType), delegate
                {
                    _selectedEventType = captured;
                    _formKey = "delete:" + captured;
                    Goto(PanelMode.DeleteForm);
                });
            }
        }

        private static void BuildDeleteForm()
        {
            AddBackButton(PanelMode.DeleteEventMenu, L("返回事件列表", "Back to event list"));
            AddHeader(L("批量删除：", "Batch Delete: ") + EventTitle(_selectedEventType));
            AddNote(L(
                "只删除指定楼层范围内该类型的事件。",
                "Deletes only this event type inside the selected floor range."));
            AddRangeInputs();
            AddButton(L("删除", "Delete"), ExecuteDelete);
        }

        private static void ExecuteGenerate()
        {
            CaptureInputs();
            RunTransaction("生成", delegate
            {
                int floor = GetInt("genFloor", 0);
                int count = GetInt("genCount", 1);
                int textReadCount = GetTextReadCount();
                if (count <= 0)
                    throw new ArgumentException("生成次数必须大于 0。");
                EditorIntegration.ReportDiagnostic(
                    "生成类型=" + _selectedEventType +
                    " 楼层=" + floor +
                    " 次数=" + count);

                int created = 0;

                for (int i = 0; i < count; i++)
                {
                    LevelEvent levelEvent = CreateEditorEvent(floor, _selectedEventType);
                    ApplyInputFields(
                        levelEvent,
                        GetEventInfo(_selectedEventType),
                        i,
                        count,
                        textReadCount,
                        "genFloor",
                        "genCount",
                        "textReadCount");
                    created++;
                }
                return created;
            });
        }

        private static void ExecuteDecorationGenerate()
        {
            CaptureInputs();
            RunTransaction("生成装饰", delegate
            {
                int floor = GetInt("decoFloor", 0);
                int count = GetInt("decoCount", 1);
                int textReadCount = GetTextReadCount();
                if (count <= 0)
                    throw new ArgumentException("生成次数必须大于 0。");

                int created = 0;
                for (int i = 0; i < count; i++)
                {
                    LevelEvent levelEvent = BuildGeneratedEvent(
                        GetEventInfo(_selectedEventType),
                        floor,
                        i,
                        count,
                        textReadCount,
                        "decoFloor",
                        "decoCount",
                        "textReadCount");
                    _editor.decorations.Add(levelEvent);
                    created++;
                }
                return created;
            });
        }

        private static void ExecutePropagate()
        {
            CaptureInputs();
            RunTransaction("推广", delegate
            {
                int sourceFloor = GetInt("sourceFloor", 0);
                int targetFloor = GetInt("targetFloor", 0);
                int count = GetInt("propagateCount", 1);
                string sourceTag = GetString("sourceTag", string.Empty).Trim();
                string tagExpression = GetString("tagExpr", "a[1]").Trim();
                string angleExpression = GetString("angleExpr", "inc(0,5)").Trim();
                int textReadCount = GetTextReadCount();
                if (count <= 0)
                    throw new ArgumentException("次数必须大于 0。");
                if (tagExpression.Length == 0)
                    throw new ArgumentException("新标签表达式不能为空。");

                List<LevelEvent> templates = _editor.events
                    .Where(x => x != null &&
                        x.eventType == LevelEventType.MoveDecorations &&
                        x.floor == sourceFloor &&
                        (sourceTag.Length == 0 ||
                         string.Equals(
                            ReadDataString(x, "tag"),
                            sourceTag,
                            StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                if (templates.Count == 0)
                    throw new InvalidOperationException("源楼层没有符合条件的 MoveDecorations。");

                int created = 0;
                for (int i = 0; i < count; i++)
                {
                    string tag = EvaluateText(tagExpression, i, count, textReadCount);
                    int delta = EvaluateInt(angleExpression, i, count);

                    foreach (LevelEvent template in templates)
                    {
                        LevelEvent clone = template.Copy();
                        clone.floor = targetFloor;
                        clone["tag"] = tag;

                        int baseAngle = 0;
                        object angleObject;
                        if (clone.GetData().TryGetValue("angleOffset", out angleObject) &&
                            angleObject != null)
                        {
                            baseAngle = Convert.ToInt32(
                                angleObject,
                                CultureInfo.InvariantCulture);
                        }
                        clone["angleOffset"] = baseAngle + delta;

                        EnableField(clone, "tag");
                        EnableField(clone, "angleOffset");
                        _editor.events.Add(clone);
                        created++;
                    }
                }
                return created;
            });
        }

        private static LevelEvent BuildGeneratedEvent(
            LevelEventInfo info,
            int floor,
            int index,
            int total,
            int textReadCount,
            params string[] skipKeys)
        {
            LevelEvent levelEvent = new LevelEvent(floor, _selectedEventType, info);
            ApplyInputFields(
                levelEvent,
                info,
                index,
                total,
                textReadCount,
                skipKeys);
            return levelEvent;
        }

        private static LevelEvent CreateEditorEvent(
            int floor,
            LevelEventType eventType)
        {
            if (_addEventMethod == null)
            {
                _addEventMethod = typeof(scnEditor).GetMethod(
                    "AddEvent",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);
            }

            if (_addEventMethod == null)
                throw new MissingMethodException("找不到 scnEditor.AddEvent。");

            _addEventMethod.Invoke(
                _editor,
                new object[] { floor, eventType });
            return _editor.events[_editor.events.Count - 1];
        }

        private static void ApplyInputFields(
            LevelEvent levelEvent,
            LevelEventInfo info,
            int index,
            int total,
            int textReadCount,
            params string[] skipKeys)
        {
            Dictionary<string, double> vectorComponents =
                new Dictionary<string, double>();

            foreach (InputRow row in _rows)
            {
                if (skipKeys.Contains(row.Key))
                    continue;

                string text = row.GetText();
                if (string.IsNullOrEmpty(text))
                    continue;

                if (row.Key.EndsWith(".x", StringComparison.OrdinalIgnoreCase) ||
                    row.Key.EndsWith(".y", StringComparison.OrdinalIgnoreCase))
                {
                    vectorComponents[row.Key] = EvaluateNumber(text, index, total);
                    continue;
                }

                ADOFAI.PropertyInfo propertyInfo;
                if (!info.propertiesInfo.TryGetValue(row.Key, out propertyInfo))
                    continue;

                switch (propertyInfo.type)
                {
                    case PropertyType.Int:
                        levelEvent[row.Key] = EvaluateInt(text, index, total);
                        break;
                    case PropertyType.Float:
                        levelEvent[row.Key] = (float)EvaluateNumber(text, index, total);
                        break;
                    case PropertyType.Bool:
                        levelEvent[row.Key] = bool.Parse(text.Trim());
                        break;
                    case PropertyType.String:
                    case PropertyType.LongString:
                        levelEvent[row.Key] = EvaluateText(
                            text,
                            index,
                            total,
                            textReadCount);
                        break;
                    case PropertyType.Enum:
                        levelEvent[row.Key] = Enum.Parse(propertyInfo.enumType, text);
                        break;
                    case PropertyType.Vector2:
                        levelEvent[row.Key] = ParseVector2(text);
                        break;
                    case PropertyType.Tile:
                        levelEvent[row.Key] = ParseTileNative(text, index, total);
                        break;
                    case PropertyType.File:
                        levelEvent[row.Key] = EvaluateText(
                            text,
                            index,
                            total,
                            textReadCount);
                        break;
                    case PropertyType.Color:
                        levelEvent[row.Key] = text;
                        break;
                    default:
                        levelEvent[row.Key] = JsonTokenToNative(JToken.Parse(text.Trim()));
                        break;
                }

                if (levelEvent.disabled != null &&
                    levelEvent.disabled.ContainsKey(row.Key))
                {
                    levelEvent.disabled[row.Key] = false;
                }
            }

            foreach (string componentKey in vectorComponents.Keys.ToList())
            {
                string baseKey = componentKey.Substring(0, componentKey.Length - 2);
                string xKey = baseKey + ".x";
                string yKey = baseKey + ".y";
                ADOFAI.PropertyInfo propertyInfo = info.propertiesInfo[baseKey];
                Vector2 vectorDefault = AsVector2(propertyInfo.value_default);

                double x = vectorComponents.ContainsKey(xKey)
                    ? vectorComponents[xKey]
                    : (double)vectorDefault.x;
                double y = vectorComponents.ContainsKey(yKey)
                    ? vectorComponents[yKey]
                    : (double)vectorDefault.y;
                levelEvent[baseKey] = new Vector2((float)x, (float)y);
                if (levelEvent.disabled != null &&
                    levelEvent.disabled.ContainsKey(baseKey))
                {
                    levelEvent.disabled[baseKey] = false;
                }
            }
        }

        private static Dictionary<string, object> BuildRawEvent(
            LevelEventInfo info,
            int floor,
            int index,
            params string[] skipKeys)
        {
            Dictionary<string, object> raw = new Dictionary<string, object>();
            raw["eventType"] = _selectedEventType.ToString();
            raw["floor"] = floor;
            Dictionary<string, double> vectorComponents =
                new Dictionary<string, double>();
            Dictionary<string, ADOFAI.PropertyInfo> propertyMap =
                info.propertiesInfo.ToDictionary(x => x.Key, x => x.Value);

            foreach (InputRow row in _rows)
            {
                if (skipKeys.Contains(row.Key))
                    continue;

                string text = row.GetText();
                if (string.IsNullOrEmpty(text))
                {
                    ADOFAI.PropertyInfo fallbackInfo;
                    if (propertyMap.TryGetValue(row.Key, out fallbackInfo) &&
                        fallbackInfo.value_default != null)
                    {
                        if (IsSimpleValueType(fallbackInfo.type))
                        {
                            raw[row.Key] = fallbackInfo.value_default;
                        }
                        else if (fallbackInfo.type == PropertyType.Tile)
                        {
                            raw[row.Key] = fallbackInfo.value_default == null
                                ? new List<object> { 0, "ThisTile" }
                                : TileDefaultToList(fallbackInfo.value_default);
                        }
                    }
                    continue;
                }

                if (row.Key.EndsWith(".x", StringComparison.OrdinalIgnoreCase) ||
                    row.Key.EndsWith(".y", StringComparison.OrdinalIgnoreCase))
                {
                    vectorComponents[row.Key] = EvaluateNumber(text, index);
                    continue;
                }

                object value = EvaluateField(row, text, index);
                if (value != null)
                    raw[row.Key] = value;
            }

            foreach (string componentKey in vectorComponents.Keys.ToList())
            {
                string baseKey = componentKey.Substring(0, componentKey.Length - 2);
                string xKey = baseKey + ".x";
                string yKey = baseKey + ".y";
                ADOFAI.PropertyInfo propertyInfo = info.propertiesInfo[baseKey];
                Vector2 vectorDefault = AsVector2(propertyInfo.value_default);

                double x = vectorComponents.ContainsKey(xKey)
                    ? vectorComponents[xKey]
                    : (double)vectorDefault.x;
                double y = vectorComponents.ContainsKey(yKey)
                    ? vectorComponents[yKey]
                    : (double)vectorDefault.y;
                raw[baseKey] = new List<object> { (float)x, (float)y };
            }

            foreach (KeyValuePair<string, object> pair in raw.ToList())
            {
                ADOFAI.PropertyInfo propertyInfo;
                if (!propertyMap.TryGetValue(pair.Key, out propertyInfo))
                    continue;

                if (propertyInfo.type == PropertyType.Enum)
                {
                    if (pair.Value is string)
                        continue;
                    if (pair.Value == null)
                    {
                        raw.Remove(pair.Key);
                        continue;
                    }

                    try
                    {
                        int numeric = Convert.ToInt32(
                            pair.Value,
                            CultureInfo.InvariantCulture);
                        string enumName = Enum.GetName(propertyInfo.enumType, numeric);
                        if (!string.IsNullOrEmpty(enumName))
                        {
                            raw[pair.Key] = enumName;
                            continue;
                        }
                    }
                    catch
                    {
                    }
                    raw.Remove(pair.Key);
                }
                else if (propertyInfo.type == PropertyType.Tile &&
                    !(pair.Value is IList))
                {
                    raw[pair.Key] = ParseTile(Convert.ToString(
                        pair.Value,
                        CultureInfo.InvariantCulture));
                }
            }

            List<string> nullKeys = raw
                .Where(x => x.Value == null)
                .Select(x => x.Key)
                .ToList();
            foreach (string key in nullKeys)
                raw.Remove(key);
            return raw;
        }

        private static object TileDefaultToList(object defaultValue)
        {
            if (defaultValue == null)
                return null;

            Type tupleType = defaultValue.GetType();
            System.Reflection.PropertyInfo item1 = tupleType.GetProperty("Item1");
            System.Reflection.PropertyInfo item2 = tupleType.GetProperty("Item2");
            if (item1 == null || item2 == null)
                return defaultValue;

            object first = item1.GetValue(defaultValue, null);
            object second = item2.GetValue(defaultValue, null);
            return new List<object>
            {
                first,
                second == null ? string.Empty : second.ToString()
            };
        }

        private static bool IsSimpleValueType(PropertyType type)
        {
            return type == PropertyType.Int ||
                type == PropertyType.Float ||
                type == PropertyType.Bool ||
                type == PropertyType.String ||
                type == PropertyType.LongString ||
                type == PropertyType.Enum ||
                type == PropertyType.File ||
                type == PropertyType.Color;
        }

        private static void ExecuteMove()
        {
            CaptureInputs();
            RunTransaction("移动", delegate
            {
                int start = GetInt("rangeStart", 0);
                int end = GetInt("rangeEnd", 0);
                string operation = GetString("operation", "+0");
                List<LevelEvent> matches = FindMatches(start, end);
                for (int index = 0; index < matches.Count; index++)
                {
                    LevelEvent levelEvent = matches[index];
                    levelEvent.floor = (int)EvaluateAdjust(
                        operation,
                        index,
                        matches.Count,
                        levelEvent.floor);
                }
                return matches.Count;
            });
        }

        private static void ExecuteParamAdjust()
        {
            CaptureInputs();
            RunTransaction("参数调整", delegate
            {
                int start = GetInt("rangeStart", 0);
                int end = GetInt("rangeEnd", 0);
                string operation = GetString("operation", "+0");
                List<LevelEvent> matches = FindMatches(start, end);

                LevelEventInfo info = GetEventInfo(_selectedEventType);
                bool isVectorComponent =
                    _selectedParameter.EndsWith(".x", StringComparison.OrdinalIgnoreCase) ||
                    _selectedParameter.EndsWith(".y", StringComparison.OrdinalIgnoreCase);
                string baseKey = isVectorComponent
                    ? _selectedParameter.Substring(0, _selectedParameter.Length - 2)
                    : _selectedParameter;
                ADOFAI.PropertyInfo propertyInfo = info.propertiesInfo[baseKey];

                for (int index = 0; index < matches.Count; index++)
                {
                    LevelEvent levelEvent = matches[index];
                    if (isVectorComponent)
                    {
                        object vectorObject;
                        if (!levelEvent.GetData().TryGetValue(baseKey, out vectorObject) ||
                            !(vectorObject is Vector2))
                        {
                            vectorObject = AsVector2(propertyInfo.value_default);
                        }

                        Vector2 vector = (Vector2)vectorObject;
                        bool isX = _selectedParameter.EndsWith(
                            ".x",
                            StringComparison.OrdinalIgnoreCase);
                        float component = isX ? vector.x : vector.y;
                        float updated = (float)EvaluateAdjust(
                            operation,
                            index,
                            matches.Count,
                            component);
                        levelEvent[baseKey] = isX
                            ? new Vector2(updated, vector.y)
                            : new Vector2(vector.x, updated);
                    }
                    else
                    {
                        object current;
                        if (!levelEvent.GetData().TryGetValue(_selectedParameter, out current) ||
                            current == null)
                        {
                            current = propertyInfo.value_default;
                        }

                        object updated = propertyInfo.type == PropertyType.Int
                            ? (object)(int)EvaluateAdjust(
                                operation,
                                index,
                                matches.Count,
                                Convert.ToInt32(current, CultureInfo.InvariantCulture))
                            : (object)(float)EvaluateAdjust(
                                operation,
                                index,
                                matches.Count,
                                Convert.ToSingle(current, CultureInfo.InvariantCulture));
                        levelEvent[_selectedParameter] = updated;
                    }
                }
                return matches.Count;
            });
        }

        private static void ExecuteDecorationAdjust()
        {
            CaptureInputs();
            RunTransaction("调整装饰", delegate
            {
                int floor = GetInt("decoAdjustFloor", 0);
                int textReadCount = GetTextReadCount();
                List<LevelEvent> matches = _editor.decorations
                    .Where(x => x != null &&
                        x.eventType == _selectedEventType &&
                        x.floor == floor)
                    .ToList();
                if (matches.Count == 0)
                    throw new InvalidOperationException("该楼层没有所选类型的装饰。");

                LevelEventInfo info = GetEventInfo(_selectedEventType);
                for (int index = 0; index < matches.Count; index++)
                {
                    ApplyAdjustmentFields(
                        matches[index],
                        info,
                        index,
                        matches.Count,
                        textReadCount);
                }
                return matches.Count;
            });
        }

        private static void ApplyAdjustmentFields(
            LevelEvent levelEvent,
            LevelEventInfo info,
            int index,
            int total,
            int textReadCount)
        {
            Dictionary<string, string> vectorExpressions =
                new Dictionary<string, string>();

            foreach (InputRow row in _rows)
            {
                if (row.Key == "decoAdjustFloor" || row.Key == "textReadCount")
                    continue;

                string text = row.GetText();
                if (string.IsNullOrEmpty(text))
                    continue;

                if (row.Key.EndsWith(".x", StringComparison.OrdinalIgnoreCase) ||
                    row.Key.EndsWith(".y", StringComparison.OrdinalIgnoreCase))
                {
                    vectorExpressions[row.Key] = text;
                    continue;
                }

                ADOFAI.PropertyInfo propertyInfo;
                if (!info.propertiesInfo.TryGetValue(row.Key, out propertyInfo))
                    continue;

                object current;
                if (!levelEvent.GetData().TryGetValue(row.Key, out current) ||
                    current == null)
                {
                    current = propertyInfo.value_default;
                }

                switch (propertyInfo.type)
                {
                    case PropertyType.Int:
                        levelEvent[row.Key] = (int)EvaluateNumber(
                            text,
                            index,
                            total,
                            Convert.ToDouble(current, CultureInfo.InvariantCulture));
                        break;
                    case PropertyType.Float:
                        levelEvent[row.Key] = (float)EvaluateNumber(
                            text,
                            index,
                            total,
                            Convert.ToDouble(current, CultureInfo.InvariantCulture));
                        break;
                    case PropertyType.Bool:
                        levelEvent[row.Key] = bool.Parse(text.Trim());
                        break;
                    case PropertyType.String:
                    case PropertyType.LongString:
                        levelEvent[row.Key] = EvaluateText(
                            text,
                            index,
                            total,
                            textReadCount);
                        break;
                    case PropertyType.Enum:
                        levelEvent[row.Key] = Enum.Parse(propertyInfo.enumType, text);
                        break;
                    case PropertyType.Vector2:
                        levelEvent[row.Key] = ParseVector2(text);
                        break;
                    case PropertyType.Tile:
                        levelEvent[row.Key] = ParseTileNative(text, index, total);
                        break;
                    case PropertyType.File:
                        levelEvent[row.Key] = EvaluateText(
                            text,
                            index,
                            total,
                            textReadCount);
                        break;
                    case PropertyType.Color:
                        levelEvent[row.Key] = text;
                        break;
                    default:
                        levelEvent[row.Key] = JsonTokenToNative(JToken.Parse(text.Trim()));
                        break;
                }
                EnableField(levelEvent, row.Key);
            }

            foreach (string componentKey in vectorExpressions.Keys.ToList())
            {
                string baseKey = componentKey.Substring(0, componentKey.Length - 2);
                ADOFAI.PropertyInfo propertyInfo = info.propertiesInfo[baseKey];
                Vector2 vector = AsVector2(propertyInfo.value_default);
                object currentObject;
                if (levelEvent.GetData().TryGetValue(baseKey, out currentObject) &&
                    currentObject is Vector2)
                {
                    vector = (Vector2)currentObject;
                }
                string xKey = baseKey + ".x";
                string yKey = baseKey + ".y";
                string expression;
                double x = vector.x;
                if (vectorExpressions.TryGetValue(xKey, out expression))
                    x = EvaluateNumber(expression, index, total, vector.x);
                double y = vector.y;
                if (vectorExpressions.TryGetValue(yKey, out expression))
                    y = EvaluateNumber(expression, index, total, vector.y);
                levelEvent[baseKey] = new Vector2((float)x, (float)y);
                EnableField(levelEvent, baseKey);
            }
        }

        private static void ExecuteDelete()
        {
            CaptureInputs();
            RunTransaction("删除", delegate
            {
                int start = GetInt("rangeStart", 0);
                int end = GetInt("rangeEnd", 0);
                List<LevelEvent> matches = FindMatches(start, end);
                foreach (LevelEvent levelEvent in matches)
                    _editor.events.Remove(levelEvent);
                return matches.Count;
            });
        }

        private static void RunTransaction(string label, Func<int> operation)
        {
            try
            {
                if (_editor == null)
                    throw new InvalidOperationException("当前不在关卡编辑器中。");
                int changed = EditorEventTools.ApplyTransaction(_editor, operation);
                EditorIntegration.ReportStatus(label + "完成：" + changed + " 个事件。");
            }
            catch (Exception exception)
            {
                EditorIntegration.ReportStatus(label + "失败: " + exception.Message);
                EditorIntegration.ReportException(exception);
            }
        }

        private static List<LevelEvent> FindMatches(int start, int end)
        {
            if (start > end)
                throw new ArgumentException("起始楼层不能大于结束楼层。");
            return _editor.events
                .Where(x => x != null &&
                    x.eventType == _selectedEventType &&
                    x.floor >= start &&
                    x.floor <= end)
                .ToList();
        }

        private static object EvaluateField(InputRow row, string text, int index)
        {
            switch (row.PropertyType)
            {
                case PropertyType.Int:
                    return EvaluateInt(text, index);
                case PropertyType.Float:
                    return (float)EvaluateNumber(text, index);
                case PropertyType.Bool:
                    return bool.Parse(text.Trim());
                case PropertyType.String:
                case PropertyType.LongString:
                    return EvaluateText(text, index);
                case PropertyType.Enum:
                case PropertyType.File:
                case PropertyType.Color:
                    return text;
                case PropertyType.Vector2:
                    Vector2 vector = ParseVector2(text);
                    return new List<object> { vector.x, vector.y };
                case PropertyType.Tile:
                    return ParseTile(text);
                default:
                    string trimmed = text.Trim();
                    if (trimmed.Length == 0)
                        return null;
                    return JsonTokenToNative(JToken.Parse(trimmed));
            }
        }

        private static object JsonTokenToNative(JToken token)
        {
            JObject jObject = token as JObject;
            if (jObject != null)
            {
                Dictionary<string, object> dictionary = new Dictionary<string, object>();
                foreach (KeyValuePair<string, JToken> pair in jObject)
                    dictionary[pair.Key] = JsonTokenToNative(pair.Value);
                return dictionary;
            }

            JArray jArray = token as JArray;
            if (jArray != null)
            {
                List<object> list = new List<object>();
                foreach (JToken item in jArray)
                    list.Add(JsonTokenToNative(item));
                return list;
            }

            JValue jValue = token as JValue;
            if (jValue != null)
                return jValue.Value;
            return token.ToString();
        }

        private static object ParseTile(string text)
        {
            string trimmed = text.Trim();
            if (trimmed.Length == 0)
                return null;

            if (trimmed[0] == '[')
                return JsonTokenToNative(JToken.Parse(trimmed));

            int first;
            if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out first))
                return new List<object> { first, "ThisTile" };

            string[] parts = trimmed.Split(',');
            if (parts.Length != 2)
                throw new ArgumentException("Tile 字段格式应为 数字,锚点类型。");

            int offset;
            if (!int.TryParse(parts[0].Trim(), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out offset))
            {
                throw new ArgumentException("Tile 字段第一个值必须是整数。");
            }
            return new List<object> { offset, parts[1].Trim() };
        }

        private static object ParseTileNative(string text)
        {
            object parsed = ParseTile(text);
            List<object> list = parsed as List<object>;
            if (list == null || list.Count != 2)
                throw new ArgumentException("Tile 字段格式无效。");

            int offset = Convert.ToInt32(list[0], CultureInfo.InvariantCulture);
            string anchor = Convert.ToString(list[1], CultureInfo.InvariantCulture);
            TileRelativeTo relativeTo =
                (TileRelativeTo)Enum.Parse(typeof(TileRelativeTo), anchor);
            return Tuple.Create(offset, relativeTo);
        }

        private static object ParseTileNative(string text, int index)
        {
            return ParseTileNative(text, index, 0);
        }

        private static object ParseTileNative(string text, int index, int total)
        {
            try
            {
                int offset = EvaluateInt(text, index, total);
                TileRelativeTo relativeTo =
                    (TileRelativeTo)Enum.Parse(typeof(TileRelativeTo), "ThisTile");
                return Tuple.Create(offset, relativeTo);
            }
            catch
            {
                return ParseTileNative(text);
            }
        }

        private static string EvaluateText(string text, int index)
        {
            return EvaluateText(text, index, 0, 1);
        }

        private static string EvaluateText(string text, int index, int total)
        {
            return EvaluateText(text, index, total, 1);
        }

        private static string EvaluateText(
            string text,
            int index,
            int total,
            int readCount)
        {
            string trimmed = text.Trim();
            if (readCount <= 0)
                return text;

            StringBuilder result = new StringBuilder();
            int position = 0;
            int processed = 0;
            while (processed < readCount)
            {
                int open = trimmed.IndexOf('[', position);
                int close = open < 0 ? -1 : trimmed.IndexOf(']', open + 1);
                if (open < 0 || close < open + 2)
                    break;

                result.Append(trimmed.Substring(position, open - position));
                string expression = trimmed.Substring(open + 1, close - open - 1);
                int start;
                if (int.TryParse(
                    expression,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out start))
                {
                    result.Append((start + index).ToString(CultureInfo.InvariantCulture));
                }
                else
                {
                    double value = NumericExpression.Evaluate(
                        expression,
                        index,
                        total,
                        0);
                    result.Append(FormatTextNumber(value));
                }

                position = close + 1;
                processed++;
            }

            result.Append(trimmed.Substring(position));
            return result.ToString();
        }

        private static string FormatTextNumber(double value)
        {
            double integral = Math.Truncate(value);
            if (Math.Abs(value - integral) < 0.0000001)
                return ((long)integral).ToString(CultureInfo.InvariantCulture);
            return value.ToString("0.######", CultureInfo.InvariantCulture);
        }

        private static string DescribeRaw(Dictionary<string, object> raw)
        {
            List<string> parts = new List<string>();
            foreach (KeyValuePair<string, object> pair in raw)
            {
                parts.Add(pair.Key + "=" + DescribeValue(pair.Value));
            }
            return string.Join(" | ", parts.ToArray());
        }

        private static string DescribeValue(object value)
        {
            if (value == null)
                return "null";
            if (value is Vector2)
            {
                Vector2 vector = (Vector2)value;
                return "Vector2(" + vector.x + "," + vector.y + ")";
            }
            if (value is IDictionary)
                return "{...}";
            if (value is IEnumerable && !(value is string))
                return "[...]";
            return value.ToString();
        }

        private static int EvaluateInt(string expression, int index)
        {
            return (int)EvaluateNumber(expression, index, 0, 0);
        }

        private static int EvaluateInt(string expression, int index, int total)
        {
            return (int)EvaluateNumber(expression, index, total, 0);
        }

        private static double EvaluateNumber(string expression, int index)
        {
            return NumericExpression.Evaluate(expression, index, 0, 0);
        }

        private static double EvaluateNumber(string expression, int index, int total)
        {
            return NumericExpression.Evaluate(expression, index, total, 0);
        }

        private static double EvaluateNumber(
            string expression,
            int index,
            int total,
            double current)
        {
            return NumericExpression.Evaluate(expression, index, total, current);
        }

        private static double EvaluateAdjust(
            string expression,
            int index,
            int total,
            double current)
        {
            string trimmed = expression.Trim();
            if (trimmed.Length >= 2)
            {
                char operation = trimmed[0];
                if (operation == '+' || operation == '-' ||
                    operation == '*' || operation == '/')
                {
                    double operand;
                    if (double.TryParse(
                        trimmed.Substring(1).Trim(),
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out operand))
                    {
                        switch (operation)
                        {
                            case '+':
                                return current + operand;
                            case '-':
                                return current - operand;
                            case '*':
                                return current * operand;
                            case '/':
                                return current / operand;
                        }
                    }
                }
            }

            return NumericExpression.Evaluate(expression, index, total, current);
        }

        private static Vector2 ParseVector2(string text)
        {
            string trimmed = text.Trim().Trim('[', ']', '(', ')');
            string[] parts = trimmed.Split(',');
            if (parts.Length != 2)
                throw new ArgumentException("Vector2 字段需要填写 x,y 两个数字。");
            return new Vector2(
                float.Parse(parts[0].Trim(), CultureInfo.InvariantCulture),
                float.Parse(parts[1].Trim(), CultureInfo.InvariantCulture));
        }

        private static void AddRangeInputs()
        {
            int maxFloor = _editor != null ? Math.Max(0, _editor.floors.Count - 1) : 0;
            AddInputRow(
                "rangeStart",
                L("起始楼层", "Start floor"),
                Saved("rangeStart", "0"),
                PropertyType.Int);
            AddInputRow(
                "rangeEnd",
                L("结束楼层", "End floor"),
                Saved("rangeEnd", maxFloor.ToString(CultureInfo.InvariantCulture)),
                PropertyType.Int);
        }

        private static void AddBackButton(PanelMode target, string label)
        {
            AddButton(label, delegate { Goto(target); });
        }

        private static void Goto(PanelMode mode)
        {
            _mode = mode;
            Rebuild();
        }

        private static void AddHeader(string text)
        {
            ADOFAI.Property row = CreateRow(string.Empty);
            if (row.label != null)
                row.label.text = text;
            if (row.controlContainer != null)
                row.controlContainer.gameObject.SetActive(false);
        }

        private static void AddNote(string text)
        {
            ADOFAI.Property row = CreateRow(string.Empty);
            if (row.label != null)
                row.label.text = text;
            if (row.controlContainer != null)
                row.controlContainer.gameObject.SetActive(false);
        }

        private static void AddInputRow(string key, string label, string value, PropertyType type)
        {
            ADOFAI.Property row = CreateRow(label);
            if (row.controlContainer == null)
            {
                EditorIntegration.ReportStatus("属性行缺少控制容器: " + key);
                return;
            }
            GameObject controlObject = UnityEngine.Object.Instantiate(
                RDConstants.data.prefab_controlText);
            controlObject.transform.SetParent(row.controlContainer, false);

            PropertyControl_Text control =
                controlObject.GetComponent<PropertyControl_Text>();
            if (control.inputField != null)
                control.inputField.text = value ?? string.Empty;
            _rows.Add(new InputRow(key, control.inputField, type));
        }

        private static void AddFileInputRow(
            string key,
            string label,
            string value,
            FileType fileType)
        {
            ADOFAI.Property row = CreateRow(label);
            if (row.controlContainer == null)
            {
                EditorIntegration.ReportStatus("文件行缺少控制容器: " + key);
                return;
            }

            GameObject inputObject = UnityEngine.Object.Instantiate(
                RDConstants.data.prefab_controlText);
            inputObject.transform.SetParent(row.controlContainer, false);
            PropertyControl_Text inputControl =
                inputObject.GetComponent<PropertyControl_Text>();
            if (inputControl.inputField != null)
                inputControl.inputField.text = value ?? string.Empty;

            GameObject buttonObject = UnityEngine.Object.Instantiate(
                RDConstants.data.prefab_controlExport);
            buttonObject.transform.SetParent(row.controlContainer, false);
            PropertyControl_Export browseControl =
                buttonObject.GetComponent<PropertyControl_Export>();
            if (browseControl.exportButton != null)
            {
                browseControl.exportButton.onClick.RemoveAllListeners();
                browseControl.exportButton.onClick.AddListener(delegate
                {
                    string picked = ShowFilePicker(inputControl.inputField.text, fileType);
                    if (!string.IsNullOrEmpty(picked) && inputControl.inputField != null)
                        inputControl.inputField.text = picked;
                });
            }
            if (browseControl.buttonText != null)
                browseControl.buttonText.text = L("打开", "Open");

            _rows.Add(new InputRow(key, inputControl.inputField, PropertyType.String));
        }

        private static void AddEnumInputRow(
            string key,
            string label,
            string value,
            ADOFAI.PropertyInfo propertyInfo)
        {
            ADOFAI.Property row = CreateRow(label);
            if (row.controlContainer == null)
            {
                EditorIntegration.ReportStatus("枚举行缺少控制容器: " + key);
                return;
            }

            GameObject controlObject = UnityEngine.Object.Instantiate(
                RDConstants.data.prefab_controlToggle);
            controlObject.transform.SetParent(row.controlContainer, false);
            PropertyControl_Toggle control =
                controlObject.GetComponent<PropertyControl_Toggle>();
            if (control == null)
            {
                EditorIntegration.ReportStatus("枚举下拉控件缺失: " + key);
                return;
            }

            control.propertyInfo = propertyInfo;
            control.propertiesPanel = _panel;
            control.propertyTransform = control.GetComponent<RectTransform>();
            List<string> names = Enum.GetNames(propertyInfo.enumType).ToList();
            string enumTypeString = propertyInfo.enumTypeString;
            bool localizeEnumStrings = true;
            if (propertyInfo.enumType != null &&
                string.Equals(
                    propertyInfo.enumType.Name,
                    "Ease",
                    StringComparison.OrdinalIgnoreCase))
            {
                enumTypeString = "EventStudioEase";
                localizeEnumStrings = false;
                names = names
                    .Where(x => !string.Equals(x, "Unset", StringComparison.OrdinalIgnoreCase))
                    .Where(x => !x.StartsWith("INTERNAL_", StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            control.EnumSetup(
                enumTypeString,
                names,
                localizeEnumStrings,
                null,
                propertyInfo.browsable);
            control.Setup(true);

            TweakableDropdown dropdown = control.dropdown;
            if (!string.IsNullOrEmpty(value) && dropdown != null)
            {
                TweakableDropdownItem match = dropdown.items.FirstOrDefault(
                    item => item != null &&
                        string.Equals(item.value, value, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                    dropdown.SelectItem(match);
            }

            TMP_InputField enumInput =
                dropdown == null ? null : dropdown.inputField;
            _rows.Add(new InputRow(
                key,
                enumInput,
                PropertyType.Enum,
                delegate
                {
                    if (dropdown != null &&
                        dropdown.selectedItem != null &&
                        (dropdown.noItem == null || dropdown.selectedItem != dropdown.noItem) &&
                        !string.IsNullOrEmpty(dropdown.selectedItem.value))
                    {
                        return dropdown.selectedItem.value;
                    }
                    return string.Empty;
                }));
        }

        private static void AddColorInputRow(
            string key,
            string label,
            string value,
            ADOFAI.PropertyInfo propertyInfo)
        {
            ADOFAI.Property row = CreateRow(label);
            if (row.controlContainer == null)
            {
                EditorIntegration.ReportStatus("颜色行缺少控制容器: " + key);
                return;
            }

            GameObject controlObject = UnityEngine.Object.Instantiate(
                RDConstants.data.prefab_controlColor);
            controlObject.transform.SetParent(row.controlContainer, false);
            PropertyControl_Color control =
                controlObject.GetComponent<PropertyControl_Color>();
            if (control == null)
            {
                EditorIntegration.ReportStatus("颜色控件缺失: " + key);
                return;
            }

            control.propertyInfo = propertyInfo;
            control.propertiesPanel = _panel;
            control.propertyTransform = control.GetComponent<RectTransform>();
            control.Setup(true);
            if (!string.IsNullOrEmpty(value))
                control.text = value;

            TMP_InputField colorInput =
                control.colorField == null ? null : control.colorField.field;
            _rows.Add(new InputRow(
                key,
                colorInput,
                PropertyType.Color,
                delegate
                {
                    return control.text ?? string.Empty;
                }));
        }

        private static string ShowFilePicker(string current, FileType fileType)
        {
            if (fileType == FileType.Audio)
                return RDEditorUtils.ShowFileSelectorForAudio(current, 0);
            if (fileType == FileType.Video)
                return RDEditorUtils.ShowFileSelectorForVideo(current, 0);
            return RDEditorUtils.ShowFileSelectorForImage(current, 0);
        }

        private static void AddButton(string label, UnityAction action)
        {
            ADOFAI.Property row = CreateRow(string.Empty);
            if (row.controlContainer == null)
            {
                EditorIntegration.ReportStatus("按钮行缺少控制容器: " + label);
                return;
            }
            GameObject controlObject = UnityEngine.Object.Instantiate(
                RDConstants.data.prefab_controlExport);
            controlObject.transform.SetParent(row.controlContainer, false);

            PropertyControl_Export control =
                controlObject.GetComponent<PropertyControl_Export>();
            if (control.exportButton != null)
            {
                control.exportButton.onClick.RemoveAllListeners();
                control.exportButton.onClick.AddListener(action);
            }
            if (control.buttonText != null)
                control.buttonText.text = label;
        }

        private static ADOFAI.Property CreateRow(string labelText)
        {
            GameObject rowObject = UnityEngine.Object.Instantiate(
                RDConstants.data.prefab_property);
            rowObject.transform.SetParent(_panel.content, false);
            ADOFAI.Property row = rowObject.GetComponent<ADOFAI.Property>();
            if (row.label != null)
                row.label.text = labelText ?? string.Empty;

            if (row.helpButton != null)
                row.helpButton.gameObject.SetActive(false);
            if (row.randomButton != null)
                row.randomButton.gameObject.SetActive(false);
            if (row.enabledButton != null)
                row.enabledButton.gameObject.SetActive(false);
            return row;
        }

        private static void ClearContent()
        {
            if (_panel == null || _panel.content == null)
                return;
            while (_panel.content.childCount > 0)
            {
                Transform child = _panel.content.GetChild(0);
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }

        private static void CaptureInputs()
        {
            foreach (InputRow row in _rows)
            {
                _saved[_formKey + ":" + row.Key] = row.GetText();
            }
        }

        private static string Saved(string key, string fallback)
        {
            string stored;
            if (_saved.TryGetValue(_formKey + ":" + key, out stored))
                return stored;
            return fallback;
        }

        private static string GetString(string key, string fallback)
        {
            InputRow row = _rows.FirstOrDefault(x => x.Key == key);
            return row == null ? fallback : row.GetText();
        }

        private static string ReadDataString(LevelEvent levelEvent, string key)
        {
            object value;
            if (levelEvent.GetData().TryGetValue(key, out value) && value != null)
                return Convert.ToString(value, CultureInfo.InvariantCulture);
            return string.Empty;
        }

        private static void EnableField(LevelEvent levelEvent, string key)
        {
            if (levelEvent.disabled != null &&
                levelEvent.disabled.ContainsKey(key))
            {
                levelEvent.disabled[key] = false;
            }
        }

        private static string L(string chinese, string english)
        {
            return Localization.T(chinese, english);
        }

        private static int GetInt(string key, int fallback)
        {
            int value;
            if (!int.TryParse(GetString(key, string.Empty), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out value))
            {
                throw new ArgumentException(key + " 必须是整数。");
            }
            return value;
        }

        private static int GetTextReadCount()
        {
            int value;
            if (!int.TryParse(
                GetString("textReadCount", "1"),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value))
            {
                return 1;
            }
            return Math.Max(0, value);
        }

        private static LevelEventInfo GetEventInfo(LevelEventType eventType)
        {
            string key = eventType.ToString();
            LevelEventInfo info;
            if (GCS.levelEventsInfo == null ||
                !GCS.levelEventsInfo.TryGetValue(key, out info))
            {
                throw new InvalidOperationException("找不到事件参数表: " + key);
            }
            return info;
        }

        private static string EventTitle(LevelEventType eventType)
        {
            return Localization.EventTitle(eventType);
        }

        private static string PropertyLabel(ADOFAI.PropertyInfo propertyInfo)
        {
            return Localization.PropertyLabel(propertyInfo.levelEventInfo, propertyInfo);
        }

        private static Vector2 AsVector2(object value)
        {
            if (value is Vector2)
                return (Vector2)value;

            string text = value == null ? "NaN,NaN" : value.ToString();
            try
            {
                return ParseVector2(text);
            }
            catch
            {
                return new Vector2(float.NaN, float.NaN);
            }
        }

        private static string DefaultText(ADOFAI.PropertyInfo propertyInfo)
        {
            object value = propertyInfo.value_default;
            if (value == null)
                return string.Empty;
            if (value is string)
                return (string)value;
            if (value is bool)
                return ((bool)value) ? "true" : "false";
            if (value is float)
                return ((float)value).ToString(CultureInfo.InvariantCulture);
            if (value is double)
                return ((double)value).ToString(CultureInfo.InvariantCulture);
            if (value is int)
                return ((int)value).ToString(CultureInfo.InvariantCulture);
            if (value is Vector2)
            {
                Vector2 vector = (Vector2)value;
                return vector.x.ToString(CultureInfo.InvariantCulture) + "," +
                    vector.y.ToString(CultureInfo.InvariantCulture);
            }
            if (value is Enum)
                return value.ToString();
            return JsonConvert.SerializeObject(value);
        }
    }
}
