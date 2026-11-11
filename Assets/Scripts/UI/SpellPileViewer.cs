using System;

using UnityEngine;
using UnityEngine.UI;

/// <summary>드로우 덱·묘지 열람 창. 조각 종류별 수량 목록을 보여 주고 바깥·닫기로 닫는다.</summary>
public sealed class SpellPileViewer : MonoBehaviour
{
    [Header("구성")]
    [SerializeField] private Button _blocker;
    [SerializeField] private Text _title;
    [SerializeField] private Text _body;
    [SerializeField] private SpellButtonView _close;

    private Action _onClose;

    void Awake()
    {
        _blocker.onClick.AddListener(Close);
        _close.SetAction(Close);
    }

    /// <summary>제목과 목록 본문, 닫힘 동작을 설정한다.</summary>
    public void Bind(string title, string body, Action onClose)
    {
        _title.text = title; _body.text = body; _onClose = onClose;
    }

    /// <summary>닫힘 동작을 전달한다.</summary>
    private void Close() { _onClose?.Invoke(); }
}
