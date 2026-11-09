using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>도안 목록에서 공유 격자로 도안을 드래그한다.</summary>
public sealed class SpellPatternDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private SpellGameUI _ui;
    private int _pattern;

    /// <summary>이 항목이 이동할 도안을 설정한다.</summary>
    public void Setup(SpellGameUI ui, int pattern) { _ui = ui; _pattern = pattern; }
    public void OnBeginDrag(PointerEventData eventData) { }
    public void OnDrag(PointerEventData eventData)
    {
        foreach (SpellBoardGraphic board in FindObjectsByType<SpellBoardGraphic>(FindObjectsSortMode.None))
            board.PreviewPattern(eventData, _pattern, Vector2Int.zero);
    }
    public void OnEndDrag(PointerEventData eventData)
    {
        foreach (SpellBoardGraphic board in FindObjectsByType<SpellBoardGraphic>(FindObjectsSortMode.None))
        {
            board.ClearPatternPreview();
            if (board.DropPattern(eventData, _pattern)) return;
        }
    }
}
