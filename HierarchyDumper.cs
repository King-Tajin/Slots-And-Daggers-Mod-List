using System.Collections.Generic;
using System.Text;
using BepInEx.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ModList
{
    internal static class HierarchyDumper
    {
        private const int MaxTextLength = 40;

        internal static void Dump(ManualLogSource log, Transform root)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Hierarchy of " + root.name);
            Append(builder, root, 0);
            log.LogInfo(builder.ToString());
        }

        private static void Append(StringBuilder builder, Transform node, int depth)
        {
            builder.Append(' ', depth * 2);
            builder.Append(node.name);

            if (!node.gameObject.activeSelf)
            {
                builder.Append(" (inactive)");
            }

            List<string> names = new List<string>();
            foreach (Component component in node.GetComponents<Component>())
            {
                names.Add(component == null ? "missing" : component.GetType().Name);
            }

            builder.Append(" [").Append(string.Join(", ", names)).Append(']');

            RectTransform rect = node as RectTransform;
            if (rect != null)
            {
                builder.Append(" anchors=").Append(rect.anchorMin).Append('-').Append(rect.anchorMax);
                builder.Append(" delta=").Append(rect.sizeDelta);
                builder.Append(" rect=").Append(Mathf.Round(rect.rect.width)).Append('x').Append(Mathf.Round(rect.rect.height));
            }

            LayoutElement element = node.GetComponent<LayoutElement>();
            if (element != null)
            {
                builder.Append(" layout[min=").Append(element.minWidth).Append(',').Append(element.minHeight);
                builder.Append(" pref=").Append(element.preferredWidth).Append(',').Append(element.preferredHeight);
                builder.Append(" flex=").Append(element.flexibleWidth).Append(',').Append(element.flexibleHeight).Append(']');
            }

            TMP_Text text = node.GetComponent<TMP_Text>();
            if (text != null)
            {
                builder.Append(" size=").Append(text.fontSize);
                builder.Append(" text=\"").Append(Shorten(text.text)).Append('"');
            }

            builder.AppendLine();

            for (int i = 0; i < node.childCount; i++)
            {
                Append(builder, node.GetChild(i), depth + 1);
            }
        }

        private static string Shorten(string value)
        {
            string flat = value.Replace('\n', ' ').Replace('\r', ' ');
            return flat.Length <= MaxTextLength ? flat : flat.Substring(0, MaxTextLength) + "...";
        }
    }
}
