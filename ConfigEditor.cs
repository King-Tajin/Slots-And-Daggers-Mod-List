using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using TMPro;
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
            UiFactory.AddText(false, "<alpha=#99>Changes are written to the .cfg file right away. Some mods only read their settings at startup, so restart the game for those.<alpha=#FF>");

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
                UiFactory.AddText(false, "<b><size=120%>" + UiFactory.Plain(section) + "</size></b>");

                foreach (ConfigEntryBase entry in bySection[section].OrderByDescending(OrderOf))
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

        private static object ReadTag(ConfigEntryBase entry, string fieldName)
        {
            object[] tags = entry.Description != null ? entry.Description.Tags : null;
            if (tags == null)
            {
                return null;
            }

            foreach (object tag in tags)
            {
                if (tag == null)
                {
                    continue;
                }

                FieldInfo field = tag.GetType().GetField(fieldName);
                if (field == null)
                {
                    continue;
                }

                object value = field.GetValue(tag);
                if (value != null)
                {
                    return value;
                }
            }

            return null;
        }

        private static bool IsBrowsable(ConfigEntryBase entry)
        {
            return !(ReadTag(entry, "Browsable") is bool browsable) || browsable;
        }

        private static bool IsReadOnly(ConfigEntryBase entry)
        {
            return ReadTag(entry, "ReadOnly") is bool readOnly && readOnly;
        }

        private static int OrderOf(ConfigEntryBase entry)
        {
            return ReadTag(entry, "Order") is int order ? order : 0;
        }

        private static void AddEntry(ConfigEntryBase entry)
        {
            string label = "<b>" + UiFactory.Plain(entry.Definition.Key) + "</b>" + Details(entry);
            EntryControl control = CreateControl(entry);

            if (IsReadOnly(entry))
            {
                control.Selectable.interactable = false;
                UiFactory.AddEntryRow(label, control.Root);
                return;
            }

            Button reset = UiFactory.CreateSmallButton("Reset", () =>
            {
                Apply(entry, entry.DefaultValue);
                control.Refresh();
            });

            UiFactory.AddEntryRow(label, control.Root, reset.gameObject);
        }

        private static void Commit(ConfigEntryBase entry)
        {
            if (!entry.ConfigFile.SaveOnConfigSet)
            {
                entry.ConfigFile.Save();
            }
        }

        private static void Apply(ConfigEntryBase entry, object value)
        {
            try
            {
                entry.BoxedValue = value;
                Commit(entry);
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
                text = UiFactory.Plain(entry.Description.Description);
            }

            string defaultValue = DefaultText(entry);
            if (defaultValue != null)
            {
                text += (text.Length > 0 ? "  " : string.Empty) + "Default: " + UiFactory.Plain(defaultValue);
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
            if (entry.SettingType == typeof(bool) && UiFactory.HasToggle)
            {
                return CreateToggle(entry);
            }

            List<object> options = GetOptions(entry);
            return options != null ? CreateCycleButton(entry, options) : CreateInput(entry);
        }

        private static EntryControl CreateToggle(ConfigEntryBase entry)
        {
            Toggle toggle = UiFactory.CreateControlToggle((bool)entry.BoxedValue, isOn => Apply(entry, isOn));

            return new EntryControl
            {
                Root = toggle.gameObject,
                Selectable = toggle,
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
            button = UiFactory.CreateControlButton(entry.GetSerializedValue(), () =>
            {
                int index = IndexOf(options, entry.BoxedValue);
                Apply(entry, options[(index + 1) % options.Count]);
                UiFactory.SetButtonLabel(button, entry.GetSerializedValue());
            });

            return new EntryControl
            {
                Root = button.gameObject,
                Selectable = button,
                Refresh = () => UiFactory.SetButtonLabel(button, entry.GetSerializedValue())
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
            input = UiFactory.CreateInputField(entry.GetSerializedValue(), text =>
            {
                if (text != entry.GetSerializedValue())
                {
                    try
                    {
                        entry.SetSerializedValue(text);
                        Commit(entry);
                    }
                    catch (System.Exception e)
                    {
                        log.LogWarning("Could not set " + entry.Definition.Key + " to \"" + text + "\": " + e.Message);
                    }
                }

                input.SetTextWithoutNotify(entry.GetSerializedValue());
            });

            return new EntryControl
            {
                Root = input.gameObject,
                Selectable = input,
                Refresh = () => input.SetTextWithoutNotify(entry.GetSerializedValue())
            };
        }

        private sealed class EntryControl
        {
            internal UnityEngine.GameObject Root;
            internal Selectable Selectable;
            internal System.Action Refresh;
        }
    }
}
