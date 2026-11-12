using System.Collections.Generic;

using UnityEngine;

/// <summary>보상 화면의 후보 카드와 건너뛰기 버튼을 보관하고, 후보 수에 맞춰 카드를 늘려 격자로 배치한다.</summary>
public sealed class SpellRewardScreen : MonoBehaviour
{
    private const float CARD_GAP = 36f;
    private const float GRID_GAP = 14f;
    private const float AREA_WIDTH = 1800f;
    private const float AREA_HEIGHT = 640f;
    private const float AREA_CENTER_Y = -75f;

    [Header("구성")]
    [SerializeField] private SpellRewardCardView[] _cards;
    [SerializeField] private SpellButtonView _skip;

    private readonly List<SpellRewardCardView> _pool = new List<SpellRewardCardView>();

    public SpellButtonView Skip { get { return _skip; } }

    /// <summary>후보 수만큼 카드를 준비해 보이게 하고 반환한다. 네 장 이하는 한 줄, 그보다 많으면 축소한 격자로 놓는다.</summary>
    public SpellRewardCardView[] ShowCards(int count)
    {
        if (_pool.Count == 0) _pool.AddRange(_cards);
        while (_pool.Count < count) _pool.Add(Instantiate(_cards[0], _cards[0].transform.parent));
        Vector2 size = ((RectTransform)_cards[0].transform).sizeDelta;
        int columns = count <= _cards.Length ? Mathf.Max(1, count) : Mathf.CeilToInt(Mathf.Sqrt(count * AREA_WIDTH / AREA_HEIGHT * size.y / size.x));
        int rows = Mathf.CeilToInt(count / (float)columns);
        float gap = count <= _cards.Length ? CARD_GAP : GRID_GAP;
        float scale = Mathf.Min(1f, (AREA_WIDTH - (columns - 1) * gap) / (columns * size.x), (AREA_HEIGHT - (rows - 1) * gap) / (rows * size.y));
        Vector2 step = size * scale + Vector2.one * gap;
        for (int i = 0; i < _pool.Count; i++)
        {
            RectTransform rect = (RectTransform)_pool[i].transform;
            rect.gameObject.SetActive(i < count);
            if (i >= count) continue;
            int row = i / columns, column = i % columns, inRow = Mathf.Min(columns, count - row * columns);
            rect.localScale = Vector3.one * scale;
            rect.anchoredPosition = new Vector2((column - (inRow - 1) / 2f) * step.x, AREA_CENTER_Y - (row - (rows - 1) / 2f) * step.y);
        }
        return _pool.ToArray();
    }
}
