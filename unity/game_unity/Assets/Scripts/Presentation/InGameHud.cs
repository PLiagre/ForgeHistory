using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;
using VictoriaGame.Core;
using VictoriaGame.Economy;
using VictoriaGame.Military;
using VictoriaGame.Politics;
using VictoriaGame.World;

namespace VictoriaGame.Presentation
{
    /// <summary>
    /// Pont MonoBehaviour scène ↔ MapDisplaySystem.
    /// Charpente UXML + styles USS ; binding / intentions / état uniquement en C#.
    /// Tax-/Tax+ : produisent une INTENTION (jamais d'écriture directe TaxPolicy).
    /// Crée le singleton <see cref="TickControl"/> pour le pacing interactif.
    /// </summary>
    public sealed class InGameHud : MonoBehaviour
    {
        public const string ResourcesPanelSettings = "UI/VictoriaPanelSettings";
        public const string ResourcesHudUxml = "UI/InGameHud";
        public const string ResourcesThemeUss = "UI/victoria-theme";
        public const string ResourcesComponentsUss = "UI/victoria-components";

        public const string PaceBarName = "PaceBar";
        public const string PauseButtonName = "PacePauseButton";
        public const string SpeedButtonNamePrefix = "PaceSpeed_";
        public const string ZoomOutButtonName = "ZoomOutButton";
        public const string ProvincePanelName = "ProvincePanel";
        public const string CountryPanelName = "CountryPanel";
        public const string HoverLabelName = "HoverLabel";
        public const string TaxBarName = "TaxBar";
        public const string TaxDownButtonName = "TaxDownButton";
        public const string TaxUpButtonName = "TaxUpButton";
        public const string TaxStatusLabelName = "TaxStatusLabel";
        public const string ViewContextLabelName = "ViewContextLabel";
        public const string DateLabelName = "DateLabel";
        public const string TaxTitleLabelName = "TaxTitleLabel";
        public const string PaceStatusBadgeName = "PaceStatusBadge";
        public const string InvestBarName = "InvestBar";
        public const string InvestTitleLabelName = "InvestTitleLabel";
        public const string InvestStatusLabelName = "InvestStatusLabel";
        public const string InvestTaxButtonName = "InvestTaxButton";
        public const string InvestProdButtonName = "InvestProdButton";
        public const string InvestManButtonName = "InvestManButton";
        public const string BuildFarmButtonName = "BuildFarmButton";
        public const string WarBarName = "WarBar";
        public const string WarTitleLabelName = "WarTitleLabel";
        public const string WarStatusLabelName = "WarStatusLabel";
        public const string DeclareWarButtonName = "DeclareWarButton";
        public const string ProposePeaceButtonName = "ProposePeaceButton";
        public const string LawBarName = "LawBar";
        public const string LawTitleLabelName = "LawTitleLabel";
        public const string LawStatusLabelName = "LawStatusLabel";
        public const string EnactLawButtonName = "EnactLawButton";
        /// <summary>Loi fiscale par défaut du bouton HUD (tax_mod +0,10, tick 0, Absolute OK).</summary>
        public const string DefaultEnactLawId = "land_tax";

        public const string ClassHud = "hud";
        public const string ClassMap = "hud__map";
        public const string ClassTopBar = "hud__topbar";
        public const string ClassTopBarZone = "hud__topbar-zone";
        public const string ClassTopBarContext = "hud__topbar-zone--context";
        public const string ClassTopBarMetrics = "hud__topbar-zone--metrics";
        public const string ClassTopBarTime = "hud__topbar-zone--time";
        public const string ClassViewContext = "hud__view-context";
        public const string ClassInfoBar = "hud__infobar";
        public const string ClassDate = "hud__date";
        public const string ClassPaceBar = "hud__pacebar";
        public const string ClassHover = "hud__hover";
        public const string ClassPanel = "hud__panel";
        public const string ClassPanelProvince = "hud__panel--province";
        public const string ClassPanelCountry = "hud__panel--country";
        public const string ClassPanelOpen = "is-open";
        public const string ClassTaxBar = "hud__taxbar";
        public const string ClassTaxTitle = "tax__title";
        public const string ClassTaxControls = "tax__controls";
        public const string ClassTaxStatus = "hud__tax-status";
        public const string ClassBtn = "hud-btn";
        public const string ClassBtnIdle = "hud-btn--idle";
        public const string ClassBtnHover = "hud-btn--hover";
        public const string ClassBtnActive = "hud-btn--active";
        public const string ClassBtnSelected = "hud-btn--selected";
        public const string ClassBtnPaused = "hud-btn--paused";
        public const string ClassBtnDisabled = "hud-btn--disabled";
        public const string ClassHidden = "is-hidden";
        public const string ClassPaceBadge = "hud__pace-badge";
        public const string ClassPaceBadgePaused = "hud__pace-badge--paused";
        public const string ClassCompact = "hud--compact";
        public const string ClassNarrow = "hud--narrow";
        public const string ClassUltrawide = "hud--ultrawide";

        public const float CompactWidthThreshold = 1440f;
        public const float CompactHeightThreshold = 800f;
        public const float NarrowWidthThreshold = 1280f;
        public const float UltrawideAspectThreshold = 2.1f;

        /// <summary>
        /// Tests : force le chemin programmatique même si le UXML est présent.
        /// Remettre à false après le test.
        /// </summary>
        public static bool ForceProgrammaticFallback;

        /// <summary>
        /// Mode debug explicite : expose TICK et identifiants C#/P# dans le bandeau.
        /// </summary>
        public static bool ShowDebugIds;

        static InGameHud _instance;

        UIDocument _document;
        VisualElement _mapRoot;
        VisualElement _topBar;
        Label _infoBar;
        Label _viewContextLabel;
        Label _dateLabel;
        VisualElement _provincePanel;
        VisualElement _countryPanel;
        Label _hoverLabel;
        VisualElement _paceBar;
        VisualElement _taxBar;
        Button _pauseButton;
        Button _zoomOutButton;
        Button _taxDownButton;
        Button _taxUpButton;
        Label _taxStatusLabel;
        Label _taxTitleLabel;
        Label _paceStatusBadge;
        VisualElement _investBar;
        Label _investTitleLabel;
        Label _investStatusLabel;
        Button _investTaxButton;
        Button _investProdButton;
        Button _investManButton;
        Button _buildFarmButton;
        VisualElement _warBar;
        Label _warTitleLabel;
        Label _warStatusLabel;
        Button _declareWarButton;
        Button _proposePeaceButton;
        VisualElement _lawBar;
        Label _lawTitleLabel;
        Label _lawStatusLabel;
        Button _enactLawButton;
        Button[] _speedButtons;
        Texture2D _mapTexture;
        PanelSettings _panelSettings;
        bool _uiReady;
        bool _usedProgrammaticFallback;
        string _lastMetricsLine = "";
        string _lastInfoBarText = "";
        string _lastProvinceDetail = "";
        string _lastCountryDetail = "";
        float _lastSeenDebt = float.NaN;
        bool _dragging;
        Vector2 _lastPointerLocal;
        int _pointerDownPx = -1;
        int _pointerDownPy = -1;
        bool _dragMoved;
        float _lastLayoutWidth;
        float _lastLayoutHeight;
        int _layoutVersion;
        bool _geometryHooked;

        public static InGameHud Instance => _instance;

