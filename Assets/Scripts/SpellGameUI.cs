using System;
using System.Collections.Generic;

using UnityEngine;

using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>주문 조각 PoC의 화면과 입력을 전투 상태에 연결한다.</summary>
public sealed class SpellGameUI : MonoBehaviour
{
    private const float LEGACY_SCALE = 1.5f;
    private const float LEGACY_CELL = 84f;
    private const float TOAST_SECONDS = 2.2f;
    private const float CURSOR_HINT_OFFSET = 24f;
    private const float CURSOR_HINT_PADDING = 24f;
    private const float TAB_HEIGHT = 36f;
    private const int PLAYER_MAX_HEALTH = 50;
    private const int BATTLE_COUNT = 6;
    private const float ENEMY_BAR_WIDTH = 320f;
    private const float ENEMY_NARROW_BAR_WIDTH = 280f;
    private const float MENU_GAP = 4f;
    private static readonly bool ONBOARDING_ENABLED = false;
    private static readonly string[] SPELL_NAMES = { "불씨", "방벽", "번개", "유성" };
    private static readonly string[] SPELL_EFFECTS = { "단일 피해 6", "방어 7", "단일 피해 12", "전체 피해 10" };
    private static readonly int[] SPELL_BASE = { 6, 7, 12, 10 };
    private static readonly string[] REWARD_NAMES = { "불꽃 삼각형\n공격 피해 +2", "수호 정사각형\n방어 +3", "공명 직사각형\n3칸 이상: 조각 1장 뽑기", "잔상 1/4 삼각형\n시전 후 1회 유지", "도안: 번개\n단일 피해 12", "도안: 유성\n전체 피해 10" };
    private static readonly IReadOnlyList<Vector2Int>[] PATTERN_CELLS = CreatePatternCells();
    private static readonly Vector2 MESSAGE_POSITION = new Vector2(705, -480);
    private static readonly Vector2 FEEDBACK_POSITION = new Vector2(960, -100);
    private static readonly Vector2 NORMAL_ENEMY_SIZE = new Vector2(150, 190);
    private static readonly Vector2 LARGE_ENEMY_SIZE = new Vector2(190, 230);
    private static readonly Vector2 BOSS_SIZE = new Vector2(210, 245);

    [Header("화면 연결")]
    [SerializeField] private GameObject _canvas;
    [SerializeField] private RectTransform _content;
    [SerializeField] private SpellCombatScreen _combatScreen;
    [SerializeField] private SpellTitleScreen _titleScreen;
    [SerializeField] private SpellRewardScreen _rewardScreen;
    [SerializeField] private SpellResultScreen _resultScreen;
    [SerializeField] private SpellPauseMenu _pauseMenu;
    [SerializeField] private SpellHelpPanel _helpPanel;
    [SerializeField] private SpellPileViewer _pileViewer;
    [SerializeField] private SpellTable3D _table;
    [SerializeField] private GameObject _background;

    [Header("상태 색")]
    [SerializeField] private Color[] _patternColors = { new Color(0.957f, 0.788f, 0.365f), new Color(0.902f, 0.922f, 0.941f), new Color(0.725f, 0.89f, 0.42f), new Color(0.949f, 0.553f, 0.698f) };
    [SerializeField] private Color _flameColor = new Color(0.878f, 0.478f, 0.247f);
    [SerializeField] private Color _guardColor = new Color(0.31f, 0.584f, 0.839f);
    [SerializeField] private Color _resonanceColor = new Color(0.655f, 0.467f, 0.851f);
    [SerializeField] private Color _afterimageColor = new Color(0.333f, 0.78f, 0.702f);
    [SerializeField] private Color[] _enemyColors = { new Color(0.478f, 0.416f, 0.549f), new Color(0.373f, 0.498f, 0.447f), new Color(0.549f, 0.431f, 0.369f) };
    [SerializeField] private Color _textColor = new Color(0.945f, 0.922f, 0.867f);
    [SerializeField] private Color _subColor = new Color(0.741f, 0.706f, 0.627f);
    [SerializeField] private Color _dimColor = new Color(0.224f, 0.267f, 0.416f);
    [SerializeField] private Color _dangerColor = new Color(0.878f, 0.376f, 0.353f);
    [SerializeField] private Color _warnColor = new Color(0.949f, 0.722f, 0.294f);

    private readonly SpellGame _game = new SpellGame();
    private readonly List<int> _rewards = new List<int>();
    private readonly SpellPatternLayout _layout = new SpellPatternLayout();
    private readonly System.Random _random = new System.Random();
    private RectTransform _root;
    private Font _font;
    private SpellBoardGraphic _boardGraphic;
    private Vector2[] _enemyPositions;
    private RectTransform _dragGhost;
    private RectTransform _aimElbow;
    private string _hintText = "";

    private SpellGame.Card _selectedCard;
    private SpellGame.Placement _selectedPiece;
    private int _selectedBoard = -1;
    private int _rotation;
    private string _message = "";
    private string _notice = "";
    private Color _noticeColor;
    private SpellGame.Phase _renderedPhase = (SpellGame.Phase)(-1);

    private string _feedback = "";
    private Color _feedbackColor;
    private float _feedbackUntil;
    private int _feedbackVersion;
    private int _feedbackShownVersion;
    private int _impactTarget = -1;
    private bool _impactAll;

    private bool _showHelp;
    private int _openPile = -1;
    private bool _showPauseMenu;
    private int _pendingCast = -1;
    private bool _castDragging;
    private int _castDragBoard = -1;
    private int _hoverEnemy = -1;

    private bool _onboardingComplete;
    private bool _onboardingPlacementExplained;
    private string _onboardingStep = "";
    private float _onboardingActivity;
    private RectTransform _onboardingGhost;
    private CanvasGroup _onboardingGhostGroup;
    private RectTransform _onboardingGhostIcon;
    private int _onboardingGhostRotation;
    private Vector2 _onboardingGhostStart;
    private Vector2 _onboardingGhostEnd;

    public SpellGame.Card SelectedCard { get { return _selectedCard; } }
    public int Rotation { get { return _rotation; } }
    public bool IsTableActive { get { return _table != null; } }
    public bool CanInteract { get { return isActiveAndEnabled && !_showPauseMenu && !_showHelp && _openPile < 0 && _game.CurrentPhase == SpellGame.Phase.Combat; } }

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

