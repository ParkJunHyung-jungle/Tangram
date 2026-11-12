using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>전투 사이 도안 장착 화면. 보유 도안을 격자에 놓고 돌리며, 격자에 놓인 도안만 다음 전투에 장착한다.</summary>
public sealed class SpellLoadoutScreen : MonoBehaviour
{
    private const float STORAGE_SCALE = 0.62f;
    private const float STORAGE_GAP = 12f;
    private const string DEFAULT_HINT = "도안을 클릭해 들고 격자를 클릭해 놓습니다 · R / 우클릭 회전 · 보관함을 클릭하면 장착 해제";

    [Header("구성")]
    [SerializeField] private SpellLoadoutGrid _grid;
    [SerializeField] private RectTransform _storage;
    [SerializeField] private Button _storageButton;
    [SerializeField] private SpellRewardCardView _storageTemplate;
    [SerializeField] private Text _storageEmpty;
    [SerializeField] private SpellButtonView _start;
    [SerializeField] private Text _hint;

    private readonly List<SpellRewardCardView> _cards = new List<SpellRewardCardView>();
    private SpellPatternLayout _layout;
    private IReadOnlyList<int> _owned;
    private Func<int, Color> _colorOf;
    private int _held = -1;
    private int _heldRotation;

    /// <summary>도안 배치, 보유 목록, 도안 색과 전투 시작 동작을 연결한다.</summary>
    public void Setup(SpellPatternLayout layout, IReadOnlyList<int> owned, Func<int, Color> colorOf, Action onStart)
    {
        _layout = layout; _owned = owned; _colorOf = colorOf;
        _grid.Setup(layout, owned, colorOf, ClickCell);
        _start.SetAction(() => { DropHeld(); onStart(); });
        _storageButton.onClick.AddListener(DropHeld);
    }

    void OnDisable()
    {
        DropHeld();
    }

    void Update()
    {
        if (_held < 0) return;
        bool rotate = (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) || (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame);
        if (rotate) { _heldRotation = (_heldRotation + 1) % 4; _grid.SetHeld(_held, _heldRotation); }
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) DropHeld();
    }

    /// <summary>보관함 카드와 격자를 현재 배치로 다시 그리고 안내를 기본 문구로 되돌린다.</summary>
    public void Refresh()
    {
        List<int> stored = new List<int>();
        foreach (int spell in _owned) if (!_layout.IsPlaced(spell) && spell != _held) stored.Add(spell);
        while (_cards.Count < stored.Count) _cards.Add(Instantiate(_storageTemplate, _storage));
        float height = ((RectTransform)_storageTemplate.transform).sizeDelta.y * STORAGE_SCALE;
        for (int i = 0; i < _cards.Count; i++)
        {
            bool show = i < stored.Count;
            _cards[i].gameObject.SetActive(show);
            if (!show) continue;
            int spell = stored[i];
            RectTransform rect = (RectTransform)_cards[i].transform;
            rect.localScale = Vector3.one * STORAGE_SCALE;
            rect.anchoredPosition = new Vector2(0, -height / 2 - i * (height + STORAGE_GAP));
            _cards[i].BindPattern(spell, $"{SpellGame.PatternName(spell)}\n{SpellGame.PatternDescription(spell)}", spell, _colorOf(spell), PickStored);
        }
        _storageEmpty.gameObject.SetActive(stored.Count == 0);
        _grid.SetHeld(_held, _heldRotation);
        ShowHint(DEFAULT_HINT);
    }

    /// <summary>장착 화면 아래 안내 문구를 바꾼다.</summary>
    public void ShowHint(string text) { _hint.text = text; }

    /// <summary>보관함의 도안을 현재 방향으로 든다. 이미 든 도안은 보관함으로 돌려놓는다.</summary>
    private void PickStored(int spell)
    {
        DropHeld();
        _held = spell; _heldRotation = _layout.Rotation(spell);
        Refresh(); ShowHint($"{SpellGame.PatternName(spell)}을(를) 놓을 칸을 클릭하세요 · R / 우클릭 회전");
    }

    /// <summary>든 도안이 있으면 그 칸에 놓고, 없으면 그 칸의 도안을 격자에서 들어 올린다.</summary>
    private void ClickCell(Vector2Int cell)
    {
        if (_held >= 0)
        {
            Vector2Int origin = _grid.HeldOrigin(cell);
            string issue = _layout.MoveIssue(_held, origin, _heldRotation, _owned);
            if (issue != null) { ShowHint(issue); return; }
            _layout.Place(_held, origin, _heldRotation); _held = -1; Refresh(); return;
        }
        for (int sector = 0; sector < 4; sector++)
        {
            if (!_layout.TryFindOwner(cell.x, cell.y, sector, _owned, out int spell)) continue;
            _held = spell; _heldRotation = _layout.Rotation(spell); _layout.Unplace(spell);
            Refresh(); ShowHint($"{SpellGame.PatternName(spell)}을(를) 놓을 칸을 클릭하세요 · 보관함을 클릭하면 장착 해제"); return;
        }
    }

    /// <summary>들고 있는 도안을 보관함에 내려놓는다.</summary>
    private void DropHeld()
    {
        if (_held < 0) return;
        _held = -1;
        if (isActiveAndEnabled) Refresh();
    }
}
