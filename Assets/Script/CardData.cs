using UnityEngine;

// CardView.cs の switch (card.cardColor) で使われている4色
public enum CardColor
{
    Red,
    Green,
    Blue,
    White
}

// カード1枚分のデータ (仕様書 3.2 / 3.4 準拠)
// ダメージはカード個別の数値では決まらず、基礎威力 x リンクレベル倍率、
// または4連鎖時の固定火力 (20 / ∞は40) で決まるため、power/cost は持たない。
// Unityエディタで 右クリック > Create > OVER-RUSH > Card Data から作成。
[CreateAssetMenu(fileName = "NewCard", menuName = "OVER-RUSH/Card Data")]
public class CardData : ScriptableObject
{
    [Header("表示 (CardViewが参照)")]
    public CardColor cardColor;
    public bool isLinkCard;

    [Header("基本情報")]
    public string cardName = "New Card";

    [TextArea]
    public string description;
}
