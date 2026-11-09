using UnityEngine;

/// <summary>손패 카드에 제작판과 같은 칸 크기의 도형을 그린다.</summary>
public sealed class SpellShapeGraphic : UnityEngine.UI.MaskableGraphic
{
    private SpellGame.Shape _shape;

    public SpellGame.Shape Shape { get { return _shape; } set { _shape = value; SetVerticesDirty(); } }

    /// <summary>도형 종류에 맞는 정사각형, 삼각형 또는 두 칸 직사각형을 그린다.</summary>
    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
    {
        vh.Clear();
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
