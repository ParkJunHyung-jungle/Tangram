using System.Collections.Generic;

using UnityEngine;

/// <summary>조각 도형이나 도안 모양을 1/4 구역 삼각형으로 영역 가운데에 맞춰 그린다.</summary>
public sealed class SpellShapeGraphic : UnityEngine.UI.MaskableGraphic
{
    private const float GAP = 1.5f;

    private readonly List<SpellGame.ShapeCell> _cells = new List<SpellGame.ShapeCell>();
    private SpellGame.Shape _shape;

    public SpellGame.Shape Shape
    {
        get { return _shape; }
        set { _shape = value; _cells.Clear(); _cells.AddRange(SpellGame.ShapeCells(value, 0)); SetVerticesDirty(); }
    }

    /// <summary>도안의 3×3 구역 마스크를 그릴 모양으로 설정한다.</summary>
    public void SetPattern(int spell)
    {
        _cells.Clear();
        for (int y = 0; y < 3; y++) for (int x = 0; x < 3; x++)
        {
            int mask = SpellGame.PatternMask(spell, x, y);
            if (mask != 0) _cells.Add(new SpellGame.ShapeCell(x, y, mask));
        }
        SetVerticesDirty();
    }

    /// <summary>칸 범위가 영역에 들어가도록 칸 크기를 정하고 켜진 구역 삼각형을 채운다.</summary>
    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
    {
        vh.Clear();
        if (_cells.Count == 0) return;
        Vector2 bounds = SpellPiece3D.CellBounds(_cells);
        Rect rect = rectTransform.rect;
        float cell = Mathf.Min(rect.width / bounds.x, rect.height / bounds.y);
        Vector2 origin = new Vector2(rect.center.x - bounds.x * cell / 2, rect.center.y + bounds.y * cell / 2);
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = new Color(0.88f, 0.91f, 0.92f) * color;
        foreach (SpellGame.ShapeCell c in _cells)
        {
            float left = origin.x + c.X * cell + GAP, right = origin.x + (c.X + 1) * cell - GAP;
            float top = origin.y - c.Y * cell - GAP, bottom = origin.y - (c.Y + 1) * cell + GAP;
            Vector2[] corners = { new Vector2(left, top), new Vector2(right, top), new Vector2(right, bottom), new Vector2(left, bottom) };
            Vector2 center = new Vector2((left + right) / 2, (top + bottom) / 2);
            for (int sector = 0; sector < 4; sector++)
            {
                if ((c.Mask & (1 << sector)) == 0) continue;
                int start = vh.currentVertCount;
                vertex.position = corners[sector]; vh.AddVert(vertex);
                vertex.position = corners[(sector + 1) % 4]; vh.AddVert(vertex);
                vertex.position = center; vh.AddVert(vertex);
                vh.AddTriangle(start, start + 1, start + 2);
            }
        }
    }
}
