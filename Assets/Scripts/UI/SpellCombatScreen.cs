using UnityEngine;
using UnityEngine.UI;

/// <summary>C안 전투 화면의 하위 뷰 참조를 보관한다. 게임 판단과 값 갱신은 SpellGameUI가 맡는다.</summary>
public sealed class SpellCombatScreen : MonoBehaviour
{
    [Header("상단 바")]
    [SerializeField] private Text _battle;
    [SerializeField] private Image[] _progressDots;
    [SerializeField] private SpellButtonView _help;
    [SerializeField] private SpellButtonView _menu;

    [Header("작업대")]
    [SerializeField] private SpellPlayerStatusView _playerStatus;
    [SerializeField] private SpellPlacementView _placement;
    [SerializeField] private SpellBoardGraphic _board;
    [SerializeField] private SpellPatternTabView[] _tabs;
    [SerializeField] private SpellCastButtonView[] _castButtons;
    [SerializeField] private SpellPreviewBarView _preview;

    [Header("전장")]
    [SerializeField] private SpellEnemyView[] _enemies;

    [Header("손패 줄")]
    [SerializeField] private SpellPileView _drawPile;
    [SerializeField] private SpellPileView _discardPile;
    [SerializeField] private SpellHandCardView[] _hand;
    [SerializeField] private RectTransform _handDropArea;
    [SerializeField] private Text _boardCount;
    [SerializeField] private Text _incoming;
    [SerializeField] private SpellButtonView _endTurn;

    [Header("겹쳐 뜨는 요소")]
    [SerializeField] private SpellMoreMenuView _moreMenu;
    [SerializeField] private SpellToastView _toast;
    [SerializeField] private SpellTooltipView _tooltip;
    [SerializeField] private RectTransform _dragLayer;
    [SerializeField] private RectTransform _pieceGhost;
    [SerializeField] private SpellShapeGraphic _pieceGhostShape;
    [SerializeField] private RectTransform _aimLine;
    [SerializeField] private RectTransform _cursorHint;
    [SerializeField] private Text _cursorHintText;

    public Text Battle { get { return _battle; } }
    public Image[] ProgressDots { get { return _progressDots; } }
    public SpellButtonView Help { get { return _help; } }
    public SpellButtonView Menu { get { return _menu; } }
    public SpellPlayerStatusView PlayerStatus { get { return _playerStatus; } }
    public SpellPlacementView Placement { get { return _placement; } }
    public SpellBoardGraphic Board { get { return _board; } }
    public SpellPatternTabView[] Tabs { get { return _tabs; } }
    public SpellCastButtonView[] CastButtons { get { return _castButtons; } }
    public SpellPreviewBarView Preview { get { return _preview; } }
    public SpellEnemyView[] Enemies { get { return _enemies; } }
    public SpellPileView DrawPile { get { return _drawPile; } }
    public SpellPileView DiscardPile { get { return _discardPile; } }
    public SpellHandCardView[] Hand { get { return _hand; } }
    public RectTransform HandDropArea { get { return _handDropArea; } }
    public Text BoardCount { get { return _boardCount; } }
    public Text Incoming { get { return _incoming; } }
    public SpellButtonView EndTurn { get { return _endTurn; } }
    public SpellMoreMenuView MoreMenu { get { return _moreMenu; } }
    public SpellToastView Toast { get { return _toast; } }
    public SpellTooltipView Tooltip { get { return _tooltip; } }
    public RectTransform DragLayer { get { return _dragLayer; } }
    public RectTransform PieceGhost { get { return _pieceGhost; } }
    public SpellShapeGraphic PieceGhostShape { get { return _pieceGhostShape; } }
    public RectTransform AimLine { get { return _aimLine; } }
    public RectTransform CursorHint { get { return _cursorHint; } }
    public Text CursorHintText { get { return _cursorHintText; } }
}
