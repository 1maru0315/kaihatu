using System.Collections.Generic;
using UnityEngine;

// ============================================================
// 手札・デッキ循環 (仕様書 3.2 / 3.5 / 3.6 準拠)
// - 初期デッキ15枚: 赤/緑/青 各 (通常4 + ∞リンク1)。テンプレートから自動構築
// - 手札は常時4枚、使用したカードは捨札へ行き、その場で1枚補充
// - NEXTスタンドに次の補充カードを常時表示
// - 山札が尽きたら捨札を自動シャッフルして山札に戻す
// - 操作: クリック (CardView経由) / キー 1=Q, 2=W, 3=E, 4=R の対応は
//   スロット1: [1]/[Q], スロット2: [2]/[W], スロット3: [3]/[E], スロット4: [4]/[R]
// ============================================================
public class HandController : MonoBehaviour
{
    [Header("手札スロットの CardView (左から1〜4)")]
    [SerializeField] private CardView[] slotViews = new CardView[4];

    [Header("NEXTカードの CardView (任意)")]
    [SerializeField] private CardView nextView;

    [Header("カード テンプレート (各色の実物アセットをセット)")]
    [SerializeField] private CardData redCard;
    [SerializeField] private CardData redLinkCard;
    [SerializeField] private CardData greenCard;
    [SerializeField] private CardData greenLinkCard;
    [SerializeField] private CardData blueCard;
    [SerializeField] private CardData blueLinkCard;
    [SerializeField] private CardData whiteCard; // 報酬「白カード追加」用

    private readonly List<CardData> drawPile = new List<CardData>();
    private readonly List<CardData> discardPile = new List<CardData>();
    private readonly List<CardData> bonusCards = new List<CardData>(); // 報酬で恒久追加されたカード
    private CardData[] hand = new CardData[4];
    private bool labelFailed;

    public int DrawPileCount => drawPile.Count;
    public int DiscardPileCount => discardPile.Count;
    public CardData NextCard => drawPile.Count > 0 ? drawPile[0] : null;

    private void Start()
    {
        BuildDeck();
        DrawInitialHand();
        RefreshViews();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Q)) SelectSlot(0);
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.W)) SelectSlot(1);
        else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.E)) SelectSlot(2);
        else if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.R)) SelectSlot(3);
    }

    // 仕様 3.2: 赤/緑/青 各5枚 (通常4 + ∞1) = 15枚 + 報酬で追加された恒久カード
    public void BuildDeck()
    {
        drawPile.Clear();
        discardPile.Clear();
        hand = new CardData[4];

        AddCopies(drawPile, redCard, 4);
        AddCopies(drawPile, redLinkCard, 1);
        AddCopies(drawPile, greenCard, 4);
        AddCopies(drawPile, greenLinkCard, 1);
        AddCopies(drawPile, blueCard, 4);
        AddCopies(drawPile, blueLinkCard, 1);
        drawPile.AddRange(bonusCards);

        Shuffle(drawPile);
        Debug.Log($"[Hand] デッキ構築: {drawPile.Count}枚");
    }

    private void AddCopies(List<CardData> list, CardData card, int count)
    {
        if (card == null)
        {
            Debug.LogWarning("[Hand] テンプレートに未設定のカードがあります。HandControllerのInspectorを確認してください。");
            return;
        }
        for (int i = 0; i < count; i++) list.Add(card);
    }

    private void DrawInitialHand()
    {
        for (int i = 0; i < hand.Length; i++) hand[i] = DrawOne();
    }

    // 山札の先頭 (インデックス0) を1枚引く。尽きたら捨札をリシャッフル (仕様 3.2)
    private CardData DrawOne()
    {
        if (drawPile.Count == 0)
        {
            if (discardPile.Count == 0) return null;
            drawPile.AddRange(discardPile);
            discardPile.Clear();
            Shuffle(drawPile);
            Debug.Log($"[Hand] 山札リシャッフル: {drawPile.Count}枚");
        }

        CardData card = drawPile[0];
        drawPile.RemoveAt(0);
        return card;
    }

    private void Shuffle(List<CardData> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // CardView.OnClick とキーボード入力の共通入口 (CardViewが要求するメソッド)
    public void SelectSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= hand.Length) return;

        if (BattleManager.Instance == null)
        {
            Debug.LogWarning("[Hand] BattleManager がシーンにありません。");
            return;
        }

        CardData card = hand[slotIndex];
        if (card == null)
        {
            Debug.Log($"[Hand] スロット{slotIndex + 1} は空です");
            return;
        }

        int damage = BattleManager.Instance.UseCard(card, slotIndex);
        if (damage < 0)
        {
            // カウントダウン中・決着後は使用できない
            return;
        }

        // 使用したカードは捨札へ、その場で1枚補充 (仕様 3.2)
        discardPile.Add(card);
        hand[slotIndex] = DrawOne();
        RefreshViews();
    }

    // 勝利報酬「白カード追加」 (仕様 3.7): 恒久的に2枚追加し、現在の捨札にも入れる
    public void AddWhiteCards(int count = 2)
    {
        if (whiteCard == null)
        {
            Debug.LogWarning("[Hand] 白カードのテンプレートが未設定です。");
            return;
        }
        for (int i = 0; i < count; i++)
        {
            bonusCards.Add(whiteCard);
            discardPile.Add(whiteCard);
        }
        Debug.Log($"[Reward] 白カード +{count}枚 (デッキ恒久追加)");
        RefreshViews();
    }

    public void RefreshViews()
    {
        for (int i = 0; i < slotViews.Length; i++)
        {
            if (slotViews[i] == null) continue;
            CardData card = i < hand.Length ? hand[i] : null;
            slotViews[i].Setup(card, i);
        }

        if (nextView != null)
        {
            // NEXTはクリックしても使えないよう slotIndex = -1 で表示 (仕様 3.2)
            nextView.Setup(NextCard, -1);
        }

        UpdateSlotLabels();
    }

    // リーチ予告ラベル (仕様 3.6): 3連鎖時に繋がるカードへ予告を出す
    private void UpdateSlotLabels()
    {
        if (labelFailed) return;
        BattleManager bm = BattleManager.Instance;
        if (bm == null) return;

        try
        {
            for (int i = 0; i < slotViews.Length; i++)
            {
                if (slotViews[i] == null) continue;
                CardData card = i < hand.Length ? hand[i] : null;
                if (card == null) continue;

                if (bm.ChainPips == 3 && bm.WouldChain(card))
                {
                    int dmg = bm.PredictDamage(card);
                    slotViews[i].SetLabel(card.isLinkCard ? $"🔥 ∞ {dmg} DMG!" : $"⚡ {dmg} DMG!");
                }
                else if (bm.WouldChain(card) && bm.ChainPips > 0)
                {
                    slotViews[i].SetLabel("LINK");
                }
                else
                {
                    slotViews[i].SetLabel("");
                }
            }
        }
        catch (System.NullReferenceException)
        {
            // CardView.labelText が未割り当ての場合はラベル機能だけ無効化する
            labelFailed = true;
            Debug.LogWarning("[Hand] CardView の labelText が未設定のため、ラベル表示を無効化しました。");
        }
    }
}
