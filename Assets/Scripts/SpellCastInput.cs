using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>도안의 시전 버튼에서 클릭과 드래그를 구분한다.</summary>
public sealed class SpellCastInput : MonoBehaviour, IPointerDownHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private SpellGameUI _ui;
    private int _board;
    private bool _dragged;

    /// <summary>시전 입력을 처리할 화면과 도안 번호를 설정한다.</summary>
    public void Setup(SpellGameUI ui, int board) { _ui = ui; _board = board; }

    public void OnPointerDown(PointerEventData eventData) { _dragged = false; }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!_dragged && eventData.button == PointerEventData.InputButton.Left) _ui.ClickCast(_board);
        _dragged = false;
    }

    public void OnBeginDrag(PointerEventData eventData) { _dragged = true; _ui.BeginCastDrag(_board); }

    public void OnDrag(PointerEventData eventData) { if (_dragged) _ui.PreviewCast(_board, eventData); }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left) _ui.DropCast(_board, eventData);
    }
}
