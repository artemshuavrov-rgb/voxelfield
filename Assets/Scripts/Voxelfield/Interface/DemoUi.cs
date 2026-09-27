using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Voxelfield.Interface
{
    /// <summary>Small, resolution-independent UI kit for the playable solo demo.</summary>
    internal static class DemoUi
    {
        internal static readonly Color Background = new(0.035f, 0.067f, 0.10f, 0.97f);
        internal static readonly Color Panel = new(0.075f, 0.12f, 0.16f, 0.97f);
        internal static readonly Color Accent = new(0.38f, 0.91f, 0.76f);
        internal static readonly Color White = new(0.95f, 0.98f, 0.98f);
        internal static readonly Color Muted = new(0.65f, 0.76f, 0.79f);

        internal static RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max,
                                           Vector2 size, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        internal static RectTransform Center(Transform parent, string name, Vector2 size, Vector2 position)
            => Rect(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), size, position);

        internal static Image Image(Transform parent, string name, Vector2 min, Vector2 max,
                                    Vector2 size, Vector2 position, Color color, bool raycast = false)
        {
            RectTransform rect = Rect(parent, name, min, max, size, position);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        internal static Image Box(Transform parent, string name, Vector2 size, Vector2 position, Color color)
            => Image(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), size, position, color);

        internal static TextMeshProUGUI Text(Transform parent, string name, string value,
                                             Vector2 size, Vector2 position, int fontSize,
                                             Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Left,
                                             FontStyles style = FontStyles.Normal)
        {
            RectTransform rect = Center(parent, name, size, position);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
        }

        internal static Button Button(Transform parent, string name, string label, Vector2 size,
                                      Vector2 position, bool primary, Action onClick)
        {
            Color baseColor = primary ? Accent : Panel;
            Image image = Box(parent, name, size, position, baseColor);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            button.colors = new ColorBlock
            {
                normalColor = Color.white,
                highlightedColor = primary ? new Color(0.85f, 1f, 0.94f) : new Color(1.35f, 1.35f, 1.35f),
                pressedColor = new Color(0.75f, 0.85f, 0.85f),
                selectedColor = Color.white,
                disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f),
                colorMultiplier = 1f,
                fadeDuration = 0.12f
            };
            Text(image.transform, "Label", label, size - new Vector2(20, 4), Vector2.zero, 18,
                 primary ? Background : White, TextAlignmentOptions.Center, FontStyles.Bold);
            button.onClick.AddListener(() => onClick());
            return button;
        }

        internal static void HideExistingChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
                parent.GetChild(i).gameObject.SetActive(false);
        }
    }
}
