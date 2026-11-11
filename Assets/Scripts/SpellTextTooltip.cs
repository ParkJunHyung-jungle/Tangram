using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>전투 상태 요소의 간단한 설명을 포인터 근처에 표시한다.</summary>
public sealed class SpellTextTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    private SpellGameUI _ui;
    private string _message;

    /// <summary>설명을 표시할 화면과 문구를 설정한다.</summary>
    public void Setup(SpellGameUI ui, string message) { _ui = ui; _message = message; }

    public void OnPointerEnter(PointerEventData eventData) { _ui.ShowTextTooltip(_message, eventData.position); }
    public void OnPointerExit(PointerEventData eventData) { _ui.ShowTextTooltip(null, eventData.position); }
    public void OnSelect(BaseEventData eventData) { _ui.ShowTextTooltip(_message, RectTransformUtility.WorldToScreenPoint(null, transform.position)); }
    public void OnDeselect(BaseEventData eventData) { _ui.ShowTextTooltip(null, Vector2.zero); }
}
