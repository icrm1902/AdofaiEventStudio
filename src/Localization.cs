using System.Collections.Generic;
using ADOFAI;
using UnityEngine;

namespace AdofaiEventStudio
{
    internal enum AppLanguage
    {
        Auto,
        Chinese,
        English
    }

    internal static class Localization
    {
        private static AppLanguage _mode = AppLanguage.Auto;

        public static AppLanguage Mode { get { return _mode; } }

        public static void SetMode(AppLanguage mode)
        {
            _mode = mode;
        }

        public static bool IsChinese
        {
            get
            {
                if (_mode == AppLanguage.Chinese)
                    return true;
                if (_mode == AppLanguage.English)
                    return false;
                return GameLanguageIsChinese();
            }
        }

        public static string T(string chinese, string english)
        {
            return IsChinese ? chinese : english;
        }

        public static string EventName(string eventKey)
        {
            if (string.IsNullOrEmpty(eventKey))
                return eventKey;

            bool found;
            string localized = RDString.GetWithCheck(
                "editor." + eventKey,
                out found,
                null);
            return found && !string.IsNullOrEmpty(localized)
                ? localized
                : eventKey;
        }

        public static string EventTitle(LevelEventType eventType)
        {
            string key;
            if (GCS.levelEventTypeString != null &&
                GCS.levelEventTypeString.TryGetValue(eventType, out key) &&
                !string.IsNullOrEmpty(key))
            {
                return EventName(key);
            }
            return EventName(eventType.ToString());
        }

        public static string PropertyLabel(LevelEventInfo info, ADOFAI.PropertyInfo propertyInfo)
        {
            bool found;
            string localized = RDString.GetWithCheck(
                "editor." + info.name + "." + propertyInfo.name,
                out found,
                null);
            if (found && !string.IsNullOrEmpty(localized))
                return localized;

            if (!string.IsNullOrEmpty(propertyInfo.customLabel))
                return propertyInfo.customLabel;
            return propertyInfo.name;
        }

        private static bool GameLanguageIsChinese()
        {
            SystemLanguage language = Persistence.language;
            return language == SystemLanguage.Chinese ||
                language == SystemLanguage.ChineseSimplified ||
                language == SystemLanguage.ChineseTraditional;
        }
    }
}
