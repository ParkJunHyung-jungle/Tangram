using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

/// <summary>주문 조각 전투의 카드, 제작판과 턴 상태를 관리한다.</summary>
public sealed class SpellGame
{
    public enum Shape { Square, Triangle, Rectangle, QuarterTriangle, Diamond, Parallelogram, MediumTriangle, House, LargeTriangle, LShape, Clay }
    public enum Effect { None, Flame, Guard, Scribe, Afterimage, Spark, Period, Resonance, Cog, Overpaint }
    public enum Phase { Title, Combat, Reward, Loadout, Victory, Defeat }
    public enum SpellKind { Single, Block, All, Scatter }
    public enum Keyword { None, OneStroke, Mosaic, Purity }

    public const int PATTERN_COUNT = 7;
    public const int MAX_HAND = 7;
    public const int MAX_HITS = 6;
    public const int SPARK_DAMAGE = 2;
    private const int LOCAL_SIZE = 3;
    private const int CELL_COUNT = LOCAL_SIZE * LOCAL_SIZE;
    private const int FULL_MASK = 15;
    private const int FLAME_BONUS = 2;
    private const int FLAME_HIT_BONUS = 1;
    private const int GUARD_BONUS = 3;
    private const int PERIOD_BONUS = 3;
    private const int RESONANCE_BONUS = 3;
    private const int RESONANCE_COUNT = 3;
    private const int COG_LIMIT = 3;
    private const int OVERPAINT_BONUS = 2;
    private const int ONE_STROKE_BONUS = 6;
    private const int PURITY_BONUS = 6;
    private const int VICTORY_HEAL = 6;
    private const int START_HEALTH = 50;
    private const int LAST_BATTLE = 6;

    /// <summary>도형이 칸 하나에서 차지하는 위·오른쪽·아래·왼쪽 구역 비트다.</summary>
    public struct ShapeCell
    {
        public int X; public int Y; public int Mask;
        public ShapeCell(int x, int y, int mask) { X = x; Y = y; Mask = mask; }
    }

    /// <summary>보상 후보 하나. 도안이면 Pattern이 0 이상이다.</summary>
    public struct Reward
    {
        public int Pattern; public Shape Shape; public Effect Effect;
        public bool IsPattern { get { return Pattern >= 0; } }
    }

    /// <summary>시전 순간의 판 상태로 계산한 주문 결과다. Value는 다타 주문이면 타격 하나의 피해다.</summary>
    public sealed class SpellEvaluation
    {
        public SpellKind Kind; public int Base; public int Value; public int Hits = 1; public int Block; public int SparkHits; public int Draws; public int Afterimages;
        public readonly List<string> Terms = new List<string>();
        public int Total { get { return Kind == SpellKind.Scatter ? Value * Hits : Value; } }
    }

    public sealed class Card
    {
        public int Id { get; internal set; }
        public Shape Shape { get; internal set; }
        public Effect Effect { get; internal set; }
        public bool HasPersisted { get; internal set; }
    }

    public sealed class Placement
    {
        internal int[] Mask = new int[CELL_COUNT];

        public Card Card { get; internal set; }
        public int X { get; internal set; }
        public int Y { get; internal set; }
        public int Rotation { get; internal set; }
        public int Turn { get; internal set; }
        public bool HasPersisted { get { return Card.HasPersisted; } }
        public bool CanRefund { get; internal set; }

        /// <summary>도안 기준 칸 (x, y)에서 이 조각이 차지한 구역 마스크를 반환한다.</summary>
        public int CellMask(int x, int y) { return x < 0 || x >= LOCAL_SIZE || y < 0 || y >= LOCAL_SIZE ? 0 : Mask[y * LOCAL_SIZE + x]; }
    }

    public sealed class Board
    {
        private readonly List<Placement> _pieces = new List<Placement>();

        public int Spell { get; }
        public ReadOnlyCollection<Placement> Pieces { get; }
        public Placement Completer { get; internal set; }
        internal List<Placement> MutablePieces { get { return _pieces; } }

        /// <summary>지정 주문의 제작판과 외부에서 변경할 수 없는 배치 목록을 초기화한다.</summary>
        internal Board(int spell)
        {
            Spell = spell;
            Pieces = _pieces.AsReadOnly();
        }
    }

    public sealed class Enemy
    {
        private readonly int[] _pattern;
        private readonly ReadOnlyCollection<int> _patternView;

        public string Name { get; internal set; }
        public int Health { get; internal set; }
        public int MaxHealth { get; internal set; }

        public int Intent { get { return _pattern[ActionCount % _pattern.Length]; } }
        public ReadOnlyCollection<int> Pattern { get { return _patternView; } }
        internal int ActionCount;

        /// <summary>이름, 체력과 공격 패턴으로 적의 전투 상태를 초기화한다.</summary>
        internal Enemy(string name, int health, int[] pattern)
        {
            Name = name; Health = health; MaxHealth = health; _pattern = pattern; _patternView = Array.AsReadOnly(pattern);
        }
    }

