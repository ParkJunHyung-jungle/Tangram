using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>공유 격자와 도안 외곽선, 조각 및 배치 미리 보기를 그린다.</summary>
public sealed class SpellBoardGraphic : UnityEngine.UI.MaskableGraphic, IPointerClickHandler, IPointerMoveHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public const float CELL_SIZE = 84f;
    private SpellGameUI _ui;
    private SpellGame _game;
    private int _hoverX = -1;
    private int _hoverY = -1;
    private int _dragPattern = -1;
    private Vector2Int _dragOffset;
    private bool _dragPiece;
    private Vector2Int _pieceOffset;
    private int _previewPattern = -1;
    private Vector2Int _previewOrigin;

    /// <summary>표시할 게임 상태를 설정한다.</summary>
    public void Setup(SpellGameUI ui, SpellGame game) { _ui = ui; _game = game; SetVerticesDirty(); }

    /// <summary>도형이 지정 칸에서 차지하는 삼각 구역 마스크를 반환한다.</summary>
    private static int CellMask(SpellGame.Card card, int x, int y, int rotation, int cellX, int cellY)
    {
        if (cellX == x && cellY == y) return card.Shape == SpellGame.Shape.QuarterTriangle ? 1 << rotation : card.Shape == SpellGame.Shape.Triangle ? (1 << rotation) | (1 << ((rotation + 1) % 4)) : 15;
        if (card.Shape == SpellGame.Shape.Rectangle && cellX == x + (rotation % 2 == 0 ? 1 : 0) && cellY == y + (rotation % 2 == 1 ? 1 : 0)) return 15;
        return 0;
    }

    /// <summary>선분에 두께를 주어 격자 경계 또는 도형 경계를 그린다.</summary>
    private static void Line(UnityEngine.UI.VertexHelper vh, Vector2 a, Vector2 b, float thickness, Color color)
    {
        Vector2 n = new Vector2(-(b - a).y, (b - a).x).normalized * thickness / 2;
        int start = vh.currentVertCount; UIVertex v = UIVertex.simpleVert; v.color = color;
        v.position = a + n; vh.AddVert(v); v.position = b + n; vh.AddVert(v); v.position = b - n; vh.AddVert(v); v.position = a - n; vh.AddVert(v);
        vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
    }

    /// <summary>3행 6열의 도안, 배치 조각 및 굵은 도안 경계를 그린다.</summary>
    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
    {
        vh.Clear(); if (_game == null) return;
        const float size = CELL_SIZE;
        int hoverBoard, hoverLocalX, hoverLocalY;
        bool hoverMapped = _ui.MapCell(_hoverX, _hoverY, out hoverBoard, out hoverLocalX, out hoverLocalY);
        bool preview = hoverMapped && _ui.SelectedCard != null;
        bool valid = preview && _ui.CanPreview(hoverBoard, hoverLocalX, hoverLocalY);
        for (int y = 0; y < 3; y++) for (int x = 0; x < 6; x++)
        {
            int board, lx, ly; bool active = _ui.MapCell(x, y, out board, out lx, out ly);
            float l = x * size, r = l + size - 3, top = -y * size, bottom = top - size + 3;
            Vector2 center = new Vector2((l + r) / 2, (top + bottom) / 2);
            Vector2[] corners = { new Vector2(l, top), new Vector2(r, top), new Vector2(r, bottom), new Vector2(l, bottom) };
            for (int sector = 0; sector < 4; sector++)
            {
                bool allowed = active && (SpellGame.PatternMask(board, lx, ly) & (1 << sector)) != 0;
                Color color = allowed ? new Color(0.31f, 0.37f, 0.42f) : new Color(0.12f, 0.15f, 0.18f);
                if (active) foreach (SpellGame.Placement p in _game.Boards[board].Pieces)
                {
                    if ((CellMask(p.Card, p.X, p.Y, p.Rotation, lx, ly) & (1 << sector)) == 0) continue;
                    color = p.Card.Effect == SpellGame.Effect.Flame ? new Color(0.85f, 0.42f, 0.2f) : p.Card.Effect == SpellGame.Effect.Guard ? new Color(0.28f, 0.55f, 0.8f) : p.Card.Effect == SpellGame.Effect.Resonance ? new Color(0.65f, 0.40f, 0.8f) : p.Card.Effect == SpellGame.Effect.Afterimage ? p.HasPersisted ? new Color(0.18f, 0.43f, 0.41f) : new Color(0.31f, 0.76f, 0.68f) : new Color(0.68f, 0.73f, 0.75f);
                }
                if (preview && board == hoverBoard && (CellMask(_ui.SelectedCard, hoverLocalX, hoverLocalY, _ui.Rotation, lx, ly) & (1 << sector)) != 0) color = valid ? new Color(0.55f, 0.87f, 0.66f) : new Color(0.9f, 0.3f, 0.3f);
                int start = vh.currentVertCount; UIVertex v = UIVertex.simpleVert; v.color = color;
                v.position = center; vh.AddVert(v); v.position = corners[sector]; vh.AddVert(v); v.position = corners[(sector + 1) % 4]; vh.AddVert(v); vh.AddTriangle(start, start + 1, start + 2);
            }
            for (int side = 0; side < 4; side++) Line(vh, center, corners[side], 1.2f, new Color(0.08f, 0.11f, 0.14f, 0.75f));
            if (!active) continue;
            int patternMask = SpellGame.PatternMask(board, lx, ly);
            for (int side = 0; side < 4; side++)
            {
                if ((patternMask & (1 << side)) == 0) continue;
                if ((patternMask & (1 << ((side + 3) % 4))) == 0) Line(vh, corners[side], center, 5, _ui.PatternColor(board));
                if ((patternMask & (1 << ((side + 1) % 4))) == 0) Line(vh, center, corners[(side + 1) % 4], 5, _ui.PatternColor(board));
                int nx = x + (side == 1 ? 1 : side == 3 ? -1 : 0), ny = y + (side == 2 ? 1 : side == 0 ? -1 : 0);
                int other, ox, oy;
                if (!_ui.MapCell(nx, ny, out other, out ox, out oy) || other != board || (SpellGame.PatternMask(other, ox, oy) & (1 << ((side + 2) % 4))) == 0)
                    Line(vh, corners[side], corners[(side + 1) % 4], 5, _ui.PatternColor(board));
            }
        }
        if (_previewPattern >= 0)
        {
            Color color = _ui.CanMovePattern(_previewPattern, _previewOrigin) ? new Color(0.45f, 1f, 0.65f) : new Color(1f, 0.3f, 0.3f);
            foreach (Vector2Int cell in _ui.PatternCells(_previewPattern))
            {
                Vector2Int pos = _previewOrigin + cell;
                if (pos.x < 0 || pos.x >= 6 || pos.y < 0 || pos.y >= 3) continue;
                float l = pos.x * size + 5, r = l + size - 13, top = -pos.y * size - 5, bottom = top - size + 13;
                Vector2 center = new Vector2((l + r) / 2, (top + bottom) / 2);
                Vector2[] corners = { new Vector2(l, top), new Vector2(r, top), new Vector2(r, bottom), new Vector2(l, bottom) };
                int mask = SpellGame.PatternMask(_previewPattern, cell.x, cell.y);
                for (int side = 0; side < 4; side++)
                {
                    if ((mask & (1 << side)) == 0) continue;
                    if ((mask & (1 << ((side + 3) % 4))) == 0) Line(vh, corners[side], center, 3, color);
                    if ((mask & (1 << ((side + 1) % 4))) == 0) Line(vh, center, corners[(side + 1) % 4], 3, color);
                    Line(vh, corners[side], corners[(side + 1) % 4], 3, color);
                }
            }
        }
    }

    /// <summary>화면 좌표를 공유 격자 좌표와 삼각 구역으로 변환한다.</summary>
    private bool GetCell(PointerEventData data, out int x, out int y, out int sector)
    {
        return GetCellAt(data, data.position, out x, out y, out sector);
    }

    /// <summary>지정 화면 좌표를 격자 칸과 삼각 구역으로 변환한다.</summary>
    private bool GetCellAt(PointerEventData data, Vector2 position, out int x, out int y, out int sector)
    {
        x = y = sector = 0; Vector2 p;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, position, data.pressEventCamera, out p)) return false;
        Rect rect = rectTransform.rect; float u = (p.x - rect.xMin) / CELL_SIZE, v = (rect.yMax - p.y) / CELL_SIZE;
        if (u < 0 || u >= 6 || v < 0 || v >= 3) return false;
        x = (int)u; y = (int)v; float dx = u - x - 0.5f, dy = v - y - 0.5f;
        sector = Mathf.Abs(dx) > Mathf.Abs(dy) ? (dx > 0 ? 1 : 3) : (dy > 0 ? 2 : 0); return true;
    }

    /// <summary>드롭 좌표가 도안에 속하면 해당 칸에 조각을 배치한다.</summary>
    public bool TryDrop(PointerEventData data)
    {
        int x, y, sector, board, lx, ly;
        if (!GetCell(data, out x, out y, out sector) || !_ui.MapCell(x, y, out board, out lx, out ly)) return false;
        _ui.ClickGrid(board, lx, ly); return true;
    }

    /// <summary>목록에서 드래그한 도안을 격자의 지정 위치로 옮긴다.</summary>
    public bool DropPattern(PointerEventData data, int pattern)
    {
        int x, y, sector; if (!GetCell(data, out x, out y, out sector)) return false;
        _ui.MovePattern(pattern, new Vector2Int(x, y)); return true;
    }

    /// <summary>드래그 중인 도안의 위치와 배치 가능 여부를 미리 표시한다.</summary>
    public void PreviewPattern(PointerEventData data, int pattern, Vector2Int offset)
    {
        int x, y, sector;
        _previewPattern = GetCell(data, out x, out y, out sector) ? pattern : -1;
        _previewOrigin = new Vector2Int(x, y) - offset; SetVerticesDirty();
    }

    /// <summary>도안 이동 미리 보기를 지운다.</summary>
    public void ClearPatternPreview() { _previewPattern = -1; SetVerticesDirty(); }

    /// <summary>현재 커서 칸에서 회전된 조각의 배치 안내와 그래픽을 즉시 갱신한다.</summary>
    public void RefreshPreview()
    {
        int board, x, y;
        if (_ui.MapCell(_hoverX, _hoverY, out board, out x, out y)) _ui.SetPreviewHint(board, x, y);
        else _ui.SetOutsidePreviewHint();
        SetVerticesDirty();
    }

    /// <summary>화면 갱신이나 비활성화 시 조각과 도안의 진행 중 드래그를 취소한다.</summary>
    public void CancelDrag()
    {
        _dragPiece = false; _dragPattern = -1; _previewPattern = -1; _hoverX = _hoverY = -1;
        SetVerticesDirty();
    }

    public void OnPointerMove(PointerEventData data)
    {
        int x, y, sector, board, lx, ly;
        if (GetCell(data, out x, out y, out sector))
        {
            if (_dragPiece) { x -= _pieceOffset.x; y -= _pieceOffset.y; }
            _hoverX = x; _hoverY = y;
            SpellGame.Card tooltip = null;
            if (_ui.MapCell(x, y, out board, out lx, out ly))
            {
                _ui.SetPreviewHint(board, lx, ly);
                if (!_dragPiece && _ui.SelectedCard == null)
                    foreach (SpellGame.Placement piece in _game.Boards[board].Pieces)
                        if (piece.Card.Effect != SpellGame.Effect.None && (CellMask(piece.Card, piece.X, piece.Y, piece.Rotation, lx, ly) & (1 << sector)) != 0) { tooltip = piece.Card; break; }
            }
            else _ui.SetOutsidePreviewHint();
            _ui.ShowPieceTooltip(tooltip, data.position);
            SetVerticesDirty();
        }
    }
    /// <summary>카드 드래그의 포인터 위치에 배치 미리 보기를 표시한다.</summary>
    public void PreviewCard(PointerEventData data)
    {
        int x, y, sector;
        if (GetCell(data, out x, out y, out sector)) OnPointerMove(data);
        else OnPointerExit(data);
    }
    public void OnPointerExit(PointerEventData data) { _hoverX = _hoverY = -1; _ui.ShowPieceTooltip(null, data.position); _ui.SetPreviewHint(-1, -1, -1); SetVerticesDirty(); }
    public void OnPointerClick(PointerEventData data)
    {
        if (data.button != PointerEventData.InputButton.Left) return;
        int x, y, sector, board, lx, ly;
        if (!GetCell(data, out x, out y, out sector)) return;
        if (!_ui.MapCell(x, y, out board, out lx, out ly)) { _ui.SetOutsidePreviewHint(); return; }
        if (_ui.SelectedCard == null)
            foreach (SpellGame.Placement p in _game.Boards[board].Pieces)
                if ((CellMask(p.Card, p.X, p.Y, p.Rotation, lx, ly) & (1 << sector)) != 0) { _ui.SelectPiece(board, p); return; }
        _ui.ClickGrid(board, lx, ly);
    }
    public void OnBeginDrag(PointerEventData data)
    {
        if (!_ui.CanInteract || !isActiveAndEnabled) return;
        if (data.button != PointerEventData.InputButton.Left) return;
        int x, y, sector, board, lx, ly;
        if (!GetCellAt(data, data.pressPosition, out x, out y, out sector) || !_ui.MapCell(x, y, out board, out lx, out ly)) return;
        foreach (SpellGame.Placement piece in _game.Boards[board].Pieces)
        {
            if ((CellMask(piece.Card, piece.X, piece.Y, piece.Rotation, lx, ly) & (1 << sector)) == 0) continue;
            _dragPiece = true; _pieceOffset = new Vector2Int(lx - piece.X, ly - piece.Y);
            _ui.BeginPieceDrag(board, piece, data.position); return;
        }
        if (_ui.SelectedCard == null) { _dragPattern = board; _dragOffset = new Vector2Int(lx, ly); }
    }
    public void OnDrag(PointerEventData data)
    {
        if (!_ui.CanInteract || !isActiveAndEnabled) return;
        if (_dragPiece) { _ui.MoveCardDrag(data.position); PreviewCard(data); }
        else if (_dragPattern >= 0) PreviewPattern(data, _dragPattern, _dragOffset);
    }
    public void OnEndDrag(PointerEventData data)
    {
        if (!_ui.CanInteract || !isActiveAndEnabled) return;
        if (_dragPiece)
        {
            _dragPiece = false;
            int gx, gy, pieceSector, board = -1, lx = -1, ly = -1;
            bool mapped = GetCell(data, out gx, out gy, out pieceSector) && _ui.MapCell(gx - _pieceOffset.x, gy - _pieceOffset.y, out board, out lx, out ly);
            _ui.EndPieceDrag(data, mapped, board, lx, ly); return;
        }
        if (_dragPattern < 0) return;
        ClearPatternPreview();
        int x, y, sector; if (GetCell(data, out x, out y, out sector)) _ui.MovePattern(_dragPattern, new Vector2Int(x, y) - _dragOffset);
        _dragPattern = -1;
    }
}