        public Texture2D MapTexture => _mapTexture;
        public string LastMetricsLine => _lastMetricsLine;
        public string LastProvinceDetail => _lastProvinceDetail;
        public string LastCountryDetail => _lastCountryDetail;
        public bool UiReady => _uiReady;
        public bool UsedProgrammaticFallback => _usedProgrammaticFallback;
        public string InfoBarText => _lastInfoBarText ?? "";
        public Button PauseButton => _pauseButton;
        public Button ZoomOutButton => _zoomOutButton;
        public VisualElement PaceBar => _paceBar;
        public VisualElement TaxBar => _taxBar;
        public Button TaxDownButton => _taxDownButton;
        public Button TaxUpButton => _taxUpButton;
        public Label TaxStatusLabel => _taxStatusLabel;
        public VisualElement InvestBar => _investBar;
        public Label InvestStatusLabel => _investStatusLabel;
        public Button InvestTaxButton => _investTaxButton;
        public Button InvestProdButton => _investProdButton;
        public Button InvestManButton => _investManButton;
        public Button BuildFarmButton => _buildFarmButton;
        public VisualElement WarBar => _warBar;
        public Label WarStatusLabel => _warStatusLabel;
        public Button DeclareWarButton => _declareWarButton;
        public Button ProposePeaceButton => _proposePeaceButton;
        public VisualElement LawBar => _lawBar;
        public Label LawStatusLabel => _lawStatusLabel;
        public Button EnactLawButton => _enactLawButton;
        public Label HoverLabel => _hoverLabel;
        public VisualElement ProvincePanel => _provincePanel;
        public VisualElement CountryPanel => _countryPanel;
        public Label ViewContextLabel => _viewContextLabel;
        public Label DateLabel => _dateLabel;
        public Label PaceStatusBadge => _paceStatusBadge;
        public float LastLayoutWidth => _lastLayoutWidth;
        public float LastLayoutHeight => _lastLayoutHeight;
        public int LayoutVersion => _layoutVersion;
        public bool IsCompact =>
            _document != null &&
            _document.rootVisualElement != null &&
            _document.rootVisualElement.ClassListContains(ClassCompact);
        public bool IsNarrow =>
            _document != null &&
            _document.rootVisualElement != null &&
            _document.rootVisualElement.ClassListContains(ClassNarrow);
        public bool IsUltrawide =>
            _document != null &&
            _document.rootVisualElement != null &&
            _document.rootVisualElement.ClassListContains(ClassUltrawide);

        public Button GetSpeedButton(float speed)
        {
            if (_speedButtons == null)
                return null;
            for (var i = 0; i < MapDisplaySystem.SpeedSteps.Length; i++)
            {
                if (Mathf.Approximately(MapDisplaySystem.SpeedSteps[i], speed))
                    return _speedButtons[i];
            }
            return null;
        }

        void Awake()
        {
            _instance = this;
            EnsureCamera();
            EnsureUi();
        }

        void Update()
        {
            EnsureTickControlSingleton();
            RefreshPaceControlsFromWorld();
        }

        void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
            if (_mapTexture != null)
            {
                Destroy(_mapTexture);
                _mapTexture = null;
            }
        }

