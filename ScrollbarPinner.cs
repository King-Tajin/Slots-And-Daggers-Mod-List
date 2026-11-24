using UnityEngine;

namespace ModList
{
    internal sealed class ScrollbarPinner : MonoBehaviour
    {
        internal RectTransform CanvasRect;
        internal float Margin;

        private RectTransform self;

        private void Awake()
        {
            self = (RectTransform)transform;
        }

        private void LateUpdate()
        {
            if (CanvasRect == null)
            {
                return;
            }

            float right = CanvasRect.TransformPoint(new Vector3(CanvasRect.rect.xMax, 0f, 0f)).x - Margin * CanvasRect.lossyScale.x;
            Vector3 position = self.position;

            if (!Mathf.Approximately(position.x, right))
            {
                position.x = right;
                self.position = position;
            }
        }
    }
}
