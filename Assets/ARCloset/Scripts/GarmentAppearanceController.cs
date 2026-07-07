using System;
using UnityEngine;

namespace ARCloset
{
    public sealed class GarmentAppearanceController : MonoBehaviour
    {
        [SerializeField] private GarmentFittingController fittingController;
        [SerializeField] private bool showControls = true;
        [SerializeField] private string colorCode = "#D94B6A";
        [SerializeField] private Rect controlsRect = new Rect(18f, 296f, 380f, 76f);

        private GarmentDefinition lastDefinition;
        private bool hasAppliedColor;
        private Color appliedColor = Color.white;
        private string status = "Color ready";

        public string ColorCode
        {
            get => colorCode;
            set => colorCode = value;
        }

        public string Status => status;

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
                ApplyColor(appliedColor);
            }

            lastDefinition = fittingController.CurrentDefinition;
        }

        public bool TryApplyColorCode(string code, out string message)
        {
            if (!TryParseHexColor(code, out Color color))
            {
                message = "Invalid color";
                status = message;
                return false;
            }

            if (!ApplyColor(color))
            {
                message = "No garment";
                status = message;
                return false;
            }

            colorCode = NormalizeHexColor(color);
            appliedColor = color;
            hasAppliedColor = true;
            message = $"Applied {colorCode}";
            status = message;
            return true;
        }

        private bool ApplyColor(Color color)
        {
            return fittingController != null && fittingController.ApplyCurrentGarmentColor(color);
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
            GUILayout.EndArea();
        }
    }
}
