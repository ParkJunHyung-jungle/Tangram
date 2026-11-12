using System;

using UnityEngine;
using UnityEngine.UI;

/// <summary>드로우 덱·묘지 더미의 수량을 표시하고 열람 클릭을 전달한다.</summary>
public sealed class SpellPileView : MonoBehaviour
{
    [Header("구성")]
    [SerializeField] private Button _button;
    [SerializeField] private Text _count;
    [SerializeField] private Text _title;

    private Action _action;

    void Awake()
    {
        _button.onClick.AddListener(Click);
    }

    /// <summary>더미 이름과 클릭 동작을 설정한다.</summary>
    public void Setup(string title, Action action) { _title.text = title; _action = action; }

    /// <summary>더미 수량을 갱신한다.</summary>
    public void Bind(int count) { _count.text = count.ToString(); }

    /// <summary>열람 클릭을 전달한다.</summary>
    private void Click() { _action?.Invoke(); }
}
