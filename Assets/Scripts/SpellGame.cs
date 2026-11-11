using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

/// <summary>주문 조각 전투의 카드, 제작판과 턴 상태를 관리한다.</summary>
public sealed class SpellGame
{
    private const int HAMMER_USES_PER_TURN = 1;
    private const int MAX_HAND_SIZE = 7;
    private const float MAX_DECOMPOSITION_CHANCE = 0.9f;
    private const int GLUE_USES_PER_TURN = 1;

    public enum Shape { Square, Triangle, Rectangle, QuarterTriangle }
    public enum Effect { None, Flame, Guard, Resonance, Afterimage }
    public enum Phase { Title, Combat, Reward, Victory, Defeat }

    public sealed class Card
    {
        public int Id { get; internal set; }
        public Shape Shape { get; internal set; }
        public Effect Effect { get; internal set; }
        public bool HasPersisted { get; internal set; }
        public GluedShape Glued { get; internal set; }
    }

    public sealed class GluedShape
    {
        private readonly int[] _cells;

        public Card First { get; }
        public Card Second { get; }
        public int FirstRotation { get; }
        public int SecondRotation { get; }
        public int RelativeX { get; }
        public int RelativeY { get; }
        public int Width { get; }
        public int Height { get; }

        /// <summary>두 원본과 상대 배치, 정규화한 전체 점유 영역을 변경 불가능한 접착 정보로 보관한다.</summary>
        internal GluedShape(Card first, int firstRotation, Card second, int secondRotation, int relativeX, int relativeY)
        {
            First = first; Second = second; FirstRotation = firstRotation; SecondRotation = secondRotation;
            RelativeX = relativeX; RelativeY = relativeY;
            int left = Math.Min(0, relativeX), top = Math.Min(0, relativeY);
            Width = Math.Max(ShapeWidth(first, firstRotation), relativeX + ShapeWidth(second, secondRotation)) - left;
            Height = Math.Max(ShapeHeight(first, firstRotation), relativeY + ShapeHeight(second, secondRotation)) - top;
            _cells = new int[Width * Height];
            for (int y = 0; y < Height; y++) for (int x = 0; x < Width; x++)
                _cells[y * Width + x] = CellMask(first, firstRotation, x + left, y + top)
                    | CellMask(second, secondRotation, x + left - relativeX, y + top - relativeY);
        }

        /// <summary>복합 도형 전체를 90도 단위로 회전한 지정 칸의 삼각 구역을 반환한다.</summary>
        internal int RotatedCell(int rotation, int x, int y)
        {
            int sx = rotation == 0 ? x : rotation == 1 ? y : rotation == 2 ? Width - 1 - x : Width - 1 - y;
            int sy = rotation == 0 ? y : rotation == 1 ? Height - 1 - x : rotation == 2 ? Height - 1 - y : x;
            if (sx < 0 || sy < 0 || sx >= Width || sy >= Height) return 0;
            int bits = _cells[sy * Width + sx];
            return ((bits << rotation) | (bits >> (4 - rotation))) & 15;
        }
    }

    public sealed class Placement
    {
        public Card Card { get; internal set; }
        public int X { get; internal set; }
        public int Y { get; internal set; }
        public int Rotation { get; internal set; }
        public int Turn { get; internal set; }
        public bool HasPersisted { get { return Card.HasPersisted; } }
        public bool CanRefund { get; internal set; }
    }

    public sealed class Board
    {
        private readonly List<Placement> _pieces = new List<Placement>();

        public int Spell { get; }
        public ReadOnlyCollection<Placement> Pieces { get; }
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

        public string Name { get; internal set; }
        public int Health { get; internal set; }
        public int MaxHealth { get; internal set; }
        public bool IsSealer { get; }
        public bool IsThief { get; }
        public int SealedSpell { get; internal set; } = -1;
        internal int PreviousSealedSpell { get; set; } = -1;

        public int Intent { get { return _pattern[ActionCount % _pattern.Length]; } }
        internal int ActionCount;

        /// <summary>이름, 체력, 공격 패턴과 봉인자·도둑손 여부로 적의 전투 상태를 초기화한다.</summary>
        internal Enemy(string name, int health, int[] pattern, bool isSealer = false, bool isThief = false)
        {
            Name = name; Health = health; MaxHealth = health; _pattern = pattern; IsSealer = isSealer; IsThief = isThief;
        }
    }

