using UnityEngine;
using UnityEngine.UI;

/// <summary>완성된 도안 위에 뜨는 시전 버튼의 색·라벨·조준 상태, 예상 수식과 위치를 표시한다.</summary>
public sealed class SpellCastButtonView : MonoBehaviour
{
    private const float NORMAL_WIDTH = 104f;
    private const float AIMING_WIDTH = 128f;
    private const float FORMULA_PADDING = 24f;

    [Header("구성")]
    [SerializeField] private RectTransform _rect;
    [SerializeField] private Image _fill;
    [SerializeField] private Image _ring;
    [SerializeField] private Text _label;
    [SerializeField] private SpellCastInput _input;
    [SerializeField] private RectTransform _formula;
    [SerializeField] private Text _formulaText;

    [Header("조준 표시")]
    [SerializeField] private Color _ringColor = Color.black;
    [SerializeField] private Color _aimingRingColor = Color.white;

    public RectTransform Rect { get { return _rect; } }

    /// <summary>입력을 처리할 화면과 도안 번호를 설정한다.</summary>
    public void Setup(SpellGameUI ui, int board) { _input.Setup(ui, board); }

    /// <summary>도안 색과 조준 상태, 예상 수식으로 표시를 갱신하고 부모 기준 중심 좌표에 놓는다.</summary>
    public void Bind(Color color, bool aiming, Vector2 center, string formula)
    {
        _fill.color = color;
        _ring.color = aiming ? _aimingRingColor : _ringColor;
        _label.text = aiming ? "조준 중" : "시전";
        _rect.sizeDelta = new Vector2(aiming ? AIMING_WIDTH : NORMAL_WIDTH, _rect.sizeDelta.y);
        _rect.anchoredPosition = center;
        _formulaText.text = formula; _formulaText.color = color;
        _formula.sizeDelta = new Vector2(_formulaText.preferredWidth + FORMULA_PADDING, _formula.sizeDelta.y);
    }
}
