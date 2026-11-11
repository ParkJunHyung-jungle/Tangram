using System;

using UnityEngine;
using UnityEngine.UI;

/// <summary>공용 버튼의 라벨과 클릭 동작을 연결한다.</summary>
public sealed class SpellButtonView : MonoBehaviour
{
    [Header("구성")]
    [SerializeField] private Button _button;
    [SerializeField] private Text _label;

    private Action _action;

    void Awake()
    {
        _button.onClick.AddListener(Invoke);
    }

    /// <summary>클릭했을 때 실행할 동작을 바꾼다.</summary>
    public void SetAction(Action action) { _action = action; }

    /// <summary>버튼 라벨을 바꾼다.</summary>
    public void SetLabel(string text) { _label.text = text; }

    /// <summary>버튼을 누를 수 있는지 설정한다.</summary>
    public void SetInteractable(bool interactable) { _button.interactable = interactable; }

    /// <summary>등록된 클릭 동작을 실행한다.</summary>
    private void Invoke() { _action?.Invoke(); }
}
