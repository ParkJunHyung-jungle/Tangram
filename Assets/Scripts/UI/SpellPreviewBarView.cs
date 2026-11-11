using UnityEngine;
using UnityEngine.UI;

/// <summary>격자 아래 고정 미리보기에 선택·조준 중인 도안의 제목, 계산식, 부가 효과를 표시하고 높이를 내용에 맞춘다.</summary>
public sealed class SpellPreviewBarView : MonoBehaviour
{
    private const float TOP_PADDING = 46f;
    private const float LINE_GAP = 6f;
    private const float BOTTOM_PADDING = 16f;
    private const float MIN_HEIGHT = 116f;

    [Header("구성")]
    [SerializeField] private RectTransform _rect;
    [SerializeField] private Image _ring;
    [SerializeField] private Image _accent;
    [SerializeField] private Text _title;
    [SerializeField] private Text _cancelHint;
    [SerializeField] private Text _formula;
    [SerializeField] private Text _detail;

    /// <summary>제목·계산식·부가 효과와 도안 색으로 갱신한다. 부가 효과가 길면 줄바꿈하고 높이를 늘린다.</summary>
    public void Bind(string title, string formula, string detail, Color color, bool showCancel)
    {
        _title.text = title; _title.color = color;
        _ring.color = color; _accent.color = color;
        _cancelHint.gameObject.SetActive(showCancel);
        _formula.text = formula;
        _detail.text = detail;
        float formulaHeight = _formula.preferredHeight;
        _formula.rectTransform.sizeDelta = new Vector2(_formula.rectTransform.sizeDelta.x, formulaHeight);
        _detail.rectTransform.anchoredPosition = new Vector2(_detail.rectTransform.anchoredPosition.x, -(TOP_PADDING + formulaHeight + LINE_GAP));
        float detailHeight = string.IsNullOrEmpty(detail) ? 0 : _detail.preferredHeight;
        _detail.rectTransform.sizeDelta = new Vector2(_detail.rectTransform.sizeDelta.x, detailHeight);
        _rect.sizeDelta = new Vector2(_rect.sizeDelta.x, Mathf.Max(MIN_HEIGHT, TOP_PADDING + formulaHeight + LINE_GAP + detailHeight + BOTTOM_PADDING));
    }
}