    private static readonly string[] PATTERN_NAMES = { "불씨", "방벽", "번개", "유성", "섬광", "산탄", "만화경" };
    private static readonly SpellKind[] PATTERN_KINDS = { SpellKind.Single, SpellKind.Block, SpellKind.Single, SpellKind.All, SpellKind.Single, SpellKind.Scatter, SpellKind.All };
    private static readonly int[] PATTERN_BASE = { 6, 7, 12, 10, 8, 2, 6 };
    private static readonly Keyword[] PATTERN_KEYWORDS = { Keyword.None, Keyword.None, Keyword.OneStroke, Keyword.None, Keyword.OneStroke, Keyword.Mosaic, Keyword.Purity };
    private static readonly int[][] SPELL_MASKS = {
        new[] { 4, 0, 0, 15, 0, 0, 0, 0, 0 },
        new[] { 15, 0, 0, 1, 0, 0, 0, 0, 0 },
        new[] { 15, 0, 0, 15, 15, 0, 0, 0, 0 },
        new[] { 15, 15, 0, 15, 15, 0, 0, 0, 0 },
        new[] { 15, 9, 0, 9, 0, 0, 0, 0, 0 },
        new[] { 6, 15, 12, 0, 0, 0, 0, 0, 0 },
        new[] { 6, 12, 0, 3, 9, 0, 0, 0, 0 } };
    private static readonly ShapeCell[][] BASE_SHAPES = {
        new[] { new ShapeCell(0, 0, 15) },
        new[] { new ShapeCell(0, 0, 3) },
        new[] { new ShapeCell(0, 0, 15), new ShapeCell(1, 0, 15) },
        new[] { new ShapeCell(0, 0, 1) },
        new[] { new ShapeCell(0, 0, 2), new ShapeCell(1, 0, 8) },
        new[] { new ShapeCell(0, 0, 6), new ShapeCell(1, 0, 9) },
        new[] { new ShapeCell(0, 0, 6), new ShapeCell(1, 0, 12) },
        new[] { new ShapeCell(0, 0, 4), new ShapeCell(0, 1, 15) },
        new[] { new ShapeCell(0, 0, 15), new ShapeCell(1, 0, 9), new ShapeCell(0, 1, 9) },
        new[] { new ShapeCell(0, 0, 15), new ShapeCell(0, 1, 15), new ShapeCell(1, 1, 15) },
        new[] { new ShapeCell(0, 0, 15) } };
    private static readonly Reward[] CARD_POOL = {
        Piece(Shape.Square, Effect.None), Piece(Shape.Triangle, Effect.None), Piece(Shape.Rectangle, Effect.None), Piece(Shape.QuarterTriangle, Effect.None),
        Piece(Shape.Triangle, Effect.Flame), Piece(Shape.Square, Effect.Guard), Piece(Shape.Rectangle, Effect.Scribe), Piece(Shape.QuarterTriangle, Effect.Afterimage),
        Piece(Shape.House, Effect.None), Piece(Shape.LShape, Effect.None), Piece(Shape.LargeTriangle, Effect.None), Piece(Shape.Square, Effect.Afterimage), Piece(Shape.Clay, Effect.None),
        Piece(Shape.Diamond, Effect.None), Piece(Shape.Parallelogram, Effect.None), Piece(Shape.MediumTriangle, Effect.None),
        Piece(Shape.QuarterTriangle, Effect.Spark), Piece(Shape.QuarterTriangle, Effect.Period), Piece(Shape.Triangle, Effect.Resonance),
        Piece(Shape.Triangle, Effect.Cog), Piece(Shape.Triangle, Effect.Overpaint), Piece(Shape.MediumTriangle, Effect.Scribe) };
    private static readonly ShapeCell[][][] SHAPE_ORIENTATIONS = CreateOrientations();

    private readonly Random _random = new Random();
    private readonly List<Card> _owned = new List<Card>();
    private readonly List<Card> _draw = new List<Card>();
    private readonly List<Card> _discard = new List<Card>();
    private int _nextId;
    private readonly List<Card> _hand = new List<Card>();
    private readonly List<Enemy> _enemies = new List<Enemy>();
    private readonly List<int> _ownedSpells = new List<int>();
    private readonly List<int> _spells = new List<int>();
    private readonly List<Reward> _rewards = new List<Reward>();
    private readonly Board[] _boards = CreateBoards();

    public ReadOnlyCollection<Card> Hand { get; }
    public ReadOnlyCollection<Enemy> Enemies { get; }
    public ReadOnlyCollection<int> Spells { get; }
    public ReadOnlyCollection<int> OwnedSpells { get; }
    public ReadOnlyCollection<Board> Boards { get; }
    public ReadOnlyCollection<Reward> Rewards { get; }
    public Phase CurrentPhase { get; private set; } = Phase.Title;
    public int Health { get; private set; }
    public int Block { get; private set; }
    public int Battle { get; private set; }
    public int Turn { get; private set; }
    public int Target { get; private set; }
    public int LastAcquiredPattern { get; private set; } = -1;
    public int DrawCount { get { return _draw.Count; } }
    public int DiscardCount { get { return _discard.Count; } }
    public int CraftCount { get { int count = 0; foreach (Board board in Boards) count += board.Pieces.Count; return count; } }

    /// <summary>카드, 적, 도안과 제작판의 읽기 전용 목록을 연결한다.</summary>
    public SpellGame()
    {
        Hand = _hand.AsReadOnly(); Enemies = _enemies.AsReadOnly(); Spells = _spells.AsReadOnly(); OwnedSpells = _ownedSpells.AsReadOnly();
        Boards = Array.AsReadOnly(_boards); Rewards = _rewards.AsReadOnly();
    }

