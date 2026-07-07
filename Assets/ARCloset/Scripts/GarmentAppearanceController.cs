using System;
using UnityEngine;

namespace ARCloset
{
    public sealed class GarmentAppearanceController : MonoBehaviour
    {
        [SerializeField] private GarmentFittingController fittingController;
        [SerializeField] private bool showControls = true;
        [SerializeField] private string colorCode = "#D94B6A";
        [SerializeField] private string stripeColorCode = "#FFFFFF";
        [SerializeField, Range(2, 64)] private int stripeWidthPixels = 8;
        [SerializeField, Range(2, 64)] private int stripeGapPixels = 10;
        [SerializeField] private bool verticalStripes = true;
        [SerializeField] private Rect controlsRect = new Rect(18f, 296f, 460f, 112f);

        private GarmentDefinition lastDefinition;
        private bool hasAppliedColor;
        private bool stripeEnabled;
        private Color appliedColor = Color.white;
        private Color appliedStripeColor = Color.white;
        private Texture2D stripeTexture;
        private string status = "Color ready";

        public string ColorCode
        {
            get => colorCode;
            set => colorCode = value;
        }

        public string Status => status;
        public bool StripeEnabled => stripeEnabled;

        private void Reset()
        {
            fittingController = GetComponent<GarmentFittingController>();
        }

        private void Awake()
        {
            if (fittingController == null)
            {
                fittingController = GetComponent<GarmentFittingController>();
            }
        }

        private void Update()
        {
            if (fittingController == null)
            {
                return;
            }

            if (hasAppliedColor && fittingController.CurrentDefinition != lastDefinition)
            {
                ApplyCurrentAppearance();
            }

            lastDefinition = fittingController.CurrentDefinition;
        }

        private void OnDestroy()
        {
            DestroyStripeTexture();
        }

        public bool TryApplyColorCode(string code, out string message)
        {
            if (!TryParseHexColor(code, out Color color))
            {
                message = "Invalid color";
                status = message;
                return false;
            }

            colorCode = NormalizeHexColor(color);
            appliedColor = color;
            hasAppliedColor = true;

            bool applied = stripeEnabled
                ? ApplyStripePattern(appliedColor, appliedStripeColor)
                : ApplySolidColor(appliedColor);
            if (!applied)
            {
                message = "No garment";
                status = message;
                return false;
            }

            message = $"Applied {colorCode}";
            status = message;
            return true;
        }

        public bool TryApplyStripePattern(out string message)
        {
            return TryApplyStripePattern(colorCode, stripeColorCode, out message);
        }

        public bool TryApplyStripePattern(string baseCode, string stripeCode, out string message)
        {
            if (!TryParseHexColor(baseCode, out Color baseColor))
            {
                message = "Invalid base color";
                status = message;
                return false;
            }

            if (!TryParseHexColor(stripeCode, out Color stripeColor))
            {
                message = "Invalid stripe color";
                status = message;
                return false;
            }

            colorCode = NormalizeHexColor(baseColor);
            stripeColorCode = NormalizeHexColor(stripeColor);
            appliedColor = baseColor;
            appliedStripeColor = stripeColor;
            hasAppliedColor = true;
            stripeEnabled = true;

            if (!ApplyStripePattern(appliedColor, appliedStripeColor))
            {
                message = "No garment";
                status = message;
                return false;
            }

            message = $"Stripe {colorCode}/{stripeColorCode}";
            status = message;
            return true;
        }

        public bool ClearStripePattern(out string message)
        {
            stripeEnabled = false;
            DestroyStripeTexture();

            bool cleared = fittingController != null && fittingController.ApplyCurrentGarmentTexture(null);
            bool colored = !hasAppliedColor || ApplySolidColor(appliedColor);
            if (!cleared || !colored)
            {
                message = "No garment";
                status = message;
                return false;
            }

            message = hasAppliedColor ? $"Applied {colorCode}" : "Stripe cleared";
            status = message;
            return true;
        }

