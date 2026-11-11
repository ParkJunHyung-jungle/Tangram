using UnityEngine;

/// <summary>손패 카드에 제작판과 같은 칸 크기의 도형을 그린다.</summary>
public sealed class SpellShapeGraphic : UnityEngine.UI.MaskableGraphic
{
    private SpellGame.Shape _shape;
    private SpellGame.Card _card;
    private int _rotation;
    private bool _isCursed;

    public SpellGame.Shape Shape { get { return _shape; } set { _shape = value; SetVerticesDirty(); } }

    /// <summary>카드 점유 영역, 회전과 저주 여부를 연결하고 저주 도형은 빨간색으로 표시한다.</summary>
    public void SetCard(SpellGame.Card card, int rotation = 0, bool isCursed = false) { _card = card; _rotation = rotation; _isCursed = isCursed; SetVerticesDirty(); }

    /// <summary>도형 종류에 맞는 정사각형, 삼각형 또는 두 칸 직사각형을 그린다.</summary>
    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
    {
        vh.Clear();
        if (_card != null)
        {
            int columns = SpellGame.ShapeWidth(_card, _rotation), rows = SpellGame.ShapeHeight(_card, _rotation);
            float size = Mathf.Min((rectTransform.rect.width - 3) / columns, (rectTransform.rect.height - 3) / rows);
            float leftEdge = rectTransform.rect.center.x - columns * size / 2;
            float topEdge = rectTransform.rect.center.y + rows * size / 2;
            for (int y = 0; y < rows; y++) for (int x = 0; x < columns; x++)
            {
                int mask = SpellGame.CellMask(_card, _rotation, x, y);
                Vector2 origin = new Vector2(leftEdge + x * size, topEdge - y * size);
                Vector2[] corners = { origin, origin + new Vector2(size, 0), origin + new Vector2(size, -size), origin + new Vector2(0, -size) };
                for (int side = 0; side < 4; side++)
                {
                    if ((mask & (1 << side)) == 0) continue;
                    int start = vh.currentVertCount; UIVertex v = UIVertex.simpleVert; v.color = _isCursed ? Color.red : color;
                    v.position = origin + new Vector2(size / 2, -size / 2); vh.AddVert(v);
                    v.position = corners[side]; vh.AddVert(v); v.position = corners[(side + 1) % 4]; vh.AddVert(v);
                    vh.AddTriangle(start, start + 1, start + 2);
                }
            }
            return;
        }
        float width = rectTransform.rect.width - 3f;
        float height = rectTransform.rect.height - 3f;
        float left = rectTransform.rect.xMin;
        float top = rectTransform.rect.yMax;
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = new Color(0.88f, 0.91f, 0.92f) * color;
        if (_shape == SpellGame.Shape.QuarterTriangle)
        {
            vertex.position = new Vector2(left, top); vh.AddVert(vertex);
            vertex.position = new Vector2(left + width, top); vh.AddVert(vertex);
            vertex.position = new Vector2(left + width / 2, top - height / 2); vh.AddVert(vertex);
            vh.AddTriangle(0, 1, 2);
            return;
        }
        vertex.position = new Vector2(left, top); vh.AddVert(vertex);
        vertex.position = new Vector2(left + width, top); vh.AddVert(vertex);
        vertex.position = new Vector2(left + width, top - height); vh.AddVert(vertex);
        vh.AddTriangle(0, 1, 2);
        if (_shape != SpellGame.Shape.Triangle)
        {
            vertex.position = new Vector2(left, top - height); vh.AddVert(vertex);
            vh.AddTriangle(0, 2, 3);
        }
    }
}