    /// <summary>도안 수만큼 빈 제작판을 만든다.</summary>
    private static Board[] CreateBoards()
    {
        Board[] boards = new Board[PATTERN_COUNT];
        for (int i = 0; i < boards.Length; i++) boards[i] = new Board(i);
        return boards;
    }

    /// <summary>모양과 효과로 조각 보상 후보를 만든다.</summary>
    private static Reward Piece(Shape shape, Effect effect) { return new Reward { Pattern = -1, Shape = shape, Effect = effect }; }

    /// <summary>모든 도형의 네 방향 칸 목록을 미리 계산한다. 평행사변형은 뒤집힌 두 방향을 회전 순서에 포함한다.</summary>
    private static ShapeCell[][][] CreateOrientations()
    {
        ShapeCell[][][] result = new ShapeCell[BASE_SHAPES.Length][][];
        for (int shape = 0; shape < BASE_SHAPES.Length; shape++)
        {
            result[shape] = new ShapeCell[4][];
            ShapeCell[] baseCells = BASE_SHAPES[shape];
            ShapeCell[] mirrored = Mirror(baseCells);
            for (int rotation = 0; rotation < 4; rotation++)
            {
                if ((Shape)shape == Shape.Parallelogram) result[shape][rotation] = Rotate(rotation < 2 ? baseCells : mirrored, rotation % 2);
                else result[shape][rotation] = Rotate(baseCells, rotation);
            }
        }
        return result;
    }

    /// <summary>칸 목록을 좌우로 뒤집는다. 오른쪽과 왼쪽 구역 비트도 맞바꾼다.</summary>
    private static ShapeCell[] Mirror(ShapeCell[] cells)
    {
        int width = 0; foreach (ShapeCell c in cells) width = Math.Max(width, c.X + 1);
        ShapeCell[] result = new ShapeCell[cells.Length];
        for (int i = 0; i < cells.Length; i++)
        {
            int m = cells[i].Mask;
            result[i] = new ShapeCell(width - 1 - cells[i].X, cells[i].Y, (m & 5) | ((m & 2) << 2) | ((m & 8) >> 2));
        }
        return result;
    }

    /// <summary>칸 목록을 시계 방향으로 rotation번 돌리고 왼쪽 위가 (0, 0)이 되게 맞춘다.</summary>
    private static ShapeCell[] Rotate(ShapeCell[] cells, int rotation)
    {
        ShapeCell[] result = (ShapeCell[])cells.Clone();
        for (int r = 0; r < rotation; r++)
        {
            int height = 0; foreach (ShapeCell c in result) height = Math.Max(height, c.Y + 1);
            for (int i = 0; i < result.Length; i++)
                result[i] = new ShapeCell(height - 1 - result[i].Y, result[i].X, RotateMask(result[i].Mask, 1));
        }
        return result;
    }

    /// <summary>구역 비트를 시계 방향으로 rotation번 돌린 마스크를 반환한다.</summary>
    public static int RotateMask(int mask, int rotation)
    {
        int r = (rotation % 4 + 4) % 4;
        return ((mask << r) | (mask >> (4 - r))) & FULL_MASK;
    }

    /// <summary>도형의 지정 방향 칸 목록을 반환한다. 왼쪽 위 범위가 (0, 0)이다. 점토는 한 칸 기준이다.</summary>
    public static IReadOnlyList<ShapeCell> ShapeCells(Shape shape, int rotation)
    {
        return SHAPE_ORIENTATIONS[(int)shape][(rotation % 4 + 4) % 4];
    }

    /// <summary>도형을 앵커 (x, y)와 방향으로 놓았을 때 칸 (cellX, cellY)에서 차지하는 구역 마스크를 반환한다.</summary>
    public static int ShapeMaskAt(Shape shape, int x, int y, int rotation, int cellX, int cellY)
    {
        foreach (ShapeCell cell in ShapeCells(shape, rotation)) if (x + cell.X == cellX && y + cell.Y == cellY) return cell.Mask;
        return 0;
    }

    /// <summary>도형이 차지하는 1/4 구역 수를 반환한다. 점토는 1칸으로 본다.</summary>
    public static int ShapeArea(Shape shape)
    {
        int area = 0; foreach (ShapeCell cell in ShapeCells(shape, 0)) area += BitCount(cell.Mask);
        return area;
    }

    /// <summary>4비트 구역 마스크에서 켜진 구역 수를 반환한다.</summary>
    public static int BitCount(int mask)
    {
        int count = 0;
        for (int i = 0; i < 4; i++) if ((mask & (1 << i)) != 0) count++;
        return count;
    }

    /// <summary>도안 이름을 반환한다.</summary>
    public static string PatternName(int spell) { return PATTERN_NAMES[spell]; }

    /// <summary>도안의 기본 효과 종류를 반환한다.</summary>
    public static SpellKind PatternKind(int spell) { return PATTERN_KINDS[spell]; }

    /// <summary>도안의 특수 키워드를 반환한다.</summary>
    public static Keyword PatternKeyword(int spell) { return PATTERN_KEYWORDS[spell]; }

