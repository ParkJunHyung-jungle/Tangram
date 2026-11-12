using UnityEngine;
using UnityEngine.UI;

/// <summary>남은 배치 횟수를 채운 마름모·빈 외곽선과 수치, 0회 안내로 표시한다.</summary>
public sealed class SpellPlacementView : MonoBehaviour
{
    private const int BASE_PLACEMENTS = 3;

    [Header("구성")]
    [SerializeField] private Image[] _pips;
    [SerializeField] private Text _extra;
    [SerializeField] private Text _count;
    [SerializeField] private Text _note;

    [Header("표시")]
    [SerializeField] private Sprite _fullPip;
    [SerializeField] private Sprite _emptyPip;
    [SerializeField] private Color _fullColor = Color.white;
    [SerializeField] private Color _emptyColor = Color.gray;
    [SerializeField] private Color _countColor = Color.white;
    [SerializeField] private Color _warnColor = Color.yellow;

    /// <summary>남은 배치 횟수로 마름모·추가 배치·수치·안내를 갱신한다.</summary>
    public void Bind(int left)
    {
        for (int i = 0; i < _pips.Length; i++)
        {
            bool full = i < left;
            _pips[i].sprite = full ? _fullPip : _emptyPip;
            _pips[i].color = full ? _fullColor : _emptyColor;
        }
        _extra.gameObject.SetActive(left > BASE_PLACEMENTS);
        _extra.text = $"+{left - BASE_PLACEMENTS}";
        _count.text = left > BASE_PLACEMENTS ? $"{left}회" : $"{left}/{BASE_PLACEMENTS}";
        _count.color = left == 0 ? _warnColor : _countColor;
        _note.gameObject.SetActive(left == 0);
    }
}
