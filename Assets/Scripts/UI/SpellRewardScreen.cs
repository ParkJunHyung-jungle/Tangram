using UnityEngine;

/// <summary>보상 화면의 후보 카드와 건너뛰기 버튼을 보관하고, 보이는 카드 수에 맞춰 가운데 정렬한다.</summary>
public sealed class SpellRewardScreen : MonoBehaviour
{
    private const float CARD_GAP = 36f;

    [Header("구성")]
    [SerializeField] private SpellRewardCardView[] _cards;
    [SerializeField] private SpellButtonView _skip;

    public SpellRewardCardView[] Cards { get { return _cards; } }
    public SpellButtonView Skip { get { return _skip; } }

    /// <summary>앞에서부터 count장만 보이게 하고 화면 가운데로 정렬한다.</summary>
    public void ShowCards(int count)
    {
        float width = ((RectTransform)_cards[0].transform).sizeDelta.x;
        float start = -(count * width + (count - 1) * CARD_GAP) / 2 + width / 2;
        for (int i = 0; i < _cards.Length; i++)
        {
            _cards[i].gameObject.SetActive(i < count);
            RectTransform rect = (RectTransform)_cards[i].transform;
            rect.anchoredPosition = new Vector2(start + i * (width + CARD_GAP), rect.anchoredPosition.y);
        }
    }
}