    void Awake()
    {
        _font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 24);
        // 꺼 둔 첫 주문 온보딩만 기존 1280×720 좌표로 그린다. 전투 화면 바로 위, 오버레이 아래에 둔다. 새 화면 연결은 다음 작업이다.
        _root = Element("Onboarding Layer", _content, 0, 0, 1280, 720);
        _root.anchorMin = _root.anchorMax = _root.pivot = new Vector2(0.5f, 0.5f);
        _root.anchoredPosition = Vector2.zero;
        _root.localScale = Vector3.one * LEGACY_SCALE;
        _root.SetSiblingIndex(_combatScreen.transform.GetSiblingIndex() + 1);
        _boardGraphic = _combatScreen.Board;
        _boardGraphic.Setup(this, _game, _layout);
        SetupCombatScreen();
        _table.Setup(this, _game, _layout, _combatScreen);
        SetupOtherScreens();
        SetupOverlays();
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
        ClearSelection();
        _pendingCast = -1;
        _openPile = -1;
        _castDragging = false;
        if (_boardGraphic != null) _boardGraphic.CancelDrag();
        HideDragGhost();
        if (_table != null) _table.SetVisible(false);
        if (_canvas != null) _canvas.SetActive(false);
    }

    void Update()
    {
        UpdateFeedback();
        UpdateCursorHint();
        if (ONBOARDING_ENABLED) UpdateOnboarding();
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (_combatScreen.MoreMenu.IsOpen) { _combatScreen.MoreMenu.Close(); return; }
            if (_openPile >= 0) _openPile = -1;
            else if (_showPauseMenu) _showPauseMenu = false;
            else OpenPauseState();
            _onboardingActivity = Time.unscaledTime; Render(); return;
        }
        if (_showPauseMenu || _openPile >= 0 || _showHelp) return;
        if (_game.CurrentPhase != SpellGame.Phase.Combat) return;
        if (_table.IsDragging) return;
        bool rotate = Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
        bool rightClick = Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
        if (!rotate && !rightClick) return;
        if (_boardGraphic.IsDraggingPattern) _boardGraphic.RotatePatternDrag();
        else if (_selectedCard != null)
        {
            _rotation = (_rotation + 1) % 4;
            if (_dragGhost == null) Render();
            else
            {
                _dragGhost.localRotation = Quaternion.Euler(0, 0, -90 * _rotation);
                _boardGraphic.RefreshPreview();
            }
        }
        else if (rightClick && _pendingCast >= 0) { _pendingCast = -1; Render(); }
    }

    /// <summary>일시정지 메뉴를 열면서 조준·선택·드래그를 정리한다.</summary>
    private void OpenPauseState()
    {
        _showHelp = false; _showPauseMenu = true; _pendingCast = -1; _castDragging = false; _message = "";
        ClearSelection(); HideDragGhost();
    }

    /// <summary>전투 화면의 고정 버튼·슬롯에 동작과 입력 대상을 한 번만 연결한다.</summary>
    private void SetupCombatScreen()
    {
        SpellCombatScreen screen = _combatScreen;
        _enemyPositions = new Vector2[screen.Enemies.Length];
        for (int i = 0; i < screen.Enemies.Length; i++) _enemyPositions[i] = ((RectTransform)screen.Enemies[i].transform).anchoredPosition;
        screen.Help.SetAction(() => { if (!CanInteract) return; _showHelp = true; Render(); });
        screen.Menu.SetAction(() => { OpenPauseState(); Render(); });
        screen.DrawPile.Setup("드로우 덱", () => OpenPile(0));
        screen.DiscardPile.Setup("묘지", () => OpenPile(1));
        screen.EndTurn.SetAction(() => { if (CanInteract) EndPlayerTurn(); });
        for (int i = 0; i < screen.Tabs.Length; i++) { int index = i; screen.Tabs[i].Setup(this, i, () => OpenPatternMenu(index)); }
        for (int i = 0; i < screen.CastButtons.Length; i++) screen.CastButtons[i].Setup(this, i);
        for (int i = 0; i < screen.Enemies.Length; i++) screen.Enemies[i].Setup(this, i, ClickEnemy);
        _aimElbow = Instantiate(screen.AimLine, screen.AimLine.parent);
        _aimElbow.name = "Aim Line Elbow";
    }

    /// <summary>일시정지·도움말·더미 열람 오버레이의 버튼 동작을 연결한다.</summary>
    private void SetupOverlays()
    {
        _pauseMenu.Continue.SetAction(() => { _showPauseMenu = false; _onboardingActivity = Time.unscaledTime; Render(); });
        _pauseMenu.Title.SetAction(() => { _game.ReturnToTitle(); _showPauseMenu = false; _showHelp = false; _pendingCast = -1; _feedbackUntil = 0; ClearSelection(); Render(); });
        _pauseMenu.Quit.SetAction(Application.Quit);
        _helpPanel.Close.SetAction(() => { _showHelp = false; Render(); });
    }

    /// <summary>드로우 덱 또는 묘지 열람을 연다.</summary>
    private void OpenPile(int pile)
    {
        if (!CanInteract) return;
        _pendingCast = -1; ClearSelection(); _openPile = pile; Render();
    }

    /// <summary>시전 결과의 적 강조를 시간에 따라 흐리게 한다.</summary>
    private void UpdateFeedback()
    {
        if (_game.CurrentPhase != SpellGame.Phase.Combat) return;
        float remaining = _feedbackUntil - Time.unscaledTime;
        SpellEnemyView[] enemies = _combatScreen.Enemies;
        float alpha = remaining > 0 ? 0.32f * Mathf.Clamp01((remaining - 1.05f) / 0.45f) : 0;
        for (int i = 0; i < enemies.Length; i++) enemies[i].SetImpact(_feedbackColor, (_impactAll || i == _impactTarget) ? alpha : 0);
    }

    /// <summary>선택·조준·도안 이동 중에만 조작 안내를 커서 옆에 표시한다.</summary>
    private void UpdateCursorHint()
    {
        RectTransform hint = _combatScreen.CursorHint;
        bool show = CanInteract && Mouse.current != null && !string.IsNullOrEmpty(_hintText)
            && (_selectedCard != null || _boardGraphic.IsDraggingPattern || _pendingCast >= 0 || _castDragging);
        if (hint.gameObject.activeSelf != show) hint.gameObject.SetActive(show);
        if (!show) return;
        UnityEngine.UI.Text label = _combatScreen.CursorHintText;
        if (label.text != _hintText) label.text = _hintText;
        Vector2 point = ToLayer(Mouse.current.position.ReadValue());
        Rect area = _combatScreen.DragLayer.rect;
        float width = label.preferredWidth + CURSOR_HINT_PADDING;
        hint.sizeDelta = new Vector2(width, hint.sizeDelta.y);
        hint.anchoredPosition = new Vector2(Mathf.Min(point.x + CURSOR_HINT_OFFSET, area.width - width), Mathf.Max(point.y - CURSOR_HINT_OFFSET, -area.height + hint.sizeDelta.y));
    }

    /// <summary>주문 결과를 기록해 화면이 갱신된 뒤에도 일시적으로 표시한다.</summary>
    private void ShowFeedback(string message, Color color)
    {
        _feedback = message; _feedbackColor = color; _feedbackUntil = Time.unscaledTime + 1.5f; _feedbackVersion++;
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
    private RectTransform Button(RectTransform parent, string caption, float x, float y, float width, float height, Action action, Color? color = null, int fontSize = 18)
    {
        RectTransform rect = Panel(parent, x, y, width, height, color ?? new Color(0.24f, 0.31f, 0.39f));
        UnityEngine.UI.Button button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.onClick.AddListener(() => action()); Label(rect, caption, 4, 2, width - 8, height - 4, fontSize);
        return rect;
    }

    /// <summary>현재 게임 단계에 맞춰 화면을 갱신한다. 전투 화면은 다시 만들지 않고 값만 갱신한다.</summary>
    private void Render()
    {
        if (_pendingCast >= 0 && (_game.CurrentPhase != SpellGame.Phase.Combat || !_game.IsComplete(_pendingCast))) _pendingCast = -1;
        _boardGraphic.CancelDrag();
        if (_dragGhost != null) ClearSelection();
        HideDragGhost(); HideAim();
        _castDragging = false; _hoverEnemy = -1;
        _combatScreen.Tooltip.Hide();
        _combatScreen.MoreMenu.Close();
        SpellGame.Phase phase = _game.CurrentPhase;
        bool combat = phase == SpellGame.Phase.Combat;
        _background.SetActive(!combat);
        bool result = phase == SpellGame.Phase.Victory || phase == SpellGame.Phase.Defeat;
        _combatScreen.gameObject.SetActive(combat);
        _table.SetVisible(combat);
        _titleScreen.gameObject.SetActive(phase == SpellGame.Phase.Title);
        _rewardScreen.gameObject.SetActive(phase == SpellGame.Phase.Reward);
        _resultScreen.gameObject.SetActive(result);
        if (combat) RefreshCombat();
        else if (phase == SpellGame.Phase.Reward) RefreshRewards();
        else if (result) _resultScreen.Bind(phase == SpellGame.Phase.Victory ? "승리" : "패배");
        RenderOnboardingLayer(combat);
        RefreshOverlays();
        _renderedPhase = phase;
    }

    /// <summary>타이틀 시작·결과 다시 시작·보상 건너뛰기 버튼의 동작을 연결한다.</summary>
    private void SetupOtherScreens()
    {
        _titleScreen.StartButton.SetAction(StartNewGame);
        _resultScreen.Restart.SetAction(StartNewGame);
        _rewardScreen.Skip.SetAction(() => ChooseReward(-1));
    }

    /// <summary>새 게임을 시작하고 도안 배치와 선택을 초기화한다.</summary>
    private void StartNewGame()
    {
        _game.StartGame(); ResetPatterns(); ClearSelection(); Render();
    }

    /// <summary>보상을 고르거나(-1은 건너뛰기) 다음 전투로 넘어간다.</summary>
    private void ChooseReward(int reward)
    {
        if (_game.ChooseReward(reward)) { ClearSelection(); _message = ""; Render(); }
    }

    /// <summary>꺼 둔 첫 주문 온보딩 층을 비우고, 켜져 있으면 전투 중에만 다시 그린다.</summary>
    private void RenderOnboardingLayer(bool combat)
    {
        _onboardingGhost = null; _onboardingGhostGroup = null; _onboardingGhostIcon = null;
        foreach (Transform child in _root) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        bool show = ONBOARDING_ENABLED && combat && !_onboardingComplete;
        _root.gameObject.SetActive(show);
        if (show) RenderOnboarding();
    }

    /// <summary>일시정지·도움말·더미 열람 오버레이를 상태에 맞게 켜고 내용을 갱신한다.</summary>
    private void RefreshOverlays()
    {
        _helpPanel.gameObject.SetActive(_showHelp);
        _pauseMenu.gameObject.SetActive(_showPauseMenu);
        _pileViewer.gameObject.SetActive(_openPile >= 0);
        if (_openPile < 0) return;
        Dictionary<string, int> summary = _game.GetPileSummary(_openPile == 0);
        List<string> keys = new List<string>(summary.Keys);
        keys.Sort(StringComparer.Ordinal);
        System.Text.StringBuilder body = new System.Text.StringBuilder();
        foreach (string key in keys)
        {
            string[] parts = key.Split(':');
            SpellGame.Card card = new SpellGame.Card { Shape = (SpellGame.Shape)Enum.Parse(typeof(SpellGame.Shape), parts[0]), Effect = (SpellGame.Effect)Enum.Parse(typeof(SpellGame.Effect), parts[1]) };
            body.Append(CardName(card).Replace("\n", " · ")).Append("  × ").Append(summary[key]).Append('\n');
        }
        if (keys.Count == 0) body.Append("비어 있습니다");
        _pileViewer.Bind(_openPile == 0 ? "드로우 덱" : "묘지", body.ToString(), () => { _openPile = -1; Render(); });
    }

    /// <summary>전투 화면의 모든 뷰에 현재 상태를 넣는다.</summary>
    private void RefreshCombat()
    {
        SpellCombatScreen screen = _combatScreen;
        EnsurePatterns();
        screen.Battle.text = $"전투 {_game.Battle}/{BATTLE_COUNT}";
        for (int i = 0; i < screen.ProgressDots.Length; i++)
            screen.ProgressDots[i].color = i < _game.Battle - 1 ? _subColor : i == _game.Battle - 1 ? _textColor : _dimColor;
        int incoming = 0;
        foreach (SpellGame.Enemy enemy in _game.Enemies) if (enemy.Health > 0) incoming += enemy.Intent;
        screen.PlayerStatus.Bind(_game.Health, PLAYER_MAX_HEALTH, _game.Block, Math.Max(0, incoming - _game.Block));
        screen.Placement.gameObject.SetActive(false);
        screen.BoardCount.gameObject.SetActive(false);
        _boardGraphic.MarkStateDirty();
        RefreshPatternControls();
        RefreshPreviewBar();
        RefreshEnemies();
        screen.DrawPile.Bind(_game.DrawCount);
        screen.DiscardPile.Bind(_game.DiscardCount);
        for (int i = 0; i < screen.Hand.Length; i++)
        {
            bool has = !IsTableActive && i < _game.Hand.Count;
            screen.Hand[i].gameObject.SetActive(has);
            if (!has) continue;
            SpellGame.Card card = _game.Hand[i];
            screen.Hand[i].Bind(this, card, DisplayName(card), EffectText(card), EffectColor(card.Effect), false, card == _selectedCard && _selectedPiece == null, SelectHandCard);
        }
        screen.EndTurn.SetLabel($"{_game.Turn}턴 종료");
        if (!string.IsNullOrEmpty(_message)) { screen.Toast.Show(_message, MESSAGE_POSITION, TOAST_SECONDS, _warnColor, true); _message = _notice = ""; }
        else if (!string.IsNullOrEmpty(_notice)) { screen.Toast.Show(_notice, MESSAGE_POSITION, TOAST_SECONDS, _noticeColor, false); _notice = ""; }
        else if (Time.unscaledTime < _feedbackUntil && _feedbackShownVersion != _feedbackVersion)
        {
            screen.Toast.Show(_feedback, FEEDBACK_POSITION, _feedbackUntil - Time.unscaledTime, _feedbackColor, false);
            _feedbackShownVersion = _feedbackVersion;
        }
        SetPreviewHint(-1, Vector2Int.zero);
        _table.Sync();
    }

    /// <summary>3D 판에서 모델 배치가 바뀌면 기존 선택과 화면 상태를 갱신한다.</summary>
    public void TableChanged()
    {
        ClearSelection(); Render();
    }

    /// <summary>3D 판 위 화면 위치를 소수 격자 좌표로 바꾸고 교차 여부를 반환한다.</summary>
    public bool TryTableGrid(Vector2 position, out Vector2 grid)
    {
        return _table.ScreenToGrid(position, out grid);
    }

    /// <summary>도안 탭과 완성 도안의 시전 버튼을 도안 위치·회전에 맞춰 놓는다.</summary>
    private void RefreshPatternControls()
    {
        float cell = _boardGraphic.CellSize;
        for (int i = 0; i < _combatScreen.Tabs.Length; i++)
        {
            bool show = _game.Spells.Contains(i) && _layout.IsPlaced(i);
            _combatScreen.Tabs[i].gameObject.SetActive(show);
            bool castable = show && _game.IsComplete(i);
            _combatScreen.CastButtons[i].gameObject.SetActive(castable);
            if (!show) continue;
            Rect bounds = PatternBounds(i, cell);
            Vector2[] candidates = { new Vector2(bounds.xMin, -bounds.yMin + TAB_HEIGHT), new Vector2(bounds.xMin, -bounds.yMax), new Vector2(bounds.xMin - cell, -bounds.yMin), new Vector2(bounds.xMax, -bounds.yMin) };
            Vector2 position = candidates[0];
            foreach (Vector2 candidate in candidates) if (IsTabPositionFree(i, candidate, cell)) { position = candidate; break; }
            _combatScreen.Tabs[i].Bind(SPELL_NAMES[i], PatternColor(i), position, cell);
            if (castable) _combatScreen.CastButtons[i].Bind(PatternColor(i), _pendingCast == i || (_castDragging && _castDragBoard == i), PatternCenter(i, cell));
        }
        _table.ProjectControls();
    }

    /// <summary>회전된 도안의 실제 삼각 구역을 포함하는 격자 기준 픽셀 경계를 반환한다.</summary>
    private Rect PatternBounds(int spell, float cell)
    {
        float left = float.PositiveInfinity, top = float.PositiveInfinity, right = float.NegativeInfinity, bottom = float.NegativeInfinity;
        for (int y = 0; y < SpellPatternLayout.ROWS; y++) for (int x = 0; x < SpellPatternLayout.COLUMNS; x++)
        {
            int mask = _layout.PlacedMask(spell, x, y);
            for (int sector = 0; sector < 4; sector++)
            {
                if ((mask & (1 << sector)) == 0) continue;
                left = Mathf.Min(left, (x + (sector == 1 ? 0.5f : 0f)) * cell);
                right = Mathf.Max(right, (x + (sector == 3 ? 0.5f : 1f)) * cell);
                top = Mathf.Min(top, (y + (sector == 2 ? 0.5f : 0f)) * cell);
                bottom = Mathf.Max(bottom, (y + (sector == 0 ? 0.5f : 1f)) * cell);
            }
        }
        return Rect.MinMaxRect(left, top, right, bottom);
    }

    /// <summary>이름표 후보가 다른 도안과 이미 놓인 이름표에 겹치지 않는지 반환한다.</summary>
    private bool IsTabPositionFree(int spell, Vector2 position, float cell)
    {
        Rect candidate = new Rect(position.x, -position.y, cell, TAB_HEIGHT);
        foreach (int other in _game.Spells)
        {
            if (other == spell || !_layout.IsPlaced(other)) continue;
            for (int y = 0; y < SpellPatternLayout.ROWS; y++) for (int x = 0; x < SpellPatternLayout.COLUMNS; x++)
                if (_layout.PlacedMask(other, x, y) != 0 && candidate.Overlaps(new Rect(x * cell, y * cell, cell, cell))) return false;
            if (other < spell && _combatScreen.Tabs[other].gameObject.activeSelf)
            {
                RectTransform tab = _combatScreen.Tabs[other].Rect;
                if (candidate.Overlaps(new Rect(tab.anchoredPosition.x, -tab.anchoredPosition.y, tab.sizeDelta.x, tab.sizeDelta.y))) return false;
            }
        }
        return true;
    }

    /// <summary>지정 줄의 열 범위를 다른 배치 도안이 쓰는지 반환한다.</summary>
    private bool RowUsedByOther(int spell, int row, int fromX, int toX)
    {
        for (int i = 0; i < _game.Spells.Count; i++)
        {
            int other = _game.Spells[i];
            if (other == spell) continue;
            for (int x = fromX; x < toX; x++) if (_layout.PlacedMask(other, x, row) != 0) return true;
        }
        return false;
    }

    /// <summary>도안이 차지한 구역 수로 가중한 칸 중심의 평균을 격자 기준 좌표(y 아래가 음수)로 반환한다.</summary>
    private Vector2 PatternCenter(int spell, float cell)
    {
        Vector2 sum = Vector2.zero; int weight = 0;
        for (int y = 0; y < SpellPatternLayout.ROWS; y++) for (int x = 0; x < SpellPatternLayout.COLUMNS; x++)
        {
            int count = BitCount(_layout.PlacedMask(spell, x, y));
            sum += new Vector2(x + 0.5f, y + 0.5f) * count; weight += count;
        }
        Vector2 center = weight > 0 ? sum / weight : Vector2.zero;
        return new Vector2(center.x * cell, -center.y * cell);
    }

    /// <summary>4비트 구역 마스크에서 켜진 구역 수를 반환한다.</summary>
    private static int BitCount(int mask)
    {
        int count = 0;
        for (int i = 0; i < 4; i++) if ((mask & (1 << i)) != 0) count++;
        return count;
    }

    /// <summary>조준 중·선택 중·완성된 도안 순으로 하나를 골라 고정 미리보기에 계산식을 표시한다.</summary>
    private void RefreshPreviewBar()
    {
        _combatScreen.Preview.gameObject.SetActive(false);
    }

    /// <summary>도안의 빈 구역을 1/4칸 단위 문구로 반환한다.</summary>
    private string RemainingText(int spell)
    {
        int total = 0, filled = 0;
        for (int y = 0; y < 3; y++) for (int x = 0; x < 3; x++) total += BitCount(SpellGame.PatternMask(spell, x, y));
        foreach (SpellGame.Placement piece in _game.Boards[spell].Pieces)
            filled += piece.Card.Shape == SpellGame.Shape.QuarterTriangle ? 1 : piece.Card.Shape == SpellGame.Shape.Triangle ? 2 : piece.Card.Shape == SpellGame.Shape.Square ? 4 : 8;
        int left = Math.Max(0, total - filled);
        if (left % 4 == 0) return $"{left / 4}칸 남음";
        return left < 4 ? $"{left}/4칸 남음" : $"{left / 4}칸 {left % 4}/4 남음";
    }

    /// <summary>도안의 기본 효과와 특수 조각 효과를 계산식과 부가 효과 문구로 만든다.</summary>
    private string BuildFormula(int spell, bool complete, out string detail)
    {
        int flame = 0, guard = 0, resonance = 0, afterimage = 0;
        foreach (SpellGame.Placement piece in _game.Boards[spell].Pieces)
        {
            if (piece.Card.Effect == SpellGame.Effect.Flame) flame++;
            if (piece.Card.Effect == SpellGame.Effect.Guard) guard++;
            if (piece.Card.Effect == SpellGame.Effect.Resonance && spell >= 2) resonance++;
            if (piece.Card.Effect == SpellGame.Effect.Afterimage && !piece.HasPersisted) afterimage++;
        }
        List<string> notes = new List<string>();
        if (resonance > 0) notes.Add($"공명 · 시전 후 조각 {resonance}장 뽑기");
        if (afterimage > 0) notes.Add($"잔상 {afterimage}장 · 시전 후 판에 1회 남음");
        if (!complete) notes.Add("빈 구역을 채우면 시전할 수 있습니다");
        detail = string.Join("    ", notes);
        if (spell == 1)
        {
            int defense = SPELL_BASE[1] + guard * 3;
            return guard > 0 ? $"기본 방어 {SPELL_BASE[1]}  +  수호 {guard * 3}  =  방어 {defense}" : $"방어 {defense}";
        }
        string word = spell == 3 ? "전체 피해" : "피해";
        int damage = _game.GetSpellDamage(spell);
        return flame > 0 ? $"기본 {word} {SPELL_BASE[spell]}  +  불꽃 {flame * 2}  =  {word} {damage}" : $"{word} {damage}";
    }

    /// <summary>적 슬롯을 적 수에 맞춰 배치하고 상태·대상·조준 미리보기를 갱신한다.</summary>
    private void RefreshEnemies()
    {
        SpellEnemyView[] views = _combatScreen.Enemies;
        int count = Mathf.Clamp(_game.Enemies.Count, 1, views.Length);
        bool aiming = _pendingCast >= 0 || _castDragging;
        int castBoard = _pendingCast >= 0 ? _pendingCast : _castDragBoard;
        for (int i = 0; i < views.Length; i++)
        {
            bool show = i < _game.Enemies.Count;
            views[i].gameObject.SetActive(show);
            if (!show) continue;
            SpellGame.Enemy enemy = _game.Enemies[i];
            ((RectTransform)views[i].transform).anchoredPosition = count == 1 ? _enemyPositions[1] : count == 2 ? Vector2.Lerp(_enemyPositions[i], _enemyPositions[i + 1], 0.5f) : _enemyPositions[i];
            views[i].SetWidth(count >= 3 ? ENEMY_NARROW_BAR_WIDTH : ENEMY_BAR_WIDTH);
            bool boss = enemy.MaxHealth >= 60, large = enemy.MaxHealth >= 26;
            Vector2 size = boss ? BOSS_SIZE : large ? LARGE_ENEMY_SIZE : NORMAL_ENEMY_SIZE;
            Color color = _enemyColors[boss ? 2 : large ? 1 : 0];
            string pattern = enemy.Pattern.Count > 1 ? "패턴 " + string.Join(" · ", enemy.Pattern) + " 반복" : "";
            int preview = aiming && i == _hoverEnemy && enemy.Health > 0 ? _game.GetSpellDamage(castBoard) : 0;
            views[i].Bind(enemy.Name, enemy.Health, enemy.MaxHealth, enemy.Intent, pattern, color, size, boss, i == _game.Target, aiming, preview);
        }
    }

    /// <summary>적 클릭으로 조준 중 주문을 시전하거나 다음 공격 대상을 고른다.</summary>
    private void ClickEnemy(int index)
    {
        if (!CanInteract) return;
        if (_pendingCast >= 0) CastSpell(_pendingCast, index);
        else { _game.SelectTarget(index); Render(); }
    }

    /// <summary>손패 카드를 클릭해 선택한다.</summary>
    private void SelectHandCard(SpellGame.Card card)
    {
        if (!CanInteract) return;
        _pendingCast = -1; _selectedCard = card; _selectedPiece = null; _selectedBoard = -1; _rotation = 0;
        Render();
    }

    /// <summary>도안 탭의 더보기 메뉴를 탭 오른쪽 아래에 연다.</summary>
    private void OpenPatternMenu(int spell)
    {
        if (!CanInteract) return;
        Vector3[] corners = new Vector3[4];
        _combatScreen.Tabs[spell].Rect.GetWorldCorners(corners);
        // 탭의 오른쪽 아래 모서리에 메뉴의 오른쪽 위를 맞춘다.
        Vector2 bottomRight = ToLayer(RectTransformUtility.WorldToScreenPoint(null, corners[3]));
        _combatScreen.MoreMenu.Open(new Vector2(bottomRight.x, bottomRight.y - MENU_GAP), () => AbandonPattern(spell));
    }

    /// <summary>도안을 비워 이번 턴 조각은 손패로, 이전 턴 조각은 묘지로 보낸다.</summary>
    private void AbandonPattern(int spell)
    {
        if (!_game.Abandon(spell)) _message = "손패가 가득 차 비울 수 없습니다";
        else { ClearSelection(); _message = ""; }
        Render();
    }

    /// <summary>화면 좌표를 전투 화면 끌기 층의 왼쪽 위 기준 좌표(y 아래가 음수)로 바꾼다.</summary>
    private Vector2 ToLayer(Vector2 screenPosition)
    {
        RectTransform layer = _combatScreen.DragLayer;
        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screenPosition, null, out local);
        return new Vector2(local.x - layer.rect.xMin, local.y - layer.rect.yMax);
    }

    /// <summary>첫 주문까지 현재 가능한 조작을 강조하고 설명과 고스트를 준비한다. 기존 좌표를 쓰며 새 화면 연결은 다음 작업이다.</summary>
    private void RenderOnboarding()
    {
        List<Rect> holes = new List<Rect>(); string step; string message; SpellGame.Card ghostCard = null; int ghostRotation = 0; bool showContinue = false;
        Vector2 start, end;
        int complete = -1;
        foreach (int spell in _game.Spells) if (_game.IsComplete(spell)) { complete = spell; break; }
        if (_pendingCast >= 0)
        {
            int target = 0;
            while (target < _game.Enemies.Count && _game.Enemies[target].Health <= 0) target++;
            float x = 640 - _game.Enemies.Count * 125 + target * 250;
            holes.Add(new Rect(x, 15, 235, 230)); start = end = new Vector2(x + 117, 125);
            step = "target"; message = "이 주문은 선택한 적 하나에게 효과를 적용합니다.\n공격할 적을 선택하세요.";
        }
        else if (!_onboardingPlacementExplained && _game.CraftCount > 0)
        {
            holes.Add(new Rect(30, 570, 190, 30)); start = end = Vector2.zero;
            step = "placements"; showContinue = true;
            message = "한 턴에 기본 3회 조각을 배치할 수 있습니다.\n시전하지 않은 조각은 다음 턴에도 도안에 남습니다.";
        }
        else if (complete >= 0)
        {
            float x = _layout.Origin(complete).x < 3 ? 230 : 900;
            float y = 280 + complete * 32;
            holes.Add(new Rect(x, y, 150, 28)); start = end = new Vector2(x + 75, y + 14);
            step = "cast" + complete; message = "도안이 완성되어 주문이 준비됐습니다.\n시전 버튼을 눌러 주문을 사용하세요.";
        }
        else
        {
            int cardIndex = -1, board = -1, cellX = 0, cellY = 0, rotation = 0;
            if (_game.Hand.Count > 0)
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
                            if (!_layout.IsPlaced(spell)) continue;
                            for (int y = 0; y < 3 && cardIndex < 0; y++) for (int x = 0; x < 3 && cardIndex < 0; x++) for (int r = 0; r < 4; r++)
                                if (_game.CanPlace(spell, candidate, x, y, r)) { cardIndex = i; board = spell; cellX = x; cellY = y; rotation = r; break; }
                            if (cardIndex >= 0) break;
                        }
                    }
            }
            if (cardIndex < 0)
            {
                holes.Add(new Rect(1080, 635, 160, 65)); start = end = new Vector2(1160, 667);
                step = "turn"; message = "턴을 종료하면 적이 공격하고 새 턴에 조각을 뽑습니다.\n주문을 이어서 완성하세요.";
            }
            else
            {
                ghostCard = _game.Hand[cardIndex]; ghostRotation = rotation;
                float cardX = 350 + (7 - _game.Hand.Count) * 25 + cardIndex * 85;
                Vector2Int gridCell = _layout.LocalToGrid(board, new Vector2Int(cellX, cellY));
                float gridX = 390 + gridCell.x * LEGACY_CELL, gridY = 280 + gridCell.y * LEGACY_CELL;
                holes.Add(new Rect(cardX, 565, 81, 115)); holes.Add(new Rect(gridX, gridY, LEGACY_CELL, LEGACY_CELL));
                start = new Vector2(cardX + 40, 622); end = new Vector2(gridX + 42, gridY + 42);
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
            icon.Shape = ghostCard.Shape; icon.color = new Color(0.6f, 1f, 0.85f, 0.75f); icon.raycastTarget = false;
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
        _pendingCast = -1; ClearSelection(); _game.EndTurn(); _message = "";
        int damage = healthBefore - _game.Health;
        _impactTarget = -1; _impactAll = false;
        ShowFeedback($"적 공격  ·  방어 {absorbed}  ·  체력 -{damage}", _dangerColor);
        Render();
    }

    /// <summary>완성된 주문의 시전을 시작하고 단일 대상 주문은 적 선택을 기다린다.</summary>
    private void BeginCast(int board)
    {
        if (!_game.IsComplete(board)) { _message = "도안을 먼저 완성하세요"; Render(); return; }
        ClearSelection();
        if (board == 0 || board == 2)
        {
            int count = 0, onlyTarget = -1;
            for (int i = 0; i < _game.Enemies.Count; i++) if (_game.Enemies[i].Health > 0) { count++; onlyTarget = i; }
            if (count == 1) CastSpell(board, onlyTarget);
            else { _pendingCast = board; Render(); _hintText = "적을 클릭해 시전 · 우클릭·Esc 취소"; }
        }
        else CastSpell(board, _game.Target);
    }

    /// <summary>완성된 도안과 선택한 적으로 주문을 시전하고 결과를 표시한다.</summary>
    private void CastSpell(int board, int target)
    {
        string result = $"{SPELL_NAMES[board]} 시전  ·  {Preview(_game.Boards[board])}";
        if (!_game.Cast(board, target)) { _message = "살아 있는 대상을 선택하세요"; Render(); return; }
        _onboardingComplete = true;
        _pendingCast = -1; ClearSelection(); _impactAll = board == 3; _impactTarget = board == 1 ? -1 : target;
        _message = ""; ShowFeedback(result, PatternColor(board)); Render();
    }

    /// <summary>완성된 도안 버튼 클릭으로 해당 주문의 시전을 시작한다.</summary>
    public void ClickCast(int board)
    {
        if (CanInteract) BeginCast(board);
    }

    /// <summary>손패의 조각 설명을 마우스 위치 근처에 표시하거나 지운다.</summary>
    public void ShowPieceTooltip(SpellGame.Card card, Vector2 screenPosition)
    {
        ShowTextTooltip(card == null ? null : CardName(card), screenPosition);
    }

    /// <summary>포인터가 가리킨 도안의 주문 효과와 완성 상태를 표시한다.</summary>
    public void ShowPatternTooltip(int spell, Vector2 screenPosition, SpellGame.Card card = null)
    {
        if (spell < 0) { ShowTextTooltip(null, screenPosition); return; }
        bool complete = _game.IsComplete(spell);
        string detail;
        string formula = BuildFormula(spell, complete, out detail);
        ShowTextTooltip($"{SPELL_NAMES[spell]} · {(complete ? "완성" : RemainingText(spell))}\n{formula}" + (string.IsNullOrEmpty(detail) ? "" : "\n" + detail) + (card == null ? "" : "\n" + CardName(card)), screenPosition);
    }

    /// <summary>지정한 설명을 화면 좌표 근처에 표시하거나 지운다. 드래그·조준 중에는 표시하지 않는다.</summary>
    public void ShowTextTooltip(string message, Vector2 screenPosition)
    {
        if (message == null || !CanInteract || _dragGhost != null || _pendingCast >= 0 || _castDragging || _boardGraphic.IsDraggingPattern) { _combatScreen.Tooltip.Hide(); return; }
        _combatScreen.Tooltip.Show(message, screenPosition);
    }

    /// <summary>단일 대상 주문을 놓은 화면 좌표의 살아 있는 적에게 시전한다.</summary>
    public void DropCast(int board, PointerEventData eventData)
    {
        HideAim();
        bool wasDragging = _castDragging;
        _castDragging = false; _hoverEnemy = -1;
        if (!CanInteract || (board != 0 && board != 2) || !wasDragging) return;
        int target = EnemyAt(eventData.position, eventData.pressEventCamera);
        if (target >= 0) { CastSpell(board, target); return; }
        _pendingCast = -1;
        _message = "살아 있는 적 위에 놓아 주세요";
        Render();
    }

    /// <summary>화면 좌표 아래의 살아 있는 적 번호를 반환한다. 없으면 -1이다.</summary>
    private int EnemyAt(Vector2 screenPosition, Camera eventCamera)
    {
        for (int i = 0; i < _game.Enemies.Count && i < _combatScreen.Enemies.Length; i++)
            if (_game.Enemies[i].Health > 0 && _combatScreen.Enemies[i].Contains(screenPosition, eventCamera)) return i;
        return -1;
    }

    /// <summary>단일 대상 주문 드래그의 조준 상태를 시작한다.</summary>
    public void BeginCastDrag(int board)
    {
        if (!CanInteract || (board != 0 && board != 2)) return;
        _combatScreen.Tooltip.Hide();
        ClearSelection();
        _castDragging = true; _castDragBoard = board; _hoverEnemy = -1;
        _hintText = "살아 있는 적에게 놓아 시전하세요";
        RefreshPatternControls(); RefreshPreviewBar(); RefreshEnemies();
    }

    /// <summary>시전 드래그 중 조준선을 그리고 포인터 아래 적의 예상 피해를 갱신한다.</summary>
    public void PreviewCast(int board, PointerEventData eventData)
    {
        if (!CanInteract || (board != 0 && board != 2)) return;
        DrawAim(board, eventData.position);
        UpdateHoverEnemy(board, EnemyAt(eventData.position, eventData.pressEventCamera));
    }

    /// <summary>시전 버튼 중심에서 포인터까지 직선 조준선을 그린다.</summary>
    private void DrawAim(int board, Vector2 pointer)
    {
        Vector3[] corners = new Vector3[4];
        _combatScreen.CastButtons[board].Rect.GetWorldCorners(corners);
        Vector2 start = ToLayer(RectTransformUtility.WorldToScreenPoint(null, (corners[0] + corners[2]) / 2));
        Vector2 end = ToLayer(pointer);
        SetSegment(_combatScreen.AimLine, start, end);
        if (_aimElbow != null) _aimElbow.gameObject.SetActive(false);
    }

    /// <summary>선분 이미지를 두 점 사이에 놓는다. 좌표는 끌기 층의 왼쪽 위 기준이다.</summary>
    private static void SetSegment(RectTransform line, Vector2 from, Vector2 to)
    {
        Vector2 difference = to - from;
        line.gameObject.SetActive(difference.sqrMagnitude > 1f);
        line.anchoredPosition = from;
        line.sizeDelta = new Vector2(difference.magnitude, line.sizeDelta.y);
        line.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(difference.y, difference.x) * Mathf.Rad2Deg);
    }

    /// <summary>조준선을 숨긴다.</summary>
    private void HideAim()
    {
        _combatScreen.AimLine.gameObject.SetActive(false);
        if (_aimElbow != null) _aimElbow.gameObject.SetActive(false);
    }

    /// <summary>가리킨 적이 바뀌면 예상 피해 표시와 안내 문구를 갱신한다.</summary>
    private void UpdateHoverEnemy(int board, int target)
    {
        if (target >= 0)
        {
            SpellGame.Enemy enemy = _game.Enemies[target];
            int damage = _game.GetSpellDamage(board);
            _hintText = $"{enemy.Name} · 피해 {damage} · 체력 {enemy.Health} → {Math.Max(0, enemy.Health - damage)}";
        }
        else _hintText = _castDragging ? "살아 있는 적에게 놓아 시전하세요" : "적을 클릭해 시전 · 우클릭·Esc 취소";
        if (target == _hoverEnemy) return;
        _hoverEnemy = target;
        RefreshEnemies();
    }

    /// <summary>클릭 시전의 대상 선택 중 가리킨 적의 예상 피해를 표시한다.</summary>
    public void PreviewPendingCast(PointerEventData eventData)
    {
        if (_pendingCast >= 0) UpdateHoverEnemy(_pendingCast, EnemyAt(eventData.position, eventData.enterEventCamera));
    }

    /// <summary>대상 포인터가 벗어나면 예상 피해 표시를 지운다.</summary>
    public void ClearPendingPreview()
    {
        if (_pendingCast >= 0 && !_castDragging) UpdateHoverEnemy(_pendingCast, -1);
    }

    /// <summary>새 게임에서 도안 배치를 기본 위치로 되돌린다.</summary>
    private void ResetPatterns()
    {
        _feedbackUntil = 0;
        _showHelp = false;
        _showPauseMenu = false;
        _onboardingComplete = false;
        _onboardingPlacementExplained = true;
        _onboardingStep = ""; _onboardingActivity = Time.unscaledTime;
        _pendingCast = -1;
        _layout.Reset();
    }

    /// <summary>새로 얻은 도안을 빈 공간에 놓고 공간이 없으면 미배치 상태로 둔다.</summary>
    private void EnsurePatterns()
    {
        _layout.EnsurePlaced(_game.Spells);
        if (_game.CraftCount != 0) return;
        foreach (int spell in _game.Spells)
        {
            if (_layout.IsPlaced(spell)) continue;
            _layout.Reset(); _layout.EnsurePlaced(_game.Spells); break;
        }
    }

    /// <summary>도안을 새 위치·회전으로 옮기고 결과를 화면에 반영한다. 불가하면 원래 위치·방향을 유지하고 이유를 표시한다.</summary>
    public void MovePattern(int spell, Vector2Int origin, int rotation)
    {
        if (!CanMovePattern(spell)) { _message = "고정된 조각이 있는 도안은 이동할 수 없습니다"; Render(); return; }
        string issue = _layout.MoveIssue(spell, origin, rotation, _game.Spells);
        if (issue == null) { _layout.Place(spell, origin, rotation); _message = ""; }
        else _message = issue + " · 원래 자리로 돌아갑니다";
        Render();
    }

    /// <summary>도안에 고정된 이전 턴 조각이나 잔상이 없는지 확인한다.</summary>
    public bool CanMovePattern(int spell)
    {
        foreach (SpellGame.Placement piece in _game.Boards[spell].Pieces) if (!_game.CanMovePiece(piece)) return false;
        return true;
    }

    /// <summary>도안별 외곽선을 구분할 색을 반환한다.</summary>
    public Color PatternColor(int spell)
    {
        return _patternColors[spell];
    }

    /// <summary>특수 효과의 표시 색을 반환한다. 일반 조각은 글자색을 반환한다.</summary>
    private Color EffectColor(SpellGame.Effect effect)
    {
        switch (effect)
        {
            case SpellGame.Effect.Flame: return _flameColor;
            case SpellGame.Effect.Guard: return _guardColor;
            case SpellGame.Effect.Resonance: return _resonanceColor;
            case SpellGame.Effect.Afterimage: return _afterimageColor;
            default: return _textColor;
        }
    }

    /// <summary>판의 기본 효과와 현재 특수 조각의 효과 합을 반환한다.</summary>
    private string Preview(SpellGame.Board board, SpellGame.Card additional = null)
    {
        int flame = 0, guard = 0, extraPlacements = 0, afterimages = 0;
        foreach (SpellGame.Placement p in board.Pieces) { if (p.Card.Effect == SpellGame.Effect.Flame) flame += 2; if (p.Card.Effect == SpellGame.Effect.Guard) guard += 3; if (p.Card.Effect == SpellGame.Effect.Resonance && board.Spell >= 2) extraPlacements++; if (p.Card.Effect == SpellGame.Effect.Afterimage && !p.HasPersisted) afterimages++; }
        if (additional != null)
        {
            if (additional.Effect == SpellGame.Effect.Flame) flame += 2;
            if (additional.Effect == SpellGame.Effect.Guard) guard += 3;
            if (additional.Effect == SpellGame.Effect.Resonance && board.Spell >= 2) extraPlacements++;
            if (additional.Effect == SpellGame.Effect.Afterimage && !additional.HasPersisted) afterimages++;
        }
        return SPELL_EFFECTS[board.Spell] + (flame > 0 && board.Spell != 1 ? $" +{flame} 피해" : "") + (guard > 0 ? $" +{guard} 방어" : "") + (extraPlacements > 0 ? $" · 조각 {extraPlacements}장 뽑기" : "") + (afterimages > 0 ? $" · 잔상 {afterimages}장 유지" : "");
    }

    /// <summary>도형 이름을 반환한다.</summary>
    private static string ShapeName(SpellGame.Shape shape)
    {
        return shape == SpellGame.Shape.Square ? "정사각형" : shape == SpellGame.Shape.Triangle ? "반칸 삼각형" : shape == SpellGame.Shape.QuarterTriangle ? "1/4 삼각형" : "직사각형";
    }

    /// <summary>카드의 도형과 특수 효과를 표시하는 이름을 반환한다.</summary>
    private string CardName(SpellGame.Card card)
    {
        string effect = card.Effect == SpellGame.Effect.Flame ? "불꽃 · 공격 피해 +2" : card.Effect == SpellGame.Effect.Guard ? "수호 · 방어 +3" : card.Effect == SpellGame.Effect.Resonance ? "공명 · 3칸 이상 조각 1장 뽑기" : card.Effect == SpellGame.Effect.Afterimage ? card.HasPersisted ? "잔상 · 다음 시전 후 버림" : "잔상 · 시전 후 1회 유지" : "일반 · 추가 효과 없음";
        return ShapeName(card.Shape) + "\n" + effect;
    }

    /// <summary>손패 카드에 쓸 짧은 이름을 반환한다. 특수 조각은 효과 이름을 앞에 붙인다.</summary>
    private static string DisplayName(SpellGame.Card card)
    {
        string shape = card.Shape == SpellGame.Shape.Triangle && card.Effect != SpellGame.Effect.None ? "삼각형" : ShapeName(card.Shape);
        switch (card.Effect)
        {
            case SpellGame.Effect.Flame: return "불꽃 " + shape;
            case SpellGame.Effect.Guard: return "수호 " + shape;
            case SpellGame.Effect.Resonance: return "공명 " + shape;
            case SpellGame.Effect.Afterimage: return "잔상 " + shape;
            default: return shape;
        }
    }

    /// <summary>손패 카드 아래 칩에 쓸 특수 효과 문구를 반환한다.</summary>
    private static string EffectText(SpellGame.Card card)
    {
        switch (card.Effect)
        {
            case SpellGame.Effect.Flame: return "공격 피해 +2";
            case SpellGame.Effect.Guard: return "방어 +3";
            case SpellGame.Effect.Resonance: return "3칸 이상 조각 1장 뽑기";
            case SpellGame.Effect.Afterimage: return card.HasPersisted ? "다음 시전 후 버림" : "시전 후 1회 유지";
            default: return "";
        }
    }

    /// <summary>미보유 도안이 있으면 혼합 후보 세 개, 없으면 특수 카드 네 종류를 보상 화면에 표시한다. 후보는 보상 단계에 처음 들어올 때만 뽑는다.</summary>
    private void RefreshRewards()
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
        SpellRewardCardView[] cards = _rewardScreen.Cards;
        _rewardScreen.ShowCards(Mathf.Min(_rewards.Count, cards.Length));
        for (int i = 0; i < _rewards.Count && i < cards.Length; i++)
        {
            int reward = _rewards[i];
            if (reward >= 4) cards[i].BindPattern(reward, REWARD_NAMES[reward], PATTERN_CELLS[reward - 2], PatternColor(reward - 2), ChooseReward);
            else
            {
                SpellGame.Effect effect = reward == 0 ? SpellGame.Effect.Flame : reward == 1 ? SpellGame.Effect.Guard : reward == 2 ? SpellGame.Effect.Resonance : SpellGame.Effect.Afterimage;
                SpellGame.Shape shape = reward == 0 ? SpellGame.Shape.Triangle : reward == 1 ? SpellGame.Shape.Square : reward == 2 ? SpellGame.Shape.Rectangle : SpellGame.Shape.QuarterTriangle;
                cards[i].BindPiece(reward, REWARD_NAMES[reward], shape, EffectColor(effect), ChooseReward);
            }
        }
    }

    /// <summary>배치한 조각을 이동 또는 회수 대상으로 선택한다.</summary>
    public void SelectPiece(int board, SpellGame.Placement piece)
    {
        if (!_game.CanMovePiece(piece)) { _message = "이전 턴 조각과 시전 후 남은 잔상은 이동할 수 없습니다"; Render(); return; }
        _pendingCast = -1; _selectedPiece = piece; _selectedCard = piece.Card; _selectedBoard = board; _message = ""; SetNotice("판을 클릭해 이동하거나 손패로 드래그하세요", _textColor);
        Vector2Int anchor;
        _layout.PieceToGrid(board, piece, out anchor, out _rotation);
        Render();
    }

    /// <summary>격자 앵커 칸과 현재 화면 회전(_rotation)을 지정 도안의 기준 칸·회전으로 바꾼다.</summary>
    private void ToLocalPlacement(int board, Vector2Int anchor, out int x, out int y, out int rotation)
    {
        Vector2Int local;
        _layout.GridToLocalPlacement(board, _selectedCard.Shape, anchor, _rotation, out local, out rotation);
        x = local.x; y = local.y;
    }

    /// <summary>격자 앵커 칸에 선택한 카드를 놓거나 기존 조각을 이동한다.</summary>
    public void ClickGrid(int board, Vector2Int anchor)
    {
        if (_selectedCard == null) { _message = "먼저 손패 또는 배치된 조각을 선택하세요"; Render(); return; }
        int x, y, rotation;
        ToLocalPlacement(board, anchor, out x, out y, out rotation);
        SpellGame.Placement ignored = _selectedBoard == board ? _selectedPiece : null;
        bool completes = CanPreviewGrid(board, anchor) && _game.WouldComplete(board, _selectedCard, x, y, rotation, ignored);
        bool success = _selectedPiece == null ? _game.Place(board, _selectedCard, x, y, rotation) : _game.Move(_selectedBoard, _selectedPiece, board, x, y, rotation);
        if (success) { ClearSelection(); _message = ""; if (completes) SetNotice($"{SPELL_NAMES[board]} 완성 · 시전 버튼을 누르세요", PatternColor(board)); } else _message = PlacementIssue(board, anchor) ?? "이 위치에는 놓을 수 없습니다";
        Render();
    }

    /// <summary>선택 카드를 격자 앵커 칸에 현재 화면 회전으로 놓을 수 있는지 반환한다.</summary>
    public bool CanPreviewGrid(int board, Vector2Int anchor)
    {
        if (_selectedCard == null) return false;
        int x, y, rotation;
        ToLocalPlacement(board, anchor, out x, out y, out rotation);
        return _game.CanPlace(board, _selectedCard, x, y, rotation, _selectedBoard == board ? _selectedPiece : null);
    }

    /// <summary>현재 선택 조각을 격자 앵커 칸에 놓을 수 없는 이유를 반환한다.</summary>
    private string PlacementIssue(int board, Vector2Int anchor)
    {
        if (_selectedCard == null) return "먼저 조각을 선택하세요";
        int x, y, rotation;
        ToLocalPlacement(board, anchor, out x, out y, out rotation);
        return _game.PlacementIssue(board, _selectedCard, x, y, rotation, _selectedBoard == board ? _selectedPiece : null);
    }

    /// <summary>포인터가 가리킨 격자 앵커 칸의 배치 가능 상태와 도안 완성 여부를 커서 안내로 쓴다. board가 음수면 기본 안내를 쓴다.</summary>
    public void SetPreviewHint(int board, Vector2Int anchor)
    {
        string hint = "R / 우클릭 회전  ·  손패에 놓으면 취소";
        if (board >= 0 && _selectedCard != null)
        {
            int x, y, rotation;
            ToLocalPlacement(board, anchor, out x, out y, out rotation);
            bool valid = CanPreviewGrid(board, anchor);
            bool complete = valid && _game.WouldComplete(board, _selectedCard, x, y, rotation, _selectedBoard == board ? _selectedPiece : null);
            SpellGame.Card additional = _selectedBoard == board && _selectedPiece != null ? null : _selectedCard;
            hint = complete ? $"놓으면 도안 완성  ·  {Preview(_game.Boards[board], additional)}" : valid ? $"배치 가능  ·  예상 {Preview(_game.Boards[board], additional)}" : PlacementIssue(board, anchor);
        }
        _hintText = hint;
    }

    /// <summary>경고가 아닌 진행 안내를 다음 화면 갱신 때 지정 강조색의 토스트로 표시하도록 예약한다.</summary>
    private void SetNotice(string notice, Color color)
    {
        _notice = notice; _noticeColor = color;
    }

    /// <summary>도안을 들고 있을 때 놓을 수 있는지와 불가 이유를 커서 안내로 쓴다.</summary>
    public void SetPatternHint(string issue)
    {
        _hintText = issue == null ? "놓을 수 있습니다 · R / 우클릭 회전" : issue;
    }

    /// <summary>선택한 조각이 도안 바깥에 놓일 수 없음을 커서 안내로 쓴다.</summary>
    public void SetOutsidePreviewHint()
    {
        if (_selectedCard != null) _hintText = "도안의 색 테두리 안에 놓아 주세요";
    }

    /// <summary>손패 카드 드래그를 시작하고 포인터를 따라갈 도형을 표시한다.</summary>
    public void BeginCardDrag(SpellGame.Card card, Vector2 position)
    {
        ShowPieceTooltip(null, position);
        _pendingCast = -1;
        if (_selectedCard != card) _rotation = 0;
        _selectedCard = card; _selectedPiece = null; _selectedBoard = -1;
        ShowDragGhost(card, position);
    }

    /// <summary>격자의 배치 조각을 선택하고 드래그 표시를 시작한다.</summary>
    public void BeginPieceDrag(int board, SpellGame.Placement piece, Vector2 position)
    {
        if (!_game.CanMovePiece(piece)) { _message = "이전 턴 조각과 시전 후 남은 잔상은 이동할 수 없습니다"; Render(); return; }
        _pendingCast = -1; _selectedCard = piece.Card; _selectedPiece = piece; _selectedBoard = board;
        Vector2Int anchor;
        _layout.PieceToGrid(board, piece, out anchor, out _rotation);
        ShowDragGhost(piece.Card, position);
    }

    /// <summary>미리 만들어 둔 조각 고스트를 카드 모양·회전으로 켠다.</summary>
    private void ShowDragGhost(SpellGame.Card card, Vector2 position)
    {
        RectTransform ghost = _combatScreen.PieceGhost;
        float cell = _boardGraphic.CellSize;
        ghost.sizeDelta = card.Shape == SpellGame.Shape.Rectangle ? new Vector2(2 * cell, cell) : new Vector2(cell, cell);
        ghost.localRotation = Quaternion.Euler(0, 0, -90 * _rotation);
        _combatScreen.PieceGhostShape.Shape = card.Shape;
        Color color = card.Effect == SpellGame.Effect.None ? Color.white : EffectColor(card.Effect);
        _combatScreen.PieceGhostShape.color = new Color(color.r, color.g, color.b, 0.85f);
        ghost.gameObject.SetActive(true);
        _dragGhost = ghost;
        MoveCardDrag(position);
    }

    /// <summary>조각 고스트를 숨긴다.</summary>
    private void HideDragGhost()
    {
        if (_dragGhost != null) _dragGhost.gameObject.SetActive(false);
        _dragGhost = null;
    }

    /// <summary>배치 조각을 손패로 회수하거나 유효한 격자 위치로 이동한다.</summary>
    public void EndPieceDrag(PointerEventData eventData, bool hasDestination, int board, Vector2Int anchor)
    {
        if (_selectedPiece == null || !CanInteract) return;
        HideDragGhost();
        bool onHand = RectTransformUtility.RectangleContainsScreenPoint(_combatScreen.HandDropArea, eventData.position, eventData.pressEventCamera);
        if (onHand)
        {
            if (_game.Recall(_selectedBoard, _selectedPiece)) { ClearSelection(); _message = ""; SetNotice("조각을 손패로 되돌렸습니다", _textColor); }
            else { ClearSelection(); _message = "손패가 가득 차 회수할 수 없습니다"; }
            Render();
        }
        else if (hasDestination) ClickGrid(board, anchor);
        else { ClearSelection(); _message = "도안이나 손패 위에 놓아 주세요"; Render(); }
    }

    /// <summary>드래그 중인 도형을 화면 좌표로 이동한다.</summary>
    public void MoveCardDrag(Vector2 position)
    {
        if (_dragGhost != null) _dragGhost.anchoredPosition = ToLayer(position);
    }

    /// <summary>드래그 중인 카드를 격자 위에 미리 표시한다.</summary>
    public void PreviewCardDrag(PointerEventData eventData)
    {
        _boardGraphic.PreviewCard(eventData);
    }

    /// <summary>드롭 지점이 제작판이면 배치하고, 아니면 선택을 유지한다.</summary>
    public void EndCardDrag(PointerEventData eventData)
    {
        if (_selectedCard == null || !CanInteract) return;
        HideDragGhost();
        if (_boardGraphic.TryDrop(eventData)) return;
        _message = "도안의 색 테두리 안에 놓아 주세요"; Render();
    }

    /// <summary>도안 이동 핸들에서 드래그를 시작한다. 핸들은 도안의 왼쪽 위 칸을 잡은 것으로 본다.</summary>
    public void BeginPatternDrag(PointerEventData eventData, int pattern)
    {
        if (!CanInteract) return;
        ShowTextTooltip(null, eventData.position);
        _boardGraphic.BeginPatternDrag(pattern, Vector2Int.zero);
        _boardGraphic.PreviewPattern(eventData);
    }

    /// <summary>이동 핸들로 끄는 도안의 고스트와 스냅 미리 보기를 갱신한다.</summary>
    public void PreviewPatternDrag(PointerEventData eventData)
    {
        if (CanInteract) _boardGraphic.PreviewPattern(eventData);
    }

    /// <summary>이동 핸들로 끈 도안을 놓아 위치·회전을 확정하거나 원래대로 둔다.</summary>
    public void EndPatternDrag(PointerEventData eventData)
    {
        if (CanInteract) _boardGraphic.EndPatternDrag(eventData);
        else _boardGraphic.CancelDrag();
    }
}
