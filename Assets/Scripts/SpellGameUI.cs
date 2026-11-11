using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.SceneManagement;

using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

/// <summary>주문 조각 PoC의 화면과 입력을 전투 상태에 연결한다.</summary>
public sealed class SpellGameUI : MonoBehaviour
{
    private const float THEFT_MIN_START_DISTANCE = 360;
    private const float THEFT_ARRIVAL_RADIUS = 12;
    private enum HammerPhase { None, Selecting, Timing, Striking, Result }

    private static readonly string[] SPELL_NAMES = { "불씨", "방벽", "번개", "유성" };
    private static readonly string[] SPELL_EFFECTS = { "단일 피해 6", "방어 7", "단일 피해 12", "전체 피해 10" };
    private static readonly string[] REWARD_NAMES = { "불꽃 삼각형\n공격 피해 +2", "수호 정사각형\n방어 +3", "공명 직사각형\n3칸 이상: 배치 +1", "잔상 1/4 삼각형\n시전 후 1회 유지", "도안: 번개\n단일 피해 12", "도안: 유성\n전체 피해 10" };
    private static readonly Color[] PATTERN_COLORS = { new Color(1f, 0.75f, 0.3f), new Color(0.4f, 0.8f, 1f), new Color(0.8f, 0.65f, 1f), new Color(1f, 0.5f, 0.48f) };
    private static readonly IReadOnlyList<Vector2Int>[] PATTERN_CELLS = CreatePatternCells();
    private static readonly Vector2[] THEFT_START_POINTS = { new Vector2(1180, 130), new Vector2(1180, 430), new Vector2(640, 65), new Vector2(80, 65) };
    private readonly SpellGame _game = new SpellGame();
    private readonly List<int> _rewards = new List<int>();
    private readonly Vector2Int[] _patternPositions = { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(4, 0) };
    private readonly bool[] _patternPlaced = new bool[4];
    private readonly System.Random _random = new System.Random();
    private RectTransform _root;
    private GameObject _canvas;
    private Font _font;
    private UnityEngine.UI.Text _previewHint;
    private RectTransform _dragGhost;
    private SpellBoardGraphic _boardGraphic;
    private SpellGame.Card _selectedCard;
    private SpellGame.Placement _selectedPiece;
    private int _selectedBoard = -1;
    private int _rotation;
    private string _message = "";
    private SpellGame.Phase _renderedPhase = (SpellGame.Phase)(-1);
    private string _feedback = "";
    private Color _feedbackColor;
    private float _feedbackUntil;
    private CanvasGroup _feedbackGroup;
    private RectTransform _feedbackRect;
    private readonly List<UnityEngine.UI.Image> _impactImages = new List<UnityEngine.UI.Image>();
    private int _impactTarget = -1;
    private bool _impactAll;
    private bool _showHelp;
    private bool _showPauseMenu;
    private bool _onboardingComplete;
    private bool _onboardingPlacementExplained;
    private int _pendingCast = -1;
    private string _onboardingStep = "";
    private float _onboardingActivity;
    private RectTransform _onboardingGhost;
    private CanvasGroup _onboardingGhostGroup;
    private RectTransform _onboardingGhostIcon;
    private int _onboardingGhostRotation;
    private Vector2 _onboardingGhostStart;
    private Vector2 _onboardingGhostEnd;

    [Header("망치 프로토타입")]
    [SerializeField, Min(0.1f)] private float _hammerGaugeSeconds = 0.25f;
    [SerializeField, Min(0.1f)] private float _hammerStrikeSeconds = 0.24f;
    [SerializeField, Min(0.2f)] private float _hammerResultSeconds = 1.1f;

    private HammerPhase _hammerPhase;
    private SpellGame.Card _hammerCard;
    private int _hammerTurn;
    private int _hammerBattle;
    private float _hammerTimingStart;
    private float _hammerStrikeStart;
    private float _hammerStrength;
    private string _hammerMessage = "";
    private SpellGame.Shape[] _hammerResultShapes = Array.Empty<SpellGame.Shape>();
    private SpellGame.Card _hammerDragCard;
    private bool _isPatternDragging;
    private RectTransform _hammerOverlay;
    private RectTransform _hammerVisual;
    private RectTransform _hammerSource;
    private RectTransform _hammerResults;
    private RectTransform _hammerMarker;
    private UnityEngine.UI.Image _hammerGaugeFill;
    private UnityEngine.UI.Text _hammerHint;
    private CanvasGroup _hammerImpact;

    private readonly SpellGame.Card[] _glueCards = new SpellGame.Card[2];
    private readonly Vector2Int[] _gluePositions = { new Vector2Int(2, 1), new Vector2Int(4, 1) };
    private readonly int[] _glueRotations = new int[2];
    private RectTransform _glueOverlay;
    private SpellGlueGraphic _glueGraphic;
    private UnityEngine.UI.Text _glueHint;
    private UnityEngine.UI.Button _glueConfirm;
    private int _glueSelected;
    private int _glueTurn;
    private int _glueBattle;
    private string _glueMessage = "";
    private SpellGame.Card _glueDragCard;
    private int _glueDragRotation;
    private bool _isSealerRun;
    private bool _isThiefRun;

    [Header("도둑손 프로토타입")]
    [SerializeField, Min(0.1f)] private float _theftIntervalSeconds = 15;
    [SerializeField, Min(1f)] private float _theftMoveSpeed = 90;

    private float _theftElapsed;
    private int _theftBattle = -1;
    private bool _theftApproaching;
    private bool _theftWasInteractive;
    private Vector2 _theftPosition;
    private RectTransform _theftHand;

    /// <summary>논리 도안 마스크에서 점유 칸 좌표를 한 번 생성해 읽기 전용 목록으로 반환한다.</summary>
    private static IReadOnlyList<Vector2Int>[] CreatePatternCells()
    {
        IReadOnlyList<Vector2Int>[] patterns = new IReadOnlyList<Vector2Int>[4];
        for (int spell = 0; spell < patterns.Length; spell++)
        {
            List<Vector2Int> cells = new List<Vector2Int>();
            for (int y = 0; y < 3; y++) for (int x = 0; x < 3; x++)
                if (SpellGame.PatternMask(spell, x, y) != 0) cells.Add(new Vector2Int(x, y));
            patterns[spell] = cells.AsReadOnly();
        }
        return patterns;
    }