    private static readonly int[][] SPELL_MASKS = { new[] { 4, 0, 0, 15, 0, 0, 0, 0, 0 }, new[] { 15, 0, 0, 1, 0, 0, 0, 0, 0 }, new[] { 15, 0, 0, 15, 15, 0, 0, 0, 0 }, new[] { 15, 15, 0, 15, 15, 0, 0, 0, 0 } };
    private readonly Random _random = new Random();
    private readonly List<Card> _owned = new List<Card>();
    private readonly List<Card> _draw = new List<Card>();
    private readonly List<Card> _discard = new List<Card>();
    private int _nextId;
    private readonly List<Card> _hand = new List<Card>();
    private readonly List<Enemy> _enemies = new List<Enemy>();
    private readonly List<int> _spells = new List<int>();
    private readonly Board[] _boards = { new Board(0), new Board(1), new Board(2), new Board(3) };
    private bool _hasSealerEncounter;
    private bool _hasThiefEncounter;
    private readonly List<Card> _stolen = new List<Card>();
    private Enemy _theftOwner;

    public Card TheftTarget { get; private set; }
    public int StolenCount { get { return _stolen.Count; } }
    public bool HasLivingThief
    {
        get { foreach (Enemy enemy in Enemies) if (enemy.IsThief && enemy.Health > 0) return true; return false; }
    }

    public ReadOnlyCollection<Card> Hand { get; }
    public ReadOnlyCollection<Enemy> Enemies { get; }
    public ReadOnlyCollection<int> Spells { get; }
    public ReadOnlyCollection<Board> Boards { get; }
    public Phase CurrentPhase { get; private set; } = Phase.Title;
    public int Health { get; private set; }
    public int Block { get; private set; }
    public int Battle { get; private set; }
    public int Turn { get; private set; }
    public int PlacementsLeft { get; private set; }
    public int HammerUsesLeft { get; private set; }
    public int GlueUsesLeft { get; private set; }
    public string SealNotice { get; private set; } = "";
    public int Target { get; private set; }
    public int DrawCount { get { return _draw.Count; } }
    public int DiscardCount { get { return _discard.Count; } }
    public int CraftCount { get { int count = 0; foreach (Board board in Boards) count += board.Pieces.Count; return count; } }

    /// <summary>카드, 적, 도안과 제작판의 읽기 전용 목록을 연결한다.</summary>
    public SpellGame()
    {
        Hand = _hand.AsReadOnly(); Enemies = _enemies.AsReadOnly(); Spells = _spells.AsReadOnly(); Boards = Array.AsReadOnly(_boards);
    }

    /// <summary>체력·카드·도안을 초기화하고 선택한 신규 적 포함 여부로 첫 전투를 시작한다.</summary>
    public void StartGame(bool withSealer = false, bool withThief = false)
    {
        _hasSealerEncounter = withSealer;
        _hasThiefEncounter = withThief;
        _owned.Clear(); _spells.Clear(); _nextId = 0; Health = 50; Battle = 0;
        _spells.Add(0); _spells.Add(1);
        for (int i = 0; i < 4; i++) AddCard(Shape.Square, Effect.None);
        for (int i = 0; i < 4; i++) AddCard(Shape.Triangle, Effect.None);
        for (int i = 0; i < 2; i++) AddCard(Shape.Rectangle, Effect.None);
        for (int i = 0; i < 4; i++) AddCard(Shape.QuarterTriangle, Effect.None);
        StartBattle();
    }

    /// <summary>현재 게임을 종료하고 타이틀 화면 상태로 전환한다.</summary>
    public void ReturnToTitle() { FinishCombat(Phase.Title); }

    /// <summary>개별 ID를 가진 카드를 소유 목록에 추가한다.</summary>
    private void AddCard(Shape shape, Effect effect)
    {
        _owned.Add(new Card { Id = ++_nextId, Shape = shape, Effect = effect });
    }

