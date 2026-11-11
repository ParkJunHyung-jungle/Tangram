using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

/// <summary>보상 후보 카드(특수 조각 또는 새 도안)의 종류, 모양, 이름을 표시하고 선택을 전달한다.</summary>
public sealed class SpellRewardCardView : MonoBehaviour
{
    private const int GRID = 3;

    [Header("구성")]
    [SerializeField] private Button _button;
    [SerializeField] private Image _stripe;
    [SerializeField] private Text _kind;
    [SerializeField] private Text _name;
    [SerializeField] private RectTransform _shapeArea;
    [SerializeField] private SpellShapeGraphic _shape;
    [SerializeField] private Image[] _patternCells;

    private int _reward;
    private Action<int> _onChoose;

    void Awake()
    {
        _button.onClick.AddListener(Choose);
    }

    /// <summary>특수 조각 보상을 표시한다.</summary>
    public void BindPiece(int reward, string displayName, SpellGame.Shape shape, Color accent, Action<int> onChoose)
    {
        Bind(reward, "특수 조각", displayName, accent, onChoose);
        _shapeArea.gameObject.SetActive(true);
        _shapeArea.sizeDelta = shape == SpellGame.Shape.Rectangle ? new Vector2(112, 56) : new Vector2(56, 56);
        _shape.Shape = shape; _shape.color = accent;
        foreach (Image cell in _patternCells) cell.gameObject.SetActive(false);
    }

    /// <summary>새 도안 보상을 표시한다. cells는 도안이 차지하는 3×3 안의 칸이다.</summary>
    public void BindPattern(int reward, string displayName, IReadOnlyList<Vector2Int> cells, Color accent, Action<int> onChoose)
    {
        Bind(reward, "새 도안", displayName, accent, onChoose);
        _shapeArea.gameObject.SetActive(false);
        for (int i = 0; i < _patternCells.Length; i++)
        {
            bool used = false;
            for (int c = 0; c < cells.Count; c++) if (cells[c].x == i % GRID && cells[c].y == i / GRID) used = true;
            _patternCells[i].gameObject.SetActive(used);
            _patternCells[i].color = accent;
        }
    }

    /// <summary>공통 문구와 강조색, 선택 동작을 설정한다.</summary>
    private void Bind(int reward, string kind, string displayName, Color accent, Action<int> onChoose)
    {
        _reward = reward; _onChoose = onChoose;
        _kind.text = kind; _name.text = displayName; _stripe.color = accent;
    }

    /// <summary>선택한 보상 번호를 전달한다.</summary>
    private void Choose() { _onChoose?.Invoke(_reward); }
}
