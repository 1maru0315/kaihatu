using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
// OVER-RUSH BOARD RPG 勝利報酬モーダル (仕様書 3.7 準拠)
// - ボス (魔王) 以外の戦闘勝利時に表示し、以下から1つだけ選ぶ:
//   1. 基礎火力強化: 基礎攻撃力を恒久的に +5
//   2. 白カード追加: 万能白カードをデッキに恒久的に +2枚
// - BattleManager / HandController / BattleUI の変更は不要。
//   既存の OnVictory イベントと、AddBaseDamage(5) /
//   AddWhiteCards(2) の報酬用メソッドだけを使う。
// - 魔王戦では表示しない (仕様 3.1: 魔王撃破時はクリアリザルトへ)。
//   判定は Inspector の isBossBattle か、敵名が「魔王」のどちらか。
//   マップ側から SetBossBattle(true) を呼ぶのが確実。
// - テスト用 autoStart (スライム) はボスではないので、勝利時に
//   このモーダルが出る。選んだ後、閉じるボタンでパネルを隠せる。
// ============================================================
public class RewardModal : MonoBehaviour
{
    [Header("パネル (勝利時に表示する GameObject)")]
    [SerializeField] private GameObject rewardPanel;

    [Header("表示 (任意: 割り当てたものだけ動く)")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI baseButtonText;
    [SerializeField] private TextMeshProUGUI whiteButtonText;
    [SerializeField] private TextMeshProUGUI resultText;

    [Header("ボタン")]
    [SerializeField] private Button baseDamageButton;
    [SerializeField] private Button whiteCardButton;
    [SerializeField] private Button closeButton; // 選択後にだけ押せる

    [Header("この戦闘がボス戦か (マップ側で設定)")]
    [SerializeField] private bool isBossBattle;

    private BattleManager bm;
    private HandController handController;
    private bool rewardChosen;

    private void Start()
    {
        bm = BattleManager.Instance;
        if (bm == null) bm = FindAnyObjectByType<BattleManager>();
        handController = FindAnyObjectByType<HandController>();

        if (bm == null)
        {
            Debug.LogWarning("[Reward] BattleManager が見つかりません。");
            return;
        }

        bm.OnVictory += HandleVictory;

        if (baseDamageButton != null) baseDamageButton.onClick.AddListener(ChooseBaseDamage);
        if (whiteCardButton != null) whiteCardButton.onClick.AddListener(ChooseWhiteCards);
        if (closeButton != null)
        {
            closeButton.interactable = false;
            closeButton.onClick.AddListener(HidePanel);
        }

        HidePanel();
    }

    private void OnDestroy()
    {
        if (bm != null) bm.OnVictory -= HandleVictory;
        if (baseDamageButton != null) baseDamageButton.onClick.RemoveListener(ChooseBaseDamage);
        if (whiteCardButton != null) whiteCardButton.onClick.RemoveListener(ChooseWhiteCards);
        if (closeButton != null) closeButton.onClick.RemoveListener(HidePanel);
    }

    // マップ側から呼ぶ: 魔王ノードの戦闘開始前に SetBossBattle(true)
    public void SetBossBattle(bool isBoss)
    {
        isBossBattle = isBoss;
    }

    private bool IsBossBattle()
    {
        return isBossBattle || (bm != null && bm.EnemyName == "魔王");
    }

    private void HandleVictory()
    {
        if (IsBossBattle()) return; // 魔王戦はクリアリザルト行き (仕様 3.1 / 3.7)

        rewardChosen = false;
        RefreshLabels();

        if (titleText != null) titleText.text = "勝利報酬 — どちらか1つ選ぶ";
        if (resultText != null) resultText.text = "";
        if (baseDamageButton != null) baseDamageButton.interactable = true;
        if (whiteCardButton != null) whiteCardButton.interactable = true;
        if (closeButton != null) closeButton.interactable = false;

        if (rewardPanel != null) rewardPanel.SetActive(true);
    }

    private void RefreshLabels()
    {
        if (bm == null) return;
        if (baseButtonText != null)
            baseButtonText.text = $"基礎火力 +5\n{bm.BaseDamage} → {bm.BaseDamage + 5}";
        if (whiteButtonText != null)
            whiteButtonText.text = "白カード +2枚\nどの色とも連鎖";
    }

    // 報酬1: 基礎火力 +5 (恒久。BattleManager が保持)
    public void ChooseBaseDamage()
    {
        if (rewardChosen || bm == null) return;
        rewardChosen = true;

        int before = bm.BaseDamage;
        bm.AddBaseDamage(5);

        if (resultText != null) resultText.text = $"基礎威力 {before} → {bm.BaseDamage} に強化！";
        FinishChoice();
        Debug.Log($"[Reward] 選択: 基礎火力 +5 ({before} → {bm.BaseDamage})");
    }

    // 報酬2: 白カード +2枚 (恒久。HandController が保持)
    public void ChooseWhiteCards()
    {
        if (rewardChosen || bm == null) return;
        if (handController == null)
        {
            Debug.LogWarning("[Reward] HandController が見つからないため、白カードを追加できません。");
            if (resultText != null) resultText.text = "白カードを追加できません。基礎火力を選んでください。";
            return;
        }

        rewardChosen = true;
        handController.AddWhiteCards(2);

        if (resultText != null) resultText.text = "白カードを2枚追加！デッキに恒久追加されました";
        FinishChoice();
        Debug.Log("[Reward] 選択: 白カード +2枚");
    }

    private void FinishChoice()
    {
        if (baseDamageButton != null) baseDamageButton.interactable = false;
        if (whiteCardButton != null) whiteCardButton.interactable = false;
        if (closeButton != null) closeButton.interactable = true;
    }

    private void HidePanel()
    {
        if (rewardPanel != null) rewardPanel.SetActive(false);
    }
}