    /// <summary>첫 장면에서 게임 화면을 자동으로 생성한다.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        new GameObject("SpellGameUI").AddComponent<SpellGameUI>();
    }

    void Awake()
    {
        _font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 24);
        GameObject canvas = new GameObject("Spell Canvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        _canvas = canvas;
        canvas.transform.SetParent(transform, false);
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        UnityEngine.UI.CanvasScaler scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        RectTransform background = Panel(canvasRect, 0, 0, 0, 0, new Color(0.095f, 0.12f, 0.16f));
        background.anchorMin = Vector2.zero; background.anchorMax = Vector2.one; background.sizeDelta = Vector2.zero;
        background.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
        _root = Element("Content", canvasRect, 0, 0, 1280, 720);
        _root.anchorMin = _root.anchorMax = _root.pivot = new Vector2(0.5f, 0.5f);
        _root.anchoredPosition = Vector2.zero;
        if (FindFirstObjectByType<EventSystem>() == null) new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        Render();
    }

    void OnEnable()
    {
        if (_canvas != null && !_canvas.activeSelf)
        {
            _canvas.SetActive(true); _onboardingActivity = Time.unscaledTime; Render();
        }
    }

    void OnDisable()
    {
        _theftWasInteractive = false;
        CloseGlue();
        CloseHammer();
        _isPatternDragging = false;
        ClearSelection();
        _pendingCast = -1;
        if (_boardGraphic != null) _boardGraphic.CancelDrag();
        if (_dragGhost != null) Destroy(_dragGhost.gameObject);
        _dragGhost = null;
        if (_canvas != null) _canvas.SetActive(false);
    }

    void Update()
    {
        UpdateFeedback();
        UpdateTheft();
        if (_glueOverlay != null) { UpdateGlue(); return; }
        if (_hammerPhase != HammerPhase.None) { UpdateHammer(); return; }
        UpdateOnboarding();
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (_showPauseMenu) _showPauseMenu = false;
            else { _showHelp = false; _showPauseMenu = true; _pendingCast = -1; _message = ""; ClearSelection(); _dragGhost = null; }
            _onboardingActivity = Time.unscaledTime; Render(); return;
        }
        if (_showPauseMenu) return;
        if (_game.CurrentPhase != SpellGame.Phase.Combat) return;
        if (_showHelp)
        {
            return;
        }
        if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
        {
            BeginGlue(); return;
        }
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            BeginHammer();
            return;
        }
        if ((Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) || (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame))
        {
            if (_selectedCard != null)
            {
                _rotation = (_rotation + 1) % 4;
                if (_dragGhost == null) Render();
                else
                {
                    _dragGhost.sizeDelta = new Vector2(72 * SpellGame.ShapeWidth(_selectedCard, _rotation), 72 * SpellGame.ShapeHeight(_selectedCard, _rotation));
                    _dragGhost.GetComponent<SpellShapeGraphic>().SetCard(_selectedCard, _rotation);
                    _boardGraphic.RefreshPreview();
                }
            }
        }
    }

    /// <summary>시전 안내를 짧게 띄우고 입력을 막지 않으며 서서히 사라지게 한다.</summary>
    private void UpdateFeedback()
    {
        if (_feedbackGroup == null) return;
        float remaining = _feedbackUntil - Time.unscaledTime;
        _feedbackGroup.alpha = Mathf.Clamp01(remaining / 0.35f);
        _feedbackRect.localScale = Vector3.one * (1f + 0.04f * Mathf.Clamp01((remaining - 1.1f) / 0.25f));
        foreach (UnityEngine.UI.Image image in _impactImages)
        {
            if (image == null) continue;
            Color color = _feedbackColor; color.a = 0.32f * Mathf.Clamp01((remaining - 1.05f) / 0.45f); image.color = color;
        }
        if (remaining <= 0) { _feedbackGroup.gameObject.SetActive(false); _feedbackGroup = null; }
    }

    /// <summary>주문 결과를 기록해 화면이 갱신된 뒤에도 일시적으로 표시한다.</summary>
    private void ShowFeedback(string message, Color color)
    {
        _feedback = message; _feedbackColor = color; _feedbackUntil = Time.unscaledTime + 1.5f;
    }

    /// <summary>현재 화면 위에 시전 결과를 겹쳐 표시한다.</summary>
    private void RenderFeedback()
    {
        _feedbackGroup = null;
        if (Time.unscaledTime >= _feedbackUntil || string.IsNullOrEmpty(_feedback)) return;
        _feedbackRect = Panel(_root, 390, 440, 500, 46, new Color(0.09f, 0.13f, 0.17f, 0.95f));
        _feedbackRect.gameObject.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
        _feedbackGroup = _feedbackRect.gameObject.AddComponent<CanvasGroup>();
        _feedbackGroup.blocksRaycasts = false; _feedbackGroup.interactable = false;
        RectTransform stripe = Panel(_feedbackRect, 0, 0, 6, 46, _feedbackColor);
        stripe.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
        Label(_feedbackRect, _feedback, 14, 0, 474, 46, 19);
    }

    /// <summary>현재 선택과 회전 상태를 해제한다.</summary>
    private void ClearSelection() { _selectedCard = null; _selectedPiece = null; _selectedBoard = -1; _rotation = 0; }

    /// <summary>지정 위치와 크기의 화면 요소를 생성한다.</summary>
    private RectTransform Element(string name, RectTransform parent, float x, float y, float width, float height)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height); return rect;
    }

    /// <summary>패널 배경을 만들고 위치를 반환한다.</summary>
    private RectTransform Panel(RectTransform parent, float x, float y, float width, float height, Color color)
    {
        RectTransform rect = Element("Panel", parent, x, y, width, height);
        rect.gameObject.AddComponent<UnityEngine.UI.Image>().color = color; return rect;
    }

    /// <summary>한글 텍스트를 지정 영역에 표시한다.</summary>
    private void Label(RectTransform parent, string value, float x, float y, float width, float height, int size = 19, TextAnchor alignment = TextAnchor.MiddleCenter)
    {
        RectTransform rect = Element("Text", parent, x, y, width, height);
        UnityEngine.UI.Text text = rect.gameObject.AddComponent<UnityEngine.UI.Text>();
        text.font = _font; text.text = value; text.fontSize = size; text.alignment = alignment; text.color = new Color(0.92f, 0.94f, 0.96f); text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow;
    }

    /// <summary>텍스트와 클릭 동작을 가진 버튼을 만든다.</summary>
    private void Button(RectTransform parent, string caption, float x, float y, float width, float height, Action action, Color? color = null, int fontSize = 18)
    {
        RectTransform rect = Panel(parent, x, y, width, height, color ?? new Color(0.24f, 0.31f, 0.39f));
        UnityEngine.UI.Button button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.onClick.AddListener(() => { if (_hammerPhase == HammerPhase.None && _glueOverlay == null) action(); }); Label(rect, caption, 4, 2, width - 8, height - 4, fontSize);
    }

    /// <summary>현재 게임 단계에 맞춰 화면 요소를 갱신한다.</summary>
    private void Render()
    {
        CloseGlue();
        CloseHammer();
        _isPatternDragging = false;
        if (_pendingCast >= 0 && (_game.CurrentPhase != SpellGame.Phase.Combat || !_game.IsComplete(_pendingCast) || _game.IsSealed(_pendingCast))) _pendingCast = -1;
        if (_boardGraphic != null) _boardGraphic.CancelDrag();
        if (_dragGhost != null) ClearSelection();
        _dragGhost = null; _boardGraphic = null; _previewHint = null;
        _onboardingGhost = null; _onboardingGhostGroup = null; _onboardingGhostIcon = null;
        _impactImages.Clear();
        _theftHand = null;
        foreach (Transform child in _root) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        Panel(_root, 0, 0, 1280, 720, new Color(0.095f, 0.12f, 0.16f));
        if (_game.CurrentPhase == SpellGame.Phase.Title)
        {
            Label(_root, "주문 조각", 340, 170, 600, 90, 48);
            RectTransform divider = Panel(_root, 500, 322, 280, 2, new Color(0.80f, 0.65f, 0.38f));
            divider.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            Button(_root, "시작", 520, 360, 240, 64, () => StartRun(false));
            if (SceneManager.GetActiveScene().path == "Assets/Scenes/TestScene.unity")
            {
                Button(_root, "형상 봉인자 전투", 520, 435, 240, 50, () => StartRun(true), null, 17);
                Button(_root, "도둑손 전투", 520, 495, 240, 50, () => StartRun(false, true), null, 17);
            }
            Label(_root, "도형 드래그 → 도안 완성 → 시전 버튼\nR 또는 우클릭으로 회전", 300, SceneManager.GetActiveScene().path == "Assets/Scenes/TestScene.unity" ? 555 : 502, 680, 65, 17);
        }
        else if (_game.CurrentPhase == SpellGame.Phase.Reward) RenderRewards();
        else if (_game.CurrentPhase == SpellGame.Phase.Victory || _game.CurrentPhase == SpellGame.Phase.Defeat)
        {
            Label(_root, _game.CurrentPhase == SpellGame.Phase.Victory ? "승리" : "패배", 340, 190, 600, 90, 48);
            Button(_root, "다시 시작", 520, 350, 240, 64, () => StartRun(_isSealerRun, _isThiefRun));
        }
        else RenderCombat();
        RenderFeedback();
        if (_game.CurrentPhase == SpellGame.Phase.Combat) RenderTheftHand();
        if (_showHelp) RenderHelp();
        if (_showPauseMenu) RenderPauseMenu();
        _renderedPhase = _game.CurrentPhase;
    }

    /// <summary>화면 입력을 가리고 계속하기, 타이틀 전환과 게임 종료 메뉴를 표시한다.</summary>
    private void RenderPauseMenu()
    {
        Panel(_root, 0, 0, 1280, 720, new Color(0, 0, 0, 0.8f));
        RectTransform panel = Panel(_root, 435, 175, 410, 370, new Color(0.18f, 0.24f, 0.30f));
        Label(panel, "일시 정지", 20, 22, 370, 50, 28);
        Button(panel, "계속하기", 70, 100, 270, 55, () => { _showPauseMenu = false; _onboardingActivity = Time.unscaledTime; Render(); });
        Button(panel, "타이틀로 가기", 70, 170, 270, 55, () => { _game.ReturnToTitle(); _showPauseMenu = false; _showHelp = false; _pendingCast = -1; _feedbackUntil = 0; ClearSelection(); Render(); });
        Button(panel, "게임 종료", 70, 240, 270, 55, () => Application.Quit());
        Label(panel, "Esc로 돌아가기", 20, 315, 370, 30, 15);
    }

    /// <summary>선택한 전투 진입 방식으로 새 게임을 시작하고 도안과 안내 상태를 준비한다.</summary>
    private void StartRun(bool withSealer, bool withThief = false)
    {
        ResetTheft(); _isSealerRun = withSealer; _isThiefRun = withThief;
        _game.StartGame(withSealer, withThief); ResetPatterns(); ClearSelection();
        if (withSealer || withThief) _onboardingComplete = true;
        _message = _game.SealNotice; _feedbackUntil = 0; Render();
    }

    private bool CanAdvanceTheft
    {
        get { return CanInteract && _pendingCast < 0 && Application.isFocused && Time.timeScale > 0
            && !(!_onboardingComplete && !_onboardingPlacementExplained && _game.CraftCount > 0); }
    }

    /// <summary>절도 대상과 손 UI를 정리하고 다음 주기를 처음부터 시작한다.</summary>
    private void ClearTheftApproach()
    {
        _game.CancelTheft();
        _theftApproaching = false; _theftElapsed = 0;
        if (_theftHand != null) { _theftHand.gameObject.SetActive(false); Destroy(_theftHand.gameObject); }
        _theftHand = null;
    }

    /// <summary>새 게임의 절도 주기와 전투 식별 상태를 초기화한다.</summary>
    private void ResetTheft()
    {
        ClearTheftApproach(); _theftBattle = -1; _theftWasInteractive = false;
    }

    /// <summary>현재 목표 카드의 점유 삼각형 중심을 합산해 공유 격자상의 실제 화면 좌표를 반환한다.</summary>
    private Vector2 TheftTargetPosition(int board, SpellGame.Placement piece)
    {
        Vector2 center = Vector2.zero; int count = 0;
        for (int y = 0; y < SpellGame.ShapeHeight(piece.Card, piece.Rotation); y++)
            for (int x = 0; x < SpellGame.ShapeWidth(piece.Card, piece.Rotation); x++)
            {
                int mask = SpellGame.CellMask(piece.Card, piece.Rotation, x, y);
                for (int side = 0; side < 4; side++)
                {
                    if ((mask & (1 << side)) == 0) continue;
                    float cx = side == 1 ? 5f / 6 : side == 3 ? 1f / 6 : 0.5f;
                    float cy = side == 2 ? 5f / 6 : side == 0 ? 1f / 6 : 0.5f;
                    center += new Vector2(x + cx, y + cy); count++;
                }
            }
        Vector2Int origin = _patternPositions[board];
        return new Vector2(115, 183) + ((Vector2)origin + new Vector2(piece.X, piece.Y) + center / count) * 72;
    }

    /// <summary>모달과 포커스 상태에 따라 절도 주기·접근 이동을 멈추거나 이어가고 도착 시 모델에 절도를 요청한다.</summary>
    private void UpdateTheft()
    {
        if (_game.CurrentPhase != SpellGame.Phase.Combat || !_game.HasLivingThief)
        {
            if (_theftApproaching || _game.TheftTarget != null) ClearTheftApproach();
            _theftElapsed = 0; _theftWasInteractive = false; return;
        }
        if (_theftBattle != _game.Battle)
        {
            ClearTheftApproach(); _theftBattle = _game.Battle; _theftWasInteractive = false;
        }
        int board; SpellGame.Placement piece;
        if (_theftApproaching && !_game.TryGetTheftPlacement(out board, out piece)) ClearTheftApproach();
        bool interactive = CanAdvanceTheft;
        if (_theftHand != null) _theftHand.gameObject.SetActive(interactive);
        if (!interactive) { _theftWasInteractive = false; return; }
        if (!_theftWasInteractive) { _theftWasInteractive = true; return; }
        float delta = Time.unscaledDeltaTime;
        if (!_theftApproaching)
        {
            _theftElapsed = Mathf.Min(_theftElapsed + delta, Mathf.Max(0.1f, _theftIntervalSeconds));
            if (_theftElapsed < Mathf.Max(0.1f, _theftIntervalSeconds) || !_game.BeginTheft()) return;
            if (!_game.TryGetTheftPlacement(out board, out piece)) { ClearTheftApproach(); return; }
            Vector2 target = TheftTargetPosition(board, piece);
            _theftPosition = new Vector2(1180, 300); int choices = 0;
            foreach (Vector2 start in THEFT_START_POINTS)
                if (Vector2.Distance(start, target) >= THEFT_MIN_START_DISTANCE && _random.Next(++choices) == 0) _theftPosition = start;
            _theftApproaching = true; _theftElapsed = 0;
            RenderTheftHand(); return;
        }
        if (!_game.TryGetTheftPlacement(out board, out piece)) { ClearTheftApproach(); return; }
        Vector2 destination = TheftTargetPosition(board, piece);
        _theftPosition = Vector2.MoveTowards(_theftPosition, destination, Mathf.Max(1, _theftMoveSpeed) * delta);
        if (_theftHand == null) RenderTheftHand();
        _theftHand.anchoredPosition = new Vector2(_theftPosition.x, -_theftPosition.y);
        Vector2 direction = destination - _theftPosition;
        _theftHand.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(-direction.y, direction.x) * Mathf.Rad2Deg - 90);
        if (Vector2.Distance(_theftPosition, destination) > THEFT_ARRIVAL_RADIUS) return;
        SpellGame.Card targetCard = _game.TheftTarget;
        bool stolen = _game.ResolveTheft();
        ClearTheftApproach();
        if (!stolen) return;
        if (_selectedCard == targetCard) ClearSelection();
        _message = "도둑손이 조각을 훔쳤습니다 · 전투 종료 후 복구됩니다";
        ShowFeedback("조각을 빼앗겼습니다!", new Color(0.95f, 0.48f, 0.35f)); Render();
    }

    /// <summary>현재 절도 대상과 연결된 손 그래픽을 생성한다. UI 재생성 시에도 접근 위치를 유지한다.</summary>
    private void RenderTheftHand()
    {
        if (!_theftApproaching || _game.TheftTarget == null || _theftHand != null) return;
        _theftHand = Element("Thief Hand", _root, _theftPosition.x, _theftPosition.y, 96, 96);
        _theftHand.pivot = new Vector2(0.5f, 0.5f);
        _theftHand.gameObject.AddComponent<CanvasRenderer>();
        _theftHand.gameObject.AddComponent<SpellThiefHandGraphic>().Setup(this, _game.TheftTarget);
        _theftHand.gameObject.SetActive(CanAdvanceTheft);
    }

    /// <summary>클릭된 손의 대상이 현재 시도와 일치할 때만 절도를 저지하며 카드와 배치 비용을 유지한다.</summary>
    public void StopTheft(SpellGame.Card expectedTarget)
    {
        if (!_theftApproaching || !CanAdvanceTheft || _game.TheftTarget != expectedTarget) return;
        ClearTheftApproach();
        _message = "도둑손 저지!"; ShowFeedback(_message, new Color(0.4f, 0.9f, 0.8f)); Render();
    }

    /// <summary>봉인된 도안의 현재 격자 위치에 수직 막대기와 잠금 표시를 그린다. 포인터 입력은 통과시킨다.</summary>
    private void RenderSeals(RectTransform panel)
    {
        foreach (int spell in _game.Spells)
        {
            if (!_game.IsSealed(spell) || !_patternPlaced[spell]) continue;
            int minX = 3, maxX = 0, minY = 3, maxY = 0;
            foreach (Vector2Int cell in PATTERN_CELLS[spell])
            {
                minX = Math.Min(minX, cell.x); maxX = Math.Max(maxX, cell.x);
                minY = Math.Min(minY, cell.y); maxY = Math.Max(maxY, cell.y);
            }
            Vector2Int origin = _patternPositions[spell];
            float centerX = 60 + (origin.x + (minX + maxX + 1) / 2f) * 72;
            float top = 48 + (origin.y + minY) * 72 - 18;
            float height = (maxY - minY + 1) * 72 + 36;
            RectTransform shadow = Panel(panel, centerX - 7, top, 14, height, new Color(0.12f, 0.05f, 0.08f, 0.85f));
            shadow.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            RectTransform rod = Panel(panel, centerX - 3, top, 6, height, new Color(0.83f, 0.55f, 0.38f));
            rod.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            RectTransform clamp = Panel(panel, centerX - 22, top + height / 2 - 6, 44, 12, new Color(0.72f, 0.22f, 0.30f));
            clamp.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            Label(panel, "봉인", centerX - 28, top - 22, 56, 22, 14);
        }
    }

    /// <summary>전투 상태와 공유 제작판, 손패 및 제어 버튼을 표시한다.</summary>
    private void RenderCombat()
    {
        Label(_root, $"체력 {_game.Health}/50   ·   방어 {_game.Block}", 30, 5, 290, 30, 18, TextAnchor.MiddleLeft);
        RectTransform playerTrack = Panel(_root, 30, 37, 250, 6, new Color(0.16f, 0.20f, 0.24f));
        playerTrack.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
        RectTransform playerFill = Panel(playerTrack, 0, 0, 250 * Mathf.Clamp01(_game.Health / 50f), 6, _game.Health <= 15 ? new Color(0.91f, 0.42f, 0.38f) : new Color(0.48f, 0.79f, 0.76f));
        playerFill.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
        Label(_root, $"전투 {_game.Battle}/6", 485, 4, 310, 38, 23);
        Label(_root, $"턴 {_game.Turn}   ·   남은 배치 {_game.PlacementsLeft}", 925, 5, 320, 34, 19, TextAnchor.MiddleRight);
        for (int i = 0; i < _game.Enemies.Count; i++)
        {
            int index = i; SpellGame.Enemy enemy = _game.Enemies[i];
            float enemyX = (_game.Enemies.Count == 1 ? 522 : 390) + i * 250;
            Button(_root, $"{(_pendingCast >= 0 && enemy.Health > 0 ? "대상 선택 ▶ " : i == _game.Target && enemy.Health > 0 ? "▶ " : "")}{enemy.Name}\n체력 {Math.Max(0, enemy.Health)}/{enemy.MaxHealth}   다음 공격 {(enemy.Health > 0 ? enemy.Intent : 0)}", enemyX, 58, 235, 66, () => { if (_pendingCast >= 0) CastSpell(_pendingCast, index); else { _game.SelectTarget(index); Render(); } }, enemy.Health <= 0 ? new Color(0.18f, 0.18f, 0.2f) : _pendingCast >= 0 ? new Color(0.57f, 0.36f, 0.27f) : new Color(0.36f, 0.25f, 0.28f), 16);
            RectTransform healthTrack = Panel(_root, enemyX + 8, 115, 219, 5, new Color(0.15f, 0.16f, 0.18f));
            healthTrack.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            RectTransform healthFill = Panel(healthTrack, 0, 0, 219 * Mathf.Clamp01((float)enemy.Health / enemy.MaxHealth), 5, new Color(0.91f, 0.48f, 0.40f));
            healthFill.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            if (Time.unscaledTime < _feedbackUntil && (_impactAll || index == _impactTarget))
            {
                RectTransform impact = Panel(_root, enemyX, 58, 235, 66, new Color(_feedbackColor.r, _feedbackColor.g, _feedbackColor.b, 0.3f));
                UnityEngine.UI.Image image = impact.GetComponent<UnityEngine.UI.Image>();
                image.raycastTarget = false; _impactImages.Add(image);
            }
        }
        RenderSharedBoard();
        if (_game.HasLivingThief || _game.StolenCount > 0)
            Label(_root, $"도둑손: 접근하는 손을 클릭해 저지 · 빼앗긴 카드 {_game.StolenCount}장", 55, 450, 1100, 25, 15, TextAnchor.MiddleLeft);
        Label(_root, $"뽑기 {_game.DrawCount}   버림 {_game.DiscardCount}   제작 중 {_game.CraftCount}     {_message}", 30, 486, 1000, 35, 18, TextAnchor.MiddleLeft);
        for (int i = 0; i < _game.Hand.Count; i++)
        {
            SpellGame.Card card = _game.Hand[i];
            RectTransform cardPanel = Panel(_root, 30 + i * 148, 525, 144, 126, CardColor(card, card == _selectedCard));
            UnityEngine.UI.Button cardButton = cardPanel.gameObject.AddComponent<UnityEngine.UI.Button>();
            cardButton.onClick.AddListener(() => { if (!CanInteract) return; _pendingCast = -1; _selectedCard = card; _selectedPiece = null; _selectedBoard = -1; _rotation = 0; _message = "R 또는 우클릭: 회전"; Render(); });
            cardPanel.gameObject.AddComponent<SpellCardDrag>().Setup(this, card);
            RectTransform iconRect = Element("Shape", cardPanel, 0, 4, 144, 72);
            iconRect.gameObject.AddComponent<CanvasRenderer>();
            SpellShapeGraphic icon = iconRect.gameObject.AddComponent<SpellShapeGraphic>();
            icon.raycastTarget = false; icon.SetCard(card);
            Label(cardPanel, CardName(card), 2, 78, 140, 46, 14);
        }
        int incoming = 0;
        foreach (SpellGame.Enemy enemy in _game.Enemies) if (enemy.Health > 0) incoming += enemy.Intent;
        int expectedDamage = Math.Max(0, incoming - _game.Block);
        Label(_root, $"예상 피해 {expectedDamage}\n공격 {incoming} − 방어 {_game.Block}", 1055, 480, 200, 58, 15);
        Button(_root, "턴 종료", 1080, 550, 160, 65, EndPlayerTurn, expectedDamage >= _game.Health ? new Color(0.59f, 0.27f, 0.27f) : new Color(0.32f, 0.40f, 0.49f));
        Button(_root, "도움말 ?", 1100, 660, 140, 35, () => { _showHelp = true; Render(); }, null, 16);
        Button(_root, $"E 망치 {_game.HammerUsesLeft}회", 1040, 620, 108, 30, BeginHammer, null, 14);
        Button(_root, $"Q 접착 {_game.GlueUsesLeft}회", 1154, 620, 108, 30, BeginGlue, null, 14);
        RectTransform hint = Element("Preview Hint", _root, 30, 660, 1020, 30);
        _previewHint = hint.gameObject.AddComponent<UnityEngine.UI.Text>();
        _previewHint.font = _font; _previewHint.fontSize = 17; _previewHint.color = Color.white; _previewHint.raycastTarget = false;
        SetPreviewHint(-1, -1, -1);
        if (!_onboardingComplete) RenderOnboarding();
    }

    /// <summary>첫 주문까지 현재 가능한 조작을 강조하고 설명과 고스트를 준비한다.</summary>
    private void RenderOnboarding()
    {
        List<Rect> holes = new List<Rect>(); string step; string message; SpellGame.Card ghostCard = null; int ghostRotation = 0; bool showContinue = false;
        Vector2 start, end;
        int complete = -1;
        foreach (int spell in _game.Spells) if (_game.IsComplete(spell) && !_game.IsSealed(spell)) { complete = spell; break; }
        if (_pendingCast >= 0)
        {
            int target = 0;
            while (target < _game.Enemies.Count && _game.Enemies[target].Health <= 0) target++;
            float x = (_game.Enemies.Count == 1 ? 522 : 390) + target * 250;
            holes.Add(new Rect(x, 58, 235, 66)); start = end = new Vector2(x + 117, 91);
            step = "target"; message = "이 주문은 선택한 적 하나에게 효과를 적용합니다.\n공격할 적을 선택하세요.";
        }
        else if (!_onboardingPlacementExplained && _game.CraftCount > 0)
        {
            holes.Add(new Rect(1080, 5, 165, 34)); start = end = Vector2.zero;
            step = "placements"; showContinue = true;
            message = "한 턴에 기본 3회 조각을 배치할 수 있습니다.\n시전하지 않은 조각은 다음 턴에도 도안에 남습니다.";
        }
        else if (complete >= 0)
        {
            float y = 155 + complete * 53;
            holes.Add(new Rect(1080, y, 95, 44)); start = end = new Vector2(1127, y + 22);
            step = "cast" + complete; message = "도안이 완성되어 주문이 준비됐습니다.\n시전 버튼을 눌러 주문을 사용하세요.";
        }
        else
        {
            int cardIndex = -1, board = -1, cellX = 0, cellY = 0, rotation = 0;
            if (_game.PlacementsLeft > 0)
            {
                for (int pass = 0; pass < 2 && cardIndex < 0; pass++)
                    for (int i = 0; i < _game.Hand.Count && cardIndex < 0; i++)
                    {
                        SpellGame.Card candidate = _game.Hand[i];
                        if (_selectedCard != null && _selectedPiece == null && candidate != _selectedCard) continue;
                        SpellGame.Shape preferred = _game.CraftCount == 0 ? SpellGame.Shape.Square : SpellGame.Shape.QuarterTriangle;
                        if (pass == 0 && candidate.Shape != preferred) continue;
                        foreach (int spell in _game.Spells)
                        {
                            if (!_patternPlaced[spell]) continue;
                            for (int y = 0; y < 3 && cardIndex < 0; y++) for (int x = 0; x < 3 && cardIndex < 0; x++) for (int r = 0; r < 4; r++)
                                if (_game.CanPlace(spell, candidate, x, y, r)) { cardIndex = i; board = spell; cellX = x; cellY = y; rotation = r; break; }
                            if (cardIndex >= 0) break;
                        }
                    }
            }
            if (cardIndex < 0)
            {
                holes.Add(new Rect(1080, 550, 160, 65)); start = end = new Vector2(1160, 582);
                step = "turn"; message = "턴을 종료하면 적이 공격하고 새 턴에 조각과 배치 횟수를 받습니다.\n주문을 이어서 완성하세요.";
            }
            else
            {
                ghostCard = _game.Hand[cardIndex]; ghostRotation = rotation;
                float cardX = 30 + cardIndex * 148;
                Vector2Int origin = _patternPositions[board];
                float gridX = 115 + (origin.x + cellX) * 72, gridY = 183 + (origin.y + cellY) * 72;
                holes.Add(new Rect(cardX, 525, 144, 126)); holes.Add(new Rect(gridX, gridY, 72, 72));
                start = new Vector2(cardX + 72, 588); end = new Vector2(gridX + 36, gridY + 36);
                step = "place" + ghostCard.Id + ":" + board + ":" + cellX + ":" + cellY + ":" + rotation;
                message = "도안에 조각을 채우면 주문을 시전할 수 있습니다.\n손패에서 조각을 선택해 도안의 빈칸으로 드래그하세요." + ((_selectedCard == ghostCard ? _rotation : 0) != rotation ? "\nR 또는 우클릭으로 회전해 빈 영역에 맞추세요." : "");
            }
        }
        if (_onboardingStep != step) { _onboardingStep = step; _onboardingActivity = Time.unscaledTime; }
        List<float> xs = new List<float> { 0, 1280 }, ys = new List<float> { 0, 720 };
        foreach (Rect hole in holes) { xs.Add(hole.xMin); xs.Add(hole.xMax); ys.Add(hole.yMin); ys.Add(hole.yMax); }
        xs.Sort(); ys.Sort();
        for (int y = 0; y < ys.Count - 1; y++) for (int x = 0; x < xs.Count - 1; x++)
        {
            float width = xs[x + 1] - xs[x], height = ys[y + 1] - ys[y];
            if (width <= 0 || height <= 0) continue;
            Vector2 center = new Vector2(xs[x] + width / 2, ys[y] + height / 2);
            bool exposed = false; foreach (Rect hole in holes) if (hole.Contains(center)) { exposed = true; break; }
            if (!exposed) Panel(_root, xs[x], ys[y], width, height, new Color(0, 0, 0, 0.76f)).GetComponent<UnityEngine.UI.Image>().raycastTarget = showContinue;
        }
        const float popupWidth = 460, popupHeight = 132;
        Vector2 popupPosition = OnboardingPopupPosition(holes, popupWidth, popupHeight);
        RectTransform popup = Panel(_root, popupPosition.x, popupPosition.y, popupWidth, popupHeight, new Color(0.12f, 0.19f, 0.25f, 0.98f));
        popup.GetComponent<UnityEngine.UI.Image>().raycastTarget = showContinue;
        Label(popup, "주문 시전 안내", 16, 6, popupWidth - 32, 25, 17, TextAnchor.MiddleLeft);
        Label(popup, message, 16, 31, popupWidth - 32, showContinue ? 58 : 94, 16, TextAnchor.MiddleLeft);
        if (showContinue)
        {
            Button(popup, "확인", popupWidth - 110, 96, 94, 30, () => { _onboardingPlacementExplained = true; Render(); }, null, 15);
            start = end = new Vector2(popupPosition.x + popupWidth - 63, popupPosition.y + 111);
        }
        _onboardingGhostStart = start; _onboardingGhostEnd = end;
        _onboardingGhostRotation = ghostRotation;
        _onboardingGhost = Element("Onboarding Ghost", _root, start.x, start.y, 48, 48);
        _onboardingGhost.pivot = new Vector2(0.5f, 0.5f);
        _onboardingGhostGroup = _onboardingGhost.gameObject.AddComponent<CanvasGroup>();
        _onboardingGhostGroup.blocksRaycasts = false; _onboardingGhostGroup.interactable = false; _onboardingGhostGroup.alpha = 0;
        if (ghostCard != null)
        {
            float width = ghostCard.Shape == SpellGame.Shape.Rectangle ? 72 : 36;
            RectTransform iconRect = Element("Ghost Shape", _onboardingGhost, 0, 0, width, 36);
            iconRect.gameObject.AddComponent<CanvasRenderer>();
            SpellShapeGraphic icon = iconRect.gameObject.AddComponent<SpellShapeGraphic>();
            icon.SetCard(ghostCard); icon.color = new Color(0.6f, 1f, 0.85f, 0.75f); icon.raycastTarget = false;
            iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            _onboardingGhostIcon = iconRect;
        }
        Label(_onboardingGhost, "☛", 20, 17, 28, 30, 24);
    }

    /// <summary>강조 영역들을 가리지 않고 화면 안에서 첫 강조 영역에 가까운 팝업 좌표를 반환한다.</summary>
    private Vector2 OnboardingPopupPosition(List<Rect> holes, float width, float height)
    {
        Rect focus = holes[0];
        Vector2[] candidates = {
            new Vector2(focus.center.x - width / 2, focus.yMin - height - 12),
            new Vector2(focus.center.x - width / 2, focus.yMax + 12),
            new Vector2(focus.xMin - width - 12, focus.center.y - height / 2),
            new Vector2(focus.xMax + 12, focus.center.y - height / 2)
        };
        Vector2 best = candidates[0]; float bestScore = float.MaxValue;
        foreach (Vector2 candidate in candidates)
        {
            Vector2 position = new Vector2(Mathf.Clamp(candidate.x, 12, 1280 - width - 12), Mathf.Clamp(candidate.y, 12, 720 - height - 12));
            Rect popup = new Rect(position.x, position.y, width, height);
            float score = Vector2.Distance(popup.center, focus.center);
            foreach (Rect hole in holes) if (popup.Overlaps(hole)) score += 10000;
            if (score < bestScore) { bestScore = score; best = position; }
        }
        return best;
    }

    /// <summary>입력 후 대기 시간을 초기화하고 일정 시간이 지나면 안내 동작을 반복한다.</summary>
    private void UpdateOnboarding()
    {
        if (_onboardingComplete || _onboardingGhost == null || _showHelp || _showPauseMenu) return;
        if ((Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame || Mouse.current.delta.ReadValue().sqrMagnitude > 0)) ||
            (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)) _onboardingActivity = Time.unscaledTime;
        float elapsed = Time.unscaledTime - _onboardingActivity;
        bool visible = elapsed >= 3f && _dragGhost == null;
        _onboardingGhostGroup.alpha = visible ? 0.85f : 0f;
        if (!visible) return;
        float progress = Mathf.Repeat((elapsed - 3f) / 2f, 1f);
        _onboardingGhost.localScale = Vector3.one * (_onboardingGhostStart == _onboardingGhostEnd ? 1f - 0.22f * Mathf.Pow(Mathf.Sin(progress * Mathf.PI), 4) : 1f);
        if (_onboardingGhostIcon != null) _onboardingGhostIcon.localRotation = Quaternion.Euler(0, 0, -90 * _onboardingGhostRotation * Mathf.Clamp01(progress * 3f));
        Vector2 point = Vector2.Lerp(_onboardingGhostStart, _onboardingGhostEnd, Mathf.SmoothStep(0, 1, Mathf.Clamp01((progress - 0.3f) * 1.7f)));
        _onboardingGhost.anchoredPosition = new Vector2(point.x, -point.y);
    }

    /// <summary>적 행동 전후 체력과 방어 차이를 안내하고 다음 화면을 표시한다.</summary>
    private void EndPlayerTurn()
    {
        int healthBefore = _game.Health;
        int incoming = 0;
        foreach (SpellGame.Enemy enemy in _game.Enemies) if (enemy.Health > 0) incoming += enemy.Intent;
        int absorbed = Math.Min(_game.Block, incoming);
        _pendingCast = -1; ClearSelection(); _game.EndTurn(); _message = _game.SealNotice;
        int damage = healthBefore - _game.Health;
        _impactTarget = -1; _impactAll = false;
        ShowFeedback($"적 공격  ·  방어 {absorbed}  ·  체력 -{damage}", new Color(0.91f, 0.48f, 0.40f));
        Render();
    }

    /// <summary>완성된 주문의 시전을 시작하고 단일 대상 주문은 적 선택을 기다린다.</summary>
    private void BeginCast(int board)
    {
        if (_game.IsSealed(board)) { _pendingCast = -1; _message = "봉인된 주문 도안은 시전할 수 없습니다"; Render(); return; }
        if (!_game.IsComplete(board)) { _message = "도안을 먼저 완성하세요"; Render(); return; }
        ClearSelection();
        if (board == 0 || board == 2) { _pendingCast = board; _message = $"{SPELL_NAMES[board]}의 대상을 선택하세요 · Esc 메뉴"; Render(); }
        else CastSpell(board, _game.Target);
    }

    /// <summary>완성된 도안과 선택한 적으로 주문을 시전하고 결과를 표시한다.</summary>
    private void CastSpell(int board, int target)
    {
        if (_game.IsSealed(board)) { _pendingCast = -1; _message = "봉인된 주문 도안은 시전할 수 없습니다"; Render(); return; }
        string result = $"{SPELL_NAMES[board]} 시전  ·  {Preview(_game.Boards[board])}";
        if (!_game.Cast(board, target)) { _message = "살아 있는 대상을 선택하세요"; Render(); return; }
        _onboardingComplete = true;
        _pendingCast = -1; ClearSelection(); _impactAll = board == 3; _impactTarget = board == 1 ? -1 : target;
        _message = _game.SealNotice; ShowFeedback(result, PatternColor(board)); Render();
    }

    /// <summary>하나의 격자에 보유 도안과 각 도안의 포기 버튼을 표시한다.</summary>
    private void RenderSharedBoard()
    {
        RectTransform panel = Panel(_root, 55, 135, 1170, 340, new Color(0.19f, 0.24f, 0.30f));
        Label(panel, "주문서  /  완성 후 시전 버튼을 누르세요", 60, 10, 432, 28, 16, TextAnchor.MiddleLeft);
        EnsurePatterns();
        for (int i = 0; i < 4; i++)
        {
            if (!_game.Spells.Contains(i)) continue;
            int index = i;
            RectTransform handle = Panel(panel, 550, 20 + i * 53, 370, 44, new Color(0.25f, 0.32f, 0.39f));
            handle.gameObject.AddComponent<SpellPatternDrag>().Setup(this, index);
            RectTransform marker = Panel(handle, 4, 6, 6, 32, PatternColor(i));
            marker.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            bool sealedSpell = _game.IsSealed(i);
            Label(handle, $"↕ {SPELL_NAMES[i]}{(sealedSpell ? " [봉인]" : "")}: {Preview(_game.Boards[i])}{(_game.IsComplete(i) ? " · 완성" : _patternPlaced[i] ? "" : " · 배치 필요")}", 16, 0, 344, 44, 15, TextAnchor.MiddleLeft);
            Button(panel, "포기", 930, 20 + i * 53, 85, 44, () => { if (!_game.Abandon(index)) _message = "손패가 가득 차 포기할 수 없습니다"; else { ClearSelection(); _message = ""; } Render(); }, null, 14);
            Button(panel, sealedSpell ? "봉인됨" : _game.IsComplete(i) ? "시전" : "미완성", 1025, 20 + i * 53, 95, 44, () => BeginCast(index), sealedSpell ? new Color(0.34f, 0.18f, 0.22f) : _game.IsComplete(i) ? PatternColor(i) * 0.65f : new Color(0.2f, 0.24f, 0.28f), 14);
            if (sealedSpell) panel.GetChild(panel.childCount - 1).GetComponent<UnityEngine.UI.Button>().interactable = false;
        }
        RectTransform grid = Element("Grid", panel, 60, 48, 432, 216);
        grid.gameObject.AddComponent<CanvasRenderer>();
        SpellBoardGraphic graphic = grid.gameObject.AddComponent<SpellBoardGraphic>();
        graphic.Setup(this, _game);
        _boardGraphic = graphic;
        RenderSeals(panel);
        Label(panel, "조각을 드래그해 이동 · 손패로 드래그해 회수", 550, 234, 520, 30, 15, TextAnchor.MiddleLeft);
        Button(panel, "선택 조각 회수", 550, 275, 180, 40, () => { if (_selectedPiece == null || !_game.Recall(_selectedBoard, _selectedPiece)) _message = "회수할 조각을 선택하세요 (손패 최대 7장)"; else { ClearSelection(); _message = ""; } Render(); }, null, 16);
    }

    /// <summary>새 게임에서 도안 배치를 기본 위치로 되돌린다.</summary>
    private void ResetPatterns()
    {
        _feedbackUntil = 0;
        _showHelp = false;
        _showPauseMenu = false;
        _onboardingComplete = false;
        _onboardingPlacementExplained = false;
        _onboardingStep = ""; _onboardingActivity = Time.unscaledTime;
        _pendingCast = -1;
        _patternPositions[0] = new Vector2Int(0, 0); _patternPositions[1] = new Vector2Int(1, 0);
        _patternPositions[2] = new Vector2Int(2, 0); _patternPositions[3] = new Vector2Int(4, 0);
        for (int i = 0; i < 4; i++) _patternPlaced[i] = false;
    }

    /// <summary>새로 얻은 도안을 빈 공간에 놓고 공간이 없으면 미배치 상태로 둔다.</summary>
    private void EnsurePatterns()
    {
        foreach (int spell in _game.Spells)
        {
            if (_patternPlaced[spell]) continue;
            Vector2Int preferred = _patternPositions[spell];
            if (CanMovePattern(spell, preferred)) { _patternPlaced[spell] = true; continue; }
            for (int y = 0; y < 3 && !_patternPlaced[spell]; y++)
                for (int x = 0; x < 6 && !_patternPlaced[spell]; x++)
                    if (CanMovePattern(spell, new Vector2Int(x, y))) { _patternPositions[spell] = new Vector2Int(x, y); _patternPlaced[spell] = true; }
        }
    }

    /// <summary>도안의 칸들이 격자 안에 있고 다른 도안과 겹치지 않는지 반환한다.</summary>
    public bool CanMovePattern(int spell, Vector2Int origin)
    {
        foreach (Vector2Int cell in PATTERN_CELLS[spell])
        {
            Vector2Int pos = origin + cell;
            if (pos.x < 0 || pos.x >= 6 || pos.y < 0 || pos.y >= 3) return false;
            foreach (int other in _game.Spells)
            {
                if (other == spell || !_patternPlaced[other]) continue;
                foreach (Vector2Int occupied in PATTERN_CELLS[other]) if (_patternPositions[other] + occupied == pos) return false;
            }
        }
        return true;
    }

    /// <summary>도안을 새 위치로 옮기고 결과를 화면에 반영한다.</summary>
    public void MovePattern(int spell, Vector2Int origin)
    {
        if (CanMovePattern(spell, origin)) { _patternPositions[spell] = origin; _patternPlaced[spell] = true; _message = ""; }
        else _message = "도안이 겹치거나 격자 밖입니다";
        Render();
    }

    /// <summary>격자 좌표에 있는 보유 도안과 도안 내부 좌표를 반환한다.</summary>
    public bool MapCell(int x, int y, out int spell, out int localX, out int localY)
    {
        foreach (int index in _game.Spells)
        {
            if (!_patternPlaced[index]) continue;
            foreach (Vector2Int cell in PATTERN_CELLS[index])
                if (_patternPositions[index] + cell == new Vector2Int(x, y)) { spell = index; localX = cell.x; localY = cell.y; return true; }
        }
        spell = localX = localY = -1; return false;
    }

    /// <summary>도안의 현재 격자상 원점을 반환한다.</summary>
    public Vector2Int PatternPosition(int spell) { return _patternPositions[spell]; }

    /// <summary>도안을 구성하는 로컬 칸 좌표를 반환한다.</summary>
    public IReadOnlyList<Vector2Int> PatternCells(int spell) { return PATTERN_CELLS[spell]; }

    /// <summary>도안별 외곽선을 구분할 색을 반환한다.</summary>
    public Color PatternColor(int spell)
    {
        return _game.IsSealed(spell) ? new Color(0.95f, 0.32f, 0.4f) : PATTERN_COLORS[spell];
    }

    /// <summary>판의 기본 효과와 현재 특수 조각의 효과 합을 반환한다.</summary>
    private string Preview(SpellGame.Board board, SpellGame.Card additional = null)
    {
        int flame = 0, guard = 0, extraPlacements = 0, afterimages = 0;
        foreach (SpellGame.Placement p in board.Pieces)
        {
            flame += 2 * SpellGame.EffectCount(p.Card, SpellGame.Effect.Flame);
            guard += 3 * SpellGame.EffectCount(p.Card, SpellGame.Effect.Guard);
            if (board.Spell >= 2) extraPlacements += SpellGame.EffectCount(p.Card, SpellGame.Effect.Resonance);
            if (SpellGame.EffectCount(p.Card, SpellGame.Effect.Afterimage) > 0 && !p.HasPersisted) afterimages++;
        }
        if (additional != null)
        {
            flame += 2 * SpellGame.EffectCount(additional, SpellGame.Effect.Flame);
            guard += 3 * SpellGame.EffectCount(additional, SpellGame.Effect.Guard);
            if (board.Spell >= 2) extraPlacements += SpellGame.EffectCount(additional, SpellGame.Effect.Resonance);
            if (SpellGame.EffectCount(additional, SpellGame.Effect.Afterimage) > 0 && !additional.HasPersisted) afterimages++;
        }
        return SPELL_EFFECTS[board.Spell] + (flame > 0 && board.Spell != 1 ? $" +{flame} 피해" : "") + (guard > 0 ? $" +{guard} 방어" : "") + (extraPlacements > 0 ? $" · 배치 +{extraPlacements}" : "") + (afterimages > 0 ? $" · 잔상 {afterimages}장 유지" : "");
    }

    /// <summary>카드의 도형과 특수 효과를 표시하는 이름을 반환한다.</summary>
    private string CardName(SpellGame.Card card)
    {
        if (card.Glued != null)
        {
            string effects = "";
            int flame = SpellGame.EffectCount(card, SpellGame.Effect.Flame), guard = SpellGame.EffectCount(card, SpellGame.Effect.Guard);
            int resonance = SpellGame.EffectCount(card, SpellGame.Effect.Resonance);
            if (flame > 0) effects += $"불꽃 +{flame * 2} ";
            if (guard > 0) effects += $"수호 +{guard * 3} ";
            if (resonance > 0) effects += $"공명 +{resonance} ";
            if (SpellGame.EffectCount(card, SpellGame.Effect.Afterimage) > 0) effects += card.HasPersisted ? "잔상 사용됨" : "잔상";
            return "접착 조각\n" + (effects.Length == 0 ? "일반 · 배치 1회" : effects);
        }
        string shape = card.Shape == SpellGame.Shape.Square ? "정사각형" : card.Shape == SpellGame.Shape.Triangle ? "반칸 삼각형" : card.Shape == SpellGame.Shape.QuarterTriangle ? "1/4 삼각형" : "직사각형";
        string effect = card.Effect == SpellGame.Effect.Flame ? "불꽃 · 공격 피해 +2" : card.Effect == SpellGame.Effect.Guard ? "수호 · 방어 +3" : card.Effect == SpellGame.Effect.Resonance ? "공명 · 3칸 이상 배치 +1" : card.Effect == SpellGame.Effect.Afterimage ? card.HasPersisted ? "잔상 · 다음 시전 후 버림" : "잔상 · 시전 후 1회 유지" : "일반 · 추가 효과 없음";
        return shape + "\n" + effect;
    }

    /// <summary>카드 효과와 선택 여부를 나타내는 색을 반환한다.</summary>
    private Color CardColor(SpellGame.Card card, bool selected)
    {
        if (selected) return new Color(0.65f, 0.57f, 0.3f);
        return SpellGame.EffectCount(card, SpellGame.Effect.Flame) > 0 ? new Color(0.65f, 0.31f, 0.16f) : SpellGame.EffectCount(card, SpellGame.Effect.Guard) > 0 ? new Color(0.18f, 0.37f, 0.56f) : SpellGame.EffectCount(card, SpellGame.Effect.Resonance) > 0 ? new Color(0.42f, 0.28f, 0.54f) : SpellGame.EffectCount(card, SpellGame.Effect.Afterimage) > 0 ? new Color(0.18f, 0.48f, 0.46f) : new Color(0.39f, 0.43f, 0.47f);
    }

    /// <summary>미보유 도안이 있으면 혼합 후보 세 개, 없으면 특수 카드 네 종류를 표시한다.</summary>
    private void RenderRewards()
    {
        if (_renderedPhase != SpellGame.Phase.Reward)
        {
            _rewards.Clear(); List<int> missing = new List<int>(); if (!_game.Spells.Contains(2)) missing.Add(4); if (!_game.Spells.Contains(3)) missing.Add(5);
            if (missing.Count == 0) _rewards.AddRange(new[] { 0, 1, 2, 3 });
            else
            {
                _rewards.Add(missing[_random.Next(missing.Count)]);
                _rewards.Add(_random.Next(4)); List<int> pool = new List<int> { 0, 1, 2, 3 }; pool.AddRange(missing);
                while (_rewards.Count < 3) { int pick = pool[_random.Next(pool.Count)]; if (!_rewards.Contains(pick)) _rewards.Add(pick); }
            }
            for (int i = _rewards.Count - 1; i > 0; i--) { int j = _random.Next(i + 1); int t = _rewards[i]; _rewards[i] = _rewards[j]; _rewards[j] = t; }
        }
        Label(_root, "전투 승리 · 체력 최대 6 회복", 300, 110, 680, 60, 30);
        Label(_root, "보상 하나를 선택하세요", 300, 180, 680, 45, 22);
        float startX = (1280 - (_rewards.Count * 285 + (_rewards.Count - 1) * 20)) / 2f;
        for (int i = 0; i < _rewards.Count; i++) RenderRewardCard(_rewards[i], startX + 305 * i);
        Button(_root, "건너뛰기", 530, 490, 220, 55, () => { _game.ChooseReward(-1); ClearSelection(); Render(); });
    }

    /// <summary>보상 종류와 실제 도형 또는 도안을 함께 표시하고 선택 동작을 연결한다.</summary>
    private void RenderRewardCard(int reward, float x)
    {
        bool isSpell = reward >= 4;
        Color accent = isSpell ? PatternColor(reward - 2) : reward == 0 ? new Color(0.90f, 0.48f, 0.24f) : reward == 1 ? new Color(0.34f, 0.67f, 0.88f) : reward == 2 ? new Color(0.70f, 0.52f, 0.85f) : new Color(0.35f, 0.76f, 0.68f);
        RectTransform card = Panel(_root, x, 250, 285, 195, new Color(0.24f, 0.31f, 0.38f));
        UnityEngine.UI.Button button = card.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.onClick.AddListener(() => { if (_game.ChooseReward(reward)) { ClearSelection(); _message = ""; Render(); } });
        RectTransform stripe = Panel(card, 0, 0, 285, 5, accent);
        stripe.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
        Label(card, isSpell ? "새 도안" : "특수 조각", 8, 7, 269, 27, 15);
        if (isSpell)
        {
            foreach (Vector2Int cell in PATTERN_CELLS[reward - 2])
            {
                RectTransform tile = Panel(card, 111 + cell.x * 32, 40 + cell.y * 32, 28, 28, accent);
                tile.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            }
        }
        else
        {
            SpellGame.Shape shape = reward == 0 ? SpellGame.Shape.Triangle : reward == 1 ? SpellGame.Shape.Square : reward == 2 ? SpellGame.Shape.Rectangle : SpellGame.Shape.QuarterTriangle;
            float width = shape == SpellGame.Shape.Rectangle ? 112 : 56;
            RectTransform iconRect = Element("Reward Shape", card, (285 - width) / 2, 40, width, 56);
            iconRect.gameObject.AddComponent<CanvasRenderer>();
            SpellShapeGraphic icon = iconRect.gameObject.AddComponent<SpellShapeGraphic>();
            icon.raycastTarget = false; icon.Shape = shape; icon.color = accent;
        }
        Label(card, REWARD_NAMES[reward], 8, 110, 269, 70, 20);
    }

    /// <summary>배치한 조각을 이동 또는 회수 대상으로 선택한다.</summary>
    public void SelectPiece(int board, SpellGame.Placement piece)
    {
        if (!CanInteract) return;
        _pendingCast = -1; _selectedPiece = piece; _selectedCard = piece.Card; _selectedBoard = board; _rotation = piece.Rotation; _message = "판을 클릭해 이동하거나 손패로 드래그하세요";
        Render();
    }
    /// <summary>격자 위치에 선택한 카드를 놓거나 기존 조각을 이동한다.</summary>
    public void ClickGrid(int board, int x, int y)
    {
        if (!CanInteract) return;
        if (_selectedCard == null) { _message = "먼저 손패 또는 배치된 조각을 선택하세요"; Render(); return; }
        SpellGame.Placement ignored = _selectedBoard == board ? _selectedPiece : null;
        bool completes = CanPreview(board, x, y) && _game.WouldComplete(board, _selectedCard, x, y, _rotation, ignored);
        bool success = _selectedPiece == null ? _game.Place(board, _selectedCard, x, y, _rotation) : _game.Move(_selectedBoard, _selectedPiece, board, x, y, _rotation);
        if (success) { ClearSelection(); _message = completes ? $"{SPELL_NAMES[board]} 완성 · 시전 버튼을 누르세요" : ""; } else _message = PlacementIssue(board, x, y) ?? "이 위치에는 놓을 수 없습니다";
        Render();
    }

    /// <summary>선택 카드의 위치와 회전이 유효한지 반환한다.</summary>
    public bool CanPreview(int board, int x, int y)
    {
        return _selectedCard != null && (_selectedPiece != null || _game.PlacementsLeft > 0) && _game.CanPlace(board, _selectedCard, x, y, _rotation, _selectedBoard == board ? _selectedPiece : null);
    }

    /// <summary>현재 선택 조각의 배치 실패 이유를 반환한다.</summary>
    private string PlacementIssue(int board, int x, int y)
    {
        if (_selectedCard == null) return "먼저 조각을 선택하세요";
        if (_selectedPiece == null && _game.PlacementsLeft <= 0) return "이번 턴의 배치 횟수를 모두 사용했습니다";
        return _game.PlacementIssue(board, _selectedCard, x, y, _rotation, _selectedBoard == board ? _selectedPiece : null);
    }

    /// <summary>포인터 위치의 배치 가능 상태와 도안 완성 여부를 안내한다.</summary>
    public void SetPreviewHint(int board, int x, int y)
    {
        if (_previewHint == null) return;
        string hint = "손패 선택 → 판 클릭 배치   ·   R / 우클릭 회전   ·   Esc 메뉴";
        if (board >= 0 && _selectedCard != null)
        {
            bool valid = CanPreview(board, x, y);
            bool complete = valid && _game.WouldComplete(board, _selectedCard, x, y, _rotation, _selectedBoard == board ? _selectedPiece : null);
            SpellGame.Card additional = _selectedBoard == board && _selectedPiece != null ? null : _selectedCard;
            hint = complete ? $"놓으면 도안 완성  ·  {Preview(_game.Boards[board], additional)}" : valid ? $"배치 가능  ·  예상 {Preview(_game.Boards[board], additional)}" : PlacementIssue(board, x, y);
        }
        _previewHint.text = hint;
    }

    /// <summary>선택한 조각이 도안 바깥에 놓일 수 없음을 안내한다.</summary>
    public void SetOutsidePreviewHint()
    {
        if (_previewHint != null && _selectedCard != null) _previewHint.text = "도안의 색 테두리 안에 놓아 주세요";
    }

    /// <summary>현재 게임을 중단하지 않고 핵심 조작과 조각 규칙을 설명한다.</summary>
    private void RenderHelp()
    {
        Panel(_root, 0, 0, 1280, 720, new Color(0.04f, 0.06f, 0.09f, 0.84f));
        RectTransform panel = Panel(_root, 260, 105, 760, 510, new Color(0.18f, 0.24f, 0.30f));
        Label(panel, "주문서 사용법", 25, 20, 710, 48, 29);
        Label(panel,
            "1  손패의 도형을 도안 안으로 드래그하세요.\n" +
            "2  삼각형은 R 또는 우클릭으로 회전합니다.\n" +
            "    반칸 둘 또는 1/4 조각 넷이 한 칸을 채웁니다.\n" +
            "3  완성된 도안의 시전을 누르고, 공격 대상이 필요하면 적을 선택하세요.\n" +
            "4  배치된 조각을 드래그해 옮기거나 손패로 되돌리세요.\n" +
            "    이번 턴 조각을 회수하면 배치 횟수도 돌아옵니다.\n" +
            "5  턴 종료 시 손패를 버리고 적이 공격합니다.\n" +
            "    미완성 도안은 유지되며 방어는 적 행동 후 사라집니다.",
            55, 85, 650, 290, 19, TextAnchor.UpperLeft);
        Label(panel, "공명: 3칸 이상 주문에서 배치 +1\n잔상: 첫 시전 뒤 남고, 다음 시전 뒤 사라집니다.", 55, 385, 650, 48, 16);
        Button(panel, "닫기", 275, 435, 210, 50, () => { _showHelp = false; Render(); });
    }

    /// <summary>손패 카드 드래그를 시작하고 포인터를 따라갈 도형을 만든다.</summary>
    public void BeginCardDrag(SpellGame.Card card, Vector2 position)
    {
        if (CanDragGlueCard)
        {
            string issue = _game.GlueCardIssue(card);
            if (issue == null && _glueCards[0] != null && _glueCards[1] != null && _glueCards[0] != card && _glueCards[1] != card)
                issue = "두 장만 선택할 수 있습니다. 기존 선택을 해제하세요";
            if (issue != null) { _glueMessage = issue; RefreshGlue(); return; }
            _glueDragCard = card;
            _glueDragRotation = _glueCards[0] == card ? _glueRotations[0] : _glueCards[1] == card ? _glueRotations[1] : 0;
            CreateDragGhost(card, position); RefreshGlue(); return;
        }
        if (CanDragHammerCard)
        {
            _hammerDragCard = card;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            CreateDragGhost(card, position); return;
        }
        if (!CanInteract) return;
        _pendingCast = -1;
        if (_selectedCard != card) _rotation = 0;
        _selectedCard = card; _selectedPiece = null; _selectedBoard = -1;
        CreateDragGhost(card, position);
    }

    /// <summary>격자의 배치 조각을 선택하고 드래그 표시를 시작한다.</summary>
    public void BeginPieceDrag(int board, SpellGame.Placement piece, Vector2 position)
    {
        if (!CanInteract) return;
        _pendingCast = -1; _selectedCard = piece.Card; _selectedPiece = piece; _selectedBoard = board; _rotation = piece.Rotation;
        CreateDragGhost(piece.Card, position);
    }

    /// <summary>지정 조각의 포인터 추적용 모양을 생성한다.</summary>
    private void CreateDragGhost(SpellGame.Card card, Vector2 position)
    {
        int rotation = CanDragGlueCard ? _glueDragRotation : CanDragHammerCard ? 0 : _rotation;
        _dragGhost = Element("Drag Preview", _root, 0, 0, 72 * SpellGame.ShapeWidth(card, rotation), 72 * SpellGame.ShapeHeight(card, rotation));
        _dragGhost.pivot = new Vector2(0.5f, 0.5f);
        _dragGhost.gameObject.AddComponent<CanvasRenderer>();
        SpellShapeGraphic icon = _dragGhost.gameObject.AddComponent<SpellShapeGraphic>();
        icon.raycastTarget = false; icon.SetCard(card, rotation);
        MoveCardDrag(position);
    }

    /// <summary>배치 조각을 손패로 회수하거나 유효한 격자 위치로 이동한다.</summary>
    public void EndPieceDrag(PointerEventData eventData, bool hasDestination, int board, int x, int y)
    {
        if (_selectedPiece == null || !CanInteract) return;
        if (_dragGhost != null) Destroy(_dragGhost.gameObject);
        _dragGhost = null;
        Vector2 point;
        bool onHand = RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, eventData.position, null, out point)
            && point.x - _root.rect.xMin >= 30 && point.x - _root.rect.xMin < 1066
            && _root.rect.yMax - point.y >= 525 && _root.rect.yMax - point.y <= 651;
        if (onHand)
        {
            if (_game.Recall(_selectedBoard, _selectedPiece)) { ClearSelection(); _message = "조각을 손패로 되돌렸습니다"; }
            else { ClearSelection(); _message = "손패가 가득 차 회수할 수 없습니다"; }
            Render();
        }
        else if (hasDestination) ClickGrid(board, x, y);
        else { ClearSelection(); _message = "도안이나 손패 위에 놓아 주세요"; Render(); }
    }

    /// <summary>드래그 중인 도형을 화면 좌표로 이동한다.</summary>
    public void MoveCardDrag(Vector2 position)
    {
        if (_dragGhost == null) return;
        Vector2 point;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, position, null, out point))
        {
            Rect rect = _root.rect;
            _dragGhost.anchoredPosition = new Vector2(point.x - rect.xMin, point.y - rect.yMax);
        }
    }

    /// <summary>드래그 중인 카드를 격자 위에 미리 표시한다.</summary>
    public void PreviewCardDrag(PointerEventData eventData)
    {
        if (CanDragHammerCard || CanDragGlueCard) return;
        if (_boardGraphic != null) _boardGraphic.PreviewCard(eventData);
    }

    /// <summary>드롭 지점이 제작판이면 배치하고, 아니면 선택을 유지한다.</summary>
    public void EndCardDrag(PointerEventData eventData)
    {
        if (CanDragGlueCard)
        {
            SpellGame.Card card = _glueDragCard;
            int rotation = _glueDragRotation;
            _glueDragCard = null;
            if (_dragGhost != null) { _dragGhost.gameObject.SetActive(false); Destroy(_dragGhost.gameObject); }
            _dragGhost = null;
            Vector2Int position;
            if (card != null && _glueGraphic.TryDropPosition(eventData, out position))
            {
                int slot = _glueCards[0] == card ? 0 : _glueCards[1] == card ? 1 : -1;
                if (slot < 0)
                {
                    SelectGlueCard(card);
                    slot = _glueCards[0] == card ? 0 : _glueCards[1] == card ? 1 : -1;
                }
                if (slot >= 0) { _glueSelected = slot; _glueRotations[slot] = rotation; MoveGluePart(slot, position); }
            }
            RefreshGlue(); return;
        }
        if (CanDragHammerCard)
        {
            SpellGame.Card card = _hammerDragCard;
            _hammerDragCard = null;
            if (_dragGhost != null) Destroy(_dragGhost.gameObject);
            _dragGhost = null;
            Vector2 point;
            if (card != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, eventData.position, null, out point)
                && Vector2.Distance(point, new Vector2(_root.rect.xMin + 560, _root.rect.yMax - 340)) <= 210)
                SelectHammerCard(card);
            return;
        }
        if (_selectedCard == null || !CanInteract) return;
        if (_dragGhost != null) Destroy(_dragGhost.gameObject);
        _dragGhost = null;
        if (_boardGraphic.TryDrop(eventData)) return;
        _message = "도안의 색 테두리 안에 놓아 주세요"; Render();
    }

    /// <summary>현재 선택한 카드와 회전을 반환한다.</summary>
    public SpellGame.Card SelectedCard { get { return _selectedCard; } }
    public int Rotation { get { return _rotation; } }
    public bool CanInteract { get { return isActiveAndEnabled && _glueOverlay == null && _hammerPhase == HammerPhase.None && !_showPauseMenu && !_showHelp && _game.CurrentPhase == SpellGame.Phase.Combat; } }
    public bool CanDragHammerCard { get { return isActiveAndEnabled && (_hammerPhase == HammerPhase.Selecting || _hammerPhase == HammerPhase.Timing); } }
    public bool CanDragGlueCard { get { return isActiveAndEnabled && _glueOverlay != null; } }

    /// <summary>도안 목록의 드래그 시작을 기록해 진행 중 망치 진입을 막는다.</summary>
    public void BeginPatternDrag()
    {
        if (CanInteract) _isPatternDragging = true;
    }

    /// <summary>충돌하는 입력과 모달을 제외하고 망치 대상 선택 화면을 연다.</summary>
    private void BeginHammer()
    {
        if (!CanInteract || _pendingCast >= 0 || _dragGhost != null || _isPatternDragging || _boardGraphic.IsDragging) return;
        if (!_onboardingComplete && !_onboardingPlacementExplained && _game.CraftCount > 0) return;
        if (_game.HammerUsesLeft <= 0) { _message = "이번 턴의 망치를 이미 사용했습니다"; Render(); return; }
        ClearSelection(); Render();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        _hammerTurn = _game.Turn; _hammerBattle = _game.Battle;
        _hammerPhase = HammerPhase.Selecting;
        _hammerMessage = _game.Hand.Count == 0 ? "손패가 비어 있습니다. Esc로 돌아가세요" : "분해할 조각 하나를 선택하세요";
        RenderHammer();
    }

    /// <summary>망치 화면과 선택 참조를 정리한다. 이미 확정한 타격 결과는 되돌리지 않는다.</summary>
    private void CloseHammer()
    {
        if (_hammerDragCard != null && _dragGhost != null) { _dragGhost.gameObject.SetActive(false); Destroy(_dragGhost.gameObject); _dragGhost = null; }
        _hammerDragCard = null;
        if (_hammerOverlay != null) { _hammerOverlay.gameObject.SetActive(false); Destroy(_hammerOverlay.gameObject); }
        _hammerOverlay = null; _hammerCard = null; _hammerPhase = HammerPhase.None;
        _hammerResultShapes = Array.Empty<SpellGame.Shape>();
        _hammerHint = null; _hammerMarker = null; _hammerGaugeFill = null;
        _onboardingActivity = Time.unscaledTime;
    }

    /// <summary>선택한 손패의 분해 조건을 안내하고 유효하면 타이밍 게이지를 시작한다.</summary>
    private void SelectHammerCard(SpellGame.Card card)
    {
        if (_hammerPhase != HammerPhase.Selecting && _hammerPhase != HammerPhase.Timing) return;
        string issue = _game.HammerIssue(card);
        _hammerCard = issue == null ? card : null;
        _hammerMessage = issue ?? "높은 게이지에서 Space를 누르세요";
        _hammerPhase = issue == null ? HammerPhase.Timing : HammerPhase.Selecting;
        _hammerTimingStart = Time.unscaledTime;
        RenderHammer();
    }

    /// <summary>현재 강도와 손패 한도로 발생 가능한 분해 결과별 확률을 안내한다.</summary>
    private string HammerOdds(float strength)
    {
        float chance = SpellGame.DecompositionChance(strength);
        return $"강도 {strength:P0}\n기본 분해 보장\n각 자식은 {chance:P0}로 추가 분해\n최대 {_game.HammerMaxPieces(_hammerCard)}장 · 혼합 결과 가능";
    }

    /// <summary>확정된 혼합 도형을 종류별 장수로 묶어 결과 안내를 반환한다.</summary>
    private string HammerResultMessage()
    {
        if (_hammerResultShapes.Length == 1) return "추가 분해 불가 · 원본 유지";
        string message = "분해 결과: ";
        for (int shape = 0; shape < 4; shape++)
        {
            int count = 0;
            foreach (SpellGame.Shape piece in _hammerResultShapes) if ((int)piece == shape) count++;
            if (count > 0) message += $"{HammerShapeName((SpellGame.Shape)shape)} {count}장  ";
        }
        return message;
    }

    /// <summary>타이밍 시작 시각과 왕복 시간으로 현재 0~1 강도를 계산한다.</summary>
    private float HammerStrength()
    {
        return Mathf.PingPong((Time.unscaledTime - _hammerTimingStart) / Mathf.Max(0.1f, _hammerGaugeSeconds), 1f);
    }

    /// <summary>망치 결과에 사용할 기존 도형의 한국어 이름을 반환한다.</summary>
    private string HammerShapeName(SpellGame.Shape shape)
    {
        return shape == SpellGame.Shape.Rectangle ? "직사각형" : shape == SpellGame.Shape.Square ? "정사각형" : shape == SpellGame.Shape.Triangle ? "반칸 삼각형" : "1/4 삼각형";
    }

    /// <summary>기존 도형 그래픽으로 망치 대상이나 결과 아이콘을 중앙 좌표에 생성한다.</summary>
    private RectTransform HammerShape(RectTransform parent, SpellGame.Shape shape, float x, float y, float size)
    {
        RectTransform rect = Element("Hammer Piece", parent, x, y, shape == SpellGame.Shape.Rectangle ? size * 2 : size, size);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.gameObject.AddComponent<CanvasRenderer>();
        SpellShapeGraphic graphic = rect.gameObject.AddComponent<SpellShapeGraphic>();
        graphic.Shape = shape; graphic.raycastTarget = false; graphic.color = new Color(0.72f, 0.9f, 0.94f);
        return rect;
    }

    /// <summary>어두운 모달, 원형 나무판, 손패 선택과 오른쪽 강도 게이지를 생성한다.</summary>
    private void RenderHammer()
    {
        if (_hammerDragCard != null && _dragGhost != null) { _dragGhost.gameObject.SetActive(false); Destroy(_dragGhost.gameObject); _dragGhost = null; }
        _hammerDragCard = null;
        if (_hammerOverlay != null) { _hammerOverlay.gameObject.SetActive(false); Destroy(_hammerOverlay.gameObject); }
        _hammerOverlay = Panel(_root, 0, 0, 1280, 720, new Color(0.015f, 0.02f, 0.03f, 0.94f));
        Label(_hammerOverlay, "망치 작업대", 350, 35, 420, 42, 30);
        Label(_hammerOverlay, "일반·특수·접착 조각 분해 · 효과는 원본별 가장 큰 조각에 유지 · 배치 비용 없음", 140, 85, 1000, 32, 16);
        RectTransform board = Element("Hammer Workbench", _hammerOverlay, 350, 130, 420, 420);
        board.gameObject.AddComponent<CanvasRenderer>();
        board.gameObject.AddComponent<SpellHammerGraphic>().raycastTarget = false;
        Label(_hammerOverlay, _hammerPhase == HammerPhase.Selecting ? "조각을 클릭하거나 작업대로 드래그하세요" : "Space: 타격   ·   Esc: 타격 전 취소", 260, 550, 600, 32, 19);

        _hammerSource = _hammerCard == null ? null : HammerShape(_hammerOverlay, _hammerCard.Shape, 560, 340, 144);
        if (_hammerSource != null) _hammerSource.GetComponent<SpellShapeGraphic>().SetCard(_hammerCard);
        Panel(_hammerOverlay, 886, 181, 66, 278, new Color(0.40f, 0.34f, 0.27f));
        HammerBand(0, 0.25f, new Color(0.28f, 0.30f, 0.33f));
        HammerBand(0.25f, 0.5f, new Color(0.25f, 0.54f, 0.45f));
        HammerBand(0.5f, 0.8f, new Color(0.74f, 0.55f, 0.22f));
        HammerBand(0.8f, 1, new Color(0.8f, 0.29f, 0.20f));
        _hammerGaugeFill = Panel(_hammerOverlay, 934, 450, 10, 0, new Color(1f, 0.9f, 0.6f)).GetComponent<UnityEngine.UI.Image>();
        _hammerMarker = Panel(_hammerOverlay, 880, 446, 80, 6, Color.white);
        Label(_hammerOverlay, "강한 타격", 856, 140, 130, 30, 17);
        Label(_hammerOverlay, "약한 타격", 856, 460, 130, 30, 17);
        Label(_hammerOverlay, "임시 확률 규칙\n기본 분해는 항상 성공\n강할수록 추가 분해 확률 증가\n최소 1/4 · 손패 7장 제한", 990, 210, 220, 150, 17, TextAnchor.UpperLeft);
        Label(_hammerOverlay, _hammerMessage, 800, 495, 440, 75, 17);
        RectTransform hint = Element("Hammer Preview", _hammerOverlay, 980, 360, 240, 120);
        _hammerHint = hint.gameObject.AddComponent<UnityEngine.UI.Text>();
        _hammerHint.font = _font; _hammerHint.fontSize = 18; _hammerHint.color = Color.white; _hammerHint.raycastTarget = false;

        _hammerResults = Element("Hammer Results", _hammerOverlay, 560, 340, 0, 0);
        _hammerResults.gameObject.SetActive(false);
        _hammerVisual = Element("Hammer", _hammerOverlay, 820, 0, 0, 0);
        Panel(_hammerVisual, -12, 25, 24, 150, new Color(0.45f, 0.25f, 0.12f));
        Panel(_hammerVisual, -87, -57, 174, 94, new Color(0.16f, 0.19f, 0.22f));
        Panel(_hammerVisual, -80, -50, 160, 80, new Color(0.56f, 0.64f, 0.68f));
        Panel(_hammerVisual, -80, -50, 160, 10, new Color(0.82f, 0.88f, 0.91f));
        _hammerVisual.gameObject.SetActive(false);
        RectTransform impact = Element("Hammer Impact", _hammerOverlay, 560, 340, 0, 0);
        _hammerImpact = impact.gameObject.AddComponent<CanvasGroup>();
        _hammerImpact.blocksRaycasts = false; _hammerImpact.alpha = 0;
        for (int i = 0; i < 12; i++)
        {
            float angle = i * Mathf.PI / 6;
            RectTransform ray = Panel(impact, 0, 0, 25 + _hammerStrength * 45, 4, new Color(1f, 0.82f, 0.42f));
            ray.pivot = new Vector2(0, 0.5f);
            ray.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (95 + _hammerStrength * 45);
            ray.localRotation = Quaternion.Euler(0, 0, i * 30);
        }
        if (_hammerPhase == HammerPhase.Selecting || _hammerPhase == HammerPhase.Timing)
        {
            for (int i = 0; i < _game.Hand.Count; i++)
            {
                SpellGame.Card card = _game.Hand[i];
                RectTransform panel = Panel(_hammerOverlay, 80 + i * 160, 590, 148, 115, CardColor(card, card == _hammerCard));
                UnityEngine.UI.Button button = panel.gameObject.AddComponent<UnityEngine.UI.Button>();
                UnityEngine.UI.Navigation navigation = button.navigation; navigation.mode = UnityEngine.UI.Navigation.Mode.None; button.navigation = navigation;
                button.onClick.AddListener(() => SelectHammerCard(card));
                panel.gameObject.AddComponent<SpellCardDrag>().Setup(this, card);
                HammerShape(panel, card.Shape, 74, 32, 54).GetComponent<SpellShapeGraphic>().SetCard(card);
                Label(panel, CardName(card), 2, 65, 144, 48, 12);
            }
        }
        else
        {
            _hammerVisual.gameObject.SetActive(true);
            foreach (SpellGame.Shape shape in _hammerResultShapes) HammerShape(_hammerResults, shape, 0, 0, 70);
        }
    }

    /// <summary>강도 구간의 색을 오른쪽 게이지의 하한부터 상한까지 표시한다.</summary>
    private void HammerBand(float low, float high, Color color)
    {
        Panel(_hammerOverlay, 894, 190 + 260 * (1 - high), 32, 260 * (high - low), color);
    }

    /// <summary>Space 순간의 강도를 기록해 모델에 한 번만 적용하고 타격 연출을 시작한다.</summary>
    private void StrikeHammer()
    {
        _hammerStrength = HammerStrength();
        if (!_game.TryHammer(_hammerCard, _hammerStrength, out _hammerResultShapes))
        {
            _hammerMessage = _game.HammerIssue(_hammerCard); _hammerCard = null; _hammerPhase = HammerPhase.Selecting; RenderHammer(); return;
        }
        _hammerStrikeStart = Time.unscaledTime;
        _hammerPhase = HammerPhase.Striking;
        _hammerMessage = HammerResultMessage();
        RenderHammer();
    }

    /// <summary>상태 유효성, 취소와 타이밍 입력을 처리하고 타격·충격·분해 결과를 시간에 맞춰 표시한다.</summary>
    private void UpdateHammer()
    {
        if (_game.CurrentPhase != SpellGame.Phase.Combat || _game.Turn != _hammerTurn || _game.Battle != _hammerBattle)
        {
            CloseHammer(); Render(); return;
        }
        bool beforeStrike = _hammerPhase == HammerPhase.Selecting || _hammerPhase == HammerPhase.Timing;
        if (beforeStrike && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CloseHammer(); Render(); return;
        }
        if (_hammerPhase == HammerPhase.Timing)
        {
            if (_game.HammerIssue(_hammerCard) != null) { SelectHammerCard(_hammerCard); return; }
            float strength = HammerStrength();
            _hammerMarker.anchoredPosition = new Vector2(880, -(446 - strength * 260));
            _hammerGaugeFill.rectTransform.anchoredPosition = new Vector2(934, -(450 - strength * 260));
            _hammerGaugeFill.rectTransform.sizeDelta = new Vector2(10, strength * 260);
            _hammerHint.text = HammerOdds(strength);
            if (_hammerDragCard == null && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) StrikeHammer();
            return;
        }
        if (beforeStrike) return;
        float elapsed = Time.unscaledTime - _hammerStrikeStart;
        float strikeSeconds = Mathf.Max(0.1f, _hammerStrikeSeconds);
        float progress = Mathf.Clamp01(elapsed / strikeSeconds);
        _hammerVisual.anchoredPosition = Vector2.Lerp(new Vector2(820, 30), new Vector2(560, -335), progress * progress);
        _hammerVisual.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(-45, 0, progress));
        _hammerMarker.anchoredPosition = new Vector2(880, -(446 - _hammerStrength * 260));
        _hammerGaugeFill.rectTransform.anchoredPosition = new Vector2(934, -(450 - _hammerStrength * 260));
        _hammerGaugeFill.rectTransform.sizeDelta = new Vector2(10, _hammerStrength * 260);
        _hammerHint.text = $"확정 강도 {_hammerStrength:P0}\n{_hammerMessage}";
        if (elapsed < strikeSeconds) return;
        _hammerPhase = HammerPhase.Result;
        float resultTime = elapsed - strikeSeconds;
        _hammerVisual.gameObject.SetActive(resultTime < 0.12f);
        _hammerImpact.alpha = Mathf.Clamp01(1 - resultTime / 0.4f) * (0.45f + _hammerStrength * 0.55f);
        if (_hammerSource != null) _hammerSource.gameObject.SetActive(_hammerResultShapes.Length == 1);
        _hammerResults.gameObject.SetActive(_hammerResultShapes.Length > 1);
        int columns = _hammerResultShapes.Length > 4 ? 4 : 2;
        int rows = (_hammerResultShapes.Length + columns - 1) / columns;
        float spread = Mathf.SmoothStep(0, 1, Mathf.Clamp01(resultTime / 0.3f));
        for (int i = 0; i < _hammerResults.childCount; i++)
        {
            RectTransform piece = (RectTransform)_hammerResults.GetChild(i);
            piece.anchoredPosition = new Vector2((i % columns - (columns - 1) / 2f) * 85, -(i / columns - (rows - 1) / 2f) * 85) * spread;
            piece.localRotation = Quaternion.Euler(0, 0, i * (360f / _hammerResultShapes.Length) * spread);
        }
        float shake = Mathf.Sin(resultTime * 90) * Mathf.Max(0, 1 - resultTime / 0.25f) * (2 + _hammerStrength * 6);
        _hammerResults.anchoredPosition = new Vector2(560 + shake, -340);
        if (resultTime >= Mathf.Max(0.2f, _hammerResultSeconds))
        {
            _message = _hammerMessage; CloseHammer(); Render();
        }
    }

    /// <summary>일반 카드 수와 모달·드래그 상태를 확인하고 접착 작업을 시작한다.</summary>
    private void BeginGlue()
    {
        if (!CanInteract || _pendingCast >= 0 || _dragGhost != null || _isPatternDragging || _boardGraphic.IsDragging) return;
        if (!_onboardingComplete && !_onboardingPlacementExplained && _game.CraftCount > 0) return;
        int count = 0;
        foreach (SpellGame.Card card in _game.Hand) if (_game.GlueCardIssue(card) == null) count++;
        if (_game.GlueUsesLeft == 0 || count < 2)
        {
            _message = _game.GlueUsesLeft == 0 ? "이번 턴의 접착을 이미 사용했습니다" : "접착 가능한 조각이 두 장 이상 필요합니다";
            Render(); return;
        }
        ClearSelection(); Render();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        _glueTurn = _game.Turn; _glueBattle = _game.Battle;
        RenderGlue();
    }

    /// <summary>접착 미리 보기와 선택을 삭제한다. 확인 전에는 카드나 사용 횟수를 변경하지 않는다.</summary>
    private void CloseGlue()
    {
        if (_glueDragCard != null && _dragGhost != null) { _dragGhost.gameObject.SetActive(false); Destroy(_dragGhost.gameObject); _dragGhost = null; }
        _glueDragCard = null;
        _glueDragRotation = 0;
        if (_glueOverlay != null) { _glueOverlay.gameObject.SetActive(false); Destroy(_glueOverlay.gameObject); }
        _glueOverlay = null; _glueGraphic = null; _glueHint = null; _glueConfirm = null;
        for (int i = 0; i < 2; i++) { _glueCards[i] = null; _gluePositions[i] = new Vector2Int(2 + i * 2, 1); _glueRotations[i] = 0; }
        _glueSelected = 0; _glueMessage = ""; _onboardingActivity = Time.unscaledTime;
    }

    /// <summary>손패에서 접착할 서로 다른 일반 카드 두 장을 선택하거나 선택을 해제한다.</summary>
    private void SelectGlueCard(SpellGame.Card card)
    {
        string issue = _game.GlueCardIssue(card);
        if (issue != null) { _glueMessage = issue; RefreshGlue(); return; }
        for (int i = 0; i < 2; i++)
            if (_glueCards[i] == card) { _glueCards[i] = null; _glueMessage = ""; RenderGlue(); return; }
        int slot = _glueCards[0] == null ? 0 : _glueCards[1] == null ? 1 : -1;
        if (slot < 0) { _glueMessage = "두 장만 선택할 수 있습니다. 기존 선택을 해제하세요"; RefreshGlue(); return; }
        _glueCards[slot] = card; _glueSelected = slot;
        _gluePositions[slot] = new Vector2Int(2 + slot * 2, 1); _glueRotations[slot] = 0;
        _glueMessage = ""; RenderGlue();
    }

    /// <summary>접착 미리 보기의 지정 원본 카드를 반환한다.</summary>
    public SpellGame.Card GlueCard(int index) { return _glueCards[index]; }

    /// <summary>작업 공간에서 지정 조각의 스냅 좌표를 반환한다.</summary>
    public Vector2Int GluePosition(int index) { return _gluePositions[index]; }

    /// <summary>작업 공간에서 지정 조각의 회전 방향을 반환한다.</summary>
    public int GlueRotation(int index) { return _glueRotations[index]; }

    public int SelectedGluePart { get { return _glueSelected; } }

    /// <summary>회전과 이동의 대상이 될 작업 공간 조각을 선택한다.</summary>
    public void SelectGluePart(int index)
    {
        if (_glueOverlay == null || _glueCards[index] == null) return;
        _glueSelected = index; _glueMessage = ""; RefreshGlue();
    }

    /// <summary>회전한 조각 전체가 작업 공간 안에 남도록 이동 좌표를 제한하고 접촉 상태를 갱신한다.</summary>
    public void MoveGluePart(int index, Vector2Int position)
    {
        if (_glueOverlay == null || _glueCards[index] == null) return;
        int width = SpellGame.ShapeWidth(_glueCards[index], _glueRotations[index]);
        int height = SpellGame.ShapeHeight(_glueCards[index], _glueRotations[index]);
        _gluePositions[index] = new Vector2Int(Mathf.Clamp(position.x, 0, 8 - width), Mathf.Clamp(position.y, 0, 4 - height));
        _glueMessage = ""; RefreshGlue();
    }

    /// <summary>작업 공간 범위와 모델의 실제 겹침·변 접촉을 검사해 확인 불가 사유를 반환한다.</summary>
    public string GluePlacementIssue()
    {
        if (_glueCards[0] == null || _glueCards[1] == null) return "접착할 조각 2개를 선택하세요";
        for (int i = 0; i < 2; i++)
        {
            Vector2Int pos = _gluePositions[i];
            if (pos.x < 0 || pos.y < 0 || pos.x + SpellGame.ShapeWidth(_glueCards[i], _glueRotations[i]) > 8
                || pos.y + SpellGame.ShapeHeight(_glueCards[i], _glueRotations[i]) > 4) return "조각을 작업 공간 안으로 옮기세요";
        }
        Vector2Int relative = _gluePositions[1] - _gluePositions[0];
        return _game.GlueIssue(_glueCards[0], _glueRotations[0], _glueCards[1], _glueRotations[1], relative.x, relative.y);
    }

    /// <summary>접착 상태 색상과 확인 가능 여부만 갱신해 진행 중 드래그 객체를 유지한다.</summary>
    private void RefreshGlue()
    {
        if (_glueOverlay == null) return;
        string issue = GluePlacementIssue();
        _glueHint.text = (_glueMessage.Length > 0 ? _glueMessage : issue ?? "접착 가능 · 확인하면 카드 한 장으로 합쳐집니다")
            + $"\n선택 조각 {_glueSelected + 1} · R / 우클릭 회전";
        _glueHint.color = issue == null ? new Color(0.55f, 1f, 0.7f) : new Color(1f, 0.55f, 0.5f);
        _glueConfirm.interactable = issue == null && !_glueGraphic.IsDragging && _glueDragCard == null;
        _glueGraphic.SetVerticesDirty();
    }

    /// <summary>두 카드의 유효성과 접촉을 다시 확인한 후 접착을 한 번 확정하고 전투 화면으로 복귀한다.</summary>
    private void ConfirmGlue()
    {
        if (_glueOverlay == null || _glueGraphic.IsDragging || _glueDragCard != null || GluePlacementIssue() != null) return;
        Vector2Int relative = _gluePositions[1] - _gluePositions[0];
        if (!_game.TryGlue(_glueCards[0], _glueRotations[0], _glueCards[1], _glueRotations[1], relative.x, relative.y))
        {
            _glueMessage = "카드 상태가 바뀌어 접착하지 못했습니다"; RefreshGlue(); return;
        }
        _message = "접착 완료 · 전체 모양을 한 장으로 배치합니다";
        CloseGlue(); Render();
    }

    /// <summary>접착 모달의 턴·카드 유효성을 확인하고 취소와 선택 조각의 회전을 처리한다.</summary>
    private void UpdateGlue()
    {
        bool invalid = _game.CurrentPhase != SpellGame.Phase.Combat || _game.Turn != _glueTurn || _game.Battle != _glueBattle;
        foreach (SpellGame.Card card in _glueCards) if (card != null && _game.GlueCardIssue(card) != null) invalid = true;
        if (invalid || (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)) { CloseGlue(); Render(); return; }
        bool rotate = (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            || (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame);
        if (rotate) RotateGluePart();
    }

    /// <summary>선택한 접착 원본을 한 번 회전하고 전체 점유·접촉 미리 보기를 갱신한다.</summary>
    private void RotateGluePart()
    {
        if (_glueOverlay == null) return;
        if (_glueDragCard != null)
        {
            _glueDragRotation = (_glueDragRotation + 1) % 4;
            _dragGhost.sizeDelta = new Vector2(72 * SpellGame.ShapeWidth(_glueDragCard, _glueDragRotation), 72 * SpellGame.ShapeHeight(_glueDragCard, _glueDragRotation));
            _dragGhost.GetComponent<SpellShapeGraphic>().SetCard(_glueDragCard, _glueDragRotation);
            return;
        }
        int part = _glueGraphic.IsDragging ? _glueGraphic.DragPart : _glueSelected;
        if (_glueCards[part] == null) return;
        int previousHeight = SpellGame.ShapeHeight(_glueCards[part], _glueRotations[part]);
        _glueRotations[part] = (_glueRotations[part] + 1) % 4;
        if (_glueGraphic.IsDragging) _glueGraphic.RotateDragOffset(previousHeight);
        else MoveGluePart(part, _gluePositions[part]);
    }

    /// <summary>접착 모달 전용 버튼을 만들고 오래된 화면 콜백과 Space 제출을 방지한다.</summary>
    private UnityEngine.UI.Button GlueButton(RectTransform overlay, string caption, float x, float y, float width, float height, Action action)
    {
        RectTransform rect = Panel(overlay, x, y, width, height, new Color(0.23f, 0.32f, 0.37f));
        UnityEngine.UI.Button button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
        UnityEngine.UI.Navigation navigation = button.navigation; navigation.mode = UnityEngine.UI.Navigation.Mode.None; button.navigation = navigation;
        button.onClick.AddListener(() => { if (isActiveAndEnabled && _glueOverlay != null && _glueOverlay == overlay) action(); });
        Label(rect, caption, 4, 2, width - 8, height - 4, 18);
        return button;
    }

    /// <summary>전투와 분리된 접착 작업 격자, 손패 선택 및 확인·취소 UI를 생성한다.</summary>
    private void RenderGlue()
    {
        if (_glueOverlay != null) { _glueOverlay.gameObject.SetActive(false); Destroy(_glueOverlay.gameObject); }
        _glueOverlay = Panel(_root, 0, 0, 1280, 720, new Color(0.025f, 0.035f, 0.04f, 0.96f));
        Label(_glueOverlay, "접착 작업대", 350, 25, 580, 45, 30);
        Label(_glueOverlay, "일반·특수 조각 2장 선택 → 드래그·R로 변 맞추기 → 접착 확인", 140, 85, 1000, 35, 19);
        RectTransform board = Element("Glue Workbench", _glueOverlay, 200, 170, 576, 288);
        board.gameObject.AddComponent<CanvasRenderer>();
        _glueGraphic = board.gameObject.AddComponent<SpellGlueGraphic>(); _glueGraphic.Setup(this);
        RectTransform hint = Element("Glue Status", _glueOverlay, 825, 175, 400, 110);
        _glueHint = hint.gameObject.AddComponent<UnityEngine.UI.Text>();
        _glueHint.font = _font; _glueHint.fontSize = 20; _glueHint.color = Color.white; _glueHint.raycastTarget = false;
        GlueButton(_glueOverlay, "회전 · R", 850, 295, 280, 45, RotateGluePart);
        _glueConfirm = GlueButton(_glueOverlay, "접착 확인", 850, 360, 280, 55, ConfirmGlue);
        GlueButton(_glueOverlay, "취소 · Esc", 850, 435, 280, 45, () => { CloseGlue(); Render(); });
        GlueButton(_glueOverlay, "선택 조각 중앙으로", 850, 505, 280, 40, () => MoveGluePart(_glueSelected, new Vector2Int(3, 1)));
        Label(_glueOverlay, "카드 2장 → 1장 · 배치 비용 1회 · 현재 전투만 유지\n드래그 또는 빈 칸 클릭으로 이동 · 손패 재클릭으로 선택 해제", 100, 490, 660, 65, 17);
        for (int i = 0; i < _game.Hand.Count; i++)
        {
            SpellGame.Card card = _game.Hand[i];
            bool selected = _glueCards[0] == card || _glueCards[1] == card;
            UnityEngine.UI.Button button = GlueButton(_glueOverlay, "", 65 + i * 165, 585, 155, 120, () => SelectGlueCard(card));
            button.GetComponent<UnityEngine.UI.Image>().color = CardColor(card, selected);
            button.gameObject.AddComponent<SpellCardDrag>().Setup(this, card);
            RectTransform iconRect = Element("Glue Hand Shape", (RectTransform)button.transform, 5, 3, 145, 64);
            iconRect.gameObject.AddComponent<CanvasRenderer>();
            SpellShapeGraphic icon = iconRect.gameObject.AddComponent<SpellShapeGraphic>(); icon.SetCard(card); icon.raycastTarget = false;
            Label((RectTransform)button.transform, CardName(card), 3, 70, 149, 45, 13);
        }
        RefreshGlue();
    }

    /// <summary>목록에서 이동하는 도안의 미리 보기를 현재 제작판에 전달한다.</summary>
    public void PreviewPatternDrag(PointerEventData eventData, int pattern)
    {
        if (CanInteract) _boardGraphic.PreviewPattern(eventData, pattern, Vector2Int.zero);
    }

    /// <summary>목록에서 이동한 도안의 미리 보기를 지우고 드롭 위치를 적용한다.</summary>
    public void EndPatternDrag(PointerEventData eventData, int pattern)
    {
        _isPatternDragging = false;
        if (!CanInteract) return;
        _boardGraphic.ClearPatternPreview();
        _boardGraphic.DropPattern(eventData, pattern);
    }
}


