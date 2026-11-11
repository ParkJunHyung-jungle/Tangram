using System.Collections.Generic;

using UnityEngine;

/// <summary>조각 하나의 입체 메시, 물리 낙하와 집기·격자 고정 상태를 표시한다.</summary>
public sealed class SpellPiece3D : MonoBehaviour
{
    private Rigidbody _body;
    private MeshCollider _collider;
    private MeshRenderer _renderer;
    private Mesh _mesh;
    private Material _material;
    private Color _color;
    private SpellGame.Card _card;
    private SpellGame.Placement _placement;
    private int _board = -1;
    private int _slot = -1;
    private bool _isHand;

    public SpellGame.Card Card { get { return _card; } }
    public SpellGame.Placement Placement { get { return _placement; } }
    public int Board { get { return _board; } }
    public int Slot { get { return _slot; } }
    public bool IsHand { get { return _isHand; } }
    public Rigidbody Body { get { return _body; } }

    /// <summary>카드 모양으로 입체 메시와 볼록 충돌체, 강체 및 색을 초기화한다.</summary>
    public void Setup(SpellGame.Card card, Material material, PhysicsMaterial physicsMaterial, Color color)
    {
        _card = card; _color = color;
        _mesh = CreatePrism(Polygon(card.Shape), 0.16f);
        gameObject.AddComponent<MeshFilter>().sharedMesh = _mesh;
        _renderer = gameObject.AddComponent<MeshRenderer>();
        _material = new Material(material); _renderer.sharedMaterial = _material;
        SetTint(color);
        _collider = gameObject.AddComponent<MeshCollider>(); _collider.sharedMesh = _mesh; _collider.convex = true;
        _collider.sharedMaterial = physicsMaterial;
        _body = gameObject.AddComponent<Rigidbody>(); _body.mass = 0.35f;
        _body.interpolation = RigidbodyInterpolation.Interpolate;
        _body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        _body.constraints = RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    /// <summary>손패 위치와 축소 비율을 설정하고 실제 중력으로 낙하·반동하게 한다.</summary>
    public void DropToHand(int slot, Vector3 position, float scale, float height)
    {
        _isHand = true; _board = -1; _placement = null; _slot = slot;
        gameObject.SetActive(true); _collider.enabled = true;
        _body.isKinematic = true;
        transform.SetPositionAndRotation(position + Vector3.up * height, Quaternion.Euler(0, Random.Range(0f, 360f), 0));
        transform.localScale = Vector3.one * scale;
        _body.isKinematic = false; _body.useGravity = true;
        _body.linearVelocity = Vector3.down * 0.4f;
        _body.angularVelocity = Vector3.up * Random.Range(-1.2f, 1.2f);
        SetTint(_color);
    }

    /// <summary>논리 배치의 격자 위치·회전에 조각을 고정하고 물리 이동을 멈춘다.</summary>
    public void PlaceOnBoard(int board, SpellGame.Placement placement, Vector3 position, float scale, int rotation)
    {
        _isHand = false; _slot = -1; _board = board; _placement = placement;
        gameObject.SetActive(true); _body.isKinematic = true; _collider.enabled = true;
        transform.SetPositionAndRotation(position, Quaternion.Euler(0, rotation * 90f, 0));
        transform.localScale = Vector3.one * scale;
        SetTint(placement.HasPersisted ? _color * 0.72f : _color);
    }

    /// <summary>집은 조각을 격자 크기와 지정 회전으로 바꾸고 충돌·중력을 잠시 중단한다.</summary>
    public void Pick(float scale, int rotation)
    {
        _body.isKinematic = true; _collider.enabled = false;
        transform.localScale = Vector3.one * scale;
        transform.rotation = Quaternion.Euler(0, rotation * 90f, 0);
    }

    /// <summary>현재 조각의 물리 표시 색을 지정 색으로 갱신한다.</summary>
    public void SetTint(Color color) { _material.color = color.linear * 0.65f; }

    /// <summary>모양의 기본 0도 다각형을 칸 중심 기준 좌표로 반환한다.</summary>
    public static Vector2[] Polygon(SpellGame.Shape shape)
    {
        if (shape == SpellGame.Shape.QuarterTriangle) return new[] { new Vector2(-0.5f, -0.5f), new Vector2(0.5f, -0.5f), Vector2.zero };
        if (shape == SpellGame.Shape.Triangle) return new[] { new Vector2(-0.5f, -0.5f), new Vector2(0.5f, -0.5f), new Vector2(0.5f, 0.5f) };
        float halfWidth = shape == SpellGame.Shape.Rectangle ? 1f : 0.5f;
        return new[] { new Vector2(-halfWidth, -0.5f), new Vector2(halfWidth, -0.5f), new Vector2(halfWidth, 0.5f), new Vector2(-halfWidth, 0.5f) };
    }

    /// <summary>시계 방향 2D 다각형을 지정 두께의 입체 메시로 만들어 반환한다.</summary>
    public static Mesh CreatePrism(Vector2[] polygon, float thickness)
    {
        List<Vector3> vertices = new List<Vector3>(); List<int> triangles = new List<int>();
        for (int face = 0; face < 2; face++)
        {
            int start = vertices.Count;
            foreach (Vector2 p in polygon) vertices.Add(new Vector3(p.x, face == 0 ? thickness : 0, -p.y));
            for (int i = 1; i < polygon.Length - 1; i++)
            {
                triangles.Add(start); triangles.Add(start + (face == 0 ? i : i + 1)); triangles.Add(start + (face == 0 ? i + 1 : i));
            }
        }
        for (int i = 0; i < polygon.Length; i++)
        {
            Vector2 a = polygon[i], b = polygon[(i + 1) % polygon.Length]; int start = vertices.Count;
            vertices.Add(new Vector3(a.x, 0, -a.y)); vertices.Add(new Vector3(b.x, 0, -b.y));
            vertices.Add(new Vector3(b.x, thickness, -b.y)); vertices.Add(new Vector3(a.x, thickness, -a.y));
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
        }
        Mesh mesh = new Mesh { name = "Spell Piece Prism" };
        mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }

    void OnDestroy()
    {
        if (_mesh != null) Destroy(_mesh);
        if (_material != null) Destroy(_material);
    }
}