    /// <summary>도안의 기본 수치를 반환한다. 다타 도안은 타격 하나의 피해다.</summary>
    public static int PatternBase(int spell) { return PATTERN_BASE[spell]; }

    /// <summary>단일 대상을 골라야 하는 도안인지 반환한다.</summary>
    public static bool NeedsTarget(int spell) { return spell >= 0 && spell < PATTERN_COUNT && PATTERN_KINDS[spell] == SpellKind.Single; }

    /// <summary>도안의 기본 효과와 키워드를 한 줄 설명으로 반환한다.</summary>
    public static string PatternDescription(int spell)
    {
        int value = PATTERN_BASE[spell];
        string text = PATTERN_KINDS[spell] == SpellKind.Single ? $"단일 피해 {value}" : PATTERN_KINDS[spell] == SpellKind.Block ? $"방어 {value}" : PATTERN_KINDS[spell] == SpellKind.All ? $"전체 피해 {value}" : $"조각 1장마다 무작위 피해 {value} (최대 {MAX_HITS}타)";
        switch (PATTERN_KEYWORDS[spell])
        {
            case Keyword.OneStroke: return text + $" · 한 방: 조각 1장으로 완성하면 +{ONE_STROKE_BONUS}";
            case Keyword.Mosaic: return "모자이크 · " + text;
            case Keyword.Purity: return text + $" · 순수: 한 가지 도형으로만 완성하면 +{PURITY_BONUS}";
            default: return text;
        }
    }

    /// <summary>도안의 1/4 구역 총수를 반환한다.</summary>
    public static int PatternArea(int spell)
    {
        int area = 0; foreach (int mask in SPELL_MASKS[spell]) area += BitCount(mask);
        return area;
    }

    /// <summary>드로우 또는 버림 더미의 조각별 수량을 읽기 전용 요약으로 반환한다.</summary>
    public Dictionary<string, int> GetPileSummary(bool isDrawPile)
    {
        Dictionary<string, int> summary = new Dictionary<string, int>();
        foreach (Card card in isDrawPile ? _draw : _discard)
        {
            string key = card.Shape + ":" + card.Effect;
            if (!summary.ContainsKey(key)) summary.Add(key, 0);
            summary[key]++;
        }
        return summary;
    }

    /// <summary>지정한 도안을 지금 시전했을 때의 공격 피해 합을 반환한다. 방어 도안은 0이다.</summary>
    public int GetSpellDamage(int boardIndex)
    {
        if (boardIndex < 0 || boardIndex >= Boards.Count || PATTERN_KINDS[boardIndex] == SpellKind.Block) return 0;
        return Evaluate(boardIndex).Total;
    }

    /// <summary>새 게임의 체력, 카드 및 도안을 초기화하고 첫 전투를 시작한다.</summary>
    public void StartGame()
    {
        _owned.Clear(); _spells.Clear(); _ownedSpells.Clear(); _nextId = 0; Health = START_HEALTH; Battle = 0; LastAcquiredPattern = -1;
        _ownedSpells.Add(0); _ownedSpells.Add(1); _spells.Add(0); _spells.Add(1);
        for (int i = 0; i < 4; i++) AddCard(Shape.Square, Effect.None);
        for (int i = 0; i < 4; i++) AddCard(Shape.Triangle, Effect.None);
        for (int i = 0; i < 2; i++) AddCard(Shape.Rectangle, Effect.None);
        for (int i = 0; i < 4; i++) AddCard(Shape.QuarterTriangle, Effect.None);
        StartBattle();
    }

    /// <summary>현재 게임을 종료하고 타이틀 화면 상태로 전환한다.</summary>
    public void ReturnToTitle() { CurrentPhase = Phase.Title; }

    /// <summary>개별 ID를 가진 카드를 소유 목록에 추가한다.</summary>
    private void AddCard(Shape shape, Effect effect)
    {
        _owned.Add(new Card { Id = ++_nextId, Shape = shape, Effect = effect });
    }

    /// <summary>다음 전투의 적과 덱을 준비하고 첫 턴을 시작한다.</summary>
    private void StartBattle()
    {
        Battle++; Turn = 0; Block = 0; Target = 0; _enemies.Clear(); _hand.Clear(); _discard.Clear(); _draw.Clear();
        foreach (Board board in Boards) { board.MutablePieces.Clear(); board.Completer = null; }
        foreach (Card card in _owned) card.HasPersisted = false;
        _draw.AddRange(_owned); Shuffle(_draw);
        GuaranteeOpeningShape(Shape.Square, _draw.Count - 1); GuaranteeOpeningShape(Shape.QuarterTriangle, _draw.Count - 2);
        if (Battle == 1) AddEnemy(0);
        if (Battle == 2) { AddEnemy(0); AddEnemy(0); }
        if (Battle == 3) AddEnemy(1);
        if (Battle == 4) { AddEnemy(1); AddEnemy(0); }
        if (Battle == 5) { AddEnemy(1); AddEnemy(1); }
        if (Battle == 6) AddEnemy(2);
        CurrentPhase = Phase.Combat; StartTurn();
    }

    /// <summary>적 종류에 따라 체력과 공격 패턴을 설정한다.</summary>
    private void AddEnemy(int kind)
    {
        _enemies.Add(kind == 0 ? new Enemy("잉크 하수인", 12, new[] { 4 }) : kind == 1 ? new Enemy("거대 잉크", 26, new[] { 2, 8 }) : new Enemy("서고의 수호자", 60, new[] { 4, 7, 12 }));
    }

