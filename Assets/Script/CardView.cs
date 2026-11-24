using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CardView : MonoBehaviour
{
    [Header("UI参照")]
    [SerializeField] private Image bgImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI iconText;
    [SerializeField] private TextMeshProUGUI labelText;
    [SerializeField] private Button button;

    private int mySlotIndex = -1;

    // カードの見た目を反映する
    public void Setup(CardData card, int slotIndex)
    {
        mySlotIndex = slotIndex;

        if (card == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        // 色とアイコンの設定
        switch (card.cardColor)
        {
            case CardColor.Red:
                bgImage.color = new Color(0.95f, 0.3f, 0.35f);
                nameText.text = "赤(炎)";
                iconText.text = card.isLinkCard ? "∞" : "🔥";
                break;
            case CardColor.Green:
                bgImage.color = new Color(0.2f, 0.8f, 0.45f);
                nameText.text = "緑(風)";
                iconText.text = card.isLinkCard ? "∞" : "🍃";
                break;
            case CardColor.Blue:
                bgImage.color = new Color(0.15f, 0.55f, 1.0f);
                nameText.text = "青(氷)";
                iconText.text = card.isLinkCard ? "∞" : "❄️";
                break;
            case CardColor.White:
                bgImage.color = new Color(0.95f, 0.95f, 0.95f);
                nameText.text = "白(万能)";
                iconText.text = "⭐";
                break;
        }

        if (card.isLinkCard)
        {
            nameText.text += " [∞]";
        }
    }

    // アクションラベル（LINK, BURSTなど）の更新
    public void SetLabel(string label)
    {
        labelText.text = label;
    }

    private void Start()
    {
        if (button != null)
        {
            button.onClick.AddListener(OnClick);
        }
    }

    private void OnClick()
    {
        if (mySlotIndex >= 0 && BattleManager.Instance != null)
        {
            // 手札としてクリックされた場合、HandController経由または直接実行
            FindAnyObjectByType<HandController>()?.SelectSlot(mySlotIndex);
        }
    }
}