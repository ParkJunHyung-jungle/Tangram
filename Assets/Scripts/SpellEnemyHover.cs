using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>단일 대상 주문의 클릭 시전 중 적에 대한 미리보기를 전달한다.</summary>
public sealed class SpellEnemyHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private SpellGameUI _ui;

    /// <summary>미리보기를 표시할 화면을 설정한다.</summary>
    public void Setup(SpellGameUI ui) { _ui = ui; }

    public void OnPointerEnter(PointerEventData eventData) { _ui.PreviewPendingCast(eventData); }
    public void OnPointerExit(PointerEventData eventData) { _ui.ClearPendingPreview(); }
}