    /// <summary>목록을 제자리에서 무작위로 섞는다.</summary>
    private void Shuffle(List<Card> cards)
    {
        for (int i = cards.Count - 1; i > 0; i--) { int j = _random.Next(i + 1); Card c = cards[i]; cards[i] = cards[j]; cards[j] = c; }
    }

    /// <summary>첫 손패에 포함할 도형을 섞인 덱의 지정된 뽑기 위치로 옮긴다.</summary>
    private void GuaranteeOpeningShape(Shape shape, int drawIndex)
    {
        for (int i = 0; i <= drawIndex; i++)
        {
            if (_draw[i].Shape != shape) continue;
            Card card = _draw[drawIndex]; _draw[drawIndex] = _draw[i]; _draw[i] = card; return;
        }
    }

    /// <summary>새 턴을 시작하고 카드를 뽑는다.</summary>
    private void StartTurn() { Turn++; Draw(5); }

    /// <summary>손패 한도를 지키며 지정 수만큼 카드를 뽑는다.</summary>
    private void Draw(int count)
    {
        while (count-- > 0 && Hand.Count < MAX_HAND)
        {
            if (_draw.Count == 0) { _draw.AddRange(_discard); _discard.Clear(); Shuffle(_draw); }
            if (_draw.Count == 0) break;
            int last = _draw.Count - 1; _hand.Add(_draw[last]); _draw.RemoveAt(last);
        }
    }

    /// <summary>카드를 버림 더미로 보내고 다음 순환을 위해 잔상 시전 이력을 초기화한다.</summary>
    private void Discard(Card card)
    {
        card.HasPersisted = false;
        _discard.Add(card);
    }

    /// <summary>손패를 버리고 생존 적의 공격을 순서대로 처리한다.</summary>
    public void EndTurn()
    {
        if (CurrentPhase != Phase.Combat) return;
        foreach (Card card in Hand) Discard(card);
        _hand.Clear();
        foreach (Enemy enemy in Enemies)
        {
            if (enemy.Health <= 0) continue;
            int damage = enemy.Intent; enemy.ActionCount++; int absorbed = Math.Min(Block, damage); Block -= absorbed; Health -= damage - absorbed;
            if (Health <= 0) { Health = 0; CurrentPhase = Phase.Defeat; return; }
        }
        Block = 0; StartTurn();
    }

    /// <summary>제외할 조각을 뺀 판의 구역별 겹침 수를 칸·구역 순서로 반환한다.</summary>
    private int[] Coverage(Board board, Placement ignored)
    {
        int[] coverage = new int[CELL_COUNT * 4];
        foreach (Placement piece in board.Pieces)
        {
            if (piece == ignored) continue;
            for (int i = 0; i < CELL_COUNT; i++) for (int s = 0; s < 4; s++) if ((piece.Mask[i] & (1 << s)) != 0) coverage[i * 4 + s]++;
        }
        return coverage;
    }

    /// <summary>카드를 도안 기준 앵커와 방향으로 놓았을 때의 칸별 마스크를 반환한다. 범위를 벗어나거나 점토가 채울 곳이 없으면 null이다.</summary>
    private int[] CandidateMask(Board board, Card card, int x, int y, int rotation, Placement ignored)
    {
        int[] mask = new int[CELL_COUNT];
        if (card.Shape == Shape.Clay)
        {
            if (x < 0 || x >= LOCAL_SIZE || y < 0 || y >= LOCAL_SIZE) return null;
            int[] coverage = Coverage(board, ignored); int index = y * LOCAL_SIZE + x, free = 0;
            for (int s = 0; s < 4; s++) if ((SPELL_MASKS[board.Spell][index] & (1 << s)) != 0 && coverage[index * 4 + s] == 0) free |= 1 << s;
            if (free == 0) return null;
            mask[index] = free; return mask;
        }
        foreach (ShapeCell cell in ShapeCells(card.Shape, rotation))
        {
            int cx = x + cell.X, cy = y + cell.Y;
            if (cx < 0 || cx >= LOCAL_SIZE || cy < 0 || cy >= LOCAL_SIZE) return null;
            mask[cy * LOCAL_SIZE + cx] = cell.Mask;
        }
        return mask;
    }

    /// <summary>기존 조각과 도안 영역을 고려해 배치 가능 여부를 반환한다.</summary>
    public bool CanPlace(int boardIndex, Card card, int x, int y, int rotation, Placement ignored = null)
    {
        return PlacementIssue(boardIndex, card, x, y, rotation, ignored) == null;
    }

