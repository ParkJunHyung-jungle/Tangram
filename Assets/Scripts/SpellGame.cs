using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

/// <summary>주문 조각 전투의 카드, 제작판과 턴 상태를 관리한다.</summary>
public sealed class SpellGame
{
    public enum Shape { Square, Triangle, Rectangle, QuarterTriangle }
    public enum Effect { None, Flame, Guard, Resonance, Afterimage }
    public enum Phase { Title, Combat, Reward, Victory, Defeat }

    public sealed class Card
    {
        public int Id { get; internal set; }
        public Shape Shape { get; internal set; }
        public Effect Effect { get; internal set; }
        public bool HasPersisted { get; internal set; }
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

        public int Intent { get { return _pattern[ActionCount % _pattern.Length]; } }
        internal int ActionCount;

        /// <summary>이름, 체력과 공격 패턴으로 적의 전투 상태를 초기화한다.</summary>
        internal Enemy(string name, int health, int[] pattern)
        {
            Name = name; Health = health; MaxHealth = health; _pattern = pattern;
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
    public int Target { get; private set; }
    public int DrawCount { get { return _draw.Count; } }
    public int DiscardCount { get { return _discard.Count; } }
    public int CraftCount { get { int count = 0; foreach (Board board in Boards) count += board.Pieces.Count; return count; } }

    /// <summary>카드, 적, 도안과 제작판의 읽기 전용 목록을 연결한다.</summary>
    public SpellGame()
    {
        Hand = _hand.AsReadOnly(); Enemies = _enemies.AsReadOnly(); Spells = _spells.AsReadOnly(); Boards = Array.AsReadOnly(_boards);
    }

    /// <summary>새 게임의 체력, 카드 및 도안을 초기화하고 첫 전투를 시작한다.</summary>
    public void StartGame()
    {
        _owned.Clear(); _spells.Clear(); _nextId = 0; Health = 50; Battle = 0;
        _spells.Add(0); _spells.Add(1);
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

    /// <summary>새 턴에 배치 횟수를 갱신하고 카드를 뽑는다.</summary>
    private void StartTurn() { Turn++; PlacementsLeft = 3; Draw(5); }

    /// <summary>손패 한도를 지키며 지정 수만큼 카드를 뽑는다.</summary>
    private void Draw(int count)
    {
        while (count-- > 0 && Hand.Count < 7)
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

    /// <summary>지정 도형의 격자 점유 마스크를 계산한다. 범위 밖이면 null을 반환한다.</summary>
    private static int[] Mask(Shape shape, int x, int y, int rotation)
    {
        if (x < 0 || x > 2 || y < 0 || y > 2) return null;
        int[] mask = new int[9]; int index = y * 3 + x; rotation = (rotation % 4 + 4) % 4;
        mask[index] = shape == Shape.QuarterTriangle ? 1 << rotation : shape == Shape.Triangle ? (1 << rotation) | (1 << ((rotation + 1) % 4)) : 15;
        if (shape == Shape.Rectangle)
        {
            int nx = x + (rotation % 2 == 0 ? 1 : 0), ny = y + (rotation % 2 == 1 ? 1 : 0);
            if (nx > 2 || ny > 2) return null;
            mask[ny * 3 + nx] = 15;
        }
        return mask;
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
        Board board = Boards[boardIndex]; int[] candidate = Mask(card.Shape, x, y, rotation);
        if (candidate == null) return "도형이 도안 범위를 벗어납니다";
        int[] allowed = SPELL_MASKS[board.Spell];
        foreach (Placement piece in board.Pieces)
        {
            if (piece == ignored) continue;
            int[] used = Mask(piece.Card.Shape, piece.X, piece.Y, piece.Rotation);
            for (int i = 0; i < 9; i++) if ((candidate[i] & used[i]) != 0) return "다른 조각과 겹칩니다";
        }
        for (int i = 0; i < 9; i++) if ((candidate[i] & ~allowed[i]) != 0) return "도안 밖으로 벗어납니다";
        return null;
    }

    /// <summary>배치가 도안을 완성하는지 반환한다.</summary>
    public bool WouldComplete(int boardIndex, Card card, int x, int y, int rotation, Placement ignored = null)
    {
        if (!CanPlace(boardIndex, card, x, y, rotation, ignored)) return false;
        int[] filled = Mask(card.Shape, x, y, rotation);
        foreach (Placement piece in Boards[boardIndex].Pieces)
        {
            if (piece == ignored) continue;
            int[] used = Mask(piece.Card.Shape, piece.X, piece.Y, piece.Rotation);
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
        if (CurrentPhase != Phase.Combat || board < 0 || board >= Boards.Count || piece == null || Hand.Count >= 7 || !Boards[board].MutablePieces.Remove(piece)) return false;
        _hand.Add(piece.Card); if (piece.Turn == Turn && piece.CanRefund) PlacementsLeft++; return true;
    }

    /// <summary>판의 현재 턴 조각은 회수하고 이전 턴 조각은 버린다.</summary>
    public bool Abandon(int board)
    {
        if (CurrentPhase != Phase.Combat || board < 0 || board >= Boards.Count) return false;
        Board b = Boards[board]; int current = 0; foreach (Placement p in b.Pieces) if (p.Turn == Turn) current++;
        if (Hand.Count + current > 7) return false;
        foreach (Placement p in b.Pieces)
        {
            if (p.Turn == Turn) { _hand.Add(p.Card); if (p.CanRefund) PlacementsLeft++; }
            else Discard(p.Card);
        }
        b.MutablePieces.Clear(); return true;
    }

    /// <summary>살아 있는 적을 단일 대상 주문의 대상으로 선택한다.</summary>
    public void SelectTarget(int index) { if (index >= 0 && index < Enemies.Count && Enemies[index].Health > 0) Target = index; }

    /// <summary>지정 도안이 모든 삼각 구역을 빈틈없이 채웠는지 반환한다.</summary>
    public bool IsComplete(int boardIndex)
    {
        if (boardIndex < 0 || boardIndex >= Boards.Count || !Spells.Contains(boardIndex)) return false;
        int[] filled = new int[9];
        foreach (Placement piece in Boards[boardIndex].Pieces)
        {
            int[] mask = Mask(piece.Card.Shape, piece.X, piece.Y, piece.Rotation);
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
        if (CurrentPhase != Phase.Combat || !IsComplete(boardIndex)) return false;
        if ((boardIndex == 0 || boardIndex == 2) && (target < 0 || target >= Enemies.Count || Enemies[target].Health <= 0)) return false;
        if (boardIndex == 0 || boardIndex == 2) Target = target;
        Board board = Boards[boardIndex]; int flame = 0, guard = 0, extraPlacements = 0;
        foreach (Placement p in board.Pieces)
        {
            if (p.Card.Effect == Effect.Flame) flame += 2;
            if (p.Card.Effect == Effect.Guard) guard += 3;
            if (p.Card.Effect == Effect.Resonance && board.Spell >= 2) extraPlacements++;
        }
        int damage = board.Spell == 0 ? 6 : board.Spell == 2 ? 12 : board.Spell == 3 ? 10 : 0;
        Block += guard + (board.Spell == 1 ? 7 : 0);
        if (damage > 0)
        {
            damage += flame;
            if (board.Spell == 3) { foreach (Enemy enemy in Enemies) if (enemy.Health > 0) enemy.Health -= damage; }
            else Enemies[Target].Health -= damage;
        }
        bool alive = false; for (int i = 0; i < Enemies.Count; i++) if (Enemies[i].Health > 0) { if (!alive && Enemies[Target].Health <= 0) Target = i; alive = true; }
        PlacementsLeft += extraPlacements;
        for (int i = board.Pieces.Count - 1; i >= 0; i--)
        {
            Placement piece = board.Pieces[i];
            piece.CanRefund = false;
            if (piece.Card.Effect == Effect.Afterimage && !piece.HasPersisted) piece.Card.HasPersisted = true;
            else { Discard(piece.Card); board.MutablePieces.RemoveAt(i); }
        }
        if (!alive) { if (Battle == 6) CurrentPhase = Phase.Victory; else { Health = Math.Min(50, Health + 6); CurrentPhase = Phase.Reward; } }
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

