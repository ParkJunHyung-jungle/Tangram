using UnityEngine;
using UnityEngine.UI;

/// <summary>체력 막대의 현재 비율, 예상 피해 구간, 수치 문구를 표시한다.</summary>
public sealed class SpellHpBarView : MonoBehaviour
{
    [Header("구성")]
    [SerializeField] private RectTransform _fill;
    [SerializeField] private RectTransform _preview;
    [SerializeField] private Text _value;

    private RectTransform _defenseFill;

    /// <summary>체력 막대 위에 방어량을 최대 체력 대비 회색 띠로 표시한다.</summary>
    public void BindDefense(int defense, int maxHealth)
    {
        if (_defenseFill == null)
        {
            GameObject bar = new GameObject("DefenseFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _defenseFill = bar.GetComponent<RectTransform>();
            _defenseFill.SetParent(_fill.parent, false);
            _defenseFill.SetAsLastSibling();
            _value.transform.SetAsLastSibling();
            Image image = bar.GetComponent<Image>();
            image.color = new Color(0.65f, 0.67f, 0.7f, 1f);
            image.raycastTarget = false;
        }
        _defenseFill.gameObject.SetActive(defense > 0);
        _defenseFill.anchorMin = new Vector2(0, 1);
        _defenseFill.anchorMax = new Vector2(maxHealth > 0 ? Mathf.Clamp01((float)defense / maxHealth) : 0, 1);
        _defenseFill.pivot = new Vector2(0, 1);
        _defenseFill.offsetMin = new Vector2(0, -6);
        _defenseFill.offsetMax = Vector2.zero;
    }

    /// <summary>현재 체력과 최대 체력, 예상 피해로 막대와 수치 문구를 갱신한다.</summary>
    public void Bind(int current, int max, int previewDamage, string text)
    {
        float now = max > 0 ? Mathf.Clamp01((float)current / max) : 0;
        float after = max > 0 ? Mathf.Clamp01((float)Mathf.Max(0, current - previewDamage) / max) : 0;
        _fill.anchorMax = new Vector2(previewDamage > 0 ? after : now, 1);
        _preview.gameObject.SetActive(previewDamage > 0 && now > after);
        _preview.anchorMin = new Vector2(after, 0); _preview.anchorMax = new Vector2(now, 1);
        _value.text = text;
    }
}
