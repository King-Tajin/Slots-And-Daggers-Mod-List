using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ModList
{
    internal static class ModListScreen
    {
        private const string ScreenName = "ModListScreen";
        private const string ScreenTitle = "Mod List";
        private const string HeadingText = "Installed Mods";
        private const float RowSpacing = 18f;
        private const float EntrySpacing = 24f;
        private const float ControlWidth = 360f;
        private const float SmallButtonWidth = 180f;
        private const float ControlHeight = 64f;
        private const float WidthFraction = 0.85f;
        private const float ScrollbarMargin = 30f;
        private const int ContentPadding = 40;
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private static readonly FieldInfo RootTransformField = typeof(UIScreenUGUI).GetField("rootTransform", Flags);
        private static readonly FieldInfo CurrentScreenField = typeof(UIManager).GetField("currentScreen", Flags);
        private static readonly FieldInfo AllScreensField = typeof(UIManager).GetField("allScreens", Flags);
        private static readonly FieldInfo BackButtonField = typeof(CreditsScreenUGUI).GetField("m_BackButton", Flags);
        private static readonly string[] SettingsToggleFields = { "toggleFullscreen", "toggleCameraMotion", "toggleFastMode", "toggleShowTutorialsAgain" };
        private static readonly MethodInfo ShowMethod = typeof(UIManager).GetMethod("Show", Flags, null, new[] { typeof(UIScreenUGUI), typeof(bool) }, null);

        private static ManualLogSource log;
        private static bool dumpHierarchy;
        private static bool buildFailed;
        private static bool inConfig;
        private static CreditsScreenUGUI screen;
        private static RectTransform content;
        private static ScrollRect scroll;
        private static GameObject headingTemplate;
        private static GameObject bodyTemplate;
        private static GameObject buttonTemplate;
        private static GameObject toggleTemplate;

        internal static bool HasToggle
        {
            get { return toggleTemplate != null; }
        }

        internal static void Initialize(ManualLogSource logger, bool dump)
        {
            log = logger;
            dumpHierarchy = dump;
            ConfigEditor.Initialize(logger);

            if (RootTransformField == null || AllScreensField == null || ShowMethod == null)
            {
                log.LogWarning("UIManager or UIScreenUGUI members were not found, the mod list cannot open.");
            }
        }

        internal static void Open()
        {
            if (Main.UIManager == null || RootTransformField == null || AllScreensField == null || ShowMethod == null)
            {
                log.LogWarning("The UI manager is not ready, the mod list cannot open.");
                return;
            }

            if (screen == null && !Build())
            {
                return;
            }

            Register();
            ShowList(false);
            ShowMethod.Invoke(Main.UIManager, new object[] { screen, true });
        }

        internal static bool TryGoBack()
        {
            if (!inConfig || screen == null || Main.UIManager == null || CurrentScreenField == null)
            {
                return false;
            }

            UIScreenUGUI current = CurrentScreenField.GetValue(Main.UIManager) as UIScreenUGUI;
            if (current != screen)
            {
                return false;
            }

            ShowList(true);
            return true;
        }

        private static bool Build()
        {
            if (buildFailed)
            {
                return false;
            }

            CreditsScreenUGUI source = FindSource();
            if (source == null)
            {
                return Fail("The credits screen was not found.");
            }

            RectTransform sourceRoot = RootTransformField.GetValue(source) as RectTransform;
            if (sourceRoot == null)
            {
                return Fail("The credits screen has no root panel.");
            }

            GameObject holder = new GameObject("ModListHolder", typeof(RectTransform));
            holder.SetActive(false);
            GameObject screenClone = null;
            GameObject panelClone = null;

            try
            {
                screenClone = Object.Instantiate(source.gameObject, holder.transform);
                screenClone.name = ScreenName;
                CreditsScreenUGUI cloneScreen = screenClone.GetComponent<CreditsScreenUGUI>();

                RectTransform cloneRoot;
                if (sourceRoot.IsChildOf(source.transform))
                {
                    cloneRoot = RootTransformField.GetValue(cloneScreen) as RectTransform;
                }
                else
                {
                    panelClone = Object.Instantiate(sourceRoot.gameObject, holder.transform);
                    panelClone.name = ScreenName + "Panel";
                    cloneRoot = (RectTransform)panelClone.transform;
                    Remap(cloneScreen, sourceRoot, cloneRoot);
                    RootTransformField.SetValue(cloneScreen, cloneRoot);
                }

                if (cloneRoot == null || cloneRoot.gameObject == screenClone)
                {
                    return Fail("The credits screen layout is not supported.");
                }

                if (dumpHierarchy)
                {
                    HierarchyDumper.Dump(log, cloneRoot);
                }

                ScrollRect cloneScroll = cloneRoot.GetComponentInChildren<ScrollRect>(true);
                if (cloneScroll == null || cloneScroll.content == null)
                {
                    return Fail("The credits panel has no scroll view to reuse. Enable the development option DumpCreditsHierarchy in the config for details.");
                }

                Button backButton = BackButtonField != null ? BackButtonField.GetValue(cloneScreen) as Button : null;
                if (backButton == null)
                {
                    return Fail("The credits screen has no Back button to copy the style from. Enable the development option DumpCreditsHierarchy in the config for details.");
                }

                TMP_Text[] texts = cloneScroll.content.GetComponentsInChildren<TMP_Text>(true);
                if (texts.Length == 0)
                {
                    return Fail("The credits scroll view has no text to copy the style from. Enable the development option DumpCreditsHierarchy in the config for details.");
                }

                System.Array.Sort(texts, (a, b) => a.fontSize.CompareTo(b.fontSize));
                headingTemplate = CreateTemplate(texts[texts.Length - 1].gameObject, screenClone.transform);
                bodyTemplate = CreateTemplate(texts[texts.Length / 2].gameObject, screenClone.transform);
                buttonTemplate = CreateTemplate(backButton.gameObject, screenClone.transform);

                Toggle settingsToggle = FindSettingsToggle();
                if (settingsToggle != null)
                {
                    toggleTemplate = CreateTemplate(settingsToggle.gameObject, screenClone.transform);

                    if (dumpHierarchy)
                    {
                        HierarchyDumper.Dump(log, toggleTemplate.transform);
                    }
                }
                else
                {
                    log.LogWarning("The settings menu checkbox was not found, on/off settings will use a button instead.");
                }

                RectTransform listContent = cloneScroll.content;
                ClearChildren(listContent);
                ConfigureLayout(listContent);
                Retitle(cloneRoot, listContent);

                foreach (CanvasGroup group in cloneRoot.GetComponents<CanvasGroup>())
                {
                    Object.DestroyImmediate(group);
                }

                cloneRoot.gameObject.SetActive(false);

                if (panelClone != null)
                {
                    panelClone.transform.SetParent(sourceRoot.parent, false);
                    panelClone.transform.SetSiblingIndex(sourceRoot.GetSiblingIndex() + 1);
                }

                screenClone.transform.SetParent(source.transform.parent, false);
                screenClone.transform.SetSiblingIndex(source.transform.GetSiblingIndex() + 1);
                screenClone.SetActive(true);
                cloneRoot.gameObject.SetActive(false);

                Widen(cloneRoot, cloneScroll);

                CanvasGroup rootGroup = cloneRoot.GetComponent<CanvasGroup>();
                if (rootGroup != null)
                {
                    rootGroup.alpha = 0f;
                    rootGroup.interactable = false;
                    rootGroup.blocksRaycasts = false;
                }

                screen = cloneScreen;
                scroll = cloneScroll;
                content = listContent;
                log.LogDebug("Built the mod list screen");
                return true;
            }
            catch (System.Exception e)
            {
                buildFailed = true;
                log.LogError(e);

                if (screenClone != null)
                {
                    Object.Destroy(screenClone);
                }

                if (panelClone != null)
                {
                    Object.Destroy(panelClone);
                }

                return false;
            }
            finally
            {
                Object.Destroy(holder);
            }
        }

        private static void Remap(CreditsScreenUGUI cloneScreen, Transform sourceRoot, Transform cloneRoot)
        {
            foreach (System.Type type in new[] { typeof(UIScreenUGUI), typeof(UIMenuScreenUGUI), typeof(CreditsScreenUGUI) })
            {
                foreach (FieldInfo field in type.GetFields(Flags | BindingFlags.DeclaredOnly))
                {
                    if (typeof(Component).IsAssignableFrom(field.FieldType))
                    {
                        Component mapped = MapComponent(field.GetValue(cloneScreen) as Component, field.FieldType, sourceRoot, cloneRoot);
                        if (mapped != null)
                        {
                            field.SetValue(cloneScreen, mapped);
                        }
                    }
                    else if (field.FieldType == typeof(List<Selectable>))
                    {
                        List<Selectable> list = field.GetValue(cloneScreen) as List<Selectable>;
                        if (list == null)
                        {
                            continue;
                        }

                        List<Selectable> remapped = new List<Selectable>();
                        foreach (Selectable item in list)
                        {
                            Selectable mapped = MapComponent(item, typeof(Selectable), sourceRoot, cloneRoot) as Selectable;
                            remapped.Add(mapped != null ? mapped : item);
                        }

                        field.SetValue(cloneScreen, remapped);
                    }
                }
            }
        }

        private static Component MapComponent(Component original, System.Type type, Transform sourceRoot, Transform cloneRoot)
        {
            if (original == null || !original.transform.IsChildOf(sourceRoot))
            {
                return null;
            }

            List<int> path = new List<int>();
            Transform node = original.transform;
            while (node != sourceRoot)
            {
                path.Add(node.GetSiblingIndex());
                node = node.parent;
            }

            Transform mapped = cloneRoot;
            for (int i = path.Count - 1; i >= 0; i--)
            {
                mapped = mapped.GetChild(path[i]);
            }

            return mapped.GetComponent(type);
        }

        private static bool Fail(string message)
        {
            buildFailed = true;
            log.LogWarning(message + " The mod list is disabled until the game restarts.");
            return false;
        }

        private static CreditsScreenUGUI FindSource()
        {
            foreach (CreditsScreenUGUI candidate in Object.FindObjectsByType<CreditsScreenUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (candidate.gameObject.name != ScreenName)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static Toggle FindSettingsToggle()
        {
            foreach (SettingsMenuUGUI menu in Object.FindObjectsByType<SettingsMenuUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                foreach (string fieldName in SettingsToggleFields)
                {
                    FieldInfo field = typeof(SettingsMenuUGUI).GetField(fieldName, Flags);
                    Toggle toggle = field != null ? field.GetValue(menu) as Toggle : null;
                    if (toggle != null)
                    {
                        return toggle;
                    }
                }
            }

            return null;
        }

        private static GameObject CreateTemplate(GameObject source, Transform parent)
        {
            GameObject template = Object.Instantiate(source, parent);
            template.name = "ModListRowTemplate";
            template.SetActive(false);
            StripComponents(template);

            foreach (LayoutElement element in template.GetComponents<LayoutElement>())
            {
                Object.DestroyImmediate(element);
            }

            foreach (ContentSizeFitter fitter in template.GetComponents<ContentSizeFitter>())
            {
                Object.DestroyImmediate(fitter);
            }

            return template;
        }

        private static void StripComponents(GameObject target)
        {
            foreach (MonoBehaviour behaviour in target.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null)
                {
                    continue;
                }

                string typeName = behaviour.GetType().Name;
                if (typeName == "LocalizeStringEvent" || typeName.Contains("Typewriter"))
                {
                    Object.DestroyImmediate(behaviour);
                }
            }
        }

        private static void ClearChildren(RectTransform target)
        {
            for (int i = target.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(target.GetChild(i).gameObject);
            }
        }

        private static void Widen(RectTransform panel, ScrollRect scrollRect)
        {
            Canvas canvas = panel.GetComponentInParent<Canvas>(true);
            RectTransform canvasRect = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
            if (canvasRect == null)
            {
                return;
            }

            float width = canvasRect.rect.width * WidthFraction;
            if (width <= 0f)
            {
                return;
            }

            RectTransform view = (RectTransform)scrollRect.transform;
            RectTransform wrapper = view.parent as RectTransform;

            float oversize = wrapper != null ? view.rect.width - wrapper.rect.width : 0f;
            if (wrapper != null)
            {
                Resize(wrapper, width);
            }

            Resize(view, width + oversize);

            Scrollbar bar = scrollRect.verticalScrollbar;
            RectTransform barRect = bar != null ? bar.transform as RectTransform : null;
            if (barRect != null)
            {
                barRect.pivot = new Vector2(1f, barRect.pivot.y);

                ScrollbarPinner pinner = barRect.gameObject.AddComponent<ScrollbarPinner>();
                pinner.CanvasRect = canvasRect;
                pinner.Margin = ScrollbarMargin;
            }
        }

        private static float Resize(RectTransform target, float width)
        {
            float oldWidth = target.rect.width;
            float left = target.anchoredPosition.x - target.pivot.x * oldWidth;

            target.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            target.anchoredPosition = new Vector2(left + target.pivot.x * width, target.anchoredPosition.y);

            return width - oldWidth;
        }

        private static void ConfigureLayout(RectTransform target)
        {
            target.anchorMin = new Vector2(0f, 1f);
            target.anchorMax = new Vector2(1f, 1f);
            target.pivot = new Vector2(0.5f, 1f);
            target.sizeDelta = new Vector2(0f, target.sizeDelta.y);
            target.anchoredPosition = Vector2.zero;

            foreach (LayoutGroup group in target.GetComponents<LayoutGroup>())
            {
                if (!(group is VerticalLayoutGroup))
                {
                    Object.DestroyImmediate(group);
                }
            }

            VerticalLayoutGroup vertical = target.GetComponent<VerticalLayoutGroup>();
            if (vertical == null)
            {
                vertical = target.gameObject.AddComponent<VerticalLayoutGroup>();
            }

            vertical.padding = new RectOffset(ContentPadding, ContentPadding, ContentPadding, ContentPadding);
            vertical.spacing = RowSpacing;
            vertical.childControlWidth = true;
            vertical.childControlHeight = true;
            vertical.childForceExpandWidth = true;
            vertical.childForceExpandHeight = false;

            ContentSizeFitter fitter = target.GetComponent<ContentSizeFitter>();
            if (fitter == null)
            {
                fitter = target.gameObject.AddComponent<ContentSizeFitter>();
            }

            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private static void Retitle(RectTransform screenRoot, RectTransform listContent)
        {
            TMP_Text byName = null;
            TMP_Text largest = null;

            foreach (TMP_Text label in screenRoot.GetComponentsInChildren<TMP_Text>(true))
            {
                if (label.transform.IsChildOf(listContent) || label.gameObject.name == "ModListRowTemplate" || label.GetComponentInParent<Button>(true) != null)
                {
                    continue;
                }

                if (byName == null && label.text.IndexOf("credit", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    byName = label;
                }

                if (largest == null || label.fontSize > largest.fontSize)
                {
                    largest = label;
                }
            }

            TMP_Text title = byName != null ? byName : largest;
            if (title == null)
            {
                log.LogDebug("No title label was found on the mod list screen.");
                return;
            }

            StripComponents(title.gameObject);
            title.text = ScreenTitle;
        }

        private static void Register()
        {
            List<UIScreenUGUI> screens = AllScreensField.GetValue(Main.UIManager) as List<UIScreenUGUI>;
            if (screens != null && !screens.Contains(screen))
            {
                screens.Add(screen);
            }
        }

        private static void ShowList(bool focus)
        {
            inConfig = false;
            DiscardChildren(content);

            List<PluginInfo> plugins = new List<PluginInfo>(Chainloader.PluginInfos.Values);
            plugins.Sort((a, b) => string.Compare(a.Metadata.Name, b.Metadata.Name, System.StringComparison.OrdinalIgnoreCase));

            AddText(true, HeadingText + " (" + plugins.Count + ")");

            foreach (PluginInfo info in plugins)
            {
                string description = Describe(info);

                if (ConfigEditor.CountEntries(info) > 0)
                {
                    PluginInfo selected = info;
                    Button config = CreateSmallButton("Config", () => ShowConfig(selected, true));
                    AddEntryRow(description, config.gameObject);
                }
                else
                {
                    AddEntryRow(description);
                }
            }

            FinishView(focus);
        }

        private static void ShowConfig(PluginInfo info, bool focus)
        {
            inConfig = true;
            DiscardChildren(content);

            AddText(true, Plain(info.Metadata.Name) + " Settings");
            ConfigEditor.Populate(info);

            FinishView(focus);
        }

        private static void FinishView(bool focus)
        {
            scroll.verticalNormalizedPosition = 1f;

            if (!focus || EventSystem.current == null)
            {
                return;
            }

            Button first = content.GetComponentInChildren<Button>(false);
            if (first != null)
            {
                EventSystem.current.SetSelectedGameObject(first.gameObject);
            }
        }

        private static void DiscardChildren(RectTransform target)
        {
            for (int i = target.childCount - 1; i >= 0; i--)
            {
                GameObject child = target.GetChild(i).gameObject;
                child.SetActive(false);
                Object.Destroy(child);
            }
        }

        private static TMP_Text CreateText(Transform parent, GameObject template, string text)
        {
            GameObject instance = Object.Instantiate(template, parent);
            instance.name = "ModListRow";
            instance.SetActive(true);

            TMP_Text label = instance.GetComponent<TMP_Text>();
            label.richText = true;
            label.raycastTarget = false;
            label.text = text;
            return label;
        }

        internal static TMP_Text AddText(bool heading, string text)
        {
            return CreateText(content, heading ? headingTemplate : bodyTemplate, text);
        }

        internal static Button CreateControlButton(string label, UnityAction onClick)
        {
            return CreateSizedButton(label, onClick, ControlWidth);
        }

        internal static Button CreateSmallButton(string label, UnityAction onClick)
        {
            return CreateSizedButton(label, onClick, SmallButtonWidth);
        }

        private static Button CreateSizedButton(string label, UnityAction onClick, float width)
        {
            Button button = CreateButton(label, onClick);
            LayoutElement element = button.gameObject.AddComponent<LayoutElement>();
            element.minWidth = width;
            element.preferredWidth = width;
            element.flexibleWidth = 0f;
            element.minHeight = ControlHeight;
            element.preferredHeight = ControlHeight;
            return button;
        }

        internal static Toggle CreateControlToggle(bool value, UnityAction<bool> onChanged)
        {
            GameObject instance = Object.Instantiate(toggleTemplate, content);
            instance.name = "ModListToggle";

            foreach (TMP_Text label in instance.GetComponentsInChildren<TMP_Text>(true))
            {
                if (label.gameObject != instance)
                {
                    label.gameObject.SetActive(false);
                }
            }

            LayoutElement element = instance.AddComponent<LayoutElement>();
            element.minWidth = SmallButtonWidth;
            element.preferredWidth = SmallButtonWidth;
            element.flexibleWidth = 0f;
            element.minHeight = ControlHeight;
            element.preferredHeight = ControlHeight;

            Toggle toggle = instance.GetComponent<Toggle>();
            toggle.onValueChanged = new Toggle.ToggleEvent();
            toggle.SetIsOnWithoutNotify(value);
            toggle.onValueChanged.AddListener(onChanged);
            return toggle;
        }

        internal static void SetButtonLabel(Button button, string text)
        {
            foreach (TMP_Text label in button.GetComponentsInChildren<TMP_Text>(true))
            {
                label.richText = true;
                label.text = text;
            }
        }

        private static Button CreateButton(string label, UnityAction onClick)
        {
            GameObject instance = Object.Instantiate(buttonTemplate, content);
            instance.name = "ModListButton";
            instance.SetActive(true);

            Button button = instance.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(onClick);
            SetButtonLabel(button, label);
            return button;
        }

        internal static TMP_InputField CreateInputField(string value, UnityAction<string> onEndEdit)
        {
            TMP_Text style = bodyTemplate.GetComponent<TMP_Text>();

            GameObject root = new GameObject("ModListInput", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            root.SetActive(false);
            root.transform.SetParent(content, false);

            Image background = root.GetComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.45f);

            LayoutElement element = root.GetComponent<LayoutElement>();
            element.minWidth = ControlWidth;
            element.preferredWidth = ControlWidth;
            element.flexibleWidth = 0f;
            element.minHeight = ControlHeight;
            element.preferredHeight = ControlHeight;

            GameObject areaObject = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
            RectTransform area = (RectTransform)areaObject.transform;
            area.SetParent(root.transform, false);
            area.anchorMin = Vector2.zero;
            area.anchorMax = Vector2.one;
            area.offsetMin = new Vector2(12f, 6f);
            area.offsetMax = new Vector2(-12f, -6f);

            GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform textRect = (RectTransform)textObject.transform;
            textRect.SetParent(area, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = style.font;
            text.fontSharedMaterial = style.fontSharedMaterial;
            text.fontSize = style.fontSize;
            text.color = style.color;
            text.enableAutoSizing = false;
            text.alignment = TextAlignmentOptions.Left;

            TMP_InputField input = root.AddComponent<TMP_InputField>();
            input.textViewport = area;
            input.textComponent = text;
            input.targetGraphic = background;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.richText = false;
            input.SetTextWithoutNotify(value);
            input.onEndEdit.AddListener(onEndEdit);
            return input;
        }

        internal static void AddEntryRow(string labelText, params GameObject[] controls)
        {
            GameObject row = new GameObject("ModListEntry", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(content, false);

            HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = EntrySpacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            TMP_Text label = CreateText(row.transform, bodyTemplate, labelText);
            label.alignment = TextAlignmentOptions.TopLeft;

            LayoutElement labelElement = label.gameObject.AddComponent<LayoutElement>();
            labelElement.flexibleWidth = 1f;
            labelElement.minWidth = 120f;

            foreach (GameObject control in controls)
            {
                control.transform.SetParent(row.transform, false);
                control.SetActive(true);
            }
        }

        private static string Describe(PluginInfo info)
        {
            bool active = info.Instance != null && info.Instance.enabled;

            string line = "<b>" + Plain(info.Metadata.Name) + "</b>  <alpha=#99>v" + info.Metadata.Version + "<alpha=#FF>";
            if (!active)
            {
                line += "  <color=#FF6B6B>Disabled</color>";
            }

            return line + "\n<size=70%><alpha=#99>" + Plain(info.Metadata.GUID) + "<alpha=#FF></size>";
        }

        internal static string Plain(string value)
        {
            return "<noparse>" + value.Replace("</noparse>", string.Empty) + "</noparse>";
        }
    }
}
