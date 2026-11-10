using System;
using System.Collections.Generic;

using UnityEngine;

using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

/// <summary>주문 조각 PoC의 화면과 입력을 전투 상태에 연결한다.</summary>
public sealed class SpellGameUI : MonoBehaviour
{
    private static readonly string[] SPELL_NAMES = { "불씨", "방벽", "번개", "유성" };
    private static readonly string[] SPELL_EFFECTS = { "단일 피해 6", "방어 7", "단일 피해 12", "전체 피해 10" };
    private static readonly string[] REWARD_NAMES = { "불꽃 삼각형\n공격 피해 +2", "수호 정사각형\n방어 +3", "공명 직사각형\n3칸 이상: 배치 +1", "잔상 1/4 삼각형\n시전 후 1회 유지", "도안: 번개\n단일 피해 12", "도안: 유성\n전체 피해 10" };
    private static readonly Color[] PATTERN_COLORS = { new Color(1f, 0.75f, 0.3f), new Color(0.4f, 0.8f, 1f), new Color(0.8f, 0.65f, 1f), new Color(1f, 0.5f, 0.48f) };
    private static readonly IReadOnlyList<Vector2Int>[] PATTERN_CELLS = CreatePatternCells();
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
        if ((Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) || (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame))
        {
            if (_selectedCard != null)
            {
                _rotation = (_rotation + 1) % 4;
                if (_dragGhost == null) Render();
                else
                {
                    _dragGhost.localRotation = Quaternion.Euler(0, 0, -90 * _rotation);
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
        button.onClick.AddListener(() => action()); Label(rect, caption, 4, 2, width - 8, height - 4, fontSize);
    }

    /// <summary>현재 게임 단계에 맞춰 화면 요소를 갱신한다.</summary>
    private void Render()
    {
        if (_pendingCast >= 0 && (_game.CurrentPhase != SpellGame.Phase.Combat || !_game.IsComplete(_pendingCast))) _pendingCast = -1;
        if (_boardGraphic != null) _boardGraphic.CancelDrag();
        if (_dragGhost != null) ClearSelection();
        _dragGhost = null; _boardGraphic = null; _previewHint = null;
        _onboardingGhost = null; _onboardingGhostGroup = null; _onboardingGhostIcon = null;
        _impactImages.Clear();
        foreach (Transform child in _root) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        Panel(_root, 0, 0, 1280, 720, new Color(0.095f, 0.12f, 0.16f));
        if (_game.CurrentPhase == SpellGame.Phase.Title)
        {
            Label(_root, "주문 조각", 340, 170, 600, 90, 48);
            RectTransform divider = Panel(_root, 500, 322, 280, 2, new Color(0.80f, 0.65f, 0.38f));
            divider.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            Button(_root, "시작", 520, 360, 240, 64, () => { _game.StartGame(); ResetPatterns(); ClearSelection(); Render(); });
            Label(_root, "도형 드래그 → 도안 완성 → 시전 버튼\nR 또는 우클릭으로 회전", 300, 502, 680, 65, 17);
        }
        else if (_game.CurrentPhase == SpellGame.Phase.Reward) RenderRewards();
        else if (_game.CurrentPhase == SpellGame.Phase.Victory || _game.CurrentPhase == SpellGame.Phase.Defeat)
        {
            Label(_root, _game.CurrentPhase == SpellGame.Phase.Victory ? "승리" : "패배", 340, 190, 600, 90, 48);
            Button(_root, "다시 시작", 520, 350, 240, 64, () => { _game.StartGame(); ResetPatterns(); ClearSelection(); Render(); });
        }
        else RenderCombat();
        RenderFeedback();
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
        Label(_root, $"뽑기 {_game.DrawCount}   버림 {_game.DiscardCount}   제작 중 {_game.CraftCount}     {_message}", 30, 486, 1000, 35, 18, TextAnchor.MiddleLeft);
        for (int i = 0; i < _game.Hand.Count; i++)
        {
            SpellGame.Card card = _game.Hand[i];
            RectTransform cardPanel = Panel(_root, 30 + i * 148, 525, 144, 126, CardColor(card, card == _selectedCard));
            UnityEngine.UI.Button cardButton = cardPanel.gameObject.AddComponent<UnityEngine.UI.Button>();
            cardButton.onClick.AddListener(() => { _pendingCast = -1; _selectedCard = card; _selectedPiece = null; _selectedBoard = -1; _rotation = 0; _message = "R 또는 우클릭: 회전"; Render(); });
            cardPanel.gameObject.AddComponent<SpellCardDrag>().Setup(this, card);
            RectTransform iconRect = Element("Shape", cardPanel, card.Shape == SpellGame.Shape.Rectangle ? 0 : 36, 4, card.Shape == SpellGame.Shape.Rectangle ? 144 : 72, 72);
            iconRect.gameObject.AddComponent<CanvasRenderer>();
            SpellShapeGraphic icon = iconRect.gameObject.AddComponent<SpellShapeGraphic>();
            icon.raycastTarget = false; icon.Shape = card.Shape;
            Label(cardPanel, CardName(card), 2, 78, 140, 46, 14);
        }
        int incoming = 0;
        foreach (SpellGame.Enemy enemy in _game.Enemies) if (enemy.Health > 0) incoming += enemy.Intent;
        int expectedDamage = Math.Max(0, incoming - _game.Block);
        Label(_root, $"예상 피해 {expectedDamage}\n공격 {incoming} − 방어 {_game.Block}", 1055, 480, 200, 58, 15);
        Button(_root, "턴 종료", 1080, 550, 160, 65, EndPlayerTurn, expectedDamage >= _game.Health ? new Color(0.59f, 0.27f, 0.27f) : new Color(0.32f, 0.40f, 0.49f));
        Button(_root, "도움말 ?", 1100, 660, 140, 35, () => { _showHelp = true; Render(); }, null, 16);
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
        foreach (int spell in _game.Spells) if (_game.IsComplete(spell)) { complete = spell; break; }
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
        ShowFeedback($"적 공격  ·  방어 {absorbed}  ·  체력 -{damage}", new Color(0.91f, 0.48f, 0.40f));
        Render();
    }

    /// <summary>완성된 주문의 시전을 시작하고 단일 대상 주문은 적 선택을 기다린다.</summary>
    private void BeginCast(int board)
    {
        if (!_game.IsComplete(board)) { _message = "도안을 먼저 완성하세요"; Render(); return; }
        ClearSelection();
        if (board == 0 || board == 2) { _pendingCast = board; _message = $"{SPELL_NAMES[board]}의 대상을 선택하세요 · Esc 메뉴"; Render(); }
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
            Label(handle, $"↕ {SPELL_NAMES[i]}: {Preview(_game.Boards[i])}{(_game.IsComplete(i) ? " · 완성" : _patternPlaced[i] ? "" : " · 배치 필요")}", 16, 0, 344, 44, 15, TextAnchor.MiddleLeft);
            Button(panel, "포기", 930, 20 + i * 53, 85, 44, () => { if (!_game.Abandon(index)) _message = "손패가 가득 차 포기할 수 없습니다"; else { ClearSelection(); _message = ""; } Render(); }, null, 14);
            Button(panel, _game.IsComplete(i) ? "시전" : "미완성", 1025, 20 + i * 53, 95, 44, () => BeginCast(index), _game.IsComplete(i) ? PatternColor(i) * 0.65f : new Color(0.2f, 0.24f, 0.28f), 14);
        }
        RectTransform grid = Element("Grid", panel, 60, 48, 432, 216);
        grid.gameObject.AddComponent<CanvasRenderer>();
        SpellBoardGraphic graphic = grid.gameObject.AddComponent<SpellBoardGraphic>();
        graphic.Setup(this, _game);
        _boardGraphic = graphic;
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
        return PATTERN_COLORS[spell];
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
        return SPELL_EFFECTS[board.Spell] + (flame > 0 && board.Spell != 1 ? $" +{flame} 피해" : "") + (guard > 0 ? $" +{guard} 방어" : "") + (extraPlacements > 0 ? $" · 배치 +{extraPlacements}" : "") + (afterimages > 0 ? $" · 잔상 {afterimages}장 유지" : "");
    }

    /// <summary>카드의 도형과 특수 효과를 표시하는 이름을 반환한다.</summary>
    private string CardName(SpellGame.Card card)
    {
        string shape = card.Shape == SpellGame.Shape.Square ? "정사각형" : card.Shape == SpellGame.Shape.Triangle ? "반칸 삼각형" : card.Shape == SpellGame.Shape.QuarterTriangle ? "1/4 삼각형" : "직사각형";
        string effect = card.Effect == SpellGame.Effect.Flame ? "불꽃 · 공격 피해 +2" : card.Effect == SpellGame.Effect.Guard ? "수호 · 방어 +3" : card.Effect == SpellGame.Effect.Resonance ? "공명 · 3칸 이상 배치 +1" : card.Effect == SpellGame.Effect.Afterimage ? card.HasPersisted ? "잔상 · 다음 시전 후 버림" : "잔상 · 시전 후 1회 유지" : "일반 · 추가 효과 없음";
        return shape + "\n" + effect;
    }

    /// <summary>카드 효과와 선택 여부를 나타내는 색을 반환한다.</summary>
    private Color CardColor(SpellGame.Card card, bool selected)
    {
        if (selected) return new Color(0.65f, 0.57f, 0.3f);
        return card.Effect == SpellGame.Effect.Flame ? new Color(0.65f, 0.31f, 0.16f) : card.Effect == SpellGame.Effect.Guard ? new Color(0.18f, 0.37f, 0.56f) : card.Effect == SpellGame.Effect.Resonance ? new Color(0.42f, 0.28f, 0.54f) : card.Effect == SpellGame.Effect.Afterimage ? new Color(0.18f, 0.48f, 0.46f) : new Color(0.39f, 0.43f, 0.47f);
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
        _pendingCast = -1; _selectedPiece = piece; _selectedCard = piece.Card; _selectedBoard = board; _rotation = piece.Rotation; _message = "판을 클릭해 이동하거나 손패로 드래그하세요";
        Render();
    }
    /// <summary>격자 위치에 선택한 카드를 놓거나 기존 조각을 이동한다.</summary>
    public void ClickGrid(int board, int x, int y)
    {
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
        _pendingCast = -1;
        if (_selectedCard != card) _rotation = 0;
        _selectedCard = card; _selectedPiece = null; _selectedBoard = -1;
        CreateDragGhost(card, position);
    }

    /// <summary>격자의 배치 조각을 선택하고 드래그 표시를 시작한다.</summary>
    public void BeginPieceDrag(int board, SpellGame.Placement piece, Vector2 position)
    {
        _pendingCast = -1; _selectedCard = piece.Card; _selectedPiece = piece; _selectedBoard = board; _rotation = piece.Rotation;
        CreateDragGhost(piece.Card, position);
    }

    /// <summary>지정 조각의 포인터 추적용 모양을 생성한다.</summary>
    private void CreateDragGhost(SpellGame.Card card, Vector2 position)
    {
        float width = card.Shape == SpellGame.Shape.Rectangle ? 144 : 72;
        _dragGhost = Element("Drag Preview", _root, 0, 0, width, 72);
        _dragGhost.pivot = new Vector2(0.5f, 0.5f);
        _dragGhost.localRotation = Quaternion.Euler(0, 0, -90 * _rotation);
        _dragGhost.gameObject.AddComponent<CanvasRenderer>();
        SpellShapeGraphic icon = _dragGhost.gameObject.AddComponent<SpellShapeGraphic>();
        icon.raycastTarget = false; icon.Shape = card.Shape;
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
        if (_boardGraphic != null) _boardGraphic.PreviewCard(eventData);
    }

    /// <summary>드롭 지점이 제작판이면 배치하고, 아니면 선택을 유지한다.</summary>
    public void EndCardDrag(PointerEventData eventData)
    {
        if (_selectedCard == null || !CanInteract) return;
        if (_dragGhost != null) Destroy(_dragGhost.gameObject);
        _dragGhost = null;
        if (_boardGraphic.TryDrop(eventData)) return;
        _message = "도안의 색 테두리 안에 놓아 주세요"; Render();
    }

    /// <summary>현재 선택한 카드와 회전을 반환한다.</summary>
    public SpellGame.Card SelectedCard { get { return _selectedCard; } }
    public int Rotation { get { return _rotation; } }
    public bool CanInteract { get { return isActiveAndEnabled && !_showPauseMenu && !_showHelp && _game.CurrentPhase == SpellGame.Phase.Combat; } }

    /// <summary>목록에서 이동하는 도안의 미리 보기를 현재 제작판에 전달한다.</summary>
    public void PreviewPatternDrag(PointerEventData eventData, int pattern)
    {
        if (CanInteract) _boardGraphic.PreviewPattern(eventData, pattern, Vector2Int.zero);
    }

    /// <summary>목록에서 이동한 도안의 미리 보기를 지우고 드롭 위치를 적용한다.</summary>
    public void EndPatternDrag(PointerEventData eventData, int pattern)
    {
        if (!CanInteract) return;
        _boardGraphic.ClearPatternPreview();
        _boardGraphic.DropPattern(eventData, pattern);
    }
}


