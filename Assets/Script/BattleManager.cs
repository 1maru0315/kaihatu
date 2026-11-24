using System;
using System.Collections;
using UnityEngine;

// ============================================================
// OVER-RUSH BOARD RPG 戦闘コア (仕様書 3章準拠)
// - カウントダウン 3,2,1,FIGHT の後に制限時間が減り始める (3.1)
// - 敵からの攻撃はなし。時間切れ = 敗北のタイムアタック (3.1)
// - 連鎖 (同色 / 白=万能) で Pips 1/4〜4/4、4枚目で特大攻撃+レベル上昇 (3.3)
// - 通常: 基礎威力 x レベル倍率 (四捨五入) / 特大: 20 (∞は40) + (基礎威力-5) (3.4)
// - 基礎威力と Bonus Time はマップ側の強化を保持する (2.4)
// UIはイベント購読かプロパティのポーリングで接続する想定。
// ============================================================
public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance { get; private set; }

    [Header("プレイヤー (マップ側で保持する値, 仕様 2.4)")]
    [SerializeField] private int baseDamage = 5;
    [SerializeField] private float bonusTime = 0f;
    [SerializeField] private int relicCount = 0;

    [Header("テスト用 敵設定 (マップ未接続時の自動開始用)")]
    [SerializeField] private bool autoStartOnStart = true;
    [SerializeField] private string testEnemyName = "スライム";
    [SerializeField] private int testEnemyMaxHp = 200;
    [SerializeField] private float testTimeLimit = 14.0f;

    [Header("カウントダウン (仕様 3.1)")]
    [SerializeField] private float countdownSeconds = 3.0f;

    // ---- バトル状態 ----
    public string EnemyName { get; private set; }
    public int EnemyMaxHp { get; private set; }
    public int EnemyHp { get; private set; }
    public float TimeLimit { get; private set; }
    public float TimeRemaining { get; private set; }
    public bool IsCountingDown { get; private set; }
    public bool IsBattleActive { get; private set; }
    public bool IsBattleOver { get; private set; }
    public bool IsVictory { get; private set; }

    // ---- プレイヤー状態 ----
    public int BaseDamage => baseDamage;
    public float BonusTime => bonusTime;
    public int RelicCount => relicCount;

    // ---- 連鎖状態 (仕様 3.3) ----
    // LinkLevel: 1=Lv1, 2=Lv2, 3=Lv3, 4=LvMAX
    public int LinkLevel { get; private set; } = 1;
    public int ChainPips { get; private set; } = 0;
    public bool HasChainColor { get; private set; }
    public CardColor ChainColor { get; private set; }
    private bool lastCardWasWhite;

    public float LevelMultiplier
    {
        get
        {
            switch (LinkLevel)
            {
                case 2: return 1.2f;
                case 3: return 1.5f;
                case 4: return 2.0f;
                default: return 1.0f;
            }
        }
    }

    public string LinkLevelLabel => LinkLevel >= 4 ? "Lv MAX" : $"Lv {LinkLevel}";

    // ---- UI接続用イベント ----
    public event Action OnBattleStarted;
    public event Action<int> OnCountdownTick; // 3,2,1
    public event Action OnFightStarted;
    public event Action<int, int> OnEnemyHpChanged; // 現在HP, 最大HP
    public event Action<float, float> OnTimerChanged; // 残り時間, 制限時間
    public event Action<int, bool, bool> OnDamageDealt; // ダメージ, 特大か, ∞特大か
    public event Action OnChainChanged;
    public event Action OnVictory;
    public event Action OnDefeat;

    private Coroutine battleRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        if (autoStartOnStart)
        {
            StartBattle(testEnemyName, testEnemyMaxHp, testTimeLimit);
        }
    }

    // マップ側からはノードのパラメータでこれを呼ぶ (仕様 2.3)
    public void StartBattle(string enemyName, int enemyMaxHp, float timeLimitSeconds)
    {
        if (battleRoutine != null) StopCoroutine(battleRoutine);

        EnemyName = enemyName;
        EnemyMaxHp = Mathf.Max(1, enemyMaxHp);
        EnemyHp = EnemyMaxHp;
        TimeLimit = timeLimitSeconds + bonusTime;
        TimeRemaining = TimeLimit;

        LinkLevel = 1;
        ChainPips = 0;
        HasChainColor = false;
        lastCardWasWhite = false;

        IsCountingDown = true;
        IsBattleActive = false;
        IsBattleOver = false;
        IsVictory = false;

        Debug.Log($"[Battle] {EnemyName} HP:{EnemyMaxHp} 制限時間:{TimeLimit:F1}s (基本{timeLimitSeconds:F1}s + Bonus{bonusTime:F1}s) 基礎威力:{baseDamage}");
        OnBattleStarted?.Invoke();
        OnEnemyHpChanged?.Invoke(EnemyHp, EnemyMaxHp);
        OnTimerChanged?.Invoke(TimeRemaining, TimeLimit);
        OnChainChanged?.Invoke();

        battleRoutine = StartCoroutine(BattleFlow());
    }

    private IEnumerator BattleFlow()
    {
        // カウントダウン中はタイマーを減らさない (仕様 3.1)
        int ticks = Mathf.CeilToInt(countdownSeconds);
        for (int i = ticks; i >= 1; i--)
        {
            Debug.Log($"[Battle] {i}");
            OnCountdownTick?.Invoke(i);
            yield return new WaitForSeconds(1.0f);
        }

        IsCountingDown = false;
        IsBattleActive = true;
        Debug.Log("[Battle] FIGHT!!");
        OnFightStarted?.Invoke();

        while (!IsBattleOver)
        {
            TimeRemaining -= Time.deltaTime;
            if (TimeRemaining <= 0f)
            {
                TimeRemaining = 0f;
                OnTimerChanged?.Invoke(TimeRemaining, TimeLimit);
                EndBattle(false);
                yield break;
            }
            OnTimerChanged?.Invoke(TimeRemaining, TimeLimit);
            yield return null;
        }
    }

    // このカードが現在の連鎖に繋がるか (仕様 3.3 / リーチ予告 3.6 用)
    public bool WouldChain(CardData card)
    {
        if (card == null) return false;
        if (!HasChainColor) return true;
        if (card.cardColor == ChainColor) return true;
        if (card.cardColor == CardColor.White) return true;
        if (lastCardWasWhite) return true;
        return false;
    }

    // このカードを今切った場合のダメージ (予告表示用。状態は変えない)
    public int PredictDamage(CardData card)
    {
        if (card == null) return 0;
        bool isFourth = WouldChain(card) && ChainPips == 3;
        if (isFourth)
        {
            return (card.isLinkCard ? 40 : 20) + (baseDamage - 5);
        }
        return Mathf.RoundToInt(baseDamage * LevelMultiplier);
    }

    // カード使用。HandController.SelectSlot から呼ばれる。
    // 戻り値: 与えたダメージ。使用できなかった場合は -1。
    public int UseCard(CardData card, int slotIndex)
    {
        if (card == null) return -1;
        if (!IsBattleActive || IsBattleOver) return -1;

        bool chains = WouldChain(card);
        bool isBurst = false;
        bool isLinkBurst = false;
        int damage;

        if (!chains)
        {
            // 異色: ゲージだけ新色の1枚目にリセット。レベルは維持 (仕様 3.3)
            ChainColor = card.cardColor;
            HasChainColor = true;
            ChainPips = 1;
            damage = Mathf.RoundToInt(baseDamage * LevelMultiplier);
        }
        else
        {
            if (!HasChainColor)
            {
                ChainColor = card.cardColor;
                HasChainColor = true;
                ChainPips = 1;
            }
            else
            {
                ChainPips++;
                // 白で繋いだ場合: 連鎖色は維持する。
                // ただし連鎖色が白のときは、次に切った色を連鎖色として採用する。
                if (ChainColor == CardColor.White && card.cardColor != CardColor.White)
                {
                    ChainColor = card.cardColor;
                }
            }

            if (ChainPips >= 4)
            {
                // 4連鎖: 特大攻撃 + レベル上昇 (仕様 3.3 / 3.4)
                isBurst = true;
                isLinkBurst = card.isLinkCard;
                damage = (card.isLinkCard ? 40 : 20) + (baseDamage - 5);
                if (LinkLevel < 4) LinkLevel++;
                ChainPips = 0;
                HasChainColor = false;
            }
            else
            {
                damage = Mathf.RoundToInt(baseDamage * LevelMultiplier);
            }
        }

        lastCardWasWhite = card.cardColor == CardColor.White;

        EnemyHp = Mathf.Max(0, EnemyHp - damage);
        Debug.Log($"[Battle] スロット{slotIndex + 1}: {card.cardName} ({card.cardColor}{(card.isLinkCard ? "/∞" : "")}) " +
        $"Pips:{ChainPips}/4 {LinkLevelLabel} -> {damage} DMG, 敵HP {EnemyHp}/{EnemyMaxHp}" +
        (isBurst ? (isLinkBurst ? " " : " ") : ""));
        OnDamageDealt?.Invoke(damage, isBurst, isLinkBurst);
        OnEnemyHpChanged?.Invoke(EnemyHp, EnemyMaxHp);
        OnChainChanged?.Invoke();

        if (EnemyHp <= 0)
        {
            EndBattle(true);
        }

        return damage;
    }

    private void EndBattle(bool victory)
    {
        IsBattleOver = true;
        IsVictory = victory;
        IsBattleActive = false;
        IsCountingDown = false;

        if (battleRoutine != null)
        {
            StopCoroutine(battleRoutine);
            battleRoutine = null;
        }

        if (victory)
        {
            Debug.Log($"[Battle] 勝利! {EnemyName} を倒した (残り時間 {TimeRemaining:F1}s)");
            OnVictory?.Invoke();
        }
        else
        {
            Debug.Log($"[Battle] 時間切れ… ゲームオーバー (敵HP {EnemyHp}/{EnemyMaxHp})");
            OnDefeat?.Invoke();
        }
    }

    // ---- マップ / 報酬からの恒久強化 (仕様 2.4 / 3.7) ----
    public void AddBaseDamage(int amount)
    {
        baseDamage += amount;
        Debug.Log($"[Reward] 基礎威力 +{amount} -> {baseDamage}");
    }

    public void AddBonusTime(float seconds)
    {
        bonusTime += seconds;
        Debug.Log($"[Reward] Bonus Time +{seconds:F1}s -> 合計 {bonusTime:F1}s");
    }

    public void AddRelic(int count = 1)
    {
        relicCount += count;
    }
}
