using UnityEngine;

/// <summary>일시정지 메뉴의 계속하기·타이틀로 가기·게임 종료 버튼 참조를 보관한다.</summary>
public sealed class SpellPauseMenu : MonoBehaviour
{
    [Header("구성")]
    [SerializeField] private SpellButtonView _continue;
    [SerializeField] private SpellButtonView _title;
    [SerializeField] private SpellButtonView _quit;

    public SpellButtonView Continue { get { return _continue; } }
    public SpellButtonView Title { get { return _title; } }
    public SpellButtonView Quit { get { return _quit; } }
}