    /// <summary>배치가 불가능한 이유를 반환하고, 가능하면 null을 반환한다. 덧칠 조각은 구역마다 한 겹까지 겹칠 수 있다.</summary>
    public string PlacementIssue(int boardIndex, Card card, int x, int y, int rotation, Placement ignored = null)
    {
        if (CurrentPhase != Phase.Combat || boardIndex < 0 || boardIndex >= Boards.Count || card == null) return "지금은 배치할 수 없습니다";
        if (!Spells.Contains(Boards[boardIndex].Spell)) return "장착하지 않은 도안입니다";
        Board board = Boards[boardIndex]; int[] candidate = CandidateMask(board, card, x, y, rotation, ignored);
        if (candidate == null) return card.Shape == Shape.Clay ? "점토가 채울 빈 구역이 없습니다" : "도형이 도안 범위를 벗어납니다";
        int[] allowed = SPELL_MASKS[board.Spell];
        for (int i = 0; i < CELL_COUNT; i++) if ((candidate[i] & ~allowed[i]) != 0) return "도안 밖으로 벗어납니다";
        int[] coverage = Coverage(board, ignored);
        bool overpaint = card.Effect == Effect.Overpaint;
        for (int i = 0; i < CELL_COUNT; i++) for (int s = 0; s < 4; s++)
        {
            if ((candidate[i] & (1 << s)) == 0) continue;
            if (overpaint && coverage[i * 4 + s] >= 2) return "덧칠은 한 겹까지만 올릴 수 있습니다";
            if (!overpaint && coverage[i * 4 + s] >= 1) return "다른 조각과 겹칩니다";
        }
        return null;
    }

    /// <summary>배치가 도안을 완성하는지 반환한다.</summary>
    public bool WouldComplete(int boardIndex, Card card, int x, int y, int rotation, Placement ignored = null)
    {
        if (!CanPlace(boardIndex, card, x, y, rotation, ignored)) return false;
        Board board = Boards[boardIndex];
        int[] filled = CandidateMask(board, card, x, y, rotation, ignored);
        foreach (Placement piece in board.Pieces)
        {
            if (piece == ignored) continue;
            for (int i = 0; i < CELL_COUNT; i++) filled[i] |= piece.Mask[i];
        }
        for (int i = 0; i < CELL_COUNT; i++) if (filled[i] != SPELL_MASKS[board.Spell][i]) return false;
        return true;
    }

    /// <summary>손패 카드를 도안의 유효한 위치에 배치한다.</summary>
    public bool Place(int board, Card card, int x, int y, int rotation)
    {
        if (!Hand.Contains(card) || !CanPlace(board, card, x, y, rotation)) return false;
        Placement placement = new Placement { Card = card, Turn = Turn, CanRefund = true };
        SetPosition(Boards[board], placement, x, y, rotation);
        _hand.Remove(card); Boards[board].MutablePieces.Add(placement); UpdateCompleter(Boards[board], placement); return true;
    }

    /// <summary>배치 위치·방향과 점유 마스크를 설정한다. 호출 전에 배치 가능 여부를 확인한다.</summary>
    private void SetPosition(Board board, Placement placement, int x, int y, int rotation)
    {
        placement.Mask = CandidateMask(board, placement.Card, x, y, rotation, placement);
        placement.X = x; placement.Y = y; placement.Rotation = placement.Card.Shape == Shape.Clay ? 0 : (rotation % 4 + 4) % 4;
    }

    /// <summary>판이 막 완성되면 그 조각을 완성 조각으로 기록하고, 미완성이 되면 기록을 지운다.</summary>
    private void UpdateCompleter(Board board, Placement changed)
    {
        if (!IsBoardComplete(board)) { board.Completer = null; return; }
        if (board.Completer == null || !board.Pieces.Contains(board.Completer)) board.Completer = changed;
    }

    /// <summary>현재 턴에 배치한 미시전 조각만 개별 이동하거나 회수할 수 있는지 반환한다.</summary>
    public bool CanMovePiece(Placement piece) { return piece != null && piece.Turn == Turn && !piece.HasPersisted && piece.CanRefund; }

    /// <summary>배치된 조각을 비용 없이 다른 위치 또는 판으로 옮긴다.</summary>
    public bool Move(int source, Placement piece, int destination, int x, int y, int rotation)
    {
        if (source < 0 || source >= Boards.Count || !CanMovePiece(piece) || !Boards[source].Pieces.Contains(piece) || !CanPlace(destination, piece.Card, x, y, rotation, source == destination ? piece : null)) return false;
        Boards[source].MutablePieces.Remove(piece); SetPosition(Boards[destination], piece, x, y, rotation); Boards[destination].MutablePieces.Add(piece);
        UpdateCompleter(Boards[source], null); UpdateCompleter(Boards[destination], piece); return true;
    }

    /// <summary>현재 턴의 미시전 조각을 손패로 회수한다.</summary>
    public bool Recall(int board, Placement piece)
    {
        if (CurrentPhase != Phase.Combat || board < 0 || board >= Boards.Count || !CanMovePiece(piece) || Hand.Count >= MAX_HAND || !Boards[board].MutablePieces.Remove(piece)) return false;
        _hand.Add(piece.Card); UpdateCompleter(Boards[board], null); return true;
    }

    /// <summary>판의 현재 턴 조각은 회수하고 이전 턴 조각은 버린다.</summary>
    public bool Abandon(int board)
    {
        if (CurrentPhase != Phase.Combat || board < 0 || board >= Boards.Count) return false;
        Board b = Boards[board]; int current = 0; foreach (Placement p in b.Pieces) if (CanMovePiece(p)) current++;
        if (Hand.Count + current > MAX_HAND) return false;
        foreach (Placement p in b.Pieces)
        {
            if (CanMovePiece(p)) { _hand.Add(p.Card); }
            else Discard(p.Card);
        }
        b.MutablePieces.Clear(); b.Completer = null; return true;
    }