        private bool ApplyCurrentAppearance()
        {
            if (stripeEnabled)
            {
                return ApplyStripePattern(appliedColor, appliedStripeColor);
            }

            return hasAppliedColor && ApplySolidColor(appliedColor);
        }

        private bool ApplySolidColor(Color color)
        {
            if (fittingController == null)
            {
                return false;
            }

            fittingController.ApplyCurrentGarmentTexture(null);
            return fittingController.ApplyCurrentGarmentColor(color);
        }

        private bool ApplyStripePattern(Color baseColor, Color stripeColor)
        {
            if (fittingController == null)
            {
                return false;
            }

            BuildStripeTexture(baseColor, stripeColor);
            bool textureApplied = fittingController.ApplyCurrentGarmentTexture(stripeTexture);
            bool tintApplied = fittingController.ApplyCurrentGarmentColor(Color.white);
            return textureApplied && tintApplied;
        }

        private void BuildStripeTexture(Color baseColor, Color stripeColor)
        {
            int stripeWidth = Mathf.Clamp(stripeWidthPixels, 2, 64);
            int stripeGap = Mathf.Clamp(stripeGapPixels, 2, 64);
            int period = stripeWidth + stripeGap;
            int size = Mathf.Clamp(period * 4, 32, 256);

            DestroyStripeTexture();
            stripeTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "RuntimeGarmentStripePattern",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Point,
            };

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int axis = verticalStripes ? x : y;
                    bool stripe = axis % period < stripeWidth;
                    pixels[y * size + x] = stripe ? stripeColor : baseColor;
                }
            }

            stripeTexture.SetPixels(pixels);
            stripeTexture.Apply(false, false);
        }

        private void DestroyStripeTexture()
        {
            if (stripeTexture == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(stripeTexture);
            }
            else
            {
                DestroyImmediate(stripeTexture);
            }

            stripeTexture = null;
        }

        private static bool TryParseHexColor(string code, out Color color)
        {
            color = Color.white;
            if (string.IsNullOrWhiteSpace(code))
            {
                return false;
            }

            string value = code.Trim();
            if (!value.StartsWith("#", StringComparison.Ordinal))
            {
                value = "#" + value;
            }

            return ColorUtility.TryParseHtmlString(value, out color);
        }

        private static string NormalizeHexColor(Color color)
        {
            return "#" + ColorUtility.ToHtmlStringRGB(color);
        }

        private void OnGUI()
        {
            if (!showControls)
            {
                return;
            }

            GUILayout.BeginArea(controlsRect, GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Color", GUILayout.Width(54f));
            colorCode = GUILayout.TextField(colorCode, 12, GUILayout.Width(116f));
            if (GUILayout.Button("Apply", GUILayout.Width(72f)))
            {
                TryApplyColorCode(colorCode, out _);
            }

            GUILayout.Label(status, GUILayout.Width(120f));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Stripe", GUILayout.Width(54f));
            stripeColorCode = GUILayout.TextField(stripeColorCode, 12, GUILayout.Width(116f));
            if (GUILayout.Button("Apply", GUILayout.Width(72f)))
            {
                TryApplyStripePattern(out _);
            }

            if (GUILayout.Button("Clear", GUILayout.Width(72f)))
            {
                ClearStripePattern(out _);
            }

            verticalStripes = GUILayout.Toggle(verticalStripes, verticalStripes ? "Vertical" : "Horizontal", GUILayout.Width(96f));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Width", GUILayout.Width(54f));
            stripeWidthPixels = Mathf.RoundToInt(GUILayout.HorizontalSlider(stripeWidthPixels, 2, 64, GUILayout.Width(116f)));
            GUILayout.Label(stripeWidthPixels.ToString(), GUILayout.Width(32f));
            GUILayout.Label("Gap", GUILayout.Width(34f));
            stripeGapPixels = Mathf.RoundToInt(GUILayout.HorizontalSlider(stripeGapPixels, 2, 64, GUILayout.Width(116f)));
            GUILayout.Label(stripeGapPixels.ToString(), GUILayout.Width(32f));
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}
