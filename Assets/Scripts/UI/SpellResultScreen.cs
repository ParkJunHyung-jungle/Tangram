using UnityEngine;
using UnityEngine.UI;

/// <summary>승리·패배 결과 화면의 제목과 다시 시작 버튼 참조를 보관한다.</summary>
public sealed class SpellResultScreen : MonoBehaviour
{
    [Header("구성")]
    [SerializeField] private Text _title;
    [SerializeField] private SpellButtonView _restart;

    public SpellButtonView Restart { get { return _restart; } }

    /// <summary>결과 제목을 바꾼다.</summary>
    public void Bind(string title) { _title.text = title; }
}