    /// <summary>다음 전투의 적과 덱을 준비하고 첫 턴을 시작한다.</summary>
    private void StartBattle()
    {
        CancelTheft(); _stolen.Clear();
        Battle++; Turn = 0; Block = 0; Target = 0; _enemies.Clear(); _hand.Clear(); _discard.Clear(); _draw.Clear();
        foreach (Board board in Boards) board.MutablePieces.Clear();
        foreach (Card card in _owned) card.HasPersisted = false;
        _draw.AddRange(_owned); Shuffle(_draw);
        GuaranteeOpeningShape(Shape.Square, _draw.Count - 1); GuaranteeOpeningShape(Shape.QuarterTriangle, _draw.Count - 2);
        if (Battle == 1) AddEnemy(0);
        if (Battle == 2) { AddEnemy(0); AddEnemy(0); }
        if (Battle == 3) AddEnemy(1);
        if (Battle == 4) { AddEnemy(1); AddEnemy(0); }
        if (Battle == 5) { AddEnemy(1); AddEnemy(1); }
        if (Battle == 6) AddEnemy(2);
        if (_hasSealerEncounter && Battle == 1) _enemies.Add(new Enemy("형상 봉인자", 12, new[] { 4 }, true));
        if (_hasThiefEncounter && Battle == 1) _enemies.Add(new Enemy("도둑손", 12, new[] { 4 }, false, true));
        CurrentPhase = Phase.Combat; StartTurn();
        RefreshSeals();
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

    /// <summary>새 턴에 배치 횟수를 갱신하고 카드를 뽑는다.</summary>
    private void StartTurn() { Turn++; PlacementsLeft = 3; HammerUsesLeft = HAMMER_USES_PER_TURN; GlueUsesLeft = GLUE_USES_PER_TURN; Draw(5); }

    /// <summary>손패 카드의 망치 대상 조건과 한 단계 분해 공간을 확인해 거부 사유를 반환한다.</summary>
    public string HammerIssue(Card card)
    {
        if (CurrentPhase != Phase.Combat) return "플레이어 턴에만 망치를 사용할 수 있습니다";
        if (HammerUsesLeft <= 0) return "이번 턴의 망치를 이미 사용했습니다";
        if (card == null || !Hand.Contains(card)) return "손패의 조각 하나를 선택하세요";
        if (card.Glued == null && card.Shape == Shape.QuarterTriangle) return "1/4 삼각형은 가장 작은 조각이라 분해할 수 없습니다";
        if (Hand.Count >= MAX_HAND_SIZE) return "손패가 가득 찼습니다. 조각을 배치해 한 칸 이상 비워 주세요";
        return null;
    }

    /// <summary>도형의 한 단계 작은 기존 도형을 반환하며 1/4 삼각형은 그대로 유지한다.</summary>
    private static Shape SmallerShape(Shape shape)
    {
        return shape == Shape.Rectangle ? Shape.Square : shape == Shape.Square ? Shape.Triangle : Shape.QuarterTriangle;
    }

    /// <summary>강도 0~1에서 첫 분해 이후 자식의 추가 분해 확률을 반환한다.</summary>
    public static float DecompositionChance(float strength)
    {
        if (float.IsNaN(strength)) throw new ArgumentOutOfRangeException(nameof(strength));
        return Math.Max(0, Math.Min(1, strength)) * MAX_DECOMPOSITION_CHANCE;
    }

    /// <summary>가능한 원본은 반드시 한 번 나누고 강도로 추가 분해를 추첨해 혼합 도형을 반환한다. 카드·전투·덱 상태는 변경하지 않는다.</summary>
    public static Shape[] DecomposeShape(Shape shape, float strength, int maxPieces, Random random)
    {
        if (shape < Shape.Square || shape > Shape.QuarterTriangle) throw new ArgumentOutOfRangeException(nameof(shape));
        if (maxPieces < 1) throw new ArgumentOutOfRangeException(nameof(maxPieces));
        if (random == null) throw new ArgumentNullException(nameof(random));
        float chance = DecompositionChance(strength);
        List<Shape> pieces = new List<Shape>();
        if (shape == Shape.QuarterTriangle || maxPieces == 1) return new[] { shape };
        int splitsLeft = maxPieces - 2;
        Shape smaller = SmallerShape(shape);
        SplitShape(smaller, chance, random, pieces, ref splitsLeft);
        SplitShape(smaller, chance, random, pieces, ref splitsLeft);
        return pieces.ToArray();
    }

    /// <summary>남은 분할 수 안에서 각 자식의 추가 분해를 추첨해 면적이 보존된 최종 도형을 누적한다.</summary>
    private static void SplitShape(Shape shape, float chance, Random random, List<Shape> pieces, ref int splitsLeft)
    {
        if (shape == Shape.QuarterTriangle || splitsLeft == 0 || random.NextDouble() >= chance)
        {
            pieces.Add(shape); return;
        }
        splitsLeft--;
        Shape smaller = SmallerShape(shape);
        SplitShape(smaller, chance, random, pieces, ref splitsLeft);
        SplitShape(smaller, chance, random, pieces, ref splitsLeft);
    }

    /// <summary>현재 손패에서 원본을 교체할 때 허용되는 최대 결과 수를 반환한다. 대상이 무효하면 0이다.</summary>
    public int HammerMaxPieces(Card card)
    {
        if (HammerIssue(card) != null) return 0;
        int smallestPieces = card.Glued == null ? SmallestPieceCount(card.Shape)
            : SmallestPieceCount(card.Glued.First.Shape) + SmallestPieceCount(card.Glued.Second.Shape);
        return Math.Min(smallestPieces, MAX_HAND_SIZE - Hand.Count + 1);
    }

    /// <summary>기본 도형을 모두 1/4까지 나눴을 때 면적에 해당하는 최소 조각 수를 반환한다.</summary>
    private static int SmallestPieceCount(Shape shape)
    {
        return shape == Shape.Rectangle ? 8 : shape == Shape.Square ? 4 : shape == Shape.Triangle ? 2 : 1;
    }

    /// <summary>공통 도형 분해 결과로 전투 손패를 교체하고 망치 횟수를 소비한다. 무효 대상은 변경하지 않는다.</summary>
    public bool TryHammer(Card card, float strength, out Shape[] resultShapes)
    {
        resultShapes = Array.Empty<Shape>();
        int maxPieces = HammerMaxPieces(card);
        if (maxPieces == 0) return false;
        List<Card> results = new List<Card>();
        if (card.Glued == null)
        {
            resultShapes = DecomposeShape(card.Shape, strength, maxPieces, _random);
            AddHammerCards(card, resultShapes, card.HasPersisted, results);
        }
        else
        {
            float chance = DecompositionChance(strength);
            List<Shape> firstPieces = new List<Shape>(), secondPieces = new List<Shape>();
            int splitsLeft = maxPieces - 2;
            SplitShape(card.Glued.First.Shape, chance, _random, firstPieces, ref splitsLeft);
            SplitShape(card.Glued.Second.Shape, chance, _random, secondPieces, ref splitsLeft);
            AddHammerCards(card.Glued.First, firstPieces, card.HasPersisted, results);
            AddHammerCards(card.Glued.Second, secondPieces, card.HasPersisted, results);
            resultShapes = new Shape[results.Count];
            for (int i = 0; i < results.Count; i++) resultShapes[i] = results[i].Shape;
        }
        HammerUsesLeft--;
        if (resultShapes.Length == 1) return true;
        int index = _hand.IndexOf(card);
        _hand.RemoveAt(index);
        _hand.InsertRange(index, results);
        return true;
    }

    /// <summary>원본별 가장 큰 결과 한 장에만 효과와 잔상 이력을 남긴 카드를 생성한다. 최대 크기 동률은 무작위로 고른다.</summary>
    private void AddHammerCards(Card source, IReadOnlyList<Shape> shapes, bool hasPersisted, List<Card> results)
    {
        int effectIndex = 0, largest = 0, ties = 0;
        if (source.Effect != Effect.None)
        {
            for (int i = 0; i < shapes.Count; i++)
            {
                int area = SmallestPieceCount(shapes[i]);
                if (area > largest) { largest = area; effectIndex = i; ties = 1; }
                else if (area == largest && _random.Next(++ties) == 0) effectIndex = i;
            }
        }
        for (int i = 0; i < shapes.Count; i++)
        {
            Effect effect = i == effectIndex ? source.Effect : Effect.None;
            results.Add(new Card { Id = ++_nextId, Shape = shapes[i], Effect = effect, HasPersisted = effect == Effect.Afterimage && hasPersisted });
        }
    }

    /// <summary>접착 카드에서는 두 원본의 효과 수를 합산하고 기본 카드에서는 해당 효과 보유 수를 반환한다.</summary>
    public static int EffectCount(Card card, Effect effect)
    {
        if (effect == Effect.None) return 0;
        return card.Glued == null ? (card.Effect == effect ? 1 : 0)
            : EffectCount(card.Glued.First, effect) + EffectCount(card.Glued.Second, effect);
    }

    /// <summary>손패의 기본 카드인지 확인해 접착 선택 거부 사유를 반환한다. 특수 효과는 허용한다.</summary>
    public string GlueCardIssue(Card card)
    {
        if (card == null || !Hand.Contains(card)) return "손패의 조각을 선택하세요";
        if (card.Glued != null) return "접착된 카드는 다시 접착할 수 없습니다";
        return null;
    }

    /// <summary>두 손패 조각의 중복·겹침과 실제 삼각 구역의 변 접촉을 검사해 접착 거부 사유를 반환한다.</summary>
    public string GlueIssue(Card first, int firstRotation, Card second, int secondRotation, int relativeX, int relativeY)
    {
        if (CurrentPhase != Phase.Combat) return "플레이어 행동 중에만 접착할 수 있습니다";
        if (GlueUsesLeft <= 0) return "이번 턴의 접착을 이미 사용했습니다";
        string issue = GlueCardIssue(first) ?? GlueCardIssue(second);
        if (issue != null) return issue;
        if (first == second) return "서로 다른 조각 두 장을 선택하세요";
        if (relativeX < -4 || relativeX > 4 || relativeY < -4 || relativeY > 4) return "조각 사이에 틈이 있습니다";
        bool touching = false;
        for (int y = 0; y < ShapeHeight(first, firstRotation); y++) for (int x = 0; x < ShapeWidth(first, firstRotation); x++)
        {
            int a = CellMask(first, firstRotation, x, y);
            int b = CellMask(second, secondRotation, x - relativeX, y - relativeY);
            if ((a & b) != 0) return "두 조각이 겹칩니다";
            int adjacent = ((a << 1) | (a >> 3) | (a >> 1) | (a << 3)) & 15;
            if ((adjacent & b) != 0) touching = true;
            for (int side = 0; side < 4; side++)
            {
                if ((a & (1 << side)) == 0) continue;
                int nx = x + (side == 1 ? 1 : side == 3 ? -1 : 0);
                int ny = y + (side == 2 ? 1 : side == 0 ? -1 : 0);
                if ((CellMask(second, secondRotation, nx - relativeX, ny - relativeY) & (1 << ((side + 2) % 4))) != 0) touching = true;
            }
        }
        return touching ? null : "실제 도형의 변을 맞대세요. 꼭짓점만 닿으면 접착할 수 없습니다";
    }

    /// <summary>확인 시 원본과 접촉을 재검증한 뒤 일반 카드 두 장을 전투 한정 복합 카드 한 장으로 교체한다.</summary>
    public bool TryGlue(Card first, int firstRotation, Card second, int secondRotation, int relativeX, int relativeY)
    {
        if (GlueIssue(first, firstRotation, second, secondRotation, relativeX, relativeY) != null) return false;
        GluedShape glued = new GluedShape(first, (firstRotation % 4 + 4) % 4, second, (secondRotation % 4 + 4) % 4, relativeX, relativeY);
        Card result = new Card { Id = _nextId + 1, Shape = first.Shape, Effect = Effect.None, Glued = glued,
            HasPersisted = (EffectCount(first, Effect.Afterimage) > 0 && first.HasPersisted) || (EffectCount(second, Effect.Afterimage) > 0 && second.HasPersisted) };
        int index = Math.Min(_hand.IndexOf(first), _hand.IndexOf(second));
        _hand.Remove(first); _hand.Remove(second); _hand.Insert(index, result);
        _nextId++; GlueUsesLeft--; return true;
    }

    /// <summary>전투 임시 카드와 배치를 정리하고 보상 카드를 포함한 소유 덱을 복구해 지정 화면으로 전환한다.</summary>
    private void FinishCombat(Phase phase)
    {
        CancelTheft(); _stolen.Clear();
        foreach (Enemy enemy in Enemies) { enemy.SealedSpell = -1; enemy.PreviousSealedSpell = -1; }
        SealNotice = "";
        _hand.Clear(); _draw.Clear(); _discard.Clear();
        foreach (Board board in Boards) board.MutablePieces.Clear();
        foreach (Card card in _owned) card.HasPersisted = false;
        _draw.AddRange(_owned);
        CurrentPhase = phase;
    }

    /// <summary>손패 한도를 지키며 지정 수만큼 카드를 뽑는다.</summary>
    private void Draw(int count)
    {
        while (count-- > 0 && Hand.Count < MAX_HAND_SIZE)
        {
            if (_draw.Count == 0) { _draw.AddRange(_discard); _discard.Clear(); Shuffle(_draw); }
            if (_draw.Count == 0) break;
            int last = _draw.Count - 1; _hand.Add(_draw[last]); _draw.RemoveAt(last);
        }
    }

    /// <summary>카드를 버림 더미로 보내고 다음 순환을 위해 잔상 시전 이력을 초기화한다.</summary>
    private void Discard(Card card)
    {
        if (TheftTarget == card) CancelTheft();
        card.HasPersisted = false;
        _discard.Add(card);
    }

    /// <summary>손패를 버리고 생존 적의 공격을 순서대로 처리한다.</summary>
    public void EndTurn()
    {
        if (CurrentPhase != Phase.Combat) return;
        SealNotice = "";
        foreach (Card card in Hand) Discard(card);
        _hand.Clear();
        foreach (Enemy enemy in Enemies)
        {
            if (enemy.Health <= 0) continue;
            int damage = enemy.Intent; enemy.ActionCount++; int absorbed = Math.Min(Block, damage); Block -= absorbed; Health -= damage - absorbed;
            if (Health <= 0) { Health = 0; FinishCombat(Phase.Defeat); return; }
        }
        Block = 0; RefreshSeals(); StartTurn();
    }

    /// <summary>생존 봉인자가 지정한 고유 주문 ID를 봉인 중인지 반환한다.</summary>
    public bool IsSealed(int spell)
    {
        foreach (Enemy enemy in Enemies) if (enemy.IsSealer && enemy.Health > 0 && enemy.SealedSpell == spell) return true;
        return false;
    }

    /// <summary>전투 시작과 턴 종료에 생존 봉인자의 대상을 소유 도안에서 고른다. 도안이 여럿이면 직전 대상을 제외한다.</summary>
    private void RefreshSeals()
    {
        SealNotice = "";
        foreach (Enemy enemy in Enemies)
        {
            if (!enemy.IsSealer) continue;
            enemy.SealedSpell = -1;
            if (enemy.Health <= 0 || Spells.Count == 0) continue;
            int previousIndex = Spells.IndexOf(enemy.PreviousSealedSpell);
            int index = _random.Next(Spells.Count - (Spells.Count > 1 && previousIndex >= 0 ? 1 : 0));
            if (Spells.Count > 1 && previousIndex >= 0 && index >= previousIndex) index++;
            enemy.SealedSpell = Spells[index]; enemy.PreviousSealedSpell = enemy.SealedSpell;
            SealNotice = "형상 봉인자가 주문 도안을 봉인했습니다. 피해를 주면 해제됩니다";
        }
    }

    /// <summary>실제 HP 피해를 적용하고 피해 받은 봉인자의 현재 봉인만 해제한다. 피해 0과 다른 적의 피격은 영향을 주지 않는다.</summary>
    private void DamageEnemy(Enemy enemy, int damage)
    {
        if (enemy.Health <= 0 || damage <= 0) return;
        int before = enemy.Health;
        enemy.Health -= damage;
        if (enemy == _theftOwner && enemy.Health <= 0) CancelTheft();
        if (enemy.IsSealer && enemy.Health < before && enemy.SealedSpell >= 0)
        {
            enemy.SealedSpell = -1;
            SealNotice = "형상 봉인자가 피해를 받아 봉인이 해제되었습니다!";
        }
    }

    /// <summary>지정 도형의 격자 점유 마스크를 계산한다. 범위 밖이면 null을 반환한다.</summary>
    private static int[] Mask(Card card, int x, int y, int rotation)
    {
        int width = ShapeWidth(card, rotation), height = ShapeHeight(card, rotation);
        if (x < 0 || y < 0 || x + width > 3 || y + height > 3) return null;
        int[] mask = new int[9];
        for (int cy = 0; cy < height; cy++) for (int cx = 0; cx < width; cx++) mask[(y + cy) * 3 + x + cx] = CellMask(card, rotation, cx, cy);
        return mask;
    }

    /// <summary>카드 전체를 회전한 뒤 점유하는 격자 너비를 반환한다.</summary>
    public static int ShapeWidth(Card card, int rotation)
    {
        bool horizontal = (rotation & 1) == 0;
        return card.Glued != null ? (horizontal ? card.Glued.Width : card.Glued.Height) : card.Shape == Shape.Rectangle && horizontal ? 2 : 1;
    }

    /// <summary>카드 전체를 회전한 뒤 점유하는 격자 높이를 반환한다.</summary>
    public static int ShapeHeight(Card card, int rotation)
    {
        bool horizontal = (rotation & 1) == 0;
        return card.Glued != null ? (horizontal ? card.Glued.Height : card.Glued.Width) : card.Shape == Shape.Rectangle && !horizontal ? 2 : 1;
    }

    /// <summary>기본·복합 카드의 회전 후 로컬 좌표에서 실제로 점유하는 삼각 구역을 반환한다.</summary>
    public static int CellMask(Card card, int rotation, int x, int y)
    {
        rotation = (rotation % 4 + 4) % 4;
        if (x < 0 || y < 0 || x >= ShapeWidth(card, rotation) || y >= ShapeHeight(card, rotation)) return 0;
        if (card.Glued != null) return card.Glued.RotatedCell(rotation, x, y);
        return card.Shape == Shape.QuarterTriangle ? 1 << rotation : card.Shape == Shape.Triangle ? (1 << rotation) | (1 << ((rotation + 1) % 4)) : 15;
    }

    /// <summary>기존 조각과 도안 영역을 고려해 배치 가능 여부를 반환한다.</summary>
    public bool CanPlace(int boardIndex, Card card, int x, int y, int rotation, Placement ignored = null)
    {
        return PlacementIssue(boardIndex, card, x, y, rotation, ignored) == null;
    }

    /// <summary>배치가 불가능한 이유를 반환하고, 가능하면 null을 반환한다.</summary>
    public string PlacementIssue(int boardIndex, Card card, int x, int y, int rotation, Placement ignored = null)
    {
        if (CurrentPhase != Phase.Combat || boardIndex < 0 || boardIndex >= Boards.Count || card == null) return "지금은 배치할 수 없습니다";
        if (!Spells.Contains(Boards[boardIndex].Spell)) return "보유하지 않은 도안입니다";
        if (IsSealed(Boards[boardIndex].Spell)) return "봉인된 도안에는 조각을 놓을 수 없습니다";
        Board board = Boards[boardIndex]; int[] candidate = Mask(card, x, y, rotation);
        if (candidate == null) return "도형이 도안 범위를 벗어납니다";
        int[] allowed = SPELL_MASKS[board.Spell];
        foreach (Placement piece in board.Pieces)
        {
            if (piece == ignored) continue;
            int[] used = Mask(piece.Card, piece.X, piece.Y, piece.Rotation);
            for (int i = 0; i < 9; i++) if ((candidate[i] & used[i]) != 0) return "다른 조각과 겹칩니다";
        }
        for (int i = 0; i < 9; i++) if ((candidate[i] & ~allowed[i]) != 0) return "도안 밖으로 벗어납니다";
        return null;
    }

    /// <summary>배치가 도안을 완성하는지 반환한다.</summary>
    public bool WouldComplete(int boardIndex, Card card, int x, int y, int rotation, Placement ignored = null)
    {
        if (!CanPlace(boardIndex, card, x, y, rotation, ignored)) return false;
        int[] filled = Mask(card, x, y, rotation);
        foreach (Placement piece in Boards[boardIndex].Pieces)
        {
            if (piece == ignored) continue;
            int[] used = Mask(piece.Card, piece.X, piece.Y, piece.Rotation);
            for (int i = 0; i < 9; i++) filled[i] |= used[i];
        }
        for (int i = 0; i < 9; i++) if (filled[i] != SPELL_MASKS[Boards[boardIndex].Spell][i]) return false;
        return true;
    }

    /// <summary>손패 카드를 도안에 배치하고 배치 횟수를 소비한다.</summary>
    public bool Place(int board, Card card, int x, int y, int rotation)
    {
        if (PlacementsLeft <= 0 || !Hand.Contains(card) || !CanPlace(board, card, x, y, rotation)) return false;
        _hand.Remove(card); PlacementsLeft--; Boards[board].MutablePieces.Add(new Placement { Card = card, X = x, Y = y, Rotation = rotation, Turn = Turn, CanRefund = true }); return true;
    }

    /// <summary>배치된 조각을 비용 없이 다른 위치 또는 판으로 옮긴다.</summary>
    public bool Move(int source, Placement piece, int destination, int x, int y, int rotation)
    {
        if (source < 0 || source >= Boards.Count || piece == null || !Boards[source].Pieces.Contains(piece) || !CanPlace(destination, piece.Card, x, y, rotation, source == destination ? piece : null)) return false;
        Boards[source].MutablePieces.Remove(piece); piece.X = x; piece.Y = y; piece.Rotation = rotation; Boards[destination].MutablePieces.Add(piece); return true;
    }

    /// <summary>배치 조각을 손패로 회수하고 이번 턴의 미시전 배치 비용만 반환한다.</summary>
    public bool Recall(int board, Placement piece)
    {
        if (CurrentPhase != Phase.Combat || board < 0 || board >= Boards.Count || piece == null || Hand.Count >= MAX_HAND_SIZE || !Boards[board].MutablePieces.Remove(piece)) return false;
        if (TheftTarget == piece.Card) CancelTheft();
        _hand.Add(piece.Card); if (piece.Turn == Turn && piece.CanRefund) PlacementsLeft++; return true;
    }

    /// <summary>판의 현재 턴 조각은 회수하고 이전 턴 조각은 버린다.</summary>
    public bool Abandon(int board)
    {
        if (CurrentPhase != Phase.Combat || board < 0 || board >= Boards.Count) return false;
        Board b = Boards[board]; int current = 0; foreach (Placement p in b.Pieces) if (p.Turn == Turn) current++;
        if (Hand.Count + current > MAX_HAND_SIZE) return false;
        foreach (Placement p in b.Pieces)
        {
            if (TheftTarget == p.Card) CancelTheft();
            if (p.Turn == Turn) { _hand.Add(p.Card); if (p.CanRefund) PlacementsLeft++; }
            else Discard(p.Card);
        }
        b.MutablePieces.Clear(); return true;
    }

    /// <summary>살아 있는 적을 단일 대상 주문의 대상으로 선택한다.</summary>
    public void SelectTarget(int index) { if (index >= 0 && index < Enemies.Count && Enemies[index].Health > 0) Target = index; }

    /// <summary>생존 도둑손이 있을 때 실제 배치 카드 중 하나를 균등 무작위로 골라 절도 대상으로 보관한다.</summary>
    public bool BeginTheft()
    {
        if (CurrentPhase != Phase.Combat || TheftTarget != null || CraftCount == 0) return false;
        Enemy owner = null;
        foreach (Enemy enemy in Enemies) if (enemy.IsThief && enemy.Health > 0) { owner = enemy; break; }
        if (owner == null) return false;
        int index = _random.Next(CraftCount);
        foreach (Board board in Boards) foreach (Placement piece in board.Pieces)
        {
            if (index-- != 0) continue;
            _theftOwner = owner; TheftTarget = piece.Card; return true;
        }
        return false;
    }

    /// <summary>현재 절도 대상이 여전히 배치되어 있는지 확인해 해당 도안 ID와 현재 배치 정보를 반환한다.</summary>
    public bool TryGetTheftPlacement(out int boardIndex, out Placement placement)
    {
        boardIndex = -1; placement = null;
        if (CurrentPhase != Phase.Combat || TheftTarget == null || _theftOwner == null || _theftOwner.Health <= 0) return false;
        for (int i = 0; i < Boards.Count; i++) foreach (Placement piece in Boards[i].Pieces)
        {
            if (piece.Card != TheftTarget) continue;
            boardIndex = i; placement = piece; return true;
        }
        return false;
    }

    /// <summary>절도 시도를 취소하며 대상 카드와 배치 횟수는 변경하지 않는다.</summary>
    public void CancelTheft() { TheftTarget = null; _theftOwner = null; }

    /// <summary>도착 시 대상을 재검증하고 배치에서 전투 내 절도 보관소로 옮긴다. 배치 횟수는 환불하지 않는다.</summary>
    public bool ResolveTheft()
    {
        int board; Placement piece;
        if (!TryGetTheftPlacement(out board, out piece)) { CancelTheft(); return false; }
        Boards[board].MutablePieces.Remove(piece);
        _stolen.Add(piece.Card);
        CancelTheft(); return true;
    }

    /// <summary>지정 도안이 모든 삼각 구역을 빈틈없이 채웠는지 반환한다.</summary>
    public bool IsComplete(int boardIndex)
    {
        if (boardIndex < 0 || boardIndex >= Boards.Count || !Spells.Contains(boardIndex)) return false;
        int[] filled = new int[9];
        foreach (Placement piece in Boards[boardIndex].Pieces)
        {
            int[] mask = Mask(piece.Card, piece.X, piece.Y, piece.Rotation);
            for (int i = 0; i < 9; i++) filled[i] |= mask[i];
        }
        for (int i = 0; i < 9; i++) if (filled[i] != SPELL_MASKS[Boards[boardIndex].Spell][i]) return false;
        return true;
    }

    /// <summary>도안의 지정 칸에 필요한 상·우·하·좌 삼각 구역을 반환한다.</summary>
    public static int PatternMask(int spell, int x, int y)
    {
        return spell >= 0 && spell < SPELL_MASKS.Length && x >= 0 && x < 3 && y >= 0 && y < 3 ? SPELL_MASKS[spell][y * 3 + x] : 0;
    }

    /// <summary>완성된 주문을 지정 대상에 시전하고 효과와 승리 및 카드 소비를 처리한다.</summary>
    public bool Cast(int boardIndex, int target)
    {
        if (CurrentPhase != Phase.Combat || !IsComplete(boardIndex) || IsSealed(boardIndex)) return false;
        if ((boardIndex == 0 || boardIndex == 2) && (target < 0 || target >= Enemies.Count || Enemies[target].Health <= 0)) return false;
        SealNotice = "";
        if (boardIndex == 0 || boardIndex == 2) Target = target;
        Board board = Boards[boardIndex]; int flame = 0, guard = 0, extraPlacements = 0;
        foreach (Placement p in board.Pieces)
        {
            flame += 2 * EffectCount(p.Card, Effect.Flame);
            guard += 3 * EffectCount(p.Card, Effect.Guard);
            if (board.Spell >= 2) extraPlacements += EffectCount(p.Card, Effect.Resonance);
        }
        int damage = board.Spell == 0 ? 6 : board.Spell == 2 ? 12 : board.Spell == 3 ? 10 : 0;
        Block += guard + (board.Spell == 1 ? 7 : 0);
        if (damage > 0)
        {
            damage += flame;
            if (board.Spell == 3) { foreach (Enemy enemy in Enemies) DamageEnemy(enemy, damage); }
            else DamageEnemy(Enemies[Target], damage);
        }
        bool alive = false; for (int i = 0; i < Enemies.Count; i++) if (Enemies[i].Health > 0) { if (!alive && Enemies[Target].Health <= 0) Target = i; alive = true; }
        PlacementsLeft += extraPlacements;
        for (int i = board.Pieces.Count - 1; i >= 0; i--)
        {
            Placement piece = board.Pieces[i];
            piece.CanRefund = false;
            if (EffectCount(piece.Card, Effect.Afterimage) > 0 && !piece.HasPersisted) piece.Card.HasPersisted = true;
            else { Discard(piece.Card); board.MutablePieces.RemoveAt(i); }
        }
        if (!alive) { if (Battle == 6) FinishCombat(Phase.Victory); else { Health = Math.Min(50, Health + 6); FinishCombat(Phase.Reward); } }
        return true;
    }

    /// <summary>보상 카드 또는 미보유 도안을 얻고 다음 전투를 시작한다. -1은 건너뛰기다.</summary>
    public bool ChooseReward(int reward)
    {
        if (CurrentPhase != Phase.Reward || reward < -1 || reward > 5 || (reward >= 4 && Spells.Contains(reward - 2))) return false;
        if (reward == 0) AddCard(Shape.Triangle, Effect.Flame);
        if (reward == 1) AddCard(Shape.Square, Effect.Guard);
        if (reward == 2) AddCard(Shape.Rectangle, Effect.Resonance);
        if (reward == 3) AddCard(Shape.QuarterTriangle, Effect.Afterimage);
        if (reward >= 4) _spells.Add(reward - 2);
        StartBattle(); return true;
    }
}

