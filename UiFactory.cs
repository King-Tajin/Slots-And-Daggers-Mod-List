using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ModList
{
    internal static class UiFactory
    {
        private const float EntrySpacing = 24f;
        private const float ControlWidth = 360f;
        private const float SmallButtonWidth = 180f;
        private const float ControlHeight = 64f;

        private static readonly Regex NoParseClose = new Regex("</\\s*noparse[^>]*>", RegexOptions.IgnoreCase);

        private static RectTransform content;
        private static GameObject headingTemplate;
        private static GameObject bodyTemplate;
        private static GameObject buttonTemplate;
        private static GameObject toggleTemplate;

        internal static bool HasToggle
        {
            get { return toggleTemplate != null; }
        }

        internal static void Bind(RectTransform target, GameObject heading, GameObject body, GameObject button, GameObject toggle)
        {
            content = target;
            headingTemplate = heading;
            bodyTemplate = body;
            buttonTemplate = button;
            toggleTemplate = toggle;
        }

        internal static string Plain(string value)
        {
            return "<noparse>" + NoParseClose.Replace(value, string.Empty) + "</noparse>";
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
    }
}
