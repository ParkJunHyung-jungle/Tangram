using UnityEngine;
using UnityEngine.UI;

/// <summary>플레이어 초상화 옆의 체력·방어를 표시한다.</summary>
public sealed class SpellPlayerStatusView : MonoBehaviour
{
    [Header("구성")]
    [SerializeField] private SpellHpBarView _health;
    [SerializeField] private Text _defense;

    /// <summary>체력과 방어 수치를 갱신한다.</summary>
    public void Bind(int health, int maxHealth, int defense)
    {
        _health.Bind(health, maxHealth, 0, $"체력 {health} / {maxHealth}");
        _defense.text = $"방어 {defense}";
    }
}
