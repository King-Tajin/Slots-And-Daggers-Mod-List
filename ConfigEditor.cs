using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ModList
{
    internal static class ConfigEditor
    {
        private static ManualLogSource log;

        internal static void Initialize(ManualLogSource logger)
        {
            log = logger;
        }

        internal static int CountEntries(PluginInfo info)
        {
            return GetVisibleEntries(info).Count;
        }

        internal static void Populate(PluginInfo info)
        {
            ModListScreen.AddText(false, "<alpha=#99>Changes are written to the .cfg file right away. Some mods only read their settings at startup, so restart the game for those.<alpha=#FF>");

            List<string> sections = new List<string>();
            Dictionary<string, List<ConfigEntryBase>> bySection = new Dictionary<string, List<ConfigEntryBase>>();

            foreach (ConfigEntryBase entry in GetVisibleEntries(info))
            {
                string section = entry.Definition.Section;
                if (!bySection.ContainsKey(section))
                {
                    bySection[section] = new List<ConfigEntryBase>();
                    sections.Add(section);
                }

                bySection[section].Add(entry);
            }

            foreach (string section in sections)
            {
                ModListScreen.AddText(false, "<b><size=120%>" + ModListScreen.Plain(section) + "</size></b>");

                foreach (ConfigEntryBase entry in bySection[section])
                {
                    AddEntry(entry);
                }
            }
        }

        private static List<ConfigEntryBase> GetVisibleEntries(PluginInfo info)
        {
            List<ConfigEntryBase> entries = new List<ConfigEntryBase>();

            if (info.Instance == null || info.Instance.Config == null)
            {
                return entries;
            }

            ConfigFile config = info.Instance.Config;
            foreach (ConfigDefinition definition in new List<ConfigDefinition>(config.Keys))
            {
                ConfigEntryBase entry = config[definition];
                if (IsBrowsable(entry))
                {
                    entries.Add(entry);
                }
            }

            return entries;
        }

        private static bool IsBrowsable(ConfigEntryBase entry)
        {
            object[] tags = entry.Description != null ? entry.Description.Tags : null;
            if (tags == null)
            {
                return true;
            }

            foreach (object tag in tags)
            {
                if (tag == null)
                {
                    continue;
                }

                FieldInfo field = tag.GetType().GetField("Browsable");
                if (field != null && field.GetValue(tag) is bool browsable && !browsable)
                {
                    return false;
                }
            }

            return true;
        }

        private static void AddEntry(ConfigEntryBase entry)
        {
            string label = "<b>" + ModListScreen.Plain(entry.Definition.Key) + "</b>" + Details(entry);
            EntryControl control = CreateControl(entry);

            Button reset = ModListScreen.CreateSmallButton("Reset", () =>
            {
                Apply(entry, entry.DefaultValue);
                control.Refresh();
            });

            ModListScreen.AddEntryRow(label, control.Root, reset.gameObject);
        }

        private static void Apply(ConfigEntryBase entry, object value)
        {
            try
            {
                entry.BoxedValue = value;
                entry.ConfigFile.Save();
            }
            catch (System.Exception e)
            {
                log.LogWarning("Could not change " + entry.Definition.Key + ": " + e.Message);
            }
        }

        private static string Details(ConfigEntryBase entry)
        {
            string text = string.Empty;

            if (entry.Description != null && !string.IsNullOrEmpty(entry.Description.Description))
            {
                text = ModListScreen.Plain(entry.Description.Description);
            }

            string defaultValue = DefaultText(entry);
            if (defaultValue != null)
            {
                text += (text.Length > 0 ? "  " : string.Empty) + "Default: " + ModListScreen.Plain(defaultValue);
            }

            return text.Length == 0 ? string.Empty : "\n<size=70%><alpha=#99>" + text + "<alpha=#FF></size>";
        }

        private static string DefaultText(ConfigEntryBase entry)
        {
            try
            {
                return TomlTypeConverter.ConvertToString(entry.DefaultValue, entry.SettingType);
            }
            catch
            {
                return null;
            }
        }

        private static EntryControl CreateControl(ConfigEntryBase entry)
        {
            if (entry.SettingType == typeof(bool) && ModListScreen.HasToggle)
            {
                return CreateToggle(entry);
            }

            List<object> options = GetOptions(entry);
            return options != null ? CreateCycleButton(entry, options) : CreateInput(entry);
        }

        private static EntryControl CreateToggle(ConfigEntryBase entry)
        {
            Toggle toggle = ModListScreen.CreateControlToggle((bool)entry.BoxedValue, isOn => Apply(entry, isOn));

            return new EntryControl
            {
                Root = toggle.gameObject,
                Refresh = () => toggle.SetIsOnWithoutNotify((bool)entry.BoxedValue)
            };
        }

        private static List<object> GetOptions(ConfigEntryBase entry)
        {
            System.Type type = entry.SettingType;

            if (type == typeof(bool))
            {
                return new List<object> { false, true };
            }

            if (type.IsEnum && !type.IsDefined(typeof(System.FlagsAttribute), false))
            {
                List<object> names = new List<object>();
                foreach (object value in System.Enum.GetValues(type))
                {
                    names.Add(value);
                }

                return names;
            }

            AcceptableValueBase acceptable = entry.Description != null ? entry.Description.AcceptableValues : null;
            if (acceptable != null && acceptable.GetType().Name.StartsWith("AcceptableValueList"))
            {
                PropertyInfo property = acceptable.GetType().GetProperty("AcceptableValues");
                System.Array values = property != null ? property.GetValue(acceptable, null) as System.Array : null;

                if (values != null && values.Length > 0)
                {
                    List<object> options = new List<object>();
                    foreach (object value in values)
                    {
                        options.Add(value);
                    }

                    return options;
                }
            }

            return null;
        }

        private static EntryControl CreateCycleButton(ConfigEntryBase entry, List<object> options)
        {
            Button button = null;
            button = ModListScreen.CreateControlButton(entry.GetSerializedValue(), () =>
            {
                int index = IndexOf(options, entry.BoxedValue);
                Apply(entry, options[(index + 1) % options.Count]);
                ModListScreen.SetButtonLabel(button, entry.GetSerializedValue());
            });

            return new EntryControl
            {
                Root = button.gameObject,
                Refresh = () => ModListScreen.SetButtonLabel(button, entry.GetSerializedValue())
            };
        }

        private static int IndexOf(List<object> options, object current)
        {
            for (int i = 0; i < options.Count; i++)
            {
                if (Equals(options[i], current))
                {
                    return i;
                }
            }

            return -1;
        }

        private static EntryControl CreateInput(ConfigEntryBase entry)
        {
            TMP_InputField input = null;
            input = ModListScreen.CreateInputField(entry.GetSerializedValue(), text =>
            {
                try
                {
                    entry.SetSerializedValue(text);
                    entry.ConfigFile.Save();
                }
                catch (System.Exception e)
                {
                    log.LogWarning("Could not set " + entry.Definition.Key + " to \"" + text + "\": " + e.Message);
                }

                input.SetTextWithoutNotify(entry.GetSerializedValue());
            });

            return new EntryControl
            {
                Root = input.gameObject,
                Refresh = () => input.SetTextWithoutNotify(entry.GetSerializedValue())
            };
        }

        private sealed class EntryControl
        {
            internal GameObject Root;
            internal System.Action Refresh;
        }
    }
}
