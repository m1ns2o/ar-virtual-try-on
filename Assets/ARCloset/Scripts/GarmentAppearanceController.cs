using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ARCloset
{
    public sealed class GarmentAppearanceController : MonoBehaviour
    {
        private static readonly Color PanelColor = new(0.07f, 0.08f, 0.11f, 0.94f);
        private static readonly Color CardColor = new(0.12f, 0.14f, 0.18f, 0.98f);
        private static readonly Color FieldColor = new(0.055f, 0.06f, 0.08f, 1f);
        private static readonly Color MutedFieldColor = new(0.16f, 0.18f, 0.23f, 1f);
        private static readonly Color AccentColor = new(0.93f, 0.28f, 0.42f, 1f);
        private static readonly Color SecondaryAccentColor = new(0.1f, 0.68f, 0.78f, 1f);
        private static readonly Color TextColor = new(0.94f, 0.96f, 0.98f, 1f);
        private static readonly Color MutedTextColor = new(0.58f, 0.64f, 0.72f, 1f);

        [SerializeField] private GarmentFittingController fittingController;
        [SerializeField] private bool showControls = true;
        [SerializeField] private string colorCode = "#D94B6A";
        [SerializeField] private string stripeColorCode = "#FFFFFF";
        [SerializeField, Range(2, 64)] private int stripeWidthPixels = 8;
        [SerializeField, Range(2, 64)] private int stripeGapPixels = 10;
        [SerializeField] private bool verticalStripes = true;
        [SerializeField, Range(300f, 440f)] private float toolbarWidth = 360f;
        [SerializeField] private Vector2 toolbarMargin = new(18f, 18f);
        [SerializeField, HideInInspector] private Rect controlsRect = new(18f, 296f, 460f, 112f);

        private readonly List<Button> garmentButtons = new();
        private readonly List<Image> garmentButtonImages = new();
        private readonly List<Text> garmentButtonLabels = new();

        private GarmentDefinition lastDefinition;
        private bool hasAppliedColor;
        private bool stripeEnabled;
        private Color appliedColor = Color.white;
        private Color appliedStripeColor = Color.white;
        private Texture2D stripeTexture;
        private string status = "Color ready";

        private GameObject toolbarRoot;
        private RectTransform toolbarPanel;
        private RectTransform garmentListRoot;
        private LayoutElement garmentListLayout;
        private CanvasScaler toolbarScaler;
        private Font uiFont;
        private Text currentGarmentLabel;
        private Text statusLabel;
        private Text stripeToggleLabel;
        private Text directionLabel;
        private Text stripeWidthValueLabel;
        private Text stripeGapValueLabel;
        private InputField colorInput;
        private InputField stripeInput;
        private Image colorSwatch;
        private Image stripeSwatch;
        private Slider stripeWidthSlider;
        private Slider stripeGapSlider;

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

        private void OnDisable()
        {
            if (toolbarRoot != null)
            {
                toolbarRoot.SetActive(false);
            }
        }

        private void Update()
        {
            if (showControls)
            {
                EnsureToolbar();
                UpdateToolbarScale();
            }
            else if (toolbarRoot != null)
            {
                toolbarRoot.SetActive(false);
            }

            if (fittingController == null)
            {
                SetStatus("No garment controller");
                return;
            }

            if (hasAppliedColor && fittingController.CurrentDefinition != lastDefinition)
            {
                ApplyCurrentAppearance();
            }

            lastDefinition = fittingController.CurrentDefinition;
            RefreshToolbarState(false);
        }

        private void OnDestroy()
        {
            DestroyStripeTexture();
            DestroyToolbar();
        }

        public bool TryApplyColorCode(string code, out string message)
        {
            if (!TryParseHexColor(code, out Color color))
            {
                message = "Invalid color";
                status = message;
                RefreshToolbarState(true);
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
                RefreshToolbarState(true);
                return false;
            }

            message = $"Applied {colorCode}";
            status = message;
            RefreshToolbarState(true);
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
                RefreshToolbarState(true);
                return false;
            }

            if (!TryParseHexColor(stripeCode, out Color stripeColor))
            {
                message = "Invalid stripe color";
                status = message;
                RefreshToolbarState(true);
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
                RefreshToolbarState(true);
                return false;
            }

            message = $"Stripe {colorCode}/{stripeColorCode}";
            status = message;
            RefreshToolbarState(true);
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
                RefreshToolbarState(true);
                return false;
            }

            message = hasAppliedColor ? $"Applied {colorCode}" : "Stripe cleared";
            status = message;
            RefreshToolbarState(true);
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

        private void EnsureToolbar()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (toolbarRoot != null)
            {
                toolbarRoot.SetActive(true);
                return;
            }

            CreateEventSystemIfNeeded();
            uiFont = ResolveDefaultFont();

            toolbarRoot = new GameObject("GarmentSettingsToolbar", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            toolbarRoot.transform.SetParent(transform, false);

            Canvas canvas = toolbarRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 120;

            toolbarScaler = toolbarRoot.GetComponent<CanvasScaler>();
            toolbarScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            UpdateToolbarScale();

            toolbarPanel = CreateRect(toolbarRoot.transform, "RightSettingsToolbar");
            toolbarPanel.anchorMin = new Vector2(1f, 0f);
            toolbarPanel.anchorMax = new Vector2(1f, 1f);
            toolbarPanel.pivot = new Vector2(1f, 0.5f);
            toolbarPanel.anchoredPosition = new Vector2(-toolbarMargin.x, 0f);
            toolbarPanel.sizeDelta = new Vector2(toolbarWidth, -toolbarMargin.y * 2f);

            Image panelImage = toolbarPanel.gameObject.AddComponent<Image>();
            panelImage.color = PanelColor;
            Shadow panelShadow = toolbarPanel.gameObject.AddComponent<Shadow>();
            panelShadow.effectColor = new Color(0f, 0f, 0f, 0.42f);
            panelShadow.effectDistance = new Vector2(-8f, -8f);

            VerticalLayoutGroup panelLayout = toolbarPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            panelLayout.padding = new RectOffset(12, 12, 12, 12);
            panelLayout.spacing = 10f;
            panelLayout.childAlignment = TextAnchor.UpperCenter;
            panelLayout.childControlWidth = true;
            panelLayout.childControlHeight = false;
            panelLayout.childForceExpandWidth = true;
            panelLayout.childForceExpandHeight = false;

            CreateHeader(toolbarPanel);
            CreateGarmentSection(toolbarPanel);
            CreateColorSection(toolbarPanel);
            CreatePatternSection(toolbarPanel);
            RefreshToolbarState(true);
        }

        private void UpdateToolbarScale()
        {
            if (toolbarScaler == null)
            {
                return;
            }

            toolbarScaler.scaleFactor = Mathf.Clamp(Screen.height / 900f, 0.48f, 1f);
        }

        private void DestroyToolbar()
        {
            garmentButtons.Clear();
            garmentButtonImages.Clear();
            garmentButtonLabels.Clear();
            toolbarPanel = null;
            garmentListRoot = null;
            garmentListLayout = null;
            toolbarScaler = null;
            currentGarmentLabel = null;
            statusLabel = null;
            stripeToggleLabel = null;
            directionLabel = null;
            stripeWidthValueLabel = null;
            stripeGapValueLabel = null;
            colorInput = null;
            stripeInput = null;
            colorSwatch = null;
            stripeSwatch = null;
            stripeWidthSlider = null;
            stripeGapSlider = null;

            if (toolbarRoot == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(toolbarRoot);
            }
            else
            {
                DestroyImmediate(toolbarRoot);
            }

            toolbarRoot = null;
        }

        private void CreateHeader(Transform parent)
        {
            RectTransform header = CreateRect(parent, "Header");
            LayoutElement layout = header.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 58f;
            layout.preferredHeight = 58f;

            VerticalLayoutGroup group = header.gameObject.AddComponent<VerticalLayoutGroup>();
            group.spacing = 3f;
            group.childControlHeight = false;
            group.childControlWidth = true;
            group.childForceExpandWidth = true;

            Text title = CreateText(header, "AR Closet", 19, FontStyle.Bold, TextColor, TextAnchor.MiddleLeft);
            title.rectTransform.sizeDelta = new Vector2(0f, 26f);

            Text subtitle = CreateText(header, "Garment settings", 12, FontStyle.Normal, MutedTextColor, TextAnchor.MiddleLeft);
            subtitle.rectTransform.sizeDelta = new Vector2(0f, 18f);

            RectTransform line = CreateRect(header, "AccentLine");
            LayoutElement lineLayout = line.gameObject.AddComponent<LayoutElement>();
            lineLayout.minHeight = 4f;
            lineLayout.preferredHeight = 4f;
            Image lineImage = line.gameObject.AddComponent<Image>();
            lineImage.color = SecondaryAccentColor;
        }

        private void CreateGarmentSection(Transform parent)
        {
            RectTransform card = CreateCard(parent, "GarmentCard", 94f);
            CreateSectionTitle(card, "Garment");
            currentGarmentLabel = CreateText(card, "Current: none", 13, FontStyle.Normal, MutedTextColor, TextAnchor.MiddleLeft);
            currentGarmentLabel.rectTransform.sizeDelta = new Vector2(0f, 20f);

            garmentListRoot = CreateRect(card, "GarmentList");
            GridLayoutGroup grid = garmentListRoot.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(Mathf.Max(32f, (toolbarWidth - 84f) / 5f), 26f);
            grid.spacing = new Vector2(4f, 4f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;
            grid.childAlignment = TextAnchor.UpperLeft;
            garmentListLayout = garmentListRoot.gameObject.AddComponent<LayoutElement>();

            RebuildGarmentButtons();
        }

        private void CreateColorSection(Transform parent)
        {
            RectTransform card = CreateCard(parent, "ColorCard", 116f);
            CreateSectionTitle(card, "Color");

            RectTransform row = CreateRow(card, "ColorInputRow", 36f, 8f);
            colorSwatch = CreateSwatch(row, "BaseColorSwatch", colorCode);
            colorInput = CreateInput(row, "BaseColorInput", colorCode);
            colorInput.onEndEdit.AddListener(value =>
            {
                colorCode = value;
                RefreshColorPreview(colorSwatch, colorCode);
            });
            CreateButton(row, "Apply", AccentColor, ApplyBaseColorFromToolbar, 72f);

            RectTransform presets = CreateRow(card, "ColorPresets", 30f, 8f);
            string[] presetColors = { "#D94B6A", "#2D9CDB", "#27AE60", "#F2C94C", "#8E44AD", "#F7F9FB" };
            foreach (string preset in presetColors)
            {
                CreateSwatchButton(presets, preset, () => ApplyColorPreset(preset));
            }
        }

        private void CreatePatternSection(Transform parent)
        {
            RectTransform card = CreateCard(parent, "PatternCard", 184f);
            CreateSectionTitle(card, "Pattern");

            RectTransform stripeRow = CreateRow(card, "StripeInputRow", 36f, 8f);
            stripeSwatch = CreateSwatch(stripeRow, "StripeColorSwatch", stripeColorCode);
            stripeInput = CreateInput(stripeRow, "StripeColorInput", stripeColorCode);
            stripeInput.onEndEdit.AddListener(value =>
            {
                stripeColorCode = value;
                RefreshColorPreview(stripeSwatch, stripeColorCode);
            });
            CreateButton(stripeRow, "Apply", SecondaryAccentColor, ApplyStripeFromToolbar, 72f);

            RectTransform actionRow = CreateRow(card, "PatternActions", 34f, 8f);
            stripeToggleLabel = CreateButton(actionRow, "Enable Stripes", MutedFieldColor, ToggleStripePattern, 0f);
            directionLabel = CreateButton(actionRow, "Vertical", MutedFieldColor, ToggleStripeDirection, 0f);

            RectTransform widthRow = CreateRow(card, "StripeWidth", 30f, 10f);
            CreateText(widthRow, "Width", 13, FontStyle.Normal, MutedTextColor, TextAnchor.MiddleLeft, 48f);
            stripeWidthSlider = CreateSlider(widthRow, "StripeWidthSlider", 2f, 64f, stripeWidthPixels);
            stripeWidthValueLabel = CreateText(widthRow, stripeWidthPixels.ToString(), 13, FontStyle.Bold, TextColor, TextAnchor.MiddleRight, 32f);
            stripeWidthSlider.onValueChanged.AddListener(value =>
            {
                stripeWidthPixels = Mathf.RoundToInt(value);
                UpdateStripeValueLabels();
                ApplyLiveStripeIfEnabled();
            });

            RectTransform gapRow = CreateRow(card, "StripeGap", 30f, 10f);
            CreateText(gapRow, "Gap", 13, FontStyle.Normal, MutedTextColor, TextAnchor.MiddleLeft, 48f);
            stripeGapSlider = CreateSlider(gapRow, "StripeGapSlider", 2f, 64f, stripeGapPixels);
            stripeGapValueLabel = CreateText(gapRow, stripeGapPixels.ToString(), 13, FontStyle.Bold, TextColor, TextAnchor.MiddleRight, 32f);
            stripeGapSlider.onValueChanged.AddListener(value =>
            {
                stripeGapPixels = Mathf.RoundToInt(value);
                UpdateStripeValueLabels();
                ApplyLiveStripeIfEnabled();
            });

        }

        private void RebuildGarmentButtons()
        {
            if (garmentListRoot == null)
            {
                return;
            }

            for (int i = garmentListRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(garmentListRoot.GetChild(i).gameObject);
            }

            garmentButtons.Clear();
            garmentButtonImages.Clear();
            garmentButtonLabels.Clear();

            int garmentCount = fittingController != null ? fittingController.GarmentCount : 0;
            if (garmentCount <= 0)
            {
                if (garmentListLayout != null)
                {
                    garmentListLayout.minHeight = 28f;
                    garmentListLayout.preferredHeight = 28f;
                }

                Text empty = CreateText(garmentListRoot, "No garments available", 13, FontStyle.Normal, MutedTextColor, TextAnchor.MiddleLeft);
                empty.rectTransform.sizeDelta = new Vector2(0f, 28f);
                return;
            }

            if (garmentListLayout != null)
            {
                int rows = Mathf.CeilToInt(garmentCount / 5f);
                float height = rows * 26f + Mathf.Max(0, rows - 1) * 4f;
                garmentListLayout.minHeight = height;
                garmentListLayout.preferredHeight = height;
            }

            for (int i = 0; i < garmentCount; i++)
            {
                int index = i;
                Text label = CreateButton(garmentListRoot, (i + 1).ToString(), MutedFieldColor, () => EquipGarmentFromToolbar(index), 0f, 26f);
                Button button = label.GetComponentInParent<Button>();
                Image image = button != null ? button.GetComponent<Image>() : null;
                if (button != null && image != null)
                {
                    garmentButtons.Add(button);
                    garmentButtonImages.Add(image);
                    garmentButtonLabels.Add(label);
                }
            }
        }

        private void RefreshToolbarState(bool forceTextFields)
        {
            if (toolbarRoot == null)
            {
                return;
            }

            if (fittingController != null && garmentButtons.Count != fittingController.GarmentCount)
            {
                RebuildGarmentButtons();
            }

            if (currentGarmentLabel != null)
            {
                string currentName = fittingController != null && fittingController.CurrentDefinition != null
                    ? fittingController.CurrentDefinition.displayName
                    : "none";
                currentGarmentLabel.text = $"Current: {currentName}";
            }

            int currentIndex = fittingController != null ? fittingController.CurrentGarmentIndex : -1;
            for (int i = 0; i < garmentButtonImages.Count; i++)
            {
                bool selected = i == currentIndex;
                Color buttonColor = selected ? AccentColor : MutedFieldColor;
                garmentButtonImages[i].color = buttonColor;
                if (i < garmentButtons.Count)
                {
                    garmentButtons[i].colors = CreateButtonColors(buttonColor);
                }

                if (i < garmentButtonLabels.Count)
                {
                    garmentButtonLabels[i].color = selected ? Color.white : TextColor;
                    garmentButtonLabels[i].fontStyle = selected ? FontStyle.Bold : FontStyle.Normal;
                }
            }

            RefreshInputField(colorInput, colorCode, forceTextFields);
            RefreshInputField(stripeInput, stripeColorCode, forceTextFields);
            RefreshColorPreview(colorSwatch, colorCode);
            RefreshColorPreview(stripeSwatch, stripeColorCode);

            if (stripeToggleLabel != null)
            {
                stripeToggleLabel.text = stripeEnabled ? "Disable Stripes" : "Enable Stripes";
                SetButtonColor(stripeToggleLabel, stripeEnabled ? SecondaryAccentColor : MutedFieldColor);
            }

            if (directionLabel != null)
            {
                directionLabel.text = verticalStripes ? "Vertical" : "Horizontal";
                SetButtonColor(directionLabel, verticalStripes ? SecondaryAccentColor : MutedFieldColor);
            }

            if (stripeWidthSlider != null && !Mathf.Approximately(stripeWidthSlider.value, stripeWidthPixels))
            {
                stripeWidthSlider.SetValueWithoutNotify(stripeWidthPixels);
            }

            if (stripeGapSlider != null && !Mathf.Approximately(stripeGapSlider.value, stripeGapPixels))
            {
                stripeGapSlider.SetValueWithoutNotify(stripeGapPixels);
            }

            UpdateStripeValueLabels();
            SetStatus(status);
        }

        private void EquipGarmentFromToolbar(int index)
        {
            if (fittingController == null)
            {
                status = "No garment controller";
                RefreshToolbarState(true);
                return;
            }

            fittingController.EquipByIndex(index);
            ApplyCurrentAppearance();

            GarmentDefinition definition = fittingController.GetGarmentDefinition(index);
            status = definition != null ? $"Equipped {definition.displayName}" : $"Equipped garment {index + 1}";
            RefreshToolbarState(true);
        }

        private void ApplyBaseColorFromToolbar()
        {
            if (colorInput != null)
            {
                colorCode = colorInput.text;
            }

            TryApplyColorCode(colorCode, out _);
        }

        private void ApplyColorPreset(string code)
        {
            colorCode = code;
            RefreshInputField(colorInput, colorCode, true);
            TryApplyColorCode(colorCode, out _);
        }

        private void ApplyStripeFromToolbar()
        {
            if (colorInput != null)
            {
                colorCode = colorInput.text;
            }

            if (stripeInput != null)
            {
                stripeColorCode = stripeInput.text;
            }

            TryApplyStripePattern(out _);
        }

        private void ToggleStripePattern()
        {
            if (stripeEnabled)
            {
                ClearStripePattern(out _);
            }
            else
            {
                ApplyStripeFromToolbar();
            }
        }

        private void ToggleStripeDirection()
        {
            verticalStripes = !verticalStripes;
            if (stripeEnabled)
            {
                TryApplyStripePattern(out _);
            }

            RefreshToolbarState(true);
        }

        private void ApplyLiveStripeIfEnabled()
        {
            if (stripeEnabled)
            {
                TryApplyStripePattern(out _);
            }
        }

        private void SetStatus(string message)
        {
            if (statusLabel != null)
            {
                statusLabel.text = message;
            }
        }

        private static void SetButtonColor(Text label, Color color)
        {
            if (label == null)
            {
                return;
            }

            Button button = label.GetComponentInParent<Button>();
            Image image = button != null ? button.GetComponent<Image>() : null;
            if (button != null)
            {
                button.colors = CreateButtonColors(color);
            }

            if (image != null)
            {
                image.color = color;
            }
        }

        private void UpdateStripeValueLabels()
        {
            if (stripeWidthValueLabel != null)
            {
                stripeWidthValueLabel.text = stripeWidthPixels.ToString();
            }

            if (stripeGapValueLabel != null)
            {
                stripeGapValueLabel.text = stripeGapPixels.ToString();
            }
        }

        private void RefreshInputField(InputField field, string value, bool force)
        {
            if (field == null)
            {
                return;
            }

            bool focused = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == field.gameObject;
            if (!force && focused)
            {
                return;
            }

            if (!string.Equals(field.text, value, StringComparison.Ordinal))
            {
                field.SetTextWithoutNotify(value);
            }
        }

        private void RefreshColorPreview(Image image, string code)
        {
            if (image == null)
            {
                return;
            }

            image.color = TryParseHexColor(code, out Color color) ? color : new Color(0.35f, 0.08f, 0.1f, 1f);
        }

        private static RectTransform CreateRect(Transform parent, string name)
        {
            GameObject gameObject = new(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject.GetComponent<RectTransform>();
        }

        private RectTransform CreateCard(Transform parent, string name, float preferredHeight)
        {
            RectTransform card = CreateRect(parent, name);
            Image image = card.gameObject.AddComponent<Image>();
            image.color = CardColor;

            Shadow shadow = card.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.24f);
            shadow.effectDistance = new Vector2(0f, -2f);

            VerticalLayoutGroup group = card.gameObject.AddComponent<VerticalLayoutGroup>();
            group.padding = new RectOffset(10, 10, 10, 10);
            group.spacing = 7f;
            group.childAlignment = TextAnchor.UpperCenter;
            group.childControlWidth = true;
            group.childControlHeight = false;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;

            LayoutElement layout = card.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = preferredHeight;
            layout.preferredHeight = preferredHeight;
            layout.flexibleHeight = 0f;
            return card;
        }

        private RectTransform CreateRow(Transform parent, string name, float height, float spacing)
        {
            RectTransform row = CreateRect(parent, name);
            HorizontalLayoutGroup group = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.spacing = spacing;
            group.childAlignment = TextAnchor.MiddleLeft;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = true;

            LayoutElement layout = row.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;
            return row;
        }

        private void CreateSectionTitle(Transform parent, string title)
        {
            Text text = CreateText(parent, title.ToUpperInvariant(), 12, FontStyle.Bold, SecondaryAccentColor, TextAnchor.MiddleLeft);
            text.rectTransform.sizeDelta = new Vector2(0f, 18f);
        }

        private Text CreateText(Transform parent, string value, int size, FontStyle style, Color color, TextAnchor alignment, float fixedWidth = 0f)
        {
            RectTransform rect = CreateRect(parent, value.Replace(" ", string.Empty) + "Text");
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = uiFont;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.text = value;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;

            LayoutElement layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = Mathf.Max(18f, size + 8f);
            layout.preferredHeight = Mathf.Max(18f, size + 8f);
            if (fixedWidth > 0f)
            {
                layout.minWidth = fixedWidth;
                layout.preferredWidth = fixedWidth;
                layout.flexibleWidth = 0f;
            }
            else
            {
                layout.flexibleWidth = 1f;
            }

            return text;
        }

        private Text CreateButton(Transform parent, string label, Color color, Action onClick, float fixedWidth = 0f, float height = 36f)
        {
            RectTransform rect = CreateRect(parent, label.Replace(" ", string.Empty) + "Button");
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.colors = CreateButtonColors(color);
            button.onClick.AddListener(() => onClick?.Invoke());

            LayoutElement layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;
            if (fixedWidth > 0f)
            {
                layout.minWidth = fixedWidth;
                layout.preferredWidth = fixedWidth;
                layout.flexibleWidth = 0f;
            }
            else
            {
                layout.flexibleWidth = 1f;
            }

            Text text = CreateText(rect, label, 13, FontStyle.Bold, TextColor, TextAnchor.MiddleCenter);
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 10;
            text.resizeTextMaxSize = 13;
            Stretch(text.rectTransform, 10f, 4f, 10f, 4f);
            return text;
        }

        private InputField CreateInput(Transform parent, string name, string value)
        {
            RectTransform rect = CreateRect(parent, name);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = FieldColor;

            InputField input = rect.gameObject.AddComponent<InputField>();
            input.targetGraphic = image;
            input.text = value;
            input.characterLimit = 12;
            input.caretColor = AccentColor;
            input.selectionColor = new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.32f);

            LayoutElement layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 36f;
            layout.preferredHeight = 36f;
            layout.flexibleWidth = 1f;

            Text text = CreateText(rect, value, 14, FontStyle.Bold, TextColor, TextAnchor.MiddleLeft);
            text.name = "Text";
            Stretch(text.rectTransform, 10f, 3f, 10f, 3f);

            input.textComponent = text;
            input.SetTextWithoutNotify(value);
            return input;
        }

        private Image CreateSwatch(Transform parent, string name, string code)
        {
            RectTransform rect = CreateRect(parent, name);
            LayoutElement layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = 36f;
            layout.preferredWidth = 36f;
            layout.minHeight = 36f;
            layout.preferredHeight = 36f;

            Image image = rect.gameObject.AddComponent<Image>();
            RefreshColorPreview(image, code);

            Outline outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.26f);
            outline.effectDistance = new Vector2(1f, -1f);
            return image;
        }

        private void CreateSwatchButton(Transform parent, string code, Action onClick)
        {
            RectTransform rect = CreateRect(parent, "Preset" + code.Replace("#", string.Empty));
            LayoutElement layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = 34f;
            layout.preferredWidth = 34f;
            layout.minHeight = 30f;
            layout.preferredHeight = 30f;

            Image image = rect.gameObject.AddComponent<Image>();
            RefreshColorPreview(image, code);

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.colors = CreateButtonColors(image.color);
            button.onClick.AddListener(() => onClick?.Invoke());

            Outline outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.24f);
            outline.effectDistance = new Vector2(1f, -1f);
        }

        private Slider CreateSlider(Transform parent, string name, float minValue, float maxValue, float value)
        {
            RectTransform sliderRect = CreateRect(parent, name);
            LayoutElement layout = sliderRect.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 30f;
            layout.preferredHeight = 30f;
            layout.flexibleWidth = 1f;

            Slider slider = sliderRect.gameObject.AddComponent<Slider>();
            slider.minValue = minValue;
            slider.maxValue = maxValue;
            slider.wholeNumbers = true;
            slider.value = value;

            RectTransform background = CreateRect(sliderRect, "Background");
            background.anchorMin = new Vector2(0f, 0.5f);
            background.anchorMax = new Vector2(1f, 0.5f);
            background.pivot = new Vector2(0.5f, 0.5f);
            background.sizeDelta = new Vector2(0f, 5f);
            Image backgroundImage = background.gameObject.AddComponent<Image>();
            backgroundImage.color = FieldColor;

            RectTransform fillArea = CreateRect(sliderRect, "FillArea");
            Stretch(fillArea, 0f, 12f, 0f, 12f);
            RectTransform fill = CreateRect(fillArea, "Fill");
            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = new Vector2(1f, 1f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            Image fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = SecondaryAccentColor;

            RectTransform handleArea = CreateRect(sliderRect, "HandleArea");
            Stretch(handleArea, 0f, 0f, 0f, 0f);
            RectTransform handle = CreateRect(handleArea, "Handle");
            handle.sizeDelta = new Vector2(18f, 18f);
            Image handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = TextColor;

            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            return slider;
        }

        private static void Stretch(RectTransform rect, float left, float top, float right, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static ColorBlock CreateButtonColors(Color baseColor)
        {
            return new ColorBlock
            {
                normalColor = baseColor,
                highlightedColor = AdjustBrightness(baseColor, 1.14f),
                pressedColor = AdjustBrightness(baseColor, 0.82f),
                selectedColor = AdjustBrightness(baseColor, 1.08f),
                disabledColor = new Color(0.18f, 0.18f, 0.2f, 0.45f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f,
            };
        }

        private static Color AdjustBrightness(Color color, float multiplier)
        {
            return new Color(
                Mathf.Clamp01(color.r * multiplier),
                Mathf.Clamp01(color.g * multiplier),
                Mathf.Clamp01(color.b * multiplier),
                color.a);
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

        private static Font ResolveDefaultFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            if (font == null)
            {
                font = Font.CreateDynamicFontFromOSFont(new[] { "Noto Sans", "DejaVu Sans", "Arial" }, 14);
            }

            return font;
        }

        private static void CreateEventSystemIfNeeded()
        {
            if (EventSystem.current != null || FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            GameObject eventSystem = new("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(eventSystem);
        }
    }
}