    /// <summary>살아 있는 적을 단일 대상 주문의 대상으로 선택한다.</summary>
    public void SelectTarget(int index) { if (index >= 0 && index < Enemies.Count && Enemies[index].Health > 0) Target = index; }

    /// <summary>지정 도안이 모든 삼각 구역을 빈틈없이 채웠는지 반환한다.</summary>
    public bool IsComplete(int boardIndex)
    {
        if (boardIndex < 0 || boardIndex >= Boards.Count || !Spells.Contains(boardIndex)) return false;
        return IsBoardComplete(Boards[boardIndex]);
    }

    /// <summary>판의 모든 도안 구역이 한 겹 이상 덮였는지 반환한다.</summary>
    private static bool IsBoardComplete(Board board)
    {
        int[] filled = new int[CELL_COUNT];
        foreach (Placement piece in board.Pieces) for (int i = 0; i < CELL_COUNT; i++) filled[i] |= piece.Mask[i];
        for (int i = 0; i < CELL_COUNT; i++) if (filled[i] != SPELL_MASKS[board.Spell][i]) return false;
        return true;
    }

    /// <summary>도안의 덮인 구역 수를 반환한다. 덧칠로 겹친 구역은 한 번만 센다.</summary>
    public int FilledArea(int boardIndex)
    {
        int[] filled = new int[CELL_COUNT]; int area = 0;
        foreach (Placement piece in Boards[boardIndex].Pieces) for (int i = 0; i < CELL_COUNT; i++) filled[i] |= piece.Mask[i];
        foreach (int mask in filled) area += BitCount(mask);
        return area;
    }

    /// <summary>도안의 지정 칸에 필요한 상·우·하·좌 삼각 구역을 반환한다.</summary>
    public static int PatternMask(int spell, int x, int y)
    {
        return spell >= 0 && spell < SPELL_MASKS.Length && x >= 0 && x < LOCAL_SIZE && y >= 0 && y < LOCAL_SIZE ? SPELL_MASKS[spell][y * LOCAL_SIZE + x] : 0;
    }

    /// <summary>두 조각이 변(구역 경계)을 맞대고 있는지 반환한다. 같은 칸의 이웃 구역과 옆 칸의 맞닿은 구역을 본다.</summary>
    private static bool Touches(Placement a, Placement b)
    {
        for (int y = 0; y < LOCAL_SIZE; y++) for (int x = 0; x < LOCAL_SIZE; x++)
        {
            int mask = a.CellMask(x, y);
            for (int s = 0; s < 4; s++)
            {
                if ((mask & (1 << s)) == 0) continue;
                if ((b.CellMask(x, y) & ((1 << ((s + 1) % 4)) | (1 << ((s + 3) % 4)))) != 0) return true;
                int nx = x + (s == 1 ? 1 : s == 3 ? -1 : 0), ny = y + (s == 2 ? 1 : s == 0 ? -1 : 0);
                if ((b.CellMask(nx, ny) & (1 << ((s + 2) % 4))) != 0) return true;
            }
        }
        return false;
    }

    /// <summary>도안을 지금 시전했을 때의 결과를 시전 순간의 판 상태로 계산한다. 이전 시전에서 남은 잔상은 조각 수에 세지 않는다.</summary>
    public SpellEvaluation Evaluate(int boardIndex)
    {
        Board board = Boards[boardIndex]; int spell = board.Spell;
        SpellEvaluation e = new SpellEvaluation { Kind = PATTERN_KINDS[spell], Base = PATTERN_BASE[spell] };
        int counted = 0, flame = 0, guard = 0, resonance = 0, overpaint = 0, cog = 0;
        bool pure = board.Pieces.Count > 0; Shape first = board.Pieces.Count > 0 ? board.Pieces[0].Card.Shape : Shape.Square;
        foreach (Placement p in board.Pieces)
        {
            if (!p.HasPersisted) counted++;
            if (p.Card.Shape != first) pure = false;
            switch (p.Card.Effect)
            {
                case Effect.Flame: flame++; break;
                case Effect.Guard: guard++; break;
                case Effect.Scribe: e.Draws++; break;
                case Effect.Spark: e.SparkHits++; break;
                case Effect.Resonance: resonance++; break;
                case Effect.Overpaint: overpaint++; break;
                case Effect.Afterimage: if (!p.HasPersisted) e.Afterimages++; break;
                case Effect.Cog:
                    int touching = 0;
                    foreach (Placement other in board.Pieces) if (other != p && Touches(p, other)) touching++;
                    cog += Math.Min(COG_LIMIT, touching); break;
            }
        }
        bool attack = e.Kind != SpellKind.Block;
        int value = e.Base;
        if (attack && flame > 0) { int bonus = flame * (e.Kind == SpellKind.Scatter ? FLAME_HIT_BONUS : FLAME_BONUS); value += bonus; e.Terms.Add($"불꽃 +{bonus}"); }
        if (attack && cog > 0) { value += cog; e.Terms.Add($"톱니 +{cog}"); }
        if (board.Completer != null && board.Completer.Card.Effect == Effect.Period) { value += PERIOD_BONUS; e.Terms.Add($"마침표 +{PERIOD_BONUS}"); }
        if (resonance > 0 && counted >= RESONANCE_COUNT) { value += resonance * RESONANCE_BONUS; e.Terms.Add($"공명 +{resonance * RESONANCE_BONUS}"); }
        if (overpaint > 0) { value += overpaint * OVERPAINT_BONUS; e.Terms.Add($"덧칠 +{overpaint * OVERPAINT_BONUS}"); }
        if (PATTERN_KEYWORDS[spell] == Keyword.OneStroke && counted == 1) { value += ONE_STROKE_BONUS; e.Terms.Add($"한 방 +{ONE_STROKE_BONUS}"); }
        if (PATTERN_KEYWORDS[spell] == Keyword.Purity && pure) { value += PURITY_BONUS; e.Terms.Add($"순수 +{PURITY_BONUS}"); }
        if (e.Kind == SpellKind.Scatter) e.Hits = Math.Max(1, Math.Min(MAX_HITS, counted));
        e.Value = value;
        e.Block = (e.Kind == SpellKind.Block ? value : 0) + guard * GUARD_BONUS;
        return e;
    }

