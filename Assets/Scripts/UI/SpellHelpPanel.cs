using UnityEngine;

/// <summary>도움말 창의 닫기 버튼 참조를 보관한다. 본문은 Prefab에 적는다.</summary>
public sealed class SpellHelpPanel : MonoBehaviour
{
    [Header("구성")]
    [SerializeField] private SpellButtonView _close;

    public SpellButtonView Close { get { return _close; } }
}
