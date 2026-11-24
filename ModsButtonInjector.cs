using System.Reflection;
using BepInEx.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ModList
{
    internal static class ModsButtonInjector
    {
        private const string ButtonName = "ModListButton";
        private const string ButtonLabel = "Mods";
        private const float FallbackSpacing = 10f;
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        internal static readonly FieldInfo StartMenuProgressField = typeof(StartMenuUGUI).GetField("m_ProgressButton", Flags);
        internal static readonly FieldInfo PauseMenuProgressField = typeof(PauseScreenUGui).GetField("m_ProgressButton", Flags);

        private static readonly FieldInfo UseToolTipField = typeof(UIMenuButton).GetField("useToolTip", Flags);

        private static ManualLogSource log;

        internal static void Initialize(ManualLogSource logger)
        {
            log = logger;

            if (StartMenuProgressField == null)
            {
                log.LogWarning("StartMenuUGUI.m_ProgressButton was not found, the Start menu button will be skipped.");
            }

            if (PauseMenuProgressField == null)
            {
                log.LogWarning("PauseScreenUGui.m_ProgressButton was not found, the Pause menu button will be skipped.");
            }

            if (UseToolTipField == null)
            {
                log.LogWarning("UIMenuButton.useToolTip was not found, the Mods button may show the Progress tooltip.");
            }
        }

        internal static void ScanExisting()
        {
            foreach (StartMenuUGUI screen in Object.FindObjectsByType<StartMenuUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Inject(StartMenuProgressField, screen);
            }

            foreach (PauseScreenUGui screen in Object.FindObjectsByType<PauseScreenUGui>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Inject(PauseMenuProgressField, screen);
            }
        }

        internal static void Inject(FieldInfo progressField, MonoBehaviour screen)
        {
            try
            {
                InjectInternal(progressField, screen);
            }
            catch (System.Exception e)
            {
                log.LogError(e);
            }
        }

        private static void InjectInternal(FieldInfo progressField, MonoBehaviour screen)
        {
            if (progressField == null)
            {
                return;
            }

            Button progressButton = progressField.GetValue(screen) as Button;
            if (progressButton == null)
            {
                return;
            }

            Transform parent = progressButton.transform.parent;
            if (parent == null || parent.Find(ButtonName) != null)
            {
                return;
            }

            GameObject clone = Object.Instantiate(progressButton.gameObject, parent);
            clone.name = ButtonName;
            clone.transform.SetSiblingIndex(progressButton.transform.GetSiblingIndex() + 1);

            foreach (MonoBehaviour behaviour in clone.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour != null && behaviour.GetType().Name == "LocalizeStringEvent")
                {
                    Object.DestroyImmediate(behaviour);
                }
            }

            foreach (Tooltip tooltip in clone.GetComponentsInChildren<Tooltip>(true))
            {
                Object.DestroyImmediate(tooltip);
            }

            if (UseToolTipField != null)
            {
                foreach (UIMenuButton menuButton in clone.GetComponentsInChildren<UIMenuButton>(true))
                {
                    UseToolTipField.SetValue(menuButton, false);
                }
            }

            foreach (TMP_Text label in clone.GetComponentsInChildren<TMP_Text>(true))
            {
                label.text = ButtonLabel;
            }

            Button button = clone.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(OnClicked);

            if (parent.GetComponent<LayoutGroup>() == null)
            {
                RectTransform source = (RectTransform)progressButton.transform;
                RectTransform target = (RectTransform)clone.transform;
                target.anchoredPosition = source.anchoredPosition + Vector2.down * (source.rect.height + FallbackSpacing);
            }

            log.LogDebug("Added Mods button to " + screen.GetType().Name);
        }

        private static void OnClicked()
        {
            ModListScreen.Open();
        }
    }
}