        void EnsureCamera()
        {
            if (Camera.main != null)
                return;
            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.06f, 0.08f, 1f);
            cam.orthographic = true;
            cam.transform.position = new Vector3(0f, 0f, -10f);
        }

        void EnsureUi()
        {
            _document = GetComponent<UIDocument>();
            if (_document == null)
                _document = gameObject.AddComponent<UIDocument>();

            _panelSettings = Resources.Load<PanelSettings>(ResourcesPanelSettings);
            if (_panelSettings == null)
            {
                _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                _panelSettings.name = "RuntimePanelSettings";
                Debug.LogWarning(
                    "InGameHud: PanelSettings asset introuvable dans Resources — " +
                    "instance runtime (texte UI Toolkit peut échouer ; panneau bitmap OK).");
            }

            _document.panelSettings = _panelSettings;

            var root = _document.rootVisualElement;
            root.Clear();
            root.AddToClassList(ClassHud);

            _usedProgrammaticFallback = false;
            var tree = ForceProgrammaticFallback
                ? null
                : Resources.Load<VisualTreeAsset>(ResourcesHudUxml);

            if (tree != null)
            {
                tree.CloneTree(root);
                ApplyStyleSheets(root);
                if (!BindFromTree(root))
                {
                    Debug.LogWarning(
                        "InGameHud: UXML chargé mais éléments contractuels manquants — fallback programmatique.");
                    root.Clear();
                    root.AddToClassList(ClassHud);
                    BuildProgrammaticFallback(root);
                    _usedProgrammaticFallback = true;
                }
            }
            else
            {
                Debug.LogWarning(
                    "InGameHud: VisualTreeAsset '" + ResourcesHudUxml +
                    "' introuvable — fallback programmatique (échec explicite, testable).");
                BuildProgrammaticFallback(root);
                _usedProgrammaticFallback = true;
            }

            WireMapCallbacks();
            WireControlCallbacks();
            ApplyAccessibilityHints();
            HookGeometryChanged(root);

            if (_paceStatusBadge == null && _paceBar != null)
            {
                _paceStatusBadge = new Label { name = PaceStatusBadgeName, text = "" };
                _paceStatusBadge.AddToClassList(ClassPaceBadge);
                _paceBar.Insert(0, _paceStatusBadge);
            }

            _uiReady = true;
            RefreshPaceControlsFromWorld();
            RefreshTaxControls();
            ApplyResponsiveFromCurrentGeometry(root);
        }

        void HookGeometryChanged(VisualElement root)
        {
            if (root == null || _geometryHooked)
                return;
            root.RegisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
            _geometryHooked = true;
        }

        void OnRootGeometryChanged(GeometryChangedEvent evt)
        {
            ApplyResponsiveClasses(evt.newRect.width, evt.newRect.height);
        }

        /// <summary>
        /// Applique les variantes compact/narrow/ultrawide d'après la géométrie panel.
        /// Appelé uniquement après GeometryChanged (ou forcé en tests après layout).
        /// </summary>
        public void ApplyResponsiveClasses(float width, float height)
        {
            if (_document == null)
                return;
            var root = _document.rootVisualElement;
            if (root == null)
                return;

            var compact = width > 1f &&
                          (width < CompactWidthThreshold || height < CompactHeightThreshold);
            var narrow = width > 1f && width <= NarrowWidthThreshold + 0.5f;
            var ultrawide = height > 1f && (width / height) >= UltrawideAspectThreshold;

            root.EnableInClassList(ClassCompact, compact);
            root.EnableInClassList(ClassNarrow, narrow);
            root.EnableInClassList(ClassUltrawide, ultrawide);

            _lastLayoutWidth = width;
            _lastLayoutHeight = height;
            _layoutVersion++;
        }

        /// <summary>Tests : force une géométrie logique puis relayout + classes responsive.</summary>
        public void ForceLayoutSizeForTests(float width, float height)
        {
            if (_document == null)
                return;
            var root = _document.rootVisualElement;
            if (root == null)
                return;

            root.style.width = width;
            root.style.height = height;
            root.MarkDirtyRepaint();
            ApplyResponsiveClasses(width, height);
        }

        void ApplyResponsiveFromCurrentGeometry(VisualElement root)
        {
            if (root == null)
                return;
            var rect = root.contentRect;
            if (rect.width > 1f && rect.height > 1f)
                ApplyResponsiveClasses(rect.width, rect.height);
            else if (Screen.width > 0 && Screen.height > 0)
                ApplyResponsiveClasses(Screen.width, Screen.height);
        }

        void ApplyAccessibilityHints()
        {
            SetControlA11y(_pauseButton, 0, "Mettre en pause ou reprendre");
            SetControlA11y(_zoomOutButton, 1, "Reculer d'un niveau de zoom");
            if (_speedButtons != null)
            {
                for (var i = 0; i < _speedButtons.Length; i++)
                {
                    var label = _speedButtons[i] != null ? _speedButtons[i].text : "";
                    SetControlA11y(_speedButtons[i], 2 + i, SpeedTooltip(label));
                }
            }

            SetControlA11y(_taxDownButton, 7, "Baisser le taux d'impôt");
            SetControlA11y(_taxUpButton, 8, "Augmenter le taux d'impôt");
            if (_mapRoot != null)
                _mapRoot.tabIndex = -1;
        }

        static void SetControlA11y(Button btn, int tabIndex, string tooltip)
        {
            if (btn == null)
                return;
            btn.focusable = true;
            btn.tabIndex = tabIndex;
            if (!string.IsNullOrEmpty(tooltip) && string.IsNullOrEmpty(btn.tooltip))
                btn.tooltip = tooltip;
        }

        static string SpeedTooltip(string label)
        {
            if (string.IsNullOrEmpty(label))
                return "Régler la vitesse";
            if (label.StartsWith("0.5"))
                return "Vitesse demi";
            if (label.StartsWith("1"))
                return "Vitesse normale";
            if (label.StartsWith("2"))
                return "Vitesse double";
            if (label.StartsWith("4"))
                return "Vitesse quadruple";
            if (label.StartsWith("8"))
                return "Vitesse octuple";
            return "Régler la vitesse";
        }

        static void ApplyStyleSheets(VisualElement root)
        {
            var theme = Resources.Load<StyleSheet>(ResourcesThemeUss);
            var components = Resources.Load<StyleSheet>(ResourcesComponentsUss);
            // StyleSheetSet.Add est idempotent (ignore les doublons déjà présents via UXML).
            if (theme != null)
                root.styleSheets.Add(theme);
            if (components != null)
                root.styleSheets.Add(components);
        }

        bool BindFromTree(VisualElement root)
        {
            _mapRoot = root.Q<VisualElement>("MapRoot");
            _topBar = root.Q<VisualElement>("TopBar");
            _infoBar = root.Q<Label>("InfoBar");
            _viewContextLabel = root.Q<Label>(ViewContextLabelName);
            _dateLabel = root.Q<Label>(DateLabelName);
            _paceBar = root.Q<VisualElement>(PaceBarName);
            _hoverLabel = root.Q<Label>(HoverLabelName);
            _provincePanel = root.Q<VisualElement>(ProvincePanelName);
            _countryPanel = root.Q<VisualElement>(CountryPanelName);
            _taxBar = root.Q<VisualElement>(TaxBarName);
            _pauseButton = root.Q<Button>(PauseButtonName);
            _zoomOutButton = root.Q<Button>(ZoomOutButtonName);
            _taxDownButton = root.Q<Button>(TaxDownButtonName);
            _taxUpButton = root.Q<Button>(TaxUpButtonName);
            _taxStatusLabel = root.Q<Label>(TaxStatusLabelName);
            _taxTitleLabel = root.Q<Label>(TaxTitleLabelName);
            _paceStatusBadge = root.Q<Label>(PaceStatusBadgeName);
            _investBar = root.Q<VisualElement>(InvestBarName);
            _investTitleLabel = root.Q<Label>(InvestTitleLabelName);
            _investStatusLabel = root.Q<Label>(InvestStatusLabelName);
            _investTaxButton = root.Q<Button>(InvestTaxButtonName);
            _investProdButton = root.Q<Button>(InvestProdButtonName);
            _investManButton = root.Q<Button>(InvestManButtonName);
            _buildFarmButton = root.Q<Button>(BuildFarmButtonName);
            _warBar = root.Q<VisualElement>(WarBarName);
            _warTitleLabel = root.Q<Label>(WarTitleLabelName);
            _warStatusLabel = root.Q<Label>(WarStatusLabelName);
            _declareWarButton = root.Q<Button>(DeclareWarButtonName);
            _proposePeaceButton = root.Q<Button>(ProposePeaceButtonName);
            _lawBar = root.Q<VisualElement>(LawBarName);
            _lawTitleLabel = root.Q<Label>(LawTitleLabelName);
            _lawStatusLabel = root.Q<Label>(LawStatusLabelName);
            _enactLawButton = root.Q<Button>(EnactLawButtonName);
            ApplyFrenchTaxChrome();
            ApplyFrenchInvestChrome();
            ApplyFrenchWarChrome();
            ApplyFrenchLawChrome();

            var steps = MapDisplaySystem.SpeedSteps;
            _speedButtons = new Button[steps.Length];
            for (var i = 0; i < steps.Length; i++)
            {
                var label = FormatSpeedLabel(steps[i]);
                _speedButtons[i] = root.Q<Button>(SpeedButtonNamePrefix + label);
            }

            return _mapRoot != null &&
                   _topBar != null &&
                   _infoBar != null &&
                   _paceBar != null &&
                   _hoverLabel != null &&
                   _provincePanel != null &&
                   _countryPanel != null &&
                   _taxBar != null &&
                   _pauseButton != null &&
                   _zoomOutButton != null &&
                   _taxDownButton != null &&
                   _taxUpButton != null &&
                   _taxStatusLabel != null &&
                   _investBar != null &&
                   _investTaxButton != null &&
                   _investProdButton != null &&
                   _investManButton != null &&
                   _buildFarmButton != null &&
                   _warBar != null &&
                   _declareWarButton != null &&
                   _proposePeaceButton != null &&
                   _lawBar != null &&
                   _enactLawButton != null &&
                   AllSpeedButtonsBound();
        }

        bool AllSpeedButtonsBound()
        {
            if (_speedButtons == null)
                return false;
            for (var i = 0; i < _speedButtons.Length; i++)
            {
                if (_speedButtons[i] == null)
                    return false;
            }
            return true;
        }

        void ApplyFrenchTaxChrome()
        {
            if (_taxTitleLabel != null)
                _taxTitleLabel.text = "Impôt";
            if (_taxDownButton != null)
            {
                _taxDownButton.text = "Impôt −";
                _taxDownButton.AddToClassList("hud-btn--tax");
            }

            if (_taxUpButton != null)
            {
                _taxUpButton.text = "Impôt +";
                _taxUpButton.AddToClassList("hud-btn--tax");
            }
        }

        void ApplyFrenchInvestChrome()
        {
            if (_investTitleLabel != null)
                _investTitleLabel.text = "Investir";
            if (_investTaxButton != null)
                _investTaxButton.text = "Tax +";
            if (_investProdButton != null)
                _investProdButton.text = "Prod +";
            if (_investManButton != null)
                _investManButton.text = "Man +";
            if (_buildFarmButton != null)
                _buildFarmButton.text = "Construire Ferme";
        }

        void ApplyFrenchWarChrome()
        {
            if (_warTitleLabel != null)
                _warTitleLabel.text = "Guerre";
            if (_declareWarButton != null)
                _declareWarButton.text = "Déclarer guerre";
            if (_proposePeaceButton != null)
                _proposePeaceButton.text = "Proposer paix";
        }

        void ApplyFrenchLawChrome()
        {
            if (_lawTitleLabel != null)
                _lawTitleLabel.text = "Lois";
            if (_enactLawButton != null)
                _enactLawButton.text = "Promulguer land_tax";
        }

        void BuildProgrammaticFallback(VisualElement root)
        {
            ApplyStyleSheets(root);

            _mapRoot = new VisualElement { name = "MapRoot" };
            _mapRoot.AddToClassList(ClassMap);
            _mapRoot.focusable = true;
            root.Add(_mapRoot);

            _topBar = new VisualElement { name = "TopBar" };
            _topBar.AddToClassList(ClassTopBar);
            root.Add(_topBar);

            var zoneContext = new VisualElement { name = "TopBarContext" };
            zoneContext.AddToClassList(ClassTopBarZone);
            zoneContext.AddToClassList(ClassTopBarContext);
            _viewContextLabel = new Label { name = ViewContextLabelName, text = "" };
            _viewContextLabel.AddToClassList(ClassViewContext);
            zoneContext.Add(_viewContextLabel);
            _topBar.Add(zoneContext);

            var zoneMetrics = new VisualElement { name = "TopBarMetrics" };
            zoneMetrics.AddToClassList(ClassTopBarZone);
            zoneMetrics.AddToClassList(ClassTopBarMetrics);
            _infoBar = new Label { name = "InfoBar", text = "" };
            _infoBar.AddToClassList(ClassInfoBar);
            zoneMetrics.Add(_infoBar);
            _topBar.Add(zoneMetrics);

            var zoneTime = new VisualElement { name = "TopBarTime" };
            zoneTime.AddToClassList(ClassTopBarZone);
            zoneTime.AddToClassList(ClassTopBarTime);
            _dateLabel = new Label { name = DateLabelName, text = "" };
            _dateLabel.AddToClassList(ClassDate);
            zoneTime.Add(_dateLabel);

            _paceBar = new VisualElement { name = PaceBarName };
            _paceBar.AddToClassList(ClassPaceBar);
            zoneTime.Add(_paceBar);
            _topBar.Add(zoneTime);

            _paceStatusBadge = new Label { name = PaceStatusBadgeName, text = "" };
            _paceStatusBadge.AddToClassList(ClassPaceBadge);
            _paceBar.Add(_paceStatusBadge);

            _pauseButton = CreateHudButton(PauseButtonName, "Pause");
            _paceBar.Add(_pauseButton);

            _zoomOutButton = CreateHudButton(ZoomOutButtonName, "Zoom-");
            _paceBar.Add(_zoomOutButton);

            var steps = MapDisplaySystem.SpeedSteps;
            _speedButtons = new Button[steps.Length];
            for (var i = 0; i < steps.Length; i++)
            {
                var label = FormatSpeedLabel(steps[i]);
                var btn = CreateHudButton(SpeedButtonNamePrefix + label, label);
                _speedButtons[i] = btn;
                _paceBar.Add(btn);
            }

            _hoverLabel = new Label { name = HoverLabelName, text = "" };
            _hoverLabel.AddToClassList(ClassHover);
            SetHidden(_hoverLabel, true);
            root.Add(_hoverLabel);

            _provincePanel = CreateStructuredPanel(ProvincePanelName, ClassPanelProvince);
            SetHidden(_provincePanel, true);
            root.Add(_provincePanel);

            _countryPanel = CreateStructuredPanel(CountryPanelName, ClassPanelCountry);
            SetHidden(_countryPanel, true);
            root.Add(_countryPanel);

            _taxBar = new VisualElement { name = TaxBarName };
            _taxBar.AddToClassList(ClassTaxBar);
            SetHidden(_taxBar, true);

            _taxTitleLabel = new Label { name = TaxTitleLabelName, text = "Impôt" };
            _taxTitleLabel.AddToClassList(ClassTaxTitle);
            _taxBar.Add(_taxTitleLabel);

            var taxControls = new VisualElement { name = "TaxControls" };
            taxControls.AddToClassList(ClassTaxControls);

            _taxDownButton = CreateHudButton(TaxDownButtonName, "Impôt −");
            _taxDownButton.AddToClassList("hud-btn--tax");
            taxControls.Add(_taxDownButton);

            _taxStatusLabel = new Label { name = TaxStatusLabelName, text = "" };
            _taxStatusLabel.AddToClassList(ClassTaxStatus);
            taxControls.Add(_taxStatusLabel);

            _taxUpButton = CreateHudButton(TaxUpButtonName, "Impôt +");
            _taxUpButton.AddToClassList("hud-btn--tax");
            taxControls.Add(_taxUpButton);

            _taxBar.Add(taxControls);
            root.Add(_taxBar);

            _investBar = new VisualElement { name = InvestBarName };
            _investBar.AddToClassList(ClassTaxBar);
            SetHidden(_investBar, true);

            _investTitleLabel = new Label { name = InvestTitleLabelName, text = "Investir" };
            _investTitleLabel.AddToClassList(ClassTaxTitle);
            _investBar.Add(_investTitleLabel);

            _investStatusLabel = new Label { name = InvestStatusLabelName, text = "" };
            _investStatusLabel.AddToClassList(ClassTaxStatus);
            _investBar.Add(_investStatusLabel);

            var investControls = new VisualElement { name = "InvestControls" };
            investControls.AddToClassList(ClassTaxControls);

            _investTaxButton = CreateHudButton(InvestTaxButtonName, "Tax +");
            _investTaxButton.AddToClassList("hud-btn--tax");
            investControls.Add(_investTaxButton);

            _investProdButton = CreateHudButton(InvestProdButtonName, "Prod +");
            _investProdButton.AddToClassList("hud-btn--tax");
            investControls.Add(_investProdButton);

            _investManButton = CreateHudButton(InvestManButtonName, "Man +");
            _investManButton.AddToClassList("hud-btn--tax");
            investControls.Add(_investManButton);

            _buildFarmButton = CreateHudButton(BuildFarmButtonName, "Construire Ferme");
            _buildFarmButton.AddToClassList("hud-btn--tax");
            investControls.Add(_buildFarmButton);

            _investBar.Add(investControls);
            root.Add(_investBar);

            _warBar = new VisualElement { name = WarBarName };
            _warBar.AddToClassList(ClassTaxBar);
            SetHidden(_warBar, true);

            _warTitleLabel = new Label { name = WarTitleLabelName, text = "Guerre" };
            _warTitleLabel.AddToClassList(ClassTaxTitle);
            _warBar.Add(_warTitleLabel);

            _warStatusLabel = new Label { name = WarStatusLabelName, text = "" };
            _warStatusLabel.AddToClassList(ClassTaxStatus);
            _warBar.Add(_warStatusLabel);

            var warControls = new VisualElement { name = "WarControls" };
            warControls.AddToClassList(ClassTaxControls);

            _declareWarButton = CreateHudButton(DeclareWarButtonName, "Déclarer guerre");
            _declareWarButton.AddToClassList("hud-btn--tax");
            warControls.Add(_declareWarButton);

            _proposePeaceButton = CreateHudButton(ProposePeaceButtonName, "Proposer paix");
            _proposePeaceButton.AddToClassList("hud-btn--tax");
            warControls.Add(_proposePeaceButton);

            _warBar.Add(warControls);
            root.Add(_warBar);

            _lawBar = new VisualElement { name = LawBarName };
            _lawBar.AddToClassList(ClassTaxBar);
            SetHidden(_lawBar, true);

            _lawTitleLabel = new Label { name = LawTitleLabelName, text = "Lois" };
            _lawTitleLabel.AddToClassList(ClassTaxTitle);
            _lawBar.Add(_lawTitleLabel);

            _lawStatusLabel = new Label { name = LawStatusLabelName, text = "" };
            _lawStatusLabel.AddToClassList(ClassTaxStatus);
            _lawBar.Add(_lawStatusLabel);

            var lawControls = new VisualElement { name = "LawControls" };
            lawControls.AddToClassList(ClassTaxControls);

            _enactLawButton = CreateHudButton(EnactLawButtonName, "Promulguer land_tax");
            _enactLawButton.AddToClassList("hud-btn--tax");
            lawControls.Add(_enactLawButton);

            _lawBar.Add(lawControls);
            root.Add(_lawBar);
        }

        static VisualElement CreateStructuredPanel(string name, string variantClass)
        {
            var panel = new VisualElement { name = name };
            panel.AddToClassList(ClassPanel);
            panel.AddToClassList(variantClass);

            var title = new Label { name = name + "_Title", text = "" };
            title.AddToClassList("panel__title");
            panel.Add(title);

            var subtitle = new Label { name = name + "_Subtitle", text = "" };
            subtitle.AddToClassList("panel__subtitle");
            panel.Add(subtitle);

            var sections = new VisualElement { name = name + "_Sections" };
            sections.AddToClassList("panel__sections");
            panel.Add(sections);

            var alerts = new VisualElement { name = name + "_Alerts" };
            alerts.AddToClassList("panel__alerts");
            panel.Add(alerts);

            return panel;
        }

        static Button CreateHudButton(string name, string text)
        {
            var btn = new Button { name = name, text = text };
            btn.AddToClassList(ClassBtn);
            btn.AddToClassList(ClassBtnIdle);
            return btn;
        }

        void WireMapCallbacks()
        {
            if (_mapRoot == null)
                return;
            _mapRoot.RegisterCallback<PointerDownEvent>(OnMapPointerDown);
            _mapRoot.RegisterCallback<PointerMoveEvent>(OnMapPointerMove);
            _mapRoot.RegisterCallback<PointerUpEvent>(OnMapPointerUp);
            _mapRoot.RegisterCallback<PointerLeaveEvent>(OnMapPointerLeave);
            _mapRoot.RegisterCallback<WheelEvent>(OnMapWheel);
        }

        void WireControlCallbacks()
        {
            if (_pauseButton != null)
                _pauseButton.clicked += OnPauseClicked;
            if (_zoomOutButton != null)
                _zoomOutButton.clicked += OnZoomOutClicked;
            if (_taxDownButton != null)
                _taxDownButton.clicked += () => OnTaxStepClicked(-1);
            if (_taxUpButton != null)
                _taxUpButton.clicked += () => OnTaxStepClicked(+1);
            if (_investTaxButton != null)
                _investTaxButton.clicked += () => OnInvestClicked(ProvinceDevelopmentInvestment.AxisTax);
            if (_investProdButton != null)
                _investProdButton.clicked += () => OnInvestClicked(ProvinceDevelopmentInvestment.AxisProduction);
            if (_investManButton != null)
                _investManButton.clicked += () => OnInvestClicked(ProvinceDevelopmentInvestment.AxisManpower);
            if (_buildFarmButton != null)
                _buildFarmButton.clicked += OnBuildFarmClicked;
            if (_declareWarButton != null)
                _declareWarButton.clicked += OnDeclareWarClicked;
            if (_proposePeaceButton != null)
                _proposePeaceButton.clicked += OnProposePeaceClicked;
            if (_enactLawButton != null)
                _enactLawButton.clicked += OnEnactLawClicked;

            if (_speedButtons == null)
                return;
            for (var i = 0; i < _speedButtons.Length; i++)
            {
                var btn = _speedButtons[i];
                if (btn == null)
                    continue;
                var captured = MapDisplaySystem.SpeedSteps[i];
                btn.clicked += () => OnSpeedTierClicked(captured);
            }
        }

        static string FormatSpeedLabel(float speed)
        {
            if (Mathf.Approximately(speed, 0.5f))
                return "0.5x";
            return ((int)speed) + "x";
        }

        void OnZoomOutClicked()
        {
            MapDisplaySystem.TryZoomOut();
        }

        /// <summary>Clic programmatique (tests) — même chemin que le bouton UI.</summary>
        public void SimulateZoomOutClick() => OnZoomOutClicked();

        /// <summary>Clic programmatique carte (tests) — px/py texture y=0 bas.</summary>
        public bool SimulateMapClick(int px, int py) =>
            MapDisplaySystem.TryClickAtTexturePixel(px, py);

        void OnMapPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0)
                return;
            _dragging = true;
            _dragMoved = false;
            _lastPointerLocal = (Vector2)evt.localPosition;
            if (TryLocalToTexture(_lastPointerLocal, out var px, out var py))
            {
                _pointerDownPx = px;
                _pointerDownPy = py;
            }
            else
            {
                _pointerDownPx = -1;
                _pointerDownPy = -1;
            }

            _mapRoot.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        void OnMapPointerMove(PointerMoveEvent evt)
        {
            var local = (Vector2)evt.localPosition;
            if (TryLocalToTexture(local, out var px, out var py))
                MapDisplaySystem.UpdateHoverAtTexturePixel(px, py);
            else
                MapViewport.ClearHover();

            if (!_dragging)
                return;

            var delta = local - _lastPointerLocal;
            if (delta.sqrMagnitude > 4f)
                _dragMoved = true;
            if (_dragMoved && _mapTexture != null)
            {
                var rect = _mapRoot.contentRect;
                var scale = Mathf.Max(
                    rect.width / _mapTexture.width,
                    rect.height / _mapTexture.height);
                if (scale > 0.0001f)
                {
                    var dpx = delta.x / scale;
                    var dpy = delta.y / scale;
                    MapDisplaySystem.TryPanByTextureDelta(dpx, dpy);
                }
            }

            _lastPointerLocal = local;
            evt.StopPropagation();
        }

        void OnMapPointerUp(PointerUpEvent evt)
        {
            if (evt.button != 0)
                return;
            if (_dragging && !_dragMoved && _pointerDownPx >= 0)
                MapDisplaySystem.TryClickAtTexturePixel(_pointerDownPx, _pointerDownPy);

            _dragging = false;
            _dragMoved = false;
            _mapRoot.ReleasePointer(evt.pointerId);
            evt.StopPropagation();
        }

        void OnMapPointerLeave(PointerLeaveEvent evt)
        {
            MapViewport.ClearHover();
            RefreshHoverLabel("");
        }

        void OnMapWheel(WheelEvent evt)
        {
            var local = evt.localMousePosition;
            if (!TryLocalToTexture(local, out var px, out var py))
                return;
            MapDisplaySystem.TryWheelZoomAtTexturePixel(px, py, -evt.delta.y);
            evt.StopPropagation();
        }

        bool TryLocalToTexture(Vector2 local, out int px, out int py)
        {
            px = 0;
            py = 0;
            if (_mapTexture == null || _mapRoot == null)
                return false;
            var rect = _mapRoot.contentRect;
            return MapClickPicker.TryLocalToTexturePixel(
                local.x, local.y, rect.width, rect.height,
                _mapTexture.width, _mapTexture.height, uiYDown: true,
                out px, out py);
        }

        /// <summary>Panneau survol (nom / pays).</summary>
        public void RefreshHoverLabel(string label)
        {
            if (_hoverLabel == null)
                return;
            if (string.IsNullOrEmpty(label))
            {
                SetHidden(_hoverLabel, true);
                _hoverLabel.text = "";
                return;
            }

            SetHidden(_hoverLabel, false);
            _hoverLabel.text = label;
        }

        void OnPauseClicked()
        {
            if (!TryGetWorldState(out var em, out var entity, out var ws))
                return;
            ws.IsPaused = !ws.IsPaused;
            em.SetComponentData(entity, ws);
            ApplyPaceVisuals(ws.IsPaused, ws.SimulationSpeed);
        }

        void OnSpeedTierClicked(float speed)
        {
            if (!TryGetWorldState(out var em, out var entity, out var ws))
                return;
            ws.SimulationSpeed = speed;
            em.SetComponentData(entity, ws);
            ApplyPaceVisuals(ws.IsPaused, ws.SimulationSpeed);
        }

        /// <summary>Clic programmatique (tests PlayMode) — même chemin que le bouton UI.</summary>
        public void SimulatePauseClick()
        {
            OnPauseClicked();
        }

        /// <summary>Clic programmatique sur un palier (tests PlayMode).</summary>
        public void SimulateSpeedTierClick(float speed)
        {
            OnSpeedTierClicked(speed);
        }

        /// <summary>Clic programmatique Tax± (tests) — même chemin que le bouton UI.</summary>
        public void SimulateTaxStepClick(int direction)
        {
            OnTaxStepClicked(direction);
        }

        /// <summary>Clic programmatique Invest (tests) — même chemin que le bouton UI.</summary>
        public void SimulateInvestClick(byte axis)
        {
            OnInvestClicked(axis);
        }

        /// <summary>Clic programmatique Construire Ferme (tests).</summary>
        public void SimulateBuildFarmClick()
        {
            OnBuildFarmClicked();
        }

        void EnsureTickControlSingleton()
        {
            var world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return;
            if (!HasWorldState(world))
                return;

            TickControlBootstrap.Ensure(world.EntityManager);
        }

        static bool HasWorldState(Unity.Entities.World world)
        {
            using var q = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<WorldState>());
            return q.CalculateEntityCount() == 1;
        }

        static bool TryGetWorldState(out EntityManager em, out Entity entity, out WorldState ws)
        {
            em = default;
            entity = Entity.Null;
            ws = default;
            var world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return false;
            em = world.EntityManager;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<WorldState>());
            if (q.CalculateEntityCount() != 1)
                return false;
            entity = q.GetSingletonEntity();
            ws = q.GetSingleton<WorldState>();
            return true;
        }

        /// <summary>
        /// Met à jour la texture affichée (appelé depuis MapDisplaySystem, lecture seule).
        /// Convention v1_079 : <paramref name="pixels"/> est un buffer carte nord@py0
        /// (glyphes droits, sans compensation locale). UI Toolkit affiche py=0 en haut
        /// de l'écran — équivalent visuel de WriteMapBufferPng. Ne pas retourner ici.
        /// </summary>
        public void PresentFrame(Color32[] pixels, int width, int height, string metricsLine)
        {
            if (pixels == null || width <= 0 || height <= 0)
                return;

            _lastMetricsLine = metricsLine ?? "";

            if (_mapTexture == null || _mapTexture.width != width || _mapTexture.height != height)
            {
                if (_mapTexture != null)
                    Destroy(_mapTexture);
                _mapTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    name = "InGameMap"
                };
            }

            _mapTexture.SetPixels32(pixels);
            _mapTexture.Apply(false, false);

            if (_mapRoot != null)
            {
                // Dynamique : image carte (ne peut pas vivre en USS).
                _mapRoot.style.backgroundImage = new StyleBackground(_mapTexture);
                _mapRoot.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
            }

            RefreshInfoBar(_lastMetricsLine);
        }

        /// <summary>
        /// v1_095 — présente un fond de carte rendu par le GPU, SANS COPIE.
        ///
        /// C'est la raison d'être du rendu GPU : PresentFrame ci-dessus fait un
        /// SetPixels32 précédé d'un ReadPixels côté appelant, soit ~18 ms mesurées
        /// en 960×720 — plus cher que tout le reste réuni. Ici la RenderTexture va
        /// directement à l'élément, et le déplacement de la carte ne coûte qu'un Blit.
        ///
        /// L'orientation est celle de PresentFrame : le shader produit « nord en
        /// rangée 0 » comme les buffers CPU, donc les deux chemins sont
        /// interchangeables sans conversion.
        /// </summary>
        public void PresentRenderTexture(RenderTexture map, string metricsLine)
        {
            if (map == null)
                return;

            _lastMetricsLine = metricsLine ?? "";
            if (_mapRoot != null)
            {
                _mapRoot.style.backgroundImage = Background.FromRenderTexture(map);
                _mapRoot.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
            }

            RefreshInfoBar(_lastMetricsLine);
        }

        /// <summary>Met à jour la barre UI (année/tick/métriques + pause/vitesse).</summary>
        public void RefreshInfoBar(string metricsLine)
        {
            if (_infoBar == null)
                return;

            var line = metricsLine ?? "";
            var status = FormatPaceStatus();
            _lastInfoBarText = string.IsNullOrEmpty(status) ? line : line + "  " + status;

            HudDetailPresenter.SplitMetricsLine(line, out var viewContext, out var metricsCore, out var dateLabel);
            if (_viewContextLabel != null)
                _viewContextLabel.text = viewContext;
            if (_dateLabel != null)
                _dateLabel.text = dateLabel;
            _infoBar.text = string.IsNullOrEmpty(status)
                ? metricsCore
                : (string.IsNullOrEmpty(metricsCore) ? status : metricsCore + "  " + status);
            RefreshPaceControlsFromWorld();
        }

        /// <summary>Panneau province (agrégats réels) — lecture seule, structure sectionnée.</summary>
        public void RefreshProvincePanel(string detail)
        {
            _lastProvinceDetail = detail ?? "";
            if (_provincePanel == null)
                return;

            if (string.IsNullOrEmpty(_lastProvinceDetail))
            {
                SetHidden(_provincePanel, true);
                _provincePanel.EnableInClassList(ClassPanelOpen, false);
                HudDetailPresenter.Populate(_provincePanel, "", "Province");
                RefreshInvestControls();
                return;
            }

            // Province prime sur pays (niveaux exclusifs).
            if (_countryPanel != null)
            {
                SetHidden(_countryPanel, true);
                _countryPanel.EnableInClassList(ClassPanelOpen, false);
                HudDetailPresenter.Populate(_countryPanel, "", "Country");
            }

            SetHidden(_provincePanel, false);
            _provincePanel.EnableInClassList(ClassPanelOpen, true);
            HudDetailPresenter.Populate(_provincePanel, _lastProvinceDetail, "Province");
            RefreshTaxControls();
            RefreshInvestControls();
        }

        /// <summary>Fiche pays (niveau Country) — même structure sectionnée, lecture seule.</summary>
        public void RefreshCountryPanel(string detail)
        {
            _lastCountryDetail = detail ?? "";
            if (_countryPanel == null)
                return;

            if (string.IsNullOrEmpty(_lastCountryDetail) ||
                !string.IsNullOrEmpty(_lastProvinceDetail))
            {
                SetHidden(_countryPanel, true);
                _countryPanel.EnableInClassList(ClassPanelOpen, false);
                if (string.IsNullOrEmpty(_lastCountryDetail))
                    HudDetailPresenter.Populate(_countryPanel, "", "Country");
                RefreshTaxControls();
                RefreshWarControls();
                RefreshLawControls();
                return;
            }

            SetHidden(_countryPanel, false);
            _countryPanel.EnableInClassList(ClassPanelOpen, true);
            HudDetailPresenter.Populate(_countryPanel, _lastCountryDetail, "Country");
            RefreshTaxControls();
            RefreshWarControls();
            RefreshLawControls();
        }

        /// <summary>
        /// Tax± : enqueue une intention SetProductionTaxRate. Aucune écriture TaxPolicy ici.
        /// </summary>
        void OnTaxStepClicked(int direction)
        {
            if (!TryGetEntityManager(out var em))
                return;

            var viewedId = MapViewport.State.TargetCountryId;
            var controlledId = PlayerControl.DefaultControlledCountryId;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerControl>()))
            {
                if (!q.IsEmptyIgnoreFilter)
                    controlledId = q.GetSingleton<PlayerControl>().ControlledCountryId;
            }

            if (viewedId != controlledId)
            {
                if (_taxStatusLabel != null)
                    _taxStatusLabel.text = "REFUS — pas votre pays";
                return;
            }

            var currentRate = TaxPolicyLimits.DefaultProductionTaxRate;
            using (var q = em.CreateEntityQuery(
                       ComponentType.ReadOnly<CountryData>(),
                       ComponentType.ReadOnly<TaxPolicy>()))
            using (var countries = q.ToComponentDataArray<CountryData>(Unity.Collections.Allocator.Temp))
            using (var policies = q.ToComponentDataArray<TaxPolicy>(Unity.Collections.Allocator.Temp))
            {
                for (var i = 0; i < countries.Length; i++)
                {
                    if (countries[i].CountryId != controlledId)
                        continue;
                    currentRate = policies[i].ProductionTaxRate;
                    break;
                }
            }

            var next = TaxPolicyLimits.Clamp(currentRate + direction * TaxPolicyLimits.UiStep);
            PlayerIntentionSubmit.EnqueueSetProductionTaxRate(em, controlledId, next);
            if (_taxStatusLabel != null)
            {
                _taxStatusLabel.text =
                    "Demande : " + HudValueFormatter.FormatTaxPercent(next);
            }
        }

        /// <summary>
        /// Invest± : enqueue InvestProvinceDevelopment. Aucune écriture ProvinceDevelopment ici.
        /// </summary>
        void OnInvestClicked(byte axis)
        {
            if (!TryGetEntityManager(out var em))
                return;

            var provinceId = MapViewport.State.TargetProvinceId;
            if (provinceId < 0)
            {
                if (_investStatusLabel != null)
                    _investStatusLabel.text = "REFUS — aucune province";
                return;
            }

            var controlledId = PlayerControl.DefaultControlledCountryId;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerControl>()))
            {
                if (!q.IsEmptyIgnoreFilter)
                    controlledId = q.GetSingleton<PlayerControl>().ControlledCountryId;
            }

            if (!DevelopmentHudSnapshot.TryCapture(
                    em, provinceId, out var dev, out _, out _, out _, out var owned) ||
                !owned)
            {
                if (_investStatusLabel != null)
                    _investStatusLabel.text = "REFUS — pas votre province";
                return;
            }

            PlayerIntentionSubmit.EnqueueInvestProvinceDevelopment(
                em, controlledId, provinceId, axis);
            if (_investStatusLabel != null)
            {
                var axisName = axis == ProvinceDevelopmentInvestment.AxisTax ? "TAX"
                    : axis == ProvinceDevelopmentInvestment.AxisProduction ? "PROD" : "MAN";
                var current = ProvinceDevelopmentInvestment.ReadAxis(in dev, axis);
                _investStatusLabel.text =
                    "Demande " + axisName + " " + current + "→" + (current + 1) +
                    "  (" + DevelopmentHudSnapshot.FormatHudLine(in dev) + ")";
            }
        }

        /// <summary>
        /// Construire : enqueue StartBuildingConstruction via ApplyBuild existant.
        /// Ville = plus petit CityId de la province (déterministe).
        /// </summary>
        void OnBuildFarmClicked()
        {
            if (!TryGetEntityManager(out var em))
                return;

            var provinceId = MapViewport.State.TargetProvinceId;
            if (provinceId < 0)
            {
                if (_investStatusLabel != null)
                    _investStatusLabel.text = "REFUS — aucune province";
                return;
            }

            var controlledId = PlayerControl.DefaultControlledCountryId;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerControl>()))
            {
                if (!q.IsEmptyIgnoreFilter)
                    controlledId = q.GetSingleton<PlayerControl>().ControlledCountryId;
            }

            if (!TryFindCityInProvince(em, provinceId, out var cityId))
            {
                if (_investStatusLabel != null)
                    _investStatusLabel.text = "REFUS — pas de ville";
                return;
            }

            PlayerIntentionSubmit.EnqueueStartBuildingConstruction(
                em, controlledId, cityId, BuildingType.Farm);
            if (_investStatusLabel != null)
                _investStatusLabel.text = "Demande construction Farm @ville " + cityId;
        }

        void RefreshInvestControls()
        {
            if (_investBar == null)
                return;

            var provinceOpen = _provincePanel != null &&
                               !_provincePanel.ClassListContains(ClassHidden) &&
                               !string.IsNullOrEmpty(_lastProvinceDetail);
            if (!provinceOpen || !TryGetEntityManager(out var em))
            {
                SetHidden(_investBar, true);
                return;
            }

            var provinceId = MapViewport.State.TargetProvinceId;
            if (!DevelopmentHudSnapshot.TryCapture(
                    em, provinceId, out var dev, out var cTax, out var cProd, out var cMan,
                    out var owned))
            {
                SetHidden(_investBar, true);
                return;
            }

            SetHidden(_investBar, false);
            if (_investStatusLabel != null)
            {
                _investStatusLabel.text = DevelopmentHudSnapshot.FormatHudLine(in dev) +
                    (owned
                        ? "  coût T/P/M " +
                          cTax.ToString("0") + "/" +
                          cProd.ToString("0") + "/" +
                          cMan.ToString("0")
                        : "  (lecture seule)");
            }

            var canAct = owned;
            SetButtonEnabled(_investTaxButton, canAct);
            SetButtonEnabled(_investProdButton, canAct);
            SetButtonEnabled(_investManButton, canAct);
            SetButtonEnabled(_buildFarmButton, canAct);
        }

        /// <summary>
        /// Guerre : enqueue DeclareWar / ProposePeace. Aucune écriture WarData ici.
        /// Visible quand le panneau pays affiche un pays AUTRE que le joueur.
        /// </summary>
        void OnDeclareWarClicked()
        {
            if (!TryGetEntityManager(out var em))
                return;

            var viewedId = MapViewport.State.TargetCountryId;
            var controlledId = PlayerControl.DefaultControlledCountryId;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerControl>()))
            {
                if (!q.IsEmptyIgnoreFilter)
                    controlledId = q.GetSingleton<PlayerControl>().ControlledCountryId;
            }

            if (viewedId < 0 || viewedId == controlledId)
            {
                if (_warStatusLabel != null)
                    _warStatusLabel.text = "REFUS — sélectionnez un autre pays";
                return;
            }

            PlayerIntentionSubmit.EnqueueDeclareWar(em, controlledId, viewedId);
            if (_warStatusLabel != null)
                _warStatusLabel.text = "Demande : guerre → pays " + viewedId;
        }

        void OnProposePeaceClicked()
        {
            if (!TryGetEntityManager(out var em))
                return;

            var viewedId = MapViewport.State.TargetCountryId;
            var controlledId = PlayerControl.DefaultControlledCountryId;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerControl>()))
            {
                if (!q.IsEmptyIgnoreFilter)
                    controlledId = q.GetSingleton<PlayerControl>().ControlledCountryId;
            }

            if (viewedId < 0 || viewedId == controlledId)
            {
                if (_warStatusLabel != null)
                    _warStatusLabel.text = "REFUS — sélectionnez un autre pays";
                return;
            }

            PlayerIntentionSubmit.EnqueueProposePeace(em, controlledId, viewedId);
            if (_warStatusLabel != null)
                _warStatusLabel.text = "Demande : paix → pays " + viewedId;
        }

        /// <summary>Clic programmatique (tests) — même chemin que le bouton UI.</summary>
        public void SimulateDeclareWarClick() => OnDeclareWarClicked();

        /// <summary>Clic programmatique (tests) — même chemin que le bouton UI.</summary>
        public void SimulateProposePeaceClick() => OnProposePeaceClicked();

        /// <summary>
        /// Lois : enqueue EnactLaw. Aucune écriture EnactedLaw ici.
        /// </summary>
        void OnEnactLawClicked()
        {
            if (!TryGetEntityManager(out var em))
                return;

            var viewedId = MapViewport.State.TargetCountryId;
            var controlledId = PlayerControl.DefaultControlledCountryId;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerControl>()))
            {
                if (!q.IsEmptyIgnoreFilter)
                    controlledId = q.GetSingleton<PlayerControl>().ControlledCountryId;
            }

            if (viewedId != controlledId)
            {
                if (_lawStatusLabel != null)
                    _lawStatusLabel.text = "REFUS — pas votre pays";
                return;
            }

            PlayerIntentionSubmit.EnqueueEnactLaw(em, controlledId, DefaultEnactLawId);
            if (_lawStatusLabel != null)
                _lawStatusLabel.text = "Demande : " + DefaultEnactLawId;
        }

        /// <summary>Clic programmatique (tests) — même chemin que le bouton UI.</summary>
        public void SimulateEnactLawClick() => OnEnactLawClicked();

        void RefreshLawControls()
        {
            if (_lawBar == null)
                return;

            var showCountry = _countryPanel != null &&
                              !IsHidden(_countryPanel) &&
                              !string.IsNullOrEmpty(_lastCountryDetail);
            if (!showCountry || !TryGetEntityManager(out var em))
            {
                SetHidden(_lawBar, true);
                return;
            }

            var viewedId = MapViewport.State.TargetCountryId;
            var controlledId = PlayerControl.DefaultControlledCountryId;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerControl>()))
            {
                if (!q.IsEmptyIgnoreFilter)
                    controlledId = q.GetSingleton<PlayerControl>().ControlledCountryId;
            }

            // Lois : actions sur son propre pays uniquement.
            if (viewedId < 0 || viewedId != controlledId)
            {
                SetHidden(_lawBar, true);
                return;
            }

            SetHidden(_lawBar, false);
            _lawBar.style.bottom = 100;
            if (_countryPanel != null)
                _countryPanel.style.bottom = 240;

            var lawList = "(aucune)";
            var lawMod = 0f;
            if (TryResolveCountryEntity(em, controlledId, out var countryEntity))
            {
                lawMod = LawTaxEffect.SumTaxModForCountry(em, countryEntity);
                if (em.HasBuffer<EnactedLaw>(countryEntity))
                {
                    var buf = em.GetBuffer<EnactedLaw>(countryEntity);
                    if (buf.Length > 0)
                    {
                        var parts = new System.Text.StringBuilder(64);
                        for (var i = 0; i < buf.Length; i++)
                        {
                            if (i > 0)
                                parts.Append(", ");
                            parts.Append(buf[i].LawId.ToString());
                        }

                        lawList = parts.ToString();
                    }
                }
            }

            if (_lawStatusLabel != null)
            {
                _lawStatusLabel.text =
                    "En vigueur : " + lawList +
                    "  ·  lawmod=" + lawMod.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            }

            SetButtonEnabled(_enactLawButton, true);
        }

        void RefreshWarControls()
        {
            if (_warBar == null)
                return;

            var showCountry = _countryPanel != null &&
                              !IsHidden(_countryPanel) &&
                              !string.IsNullOrEmpty(_lastCountryDetail);
            if (!showCountry || !TryGetEntityManager(out var em))
            {
                SetHidden(_warBar, true);
                return;
            }

            var viewedId = MapViewport.State.TargetCountryId;
            var controlledId = PlayerControl.DefaultControlledCountryId;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerControl>()))
            {
                if (!q.IsEmptyIgnoreFilter)
                    controlledId = q.GetSingleton<PlayerControl>().ControlledCountryId;
            }

            // Guerre : actions sur un pays étranger visualisé.
            if (viewedId < 0 || viewedId == controlledId)
            {
                SetHidden(_warBar, true);
                return;
            }

            SetHidden(_warBar, false);
            _warBar.style.bottom = 100;
            if (_countryPanel != null)
                _countryPanel.style.bottom = 240;

            var atWar = AreCountriesAtWar(em, controlledId, viewedId);
            var warsForViewed = CountActiveWarsForCountry(em, viewedId);
            if (_warStatusLabel != null)
            {
                _warStatusLabel.text = atWar
                    ? "EN GUERRE avec pays " + viewedId + "  ·  guerres actives cible=" + warsForViewed
                    : "Paix avec pays " + viewedId + "  ·  guerres actives cible=" + warsForViewed;
            }

            SetButtonEnabled(_declareWarButton, !atWar);
            SetButtonEnabled(_proposePeaceButton, atWar);
        }

        static bool AreCountriesAtWar(EntityManager em, int countryIdA, int countryIdB)
        {
            if (!TryResolveCountryEntity(em, countryIdA, out var a) ||
                !TryResolveCountryEntity(em, countryIdB, out var b))
                return false;

            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<WarData>());
            using var wars = q.ToComponentDataArray<WarData>(Unity.Collections.Allocator.Temp);
            for (var i = 0; i < wars.Length; i++)
            {
                if (!wars[i].IsActive)
                    continue;
                if ((wars[i].Attacker == a && wars[i].Defender == b) ||
                    (wars[i].Attacker == b && wars[i].Defender == a))
                    return true;
            }

            return false;
        }

        static int CountActiveWarsForCountry(EntityManager em, int countryId)
        {
            if (!TryResolveCountryEntity(em, countryId, out var entity))
                return 0;
            var n = 0;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<WarData>());
            using var wars = q.ToComponentDataArray<WarData>(Unity.Collections.Allocator.Temp);
            for (var i = 0; i < wars.Length; i++)
            {
                if (!wars[i].IsActive)
                    continue;
                if (wars[i].Attacker == entity || wars[i].Defender == entity)
                    n++;
            }

            return n;
        }

        static bool TryResolveCountryEntity(EntityManager em, int countryId, out Entity entity)
        {
            entity = Entity.Null;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<CountryData>());
            using var entities = q.ToEntityArray(Unity.Collections.Allocator.Temp);
            using var countries = q.ToComponentDataArray<CountryData>(Unity.Collections.Allocator.Temp);
            for (var i = 0; i < countries.Length; i++)
            {
                if (countries[i].CountryId != countryId)
                    continue;
                entity = entities[i];
                return true;
            }

            return false;
        }

        static bool TryFindCityInProvince(EntityManager em, int provinceId, out int cityId)
        {
            cityId = -1;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<CityData>());
            using var cities = q.ToComponentDataArray<CityData>(Unity.Collections.Allocator.Temp);
            for (var i = 0; i < cities.Length; i++)
            {
                if (cities[i].ProvinceId != provinceId)
                    continue;
                if (cityId < 0 || cities[i].CityId < cityId)
                    cityId = cities[i].CityId;
            }

            return cityId >= 0;
        }

        static void SetButtonEnabled(Button button, bool enabled)
        {
            if (button == null)
                return;
            button.SetEnabled(enabled);
            button.EnableInClassList(ClassBtnDisabled, !enabled);
        }

        void RefreshTaxControls()
        {
            if (_taxBar == null)
                return;

            var showCountry = _countryPanel != null &&
                              !IsHidden(_countryPanel) &&
                              !string.IsNullOrEmpty(_lastCountryDetail);
            if (!showCountry)
            {
                SetHidden(_taxBar, true);
                if (_countryPanel != null)
                    _countryPanel.style.bottom = 10;
                return;
            }

            SetHidden(_taxBar, false);
            // Décision souveraine empilée au-dessus du panneau pays (valeur dynamique de layout).
            _taxBar.style.bottom = 10;
            var warOpen = _warBar != null && !IsHidden(_warBar);
            _countryPanel.style.bottom = warOpen ? 240 : 148;

            if (!TryGetEntityManager(out var em))
            {
                if (_taxStatusLabel != null)
                    _taxStatusLabel.text = "";
                return;
            }

            var viewedId = MapViewport.State.TargetCountryId;
            var controlledId = PlayerControl.DefaultControlledCountryId;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerControl>()))
            {
                if (!q.IsEmptyIgnoreFilter)
                    controlledId = q.GetSingleton<PlayerControl>().ControlledCountryId;
            }

            var isPlayer = viewedId == controlledId;
            ApplyButtonEnabled(_taxDownButton, isPlayer);
            ApplyButtonEnabled(_taxUpButton, isPlayer);

            float rate = TaxPolicyLimits.DefaultProductionTaxRate;
            float income = 0f;
            float debt = 0f;
            float gold = 0f;
            using (var q = em.CreateEntityQuery(
                       ComponentType.ReadOnly<CountryData>(),
                       ComponentType.ReadOnly<TaxPolicy>(),
                       ComponentType.ReadOnly<TreasuryData>()))
            using (var countries = q.ToComponentDataArray<CountryData>(Unity.Collections.Allocator.Temp))
            using (var policies = q.ToComponentDataArray<TaxPolicy>(Unity.Collections.Allocator.Temp))
            using (var treasuries = q.ToComponentDataArray<TreasuryData>(Unity.Collections.Allocator.Temp))
            {
                for (var i = 0; i < countries.Length; i++)
                {
                    if (countries[i].CountryId != viewedId)
                        continue;
                    rate = policies[i].ProductionTaxRate;
                    income = treasuries[i].Income;
                    debt = treasuries[i].Debt;
                    gold = treasuries[i].Balance;
                    break;
                }
            }

            var debtTrend = "";
            if (!float.IsNaN(_lastSeenDebt))
            {
                var delta = debt - _lastSeenDebt;
                if (delta > 0.05f)
                    debtTrend = "en hausse";
                else if (delta < -0.05f)
                    debtTrend = "en baisse";
                else
                    debtTrend = "stable";
            }

            _lastSeenDebt = debt;

            TaxCostSnapshot.Capture(em, viewedId, out var sat, out var hungry, out var hungryProvinces);

            if (_taxStatusLabel != null)
            {
                var who = isPlayer ? "Vous" : "IA";
                var rateTxt = HudValueFormatter.FormatTaxPercent(rate);
                var minTxt = HudValueFormatter.FormatTaxPercent(TaxPolicyLimits.MinProductionTaxRate);
                var maxTxt = HudValueFormatter.FormatTaxPercent(TaxPolicyLimits.MaxProductionTaxRate);
                var line1 = who + "  ·  Taux " + rateTxt + "  ·  plage " + minTxt + " – " + maxTxt;
                var line2 = "Revenu " + HudValueFormatter.FormatMoney(income) +
                            "  ·  Trésor " + HudValueFormatter.FormatMoney(gold) +
                            "  ·  Dette " + HudValueFormatter.FormatMoney(debt);
                if (!string.IsNullOrEmpty(debtTrend))
                    line2 += " (" + debtTrend + ")";
                // v1_086 — coût visible à côté du levier (pas seulement la recette).
                var line3 = "Sat " + HudValueFormatter.FormatNumber(sat, "0.000") +
                            "  ·  Affamés " + hungry.ToString() +
                            "  ·  Prov. affamées " + hungryProvinces.ToString();
                _taxStatusLabel.text = line1 + "\n" + line2 + "\n" + line3;
            }
        }

        static bool TryGetEntityManager(out EntityManager em)
        {
            em = default;
            var world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return false;
            em = world.EntityManager;
            return true;
        }

        void RefreshPaceControlsFromWorld()
        {
            if (!TryGetWorldState(out _, out _, out var ws))
                return;
            ApplyPaceVisuals(ws.IsPaused, ws.SimulationSpeed);
        }

        void ApplyPaceVisuals(bool isPaused, float simulationSpeed)
        {
            if (_pauseButton != null)
            {
                // Action du bouton (≠ état courant).
                _pauseButton.text = isPaused ? "Lecture" : "Pause";
                SetBtnClasses(_pauseButton, idle: !isPaused, active: false, paused: isPaused, disabled: false);
            }

            if (_paceStatusBadge != null)
            {
                if (isPaused)
                {
                    _paceStatusBadge.text = "EN PAUSE";
                    _paceStatusBadge.EnableInClassList(ClassPaceBadgePaused, true);
                    SetHidden(_paceStatusBadge, false);
                }
                else
                {
                    _paceStatusBadge.text = "VITESSE x" + FormatSpeedLabel(simulationSpeed).Replace("x", "");
                    _paceStatusBadge.EnableInClassList(ClassPaceBadgePaused, false);
                    SetHidden(_paceStatusBadge, false);
                }
            }

            if (_speedButtons == null)
                return;

            var activeIdx = MapDisplaySystem.NearestSpeedStepIndex(simulationSpeed);
            for (var i = 0; i < _speedButtons.Length; i++)
            {
                var btn = _speedButtons[i];
                if (btn == null)
                    continue;
                var isActive = i == activeIdx;
                SetBtnClasses(btn, idle: !isActive, active: isActive, paused: false, disabled: false);
            }
        }

        static void ApplyButtonEnabled(Button btn, bool enabled)
        {
            if (btn == null)
                return;
            btn.SetEnabled(enabled);
            SetBtnClasses(btn, idle: enabled, active: false, paused: false, disabled: !enabled);
        }

        static void SetBtnClasses(Button btn, bool idle, bool active, bool paused, bool disabled)
        {
            btn.EnableInClassList(ClassBtn, true);
            btn.EnableInClassList(ClassBtnIdle, idle && !active && !paused && !disabled);
            btn.EnableInClassList(ClassBtnActive, active);
            btn.EnableInClassList(ClassBtnSelected, active);
            btn.EnableInClassList(ClassBtnPaused, paused);
            btn.EnableInClassList(ClassBtnDisabled, disabled);
        }

        static void SetHidden(VisualElement el, bool hidden)
        {
            if (el == null)
                return;
            el.EnableInClassList(ClassHidden, hidden);
        }

        static bool IsHidden(VisualElement el)
        {
            return el != null && el.ClassListContains(ClassHidden);
        }

        static string FormatPaceStatus()
        {
            if (!TryGetWorldState(out _, out _, out var ws))
                return "";

            // État courant (badge) — distinct de l'action du bouton Pause/Lecture.
            if (ws.IsPaused)
                return "EN PAUSE";
            return "VITESSE x" + ws.SimulationSpeed.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>Compte les couleurs distinctes (preuve capture non uniforme).</summary>
        public static int CountDistinctColors(Texture2D tex, int maxSample = 200000)
        {
            if (tex == null)
                return 0;
            var pixels = tex.GetPixels32();
            var set = new System.Collections.Generic.HashSet<int>();
            var step = 1;
            if (pixels.Length > maxSample)
                step = pixels.Length / maxSample;
            for (var i = 0; i < pixels.Length; i += step)
            {
                var c = pixels[i];
                set.Add((c.r << 16) | (c.g << 8) | c.b);
            }
            return set.Count;
        }
    }
}
