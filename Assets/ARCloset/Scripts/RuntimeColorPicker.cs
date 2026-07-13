using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ARCloset
{
    public sealed class RuntimeColorPicker : MonoBehaviour
    {
        private const int SaturationValueTextureSize = 48;
        private const int HueTextureHeight = 96;

        private RawImage saturationValueImage;
        private RawImage hueImage;
        private RectTransform saturationValueMarker;
        private RectTransform hueMarker;
        private Texture2D saturationValueTexture;
        private Texture2D hueTexture;
        private float hue;
        private float saturation;
        private float value = 1f;
        private bool initialized;

        public event Action<Color> ColorChanged;

        public Color CurrentColor => Color.HSVToRGB(hue, saturation, value);

        public void Initialize(
            RawImage saturationValue,
            RectTransform saturationMarker,
            RawImage hueStrip,
            RectTransform hueStripMarker)
        {
            saturationValueImage = saturationValue;
            saturationValueMarker = saturationMarker;
            hueImage = hueStrip;
            hueMarker = hueStripMarker;

            ColorPickerPointerArea saturationArea = saturationValueImage.gameObject.AddComponent<ColorPickerPointerArea>();
            saturationArea.ValueChanged = SetSaturationAndValue;

            ColorPickerPointerArea hueArea = hueImage.gameObject.AddComponent<ColorPickerPointerArea>();
            hueArea.ValueChanged = normalized => SetHue(normalized.y);

            BuildHueTexture();
            initialized = true;
            RefreshSaturationValueTexture();
            RefreshMarkers();
        }

        public void SetColor(Color color, bool notify = false)
        {
            Color.RGBToHSV(color, out hue, out saturation, out value);
            value = Mathf.Max(0.001f, value);

            if (initialized)
            {
                RefreshSaturationValueTexture();
                RefreshMarkers();
            }

            if (notify)
            {
                ColorChanged?.Invoke(CurrentColor);
            }
        }

        private void SetSaturationAndValue(Vector2 normalized)
        {
            saturation = Mathf.Clamp01(normalized.x);
            value = Mathf.Clamp01(normalized.y);
            RefreshMarkers();
            ColorChanged?.Invoke(CurrentColor);
        }

        private void SetHue(float normalizedHue)
        {
            hue = Mathf.Repeat(normalizedHue, 1f);
            RefreshSaturationValueTexture();
            RefreshMarkers();
            ColorChanged?.Invoke(CurrentColor);
        }

        private void BuildHueTexture()
        {
            DestroyTexture(ref hueTexture);
            hueTexture = new Texture2D(1, HueTextureHeight, TextureFormat.RGBA32, false)
            {
                name = "RuntimeHueStrip",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };

            for (int y = 0; y < HueTextureHeight; y++)
            {
                float normalized = y / (HueTextureHeight - 1f);
                hueTexture.SetPixel(0, y, Color.HSVToRGB(normalized, 1f, 1f));
            }

            hueTexture.Apply(false, false);
            hueImage.texture = hueTexture;
        }

        private void RefreshSaturationValueTexture()
        {
            if (saturationValueImage == null)
            {
                return;
            }

            if (saturationValueTexture == null)
            {
                saturationValueTexture = new Texture2D(
                    SaturationValueTextureSize,
                    SaturationValueTextureSize,
                    TextureFormat.RGBA32,
                    false)
                {
                    name = "RuntimeSaturationValueField",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.DontSave,
                };
                saturationValueImage.texture = saturationValueTexture;
            }

            Color[] pixels = new Color[SaturationValueTextureSize * SaturationValueTextureSize];
            for (int y = 0; y < SaturationValueTextureSize; y++)
            {
                float normalizedValue = y / (SaturationValueTextureSize - 1f);
                for (int x = 0; x < SaturationValueTextureSize; x++)
                {
                    float normalizedSaturation = x / (SaturationValueTextureSize - 1f);
                    pixels[y * SaturationValueTextureSize + x] = Color.HSVToRGB(hue, normalizedSaturation, normalizedValue);
                }
            }

            saturationValueTexture.SetPixels(pixels);
            saturationValueTexture.Apply(false, false);
        }

        private void RefreshMarkers()
        {
            if (saturationValueMarker != null)
            {
                Vector2 anchor = new(saturation, value);
                saturationValueMarker.anchorMin = anchor;
                saturationValueMarker.anchorMax = anchor;
                saturationValueMarker.anchoredPosition = Vector2.zero;
            }

            if (hueMarker != null)
            {
                Vector2 anchor = new(0.5f, hue);
                hueMarker.anchorMin = anchor;
                hueMarker.anchorMax = anchor;
                hueMarker.anchoredPosition = Vector2.zero;
            }
        }

        private void OnDestroy()
        {
            DestroyTexture(ref saturationValueTexture);
            DestroyTexture(ref hueTexture);
        }

        private static void DestroyTexture(ref Texture2D texture)
        {
            if (texture == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(texture);
            }
            else
            {
                DestroyImmediate(texture);
            }

            texture = null;
        }
    }

    internal sealed class ColorPickerPointerArea : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        private RectTransform rectTransform;

        internal Action<Vector2> ValueChanged { get; set; }

        private void Awake()
        {
            rectTransform = (RectTransform)transform;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            UpdateValue(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            UpdateValue(eventData);
        }

        private void UpdateValue(PointerEventData eventData)
        {
            if (rectTransform == null ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rectTransform,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 localPoint))
            {
                return;
            }

            Rect rect = rectTransform.rect;
            Vector2 normalized = new(
                Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x),
                Mathf.InverseLerp(rect.yMin, rect.yMax, localPoint.y));
            ValueChanged?.Invoke(normalized);
        }
    }
}
