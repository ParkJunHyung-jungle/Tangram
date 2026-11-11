using UnityEngine;

/// <summary>타이틀 화면의 시작 버튼 참조를 보관한다.</summary>
public sealed class SpellTitleScreen : MonoBehaviour
{
    [Header("구성")]
    [SerializeField] private SpellButtonView _start;

    public SpellButtonView StartButton { get { return _start; } }
}
