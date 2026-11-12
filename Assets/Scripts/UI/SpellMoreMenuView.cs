using System;

using UnityEngine;
using UnityEngine.UI;

/// <summary>도안 탭의 더보기 메뉴. 도안 비우기 항목과 그 결과 설명을 보여 주고, 바깥을 누르면 닫는다.</summary>
public sealed class SpellMoreMenuView : MonoBehaviour
{
    [Header("구성")]
    [SerializeField] private Button _blocker;
    [SerializeField] private RectTransform _panel;
    [SerializeField] private SpellButtonView _clear;

    private Action _onClear;

    public bool IsOpen { get { return gameObject.activeSelf; } }

    void Awake()
    {
        _blocker.onClick.AddListener(Close);
        _clear.SetAction(Clear);
    }

    /// <summary>메뉴를 부모 기준 좌표(패널 오른쪽 위)에 열고 비우기 동작을 연결한다.</summary>
    public void Open(Vector2 topRight, Action onClear)
    {
        _onClear = onClear;
        _panel.anchoredPosition = topRight - new Vector2(_panel.sizeDelta.x, 0);
        gameObject.SetActive(true);
    }

    /// <summary>메뉴를 닫는다.</summary>
    public void Close() { gameObject.SetActive(false); }

    /// <summary>메뉴를 닫고 도안 비우기를 실행한다.</summary>
    private void Clear()
    {
        Close();
        _onClear?.Invoke();
    }
}
