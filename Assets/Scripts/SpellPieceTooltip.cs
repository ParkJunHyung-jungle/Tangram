using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>손패 조각에 포인터를 올리면 설명을 표시한다.</summary>
public sealed class SpellPieceTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private SpellGameUI _ui;
    private SpellGame.Card _card;

    /// <summary>설명할 조각과 화면을 설정한다.</summary>
    public void Setup(SpellGameUI ui, SpellGame.Card card) { _ui = ui; _card = card; }

    public void OnPointerEnter(PointerEventData eventData) { _ui.ShowPieceTooltip(_card, eventData.position); }
    public void OnPointerExit(PointerEventData eventData) { _ui.ShowPieceTooltip(null, eventData.position); }
}
