using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ARCloset
{
    public sealed class GarmentAppearanceController : MonoBehaviour
    {
        private static readonly Color PanelColor = new(0.055f, 0.06f, 0.058f, 0.97f);
        private static readonly Color CardColor = new(0.115f, 0.125f, 0.12f, 0.98f);
        private static readonly Color FieldColor = new(0.035f, 0.04f, 0.038f, 1f);
        private static readonly Color MutedFieldColor = new(0.19f, 0.205f, 0.198f, 1f);
        private static readonly Color AccentColor = new(1f, 0.31f, 0.34f, 1f);
        private static readonly Color SecondaryAccentColor = new(0.2f, 0.8f, 0.61f, 1f);
        private static readonly Color TextColor = new(0.97f, 0.96f, 0.92f, 1f);
        private static readonly Color MutedTextColor = new(0.68f, 0.7f, 0.67f, 1f);
        private static readonly Color WarningColor = new(1f, 0.72f, 0.25f, 1f);

        [SerializeField] private GarmentFittingController fittingController;
        [SerializeField] private MediaPipeUnityPoseSource poseSource;
        [SerializeField] private bool showControls = true;
        [SerializeField] private string colorCode = "#D94B6A";
        [SerializeField] private string stripeColorCode = "#FFFFFF";
        [SerializeField, Range(2, 64)] private int stripeWidthPixels = 8;
        [SerializeField, Range(2, 64)] private int stripeGapPixels = 10;
        [SerializeField] private bool verticalStripes = true;
        [SerializeField, Range(360f, 460f)] private float toolbarWidth = 396f;
        [SerializeField] private Vector2 toolbarMargin = new(12f, 12f);
        [SerializeField, HideInInspector] private Rect controlsRect = new(18f, 296f, 460f, 112f);

        private readonly List<Button> garmentButtons = new();
        private readonly List<Image> garmentButtonImages = new();
        private readonly List<Text> garmentButtonLabels = new();

        private enum ColorTarget
        {
            Garment,
            Stripe,
        }

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
        private Text trackingLabel;
        private Text cameraLabel;
        private Text garmentColorTargetLabel;
        private Text stripeColorTargetLabel;
        private Text stripeToggleLabel;
        private Text verticalDirectionLabel;
        private Text horizontalDirectionLabel;
        private Text mirrorToggleLabel;
        private Text stripeWidthValueLabel;
        private Text stripeGapValueLabel;
        private InputField colorInput;
        private Image colorSwatch;
        private Image stripeSwatch;
        private Slider stripeWidthSlider;
        private Slider stripeGapSlider;
        private Image trackingDot;
        private RuntimeColorPicker colorPicker;
        private Toggle stripeToggle;
        private Toggle mirrorToggle;
        private ColorTarget activeColorTarget;
        private bool suppressPickerCallback;

        public string ColorCode
        {
            get => colorCode;
            set => colorCode = value;
        }

        public string Status => status;
        public bool StripeEnabled => stripeEnabled;
        private string ActiveColorCode => activeColorTarget == ColorTarget.Garment ? colorCode : stripeColorCode;

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

            if (poseSource == null)
            {
                poseSource = FindFirstObjectByType<MediaPipeUnityPoseSource>();
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
            panelLayout.padding = new RectOffset(14, 14, 14, 14);
            panelLayout.spacing = 8f;
            panelLayout.childAlignment = TextAnchor.UpperCenter;
            panelLayout.childControlWidth = true;
            panelLayout.childControlHeight = true;
            panelLayout.childForceExpandWidth = true;
            panelLayout.childForceExpandHeight = false;

            CreateHeader(toolbarPanel);

            RectTransform scrollRoot = CreateRect(toolbarPanel, "ControlsScroll");
            LayoutElement scrollLayout = scrollRoot.gameObject.AddComponent<LayoutElement>();
            scrollLayout.minHeight = 100f;
            scrollLayout.flexibleHeight = 1f;

            ScrollRect scrollRect = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 26f;

            RectTransform viewport = CreateRect(scrollRoot, "Viewport");
            Stretch(viewport, 0f, 0f, 0f, 0f);
            Image viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.001f);
            viewport.gameObject.AddComponent<RectMask2D>();

            RectTransform content = CreateRect(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            VerticalLayoutGroup contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 8f;
            contentLayout.childAlignment = TextAnchor.UpperCenter;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;

            ContentSizeFitter contentFitter = content.gameObject.AddComponent<ContentSizeFitter>();
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewport;
            scrollRect.content = content;

            CreateCameraSection(content);
            CreateGarmentSection(content);
            CreateColorSection(content);
            CreatePatternSection(content);
            RefreshToolbarState(true);
        }

        private void UpdateToolbarScale()
        {
            if (toolbarScaler == null)
            {
                return;
            }

            float heightScale = Screen.height / 600f;
            float widthScale = Screen.width / 1000f;
            toolbarScaler.scaleFactor = Mathf.Clamp(Mathf.Min(heightScale, widthScale), 0.7f, 1.05f);
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
            trackingLabel = null;
            cameraLabel = null;
            garmentColorTargetLabel = null;
            stripeColorTargetLabel = null;
            stripeToggleLabel = null;
            verticalDirectionLabel = null;
            horizontalDirectionLabel = null;
            mirrorToggleLabel = null;
            stripeWidthValueLabel = null;
            stripeGapValueLabel = null;
            colorInput = null;
            colorSwatch = null;
            stripeSwatch = null;
            stripeWidthSlider = null;
            stripeGapSlider = null;
            trackingDot = null;
            stripeToggle = null;
            mirrorToggle = null;
            if (colorPicker != null)
            {
                colorPicker.ColorChanged -= HandlePickerColorChanged;
            }
            colorPicker = null;

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
            layout.minHeight = 88f;
            layout.preferredHeight = 88f;

            VerticalLayoutGroup group = header.gameObject.AddComponent<VerticalLayoutGroup>();
            group.spacing = 4f;
            group.childControlHeight = true;
            group.childControlWidth = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;

            Text title = CreateText(header, "AR CLOSET", 20, FontStyle.Bold, TextColor, TextAnchor.MiddleLeft);
            title.rectTransform.sizeDelta = new Vector2(0f, 28f);

            RectTransform trackingRow = CreateRow(header, "TrackingStatus", 20f, 8f);
            RectTransform dotRect = CreateRect(trackingRow, "TrackingDot");
            LayoutElement dotLayout = dotRect.gameObject.AddComponent<LayoutElement>();
            dotLayout.minWidth = 10f;
            dotLayout.preferredWidth = 10f;
            dotLayout.minHeight = 10f;
            dotLayout.preferredHeight = 10f;
            trackingDot = dotRect.gameObject.AddComponent<Image>();
            trackingDot.color = WarningColor;

            trackingLabel = CreateText(trackingRow, "Starting camera", 12, FontStyle.Bold, MutedTextColor, TextAnchor.MiddleLeft);
            statusLabel = CreateText(header, status, 11, FontStyle.Normal, MutedTextColor, TextAnchor.MiddleLeft);
            statusLabel.rectTransform.sizeDelta = new Vector2(0f, 18f);

            RectTransform line = CreateRect(header, "AccentLine");
            LayoutElement lineLayout = line.gameObject.AddComponent<LayoutElement>();
            lineLayout.minHeight = 3f;
            lineLayout.preferredHeight = 3f;
            Image lineImage = line.gameObject.AddComponent<Image>();
            lineImage.color = AccentColor;
        }

        private void CreateCameraSection(Transform parent)
        {
            RectTransform card = CreateCard(parent, "CameraSection", 88f);
            CreateSectionTitle(card, "Camera");

            RectTransform row = CreateRow(card, "CameraControls", 42f, 8f);
            cameraLabel = CreateText(row, "Camera", 12, FontStyle.Normal, TextColor, TextAnchor.MiddleLeft);
            CreateButton(row, "Switch", MutedFieldColor, CycleCameraFromToolbar, 76f, 40f);
            mirrorToggle = CreateToggle(
                row,
                "Mirror",
                poseSource == null || poseSource.IsMirrored,
                SetMirrorFromToolbar,
                out mirrorToggleLabel,
                100f);
        }

        private void CreateGarmentSection(Transform parent)
        {
            RectTransform card = CreateCard(parent, "GarmentSection", 206f);
            CreateSectionTitle(card, "Garment");
            currentGarmentLabel = CreateText(card, "Selected: none", 13, FontStyle.Bold, TextColor, TextAnchor.MiddleLeft);
            currentGarmentLabel.rectTransform.sizeDelta = new Vector2(0f, 22f);

            garmentListRoot = CreateRect(card, "GarmentList");
            GridLayoutGroup grid = garmentListRoot.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(Mathf.Max(120f, (toolbarWidth - 66f) / 2f), 40f);
            grid.spacing = new Vector2(6f, 6f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.childAlignment = TextAnchor.UpperLeft;
            garmentListLayout = garmentListRoot.gameObject.AddComponent<LayoutElement>();

            RebuildGarmentButtons();
        }

        private void CreateColorSection(Transform parent)
        {
            RectTransform card = CreateCard(parent, "ColorSection", 318f);
            CreateSectionTitle(card, "Color");

            RectTransform targetRow = CreateRow(card, "ColorTarget", 40f, 6f);
            garmentColorTargetLabel = CreateButton(
                targetRow,
                "Garment",
                AccentColor,
                () => SetActiveColorTarget(ColorTarget.Garment),
                0f,
                40f);
            stripeColorTargetLabel = CreateButton(
                targetRow,
                "Stripe",
                MutedFieldColor,
                () => SetActiveColorTarget(ColorTarget.Stripe),
                0f,
                40f);

            CreateColorPicker(card);

            RectTransform inputRow = CreateRow(card, "ColorInputRow", 40f, 8f);
            colorSwatch = CreateSwatch(inputRow, "ActiveColorSwatch", colorCode);
            colorInput = CreateInput(inputRow, "ActiveColorInput", colorCode);
            colorInput.onEndEdit.AddListener(ApplyActiveColorCode);

            RectTransform presets = CreateRow(card, "ColorPresets", 34f, 7f);
            string[] presetColors =
            {
                "#FF5A5F",
                "#3A86FF",
                "#2EC4B6",
                "#70C66B",
                "#FFBE0B",
                "#9B5DE5",
                "#F6F2EA",
            };
            foreach (string preset in presetColors)
            {
                CreateSwatchButton(presets, preset, () => ApplyColorPreset(preset));
            }
        }

        private void CreateColorPicker(Transform parent)
        {
            RectTransform pickerRow = CreateRow(parent, "ColorPicker", 132f, 10f);

            RectTransform saturationValueRect = CreateRect(pickerRow, "SaturationValue");
            LayoutElement saturationValueLayout = saturationValueRect.gameObject.AddComponent<LayoutElement>();
            saturationValueLayout.minWidth = 220f;
            saturationValueLayout.minHeight = 128f;
            saturationValueLayout.preferredHeight = 128f;
            saturationValueLayout.flexibleWidth = 1f;
            RawImage saturationValueImage = saturationValueRect.gameObject.AddComponent<RawImage>();
            saturationValueImage.color = Color.white;

            RectTransform saturationValueMarker = CreateRect(saturationValueRect, "Selection");
            saturationValueMarker.sizeDelta = new Vector2(14f, 14f);
            Image saturationValueMarkerImage = saturationValueMarker.gameObject.AddComponent<Image>();
            saturationValueMarkerImage.color = Color.white;
            saturationValueMarkerImage.raycastTarget = false;
            Outline saturationValueOutline = saturationValueMarker.gameObject.AddComponent<Outline>();
            saturationValueOutline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            saturationValueOutline.effectDistance = new Vector2(2f, -2f);

            RectTransform hueRect = CreateRect(pickerRow, "Hue");
            LayoutElement hueLayout = hueRect.gameObject.AddComponent<LayoutElement>();
            hueLayout.minWidth = 24f;
            hueLayout.preferredWidth = 24f;
            hueLayout.minHeight = 128f;
            hueLayout.preferredHeight = 128f;
            RawImage hueImage = hueRect.gameObject.AddComponent<RawImage>();
            hueImage.color = Color.white;

            RectTransform hueMarker = CreateRect(hueRect, "Selection");
            hueMarker.sizeDelta = new Vector2(32f, 4f);
            Image hueMarkerImage = hueMarker.gameObject.AddComponent<Image>();
            hueMarkerImage.color = Color.white;
            hueMarkerImage.raycastTarget = false;
            Outline hueOutline = hueMarker.gameObject.AddComponent<Outline>();
            hueOutline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            hueOutline.effectDistance = new Vector2(1f, -1f);

            colorPicker = pickerRow.gameObject.AddComponent<RuntimeColorPicker>();
            colorPicker.Initialize(saturationValueImage, saturationValueMarker, hueImage, hueMarker);
            colorPicker.ColorChanged += HandlePickerColorChanged;
            if (TryParseHexColor(colorCode, out Color initialColor))
            {
                colorPicker.SetColor(initialColor);
            }
        }

        private void CreatePatternSection(Transform parent)
        {
            RectTransform card = CreateCard(parent, "PatternSection", 220f);
            CreateSectionTitle(card, "Pattern");

            stripeToggle = CreateToggle(
                card,
                "Use stripes",
                stripeEnabled,
                SetStripeEnabledFromToolbar,
                out stripeToggleLabel);

            RectTransform directionRow = CreateRow(card, "StripeDirection", 40f, 6f);
            CreateText(directionRow, "Direction", 12, FontStyle.Normal, MutedTextColor, TextAnchor.MiddleLeft, 72f);
            verticalDirectionLabel = CreateButton(
                directionRow,
                "Vertical",
                SecondaryAccentColor,
                () => SetStripeDirection(true),
                0f,
                40f);
            horizontalDirectionLabel = CreateButton(
                directionRow,
                "Horizontal",
                MutedFieldColor,
                () => SetStripeDirection(false),
                0f,
                40f);

            RectTransform widthRow = CreateRow(card, "StripeWidth", 30f, 10f);
            CreateText(widthRow, "Width", 12, FontStyle.Normal, MutedTextColor, TextAnchor.MiddleLeft, 52f);
            stripeWidthSlider = CreateSlider(widthRow, "StripeWidthSlider", 2f, 64f, stripeWidthPixels);
            stripeWidthValueLabel = CreateText(widthRow, stripeWidthPixels.ToString(), 13, FontStyle.Bold, TextColor, TextAnchor.MiddleRight, 32f);
            stripeWidthSlider.onValueChanged.AddListener(value =>
            {
                stripeWidthPixels = Mathf.RoundToInt(value);
                UpdateStripeValueLabels();
                ApplyLiveStripeIfEnabled();
            });

            RectTransform gapRow = CreateRow(card, "StripeGap", 30f, 10f);
            CreateText(gapRow, "Spacing", 12, FontStyle.Normal, MutedTextColor, TextAnchor.MiddleLeft, 52f);
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
                int rows = Mathf.CeilToInt(garmentCount / 2f);
                float height = rows * 40f + Mathf.Max(0, rows - 1) * 6f;
                garmentListLayout.minHeight = height;
                garmentListLayout.preferredHeight = height;
            }

            for (int i = 0; i < garmentCount; i++)
            {
                int index = i;
                GarmentDefinition definition = fittingController.GetGarmentDefinition(i);
                string labelText = ShortGarmentName(definition, i);
                Text label = CreateButton(garmentListRoot, labelText, MutedFieldColor, () => EquipGarmentFromToolbar(index), 0f, 40f);
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
                currentGarmentLabel.text = $"Selected: {currentName}";
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

            string activeCode = ActiveColorCode;
            RefreshInputField(colorInput, activeCode, forceTextFields);
            RefreshColorPreview(colorSwatch, activeCode);
            RefreshColorPreview(stripeSwatch, stripeColorCode);

            Color garmentTargetColor = activeColorTarget == ColorTarget.Garment ? AccentColor : MutedFieldColor;
            Color stripeTargetColor = activeColorTarget == ColorTarget.Stripe ? SecondaryAccentColor : MutedFieldColor;
            SetButtonColor(garmentColorTargetLabel, garmentTargetColor);
            SetButtonColor(stripeColorTargetLabel, stripeTargetColor);

            if (forceTextFields && colorPicker != null && TryParseHexColor(activeCode, out Color pickerColor))
            {
                suppressPickerCallback = true;
                colorPicker.SetColor(pickerColor);
                suppressPickerCallback = false;
            }

            if (stripeToggle != null && stripeToggle.isOn != stripeEnabled)
            {
                stripeToggle.SetIsOnWithoutNotify(stripeEnabled);
            }

            if (stripeToggleLabel != null)
            {
                stripeToggleLabel.color = stripeEnabled ? TextColor : MutedTextColor;
            }

            SetButtonColor(verticalDirectionLabel, verticalStripes ? SecondaryAccentColor : MutedFieldColor);
            SetButtonColor(horizontalDirectionLabel, verticalStripes ? MutedFieldColor : SecondaryAccentColor);

            if (mirrorToggle != null && poseSource != null && mirrorToggle.isOn != poseSource.IsMirrored)
            {
                mirrorToggle.SetIsOnWithoutNotify(poseSource.IsMirrored);
            }

            if (mirrorToggleLabel != null)
            {
                mirrorToggleLabel.color = poseSource != null && poseSource.IsMirrored ? TextColor : MutedTextColor;
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
            UpdateTrackingState();
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
            ApplyActiveColorCode(colorInput != null ? colorInput.text : ActiveColorCode);
        }

        private void ApplyColorPreset(string code)
        {
            ApplyActiveColorCode(code);
        }

        private void ApplyStripeFromToolbar()
        {
            TryApplyStripePattern(out _);
        }

        private void ApplyActiveColorCode(string code)
        {
            if (activeColorTarget == ColorTarget.Garment)
            {
                TryApplyColorCode(code, out _);
                return;
            }

            if (!TryParseHexColor(code, out Color color))
            {
                status = "Invalid stripe color";
                RefreshToolbarState(true);
                return;
            }

            stripeColorCode = NormalizeHexColor(color);
            appliedStripeColor = color;
            if (stripeEnabled)
            {
                TryApplyStripePattern(colorCode, stripeColorCode, out _);
            }
            else
            {
                status = $"Stripe color {stripeColorCode}";
                RefreshToolbarState(true);
            }
        }

        private void HandlePickerColorChanged(Color color)
        {
            if (suppressPickerCallback)
            {
                return;
            }

            ApplyActiveColorCode(NormalizeHexColor(color));
        }

        private void SetActiveColorTarget(ColorTarget target)
        {
            if (activeColorTarget == target)
            {
                return;
            }

            activeColorTarget = target;
            RefreshToolbarState(true);
        }

        private void SetStripeEnabledFromToolbar(bool enabled)
        {
            if (enabled == stripeEnabled)
            {
                return;
            }

            if (enabled)
            {
                ApplyStripeFromToolbar();
            }
            else
            {
                ClearStripePattern(out _);
            }
        }

        private void SetStripeDirection(bool vertical)
        {
            if (verticalStripes == vertical)
            {
                return;
            }

            verticalStripes = vertical;
            ApplyLiveStripeIfEnabled();
            RefreshToolbarState(false);
        }

        private void CycleCameraFromToolbar()
        {
            if (poseSource == null)
            {
                status = "Camera source unavailable";
                return;
            }

            poseSource.CycleCameraDevice();
            status = "Switching camera";
        }

        private void SetMirrorFromToolbar(bool mirrored)
        {
            if (poseSource == null)
            {
                return;
            }

            poseSource.SetVideoAndInputMirrored(mirrored);
            status = mirrored ? "Mirror on" : "Mirror off";
        }

        private void ApplyLiveStripeIfEnabled()
        {
            if (stripeEnabled)
            {
                TryApplyStripePattern(out _);
            }
        }

        private void UpdateTrackingState()
        {
            if (poseSource == null)
            {
                poseSource = FindFirstObjectByType<MediaPipeUnityPoseSource>();
            }

            if (poseSource == null)
            {
                if (trackingLabel != null)
                {
                    trackingLabel.text = "Camera unavailable";
                }

                if (trackingDot != null)
                {
                    trackingDot.color = AccentColor;
                }

                return;
            }

            string rawStatus = poseSource.Status ?? string.Empty;
            string normalized = rawStatus.ToLowerInvariant();
            string label;
            Color indicator;
            if (normalized.Contains("tracking"))
            {
                label = "Pose tracking";
                indicator = SecondaryAccentColor;
            }
            else if (normalized.Contains("searching"))
            {
                label = "Finding pose";
                indicator = WarningColor;
            }
            else if (normalized.Contains("error") || normalized.Contains("denied") || normalized.Contains("unavailable") || normalized.Contains("missing"))
            {
                label = "Camera issue";
                indicator = AccentColor;
            }
            else
            {
                label = "Starting camera";
                indicator = WarningColor;
            }

            if (trackingLabel != null)
            {
                trackingLabel.text = label;
                trackingLabel.color = indicator;
            }

            if (trackingDot != null)
            {
                trackingDot.color = indicator;
            }

            if (cameraLabel != null)
            {
                cameraLabel.text = ShortCameraName(poseSource.ActiveDeviceName);
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
            group.childControlHeight = true;
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
            group.childForceExpandHeight = false;

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

        private Toggle CreateToggle(
            Transform parent,
            string labelValue,
            bool initialValue,
            Action<bool> onChanged,
            out Text label,
            float fixedWidth = 0f)
        {
            RectTransform rect = CreateRect(parent, labelValue.Replace(" ", string.Empty) + "Toggle");
            Image background = rect.gameObject.AddComponent<Image>();
            background.color = MutedFieldColor;

            Toggle toggle = rect.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = background;
            toggle.colors = CreateButtonColors(MutedFieldColor);

            LayoutElement layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 40f;
            layout.preferredHeight = 40f;
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

            RectTransform box = CreateRect(rect, "Box");
            box.anchorMin = new Vector2(0f, 0.5f);
            box.anchorMax = new Vector2(0f, 0.5f);
            box.pivot = new Vector2(0f, 0.5f);
            box.anchoredPosition = new Vector2(11f, 0f);
            box.sizeDelta = new Vector2(20f, 20f);
            Image boxImage = box.gameObject.AddComponent<Image>();
            boxImage.color = FieldColor;
            boxImage.raycastTarget = false;

            RectTransform check = CreateRect(box, "Check");
            Stretch(check, 4f, 4f, 4f, 4f);
            Image checkImage = check.gameObject.AddComponent<Image>();
            checkImage.color = SecondaryAccentColor;
            checkImage.raycastTarget = false;
            toggle.graphic = checkImage;

            label = CreateText(rect, labelValue, 12, FontStyle.Bold, TextColor, TextAnchor.MiddleLeft);
            label.raycastTarget = false;
            Stretch(label.rectTransform, 42f, 4f, 8f, 4f);

            toggle.SetIsOnWithoutNotify(initialValue);
            toggle.onValueChanged.AddListener(value => onChanged?.Invoke(value));
            return toggle;
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

        private static string ShortGarmentName(GarmentDefinition definition, int index)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.displayName))
            {
                return $"Garment {index + 1}";
            }

            string name = definition.displayName.Replace("MakeHuman ", string.Empty);
            return name.Replace("Fisherman Sweater", "Sweater");
        }

        private static string ShortCameraName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || string.Equals(name, "default", StringComparison.OrdinalIgnoreCase))
            {
                return "Default camera";
            }

            const int maxLength = 24;
            return name.Length <= maxLength ? name : name.Substring(0, maxLength - 3) + "...";
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
