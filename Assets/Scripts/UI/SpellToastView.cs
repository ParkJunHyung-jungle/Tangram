using UnityEngine;
using UnityEngine.UI;

/// <summary>행동한 위치 근처에 짧은 안내를 띄우고 시간이 지나면 서서히 숨긴다.</summary>
public sealed class SpellToastView : MonoBehaviour
{
    private const float FADE_SECONDS = 0.35f;

    [Header("구성")]
    [SerializeField] private RectTransform _rect;
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private Text _label;

    private float _hideAt;

    void Update()
    {
        float remaining = _hideAt - Time.unscaledTime;
        _group.alpha = Mathf.Clamp01(remaining / FADE_SECONDS);
        if (remaining <= 0) gameObject.SetActive(false);
    }

    /// <summary>문구를 부모 기준 중심 좌표에 seconds초 동안 표시한다.</summary>
    public void Show(string text, Vector2 center, float seconds)
    {
        _label.text = text;
        _rect.anchoredPosition = center;
        _hideAt = Time.unscaledTime + seconds;
        _group.alpha = 1;
        gameObject.SetActive(true);
    }

    /// <summary>표시 중인 안내를 즉시 숨긴다.</summary>
    public void Hide() { gameObject.SetActive(false); }
}
