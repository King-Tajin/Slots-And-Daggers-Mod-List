using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using TMPro;
using UnityEngine;
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
        private const float WidthFraction = 0.85f;
        private const float ScrollbarMargin = 30f;
        private const int ContentPadding = 40;
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

#if DEBUG
        private const string DumpHint = " Enable the development option DumpCreditsHierarchy in the config for details.";
#else
        private const string DumpHint = "";
#endif

        private static readonly FieldInfo RootTransformField = typeof(UIScreenUGUI).GetField("rootTransform", Flags);
        private static readonly FieldInfo CurrentScreenField = typeof(UIManager).GetField("currentScreen", Flags);
        private static readonly FieldInfo AllScreensField = typeof(UIManager).GetField("allScreens", Flags);
        private static readonly FieldInfo BackButtonField = typeof(CreditsScreenUGUI).GetField("m_BackButton", Flags);
        private static readonly string[] SettingsToggleFields = { "toggleFullscreen", "toggleCameraMotion", "toggleFastMode", "toggleShowTutorialsAgain" };
        private static readonly MethodInfo ShowMethod = typeof(UIManager).GetMethod("Show", Flags, null, new[] { typeof(UIScreenUGUI), typeof(bool) }, null);

        private static ManualLogSource log;
        private static ConfigEntry<bool> dumpHierarchy;
        private static bool buildFailed;
        private static bool inConfig;
        private static float appliedCanvasWidth;
        private static CreditsScreenUGUI screen;
        private static RectTransform panelRoot;
        private static RectTransform content;
        private static ScrollRect scroll;

        internal static void Initialize(ManualLogSource logger, ConfigEntry<bool> dump)
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
            Widen(panelRoot, scroll);
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

        [System.Diagnostics.Conditional("DEBUG")]
        private static void DumpIfEnabled(Transform root)
        {
#if DEBUG
            if (dumpHierarchy != null && dumpHierarchy.Value)
            {
                HierarchyDumper.Dump(log, root);
            }
#endif
        }

        private static bool Build()
        {
            if (buildFailed)
            {
                return false;
            }

            appliedCanvasWidth = 0f;

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

                DumpIfEnabled(cloneRoot);

                ScrollRect cloneScroll = cloneRoot.GetComponentInChildren<ScrollRect>(true);
                if (cloneScroll == null || cloneScroll.content == null)
                {
                    return Fail("The credits panel has no scroll view to reuse." + DumpHint);
                }

                Button backButton = BackButtonField != null ? BackButtonField.GetValue(cloneScreen) as Button : null;
                if (backButton == null)
                {
                    return Fail("The credits screen has no Back button to copy the style from." + DumpHint);
                }

                TMP_Text[] texts = cloneScroll.content.GetComponentsInChildren<TMP_Text>(true);
                if (texts.Length == 0)
                {
                    return Fail("The credits scroll view has no text to copy the style from." + DumpHint);
                }

                System.Array.Sort(texts, (a, b) => a.fontSize.CompareTo(b.fontSize));
                GameObject headingTemplate = CreateTemplate(texts[texts.Length - 1].gameObject, screenClone.transform);
                GameObject bodyTemplate = CreateTemplate(texts[texts.Length / 2].gameObject, screenClone.transform);
                GameObject buttonTemplate = CreateTemplate(backButton.gameObject, screenClone.transform);
                GameObject toggleTemplate = null;

                Toggle settingsToggle = FindSettingsToggle();
                if (settingsToggle != null)
                {
                    toggleTemplate = CreateTemplate(settingsToggle.gameObject, screenClone.transform);
                    DumpIfEnabled(toggleTemplate.transform);
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

                UiFactory.Bind(listContent, headingTemplate, bodyTemplate, buttonTemplate, toggleTemplate);

                screen = cloneScreen;
                panelRoot = cloneRoot;
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

            float canvasWidth = canvasRect.rect.width;
            if (Mathf.Approximately(canvasWidth, appliedCanvasWidth))
            {
                return;
            }

            float width = canvasWidth * WidthFraction;
            if (width <= 0f)
            {
                return;
            }

            appliedCanvasWidth = canvasWidth;

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

                ScrollbarPinner pinner = barRect.GetComponent<ScrollbarPinner>();
                if (pinner == null)
                {
                    pinner = barRect.gameObject.AddComponent<ScrollbarPinner>();
                }

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
            if (screens == null)
            {
                return;
            }

            screens.RemoveAll(s => s == null);

            if (!screens.Contains(screen))
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

            UiFactory.AddText(true, HeadingText + " (" + plugins.Count + ")");

            foreach (PluginInfo info in plugins)
            {
                string description = Describe(info);

                if (ConfigEditor.CountEntries(info) > 0)
                {
                    PluginInfo selected = info;
                    Button config = UiFactory.CreateSmallButton("Config", () => ShowConfig(selected, true));
                    UiFactory.AddEntryRow(description, config.gameObject);
                }
                else
                {
                    UiFactory.AddEntryRow(description);
                }
            }

            FinishView(focus);
        }

        private static void ShowConfig(PluginInfo info, bool focus)
        {
            inConfig = true;
            DiscardChildren(content);

            UiFactory.AddText(true, UiFactory.Plain(info.Metadata.Name) + " Settings");
            ConfigEditor.Populate(info);

            FinishView(focus);
        }

        private static void FinishView(bool focus)
        {
            scroll.StopMovement();
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

        private static string Describe(PluginInfo info)
        {
            string line = "<b>" + UiFactory.Plain(info.Metadata.Name) + "</b>  <alpha=#99>v" + info.Metadata.Version + "<alpha=#FF>";

            if (info.Instance == null)
            {
                line += "  <color=#FF6B6B>Not loaded</color>";
            }
            else if (!info.Instance.enabled)
            {
                line += "  <color=#FF6B6B>Disabled</color>";
            }

            return line + "\n<size=70%><alpha=#99>" + UiFactory.Plain(info.Metadata.GUID) + "<alpha=#FF></size>";
        }
    }
}