    /// <summary>살아 있는 적 중 하나를 무작위로 골라 피해를 준다. 모두 쓰러졌으면 아무것도 하지 않는다.</summary>
    private void HitRandom(int damage)
    {
        int alive = 0; foreach (Enemy enemy in Enemies) if (enemy.Health > 0) alive++;
        if (alive == 0) return;
        int pick = _random.Next(alive);
        foreach (Enemy enemy in Enemies)
        {
            if (enemy.Health <= 0) continue;
            if (pick-- == 0) { enemy.Health -= damage; return; }
        }
    }

    /// <summary>완성된 주문을 지정 대상에 시전하고 효과와 승리 및 카드 소비를 처리한다.</summary>
    public bool Cast(int boardIndex, int target)
    {
        if (CurrentPhase != Phase.Combat || !IsComplete(boardIndex)) return false;
        bool single = NeedsTarget(boardIndex);
        if (single && (target < 0 || target >= Enemies.Count || Enemies[target].Health <= 0)) return false;
        if (single) Target = target;
        Board board = Boards[boardIndex];
        SpellEvaluation e = Evaluate(boardIndex);
        Block += e.Block;
        if (e.Kind == SpellKind.Single) Enemies[Target].Health -= e.Value;
        else if (e.Kind == SpellKind.All) { foreach (Enemy enemy in Enemies) if (enemy.Health > 0) enemy.Health -= e.Value; }
        else if (e.Kind == SpellKind.Scatter) for (int i = 0; i < e.Hits; i++) HitRandom(e.Value);
        for (int i = 0; i < e.SparkHits; i++) HitRandom(SPARK_DAMAGE);
        bool alive = false; for (int i = 0; i < Enemies.Count; i++) if (Enemies[i].Health > 0) { if (!alive && Enemies[Target].Health <= 0) Target = i; alive = true; }
        for (int i = board.Pieces.Count - 1; i >= 0; i--)
        {
            Placement piece = board.Pieces[i];
            piece.CanRefund = false;
            if (piece.Card.Effect == Effect.Afterimage && !piece.HasPersisted) piece.Card.HasPersisted = true;
            else { Discard(piece.Card); board.MutablePieces.RemoveAt(i); }
        }
        board.Completer = null;
        if (e.Draws > 0 && alive) Draw(e.Draws);
        if (!alive) { if (Battle == LAST_BATTLE) CurrentPhase = Phase.Victory; else { Health = Math.Min(START_HEALTH, Health + VICTORY_HEAL); EnterReward(); } }
        return true;
    }

    /// <summary>모든 조각 카드와 아직 없는 도안을 보상 후보로 만들고 보상 단계로 넘어간다.</summary>
    private void EnterReward()
    {
        _rewards.Clear();
        for (int spell = 0; spell < PATTERN_COUNT; spell++) if (!_ownedSpells.Contains(spell)) _rewards.Add(new Reward { Pattern = spell });
        _rewards.AddRange(CARD_POOL);
        CurrentPhase = Phase.Reward;
    }

    /// <summary>보상 후보 하나를 얻고 도안 장착 단계로 넘어간다. -1은 건너뛰기다.</summary>
    public bool ChooseReward(int index)
    {
        if (CurrentPhase != Phase.Reward || index < -1 || index >= _rewards.Count) return false;
        LastAcquiredPattern = -1;
        if (index >= 0)
        {
            Reward reward = _rewards[index];
            if (reward.IsPattern) { _ownedSpells.Add(reward.Pattern); LastAcquiredPattern = reward.Pattern; }
            else AddCard(reward.Shape, reward.Effect);
        }
        CurrentPhase = Phase.Loadout; return true;
    }

    /// <summary>장착할 보유 도안을 정하고 다음 전투를 시작한다. 하나 이상 장착해야 한다.</summary>
    public bool StartNextBattle(IReadOnlyList<int> equipped)
    {
        if (CurrentPhase != Phase.Loadout || equipped.Count == 0) return false;
        foreach (int spell in equipped) if (!_ownedSpells.Contains(spell)) return false;
        _spells.Clear(); _spells.AddRange(equipped); _spells.Sort();
        StartBattle(); return true;
    }
}
