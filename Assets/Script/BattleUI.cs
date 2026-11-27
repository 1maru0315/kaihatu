using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
// 戦闘UI接続 (仕様書 3.6 準拠)
// BattleManager のイベントとプロパティを画面表示につなぐ。
// すべての参照は任意。Inspectorで割り当てたものだけが動くので、
// まずは敵HP・タイマー・カウントダウンだけ繋げばよい。
// ============================================================
public class BattleUI : MonoBehaviour
{
    [Header("敵HP")]
    [SerializeField] private TextMeshProUGUI enemyNameText;
    [SerializeField] private TextMeshProUGUI enemyHpText;
    [SerializeField] private Slider enemyHpSlider;
    [SerializeField] private Image enemyHpBarFill; // Filled Image でも可

    [Header("タイマー")]
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private Slider timerSlider;
    [SerializeField] private Image timerBarFill;

    [Header("連鎖 (仕様 3.6 連鎖色バッジ)")]
    [SerializeField] private TextMeshProUGUI chainColorText;
    [SerializeField] private TextMeshProUGUI chainPipsText;
    [SerializeField] private TextMeshProUGUI linkLevelText;
    [SerializeField] private TextMeshProUGUI baseDamageText;

    [Header("中央表示")]
    [SerializeField] private TextMeshProUGUI countdownText; // 3,2,1,FIGHT!!
    [SerializeField] private TextMeshProUGUI damagePopupText; // ダメージポップアップ
    [SerializeField] private TextMeshProUGUI resultText; // VICTORY / GAME OVER

    private BattleManager bm;
    private Coroutine popupRoutine;
    private Coroutine centerRoutine;

    private void Start()
    {
        bm = BattleManager.Instance;
        if (bm == null) bm = FindAnyObjectByType<BattleManager>();
        if (bm == null)
        {
            Debug.LogWarning("[BattleUI] BattleManager が見つかりません。");
            return;
        }

        bm.OnCountdownTick += HandleCountdownTick;
        bm.OnFightStarted += HandleFightStarted;
        bm.OnEnemyHpChanged += HandleHpChanged;
        bm.OnChainChanged += RefreshChain;
        bm.OnDamageDealt += HandleDamageDealt;
        bm.OnVictory += HandleVictory;
        bm.OnDefeat += HandleDefeat;

        if (resultText != null) resultText.gameObject.SetActive(false);
        if (damagePopupText != null) damagePopupText.gameObject.SetActive(false);

        RefreshAll();
    }

    private void OnDestroy()
    {
        if (bm == null) return;
        bm.OnCountdownTick -= HandleCountdownTick;
        bm.OnFightStarted -= HandleFightStarted;
        bm.OnEnemyHpChanged -= HandleHpChanged;
        bm.OnChainChanged -= RefreshChain;
        bm.OnDamageDealt -= HandleDamageDealt;
        bm.OnVictory -= HandleVictory;
        bm.OnDefeat -= HandleDefeat;
    }

    private void HandleVictory() => ShowResult("VICTORY!", new Color(1f, 0.85f, 0.2f));
    private void HandleDefeat() => ShowResult("GAME OVER", new Color(1f, 0.3f, 0.3f));

    private void Update()
    {
        if (bm == null) return;

        // タイマーは毎フレーム滑らかに更新する
        if (timerText != null)
        {
            timerText.text = bm.TimeRemaining.ToString("F1");
            timerText.color = bm.TimeRemaining <= 3f && bm.IsBattleActive
                ? new Color(1f, 0.3f, 0.3f)
                : Color.white;
        }
        float timerRatio = bm.TimeLimit > 0f ? bm.TimeRemaining / bm.TimeLimit : 0f;
        if (timerSlider != null) timerSlider.value = timerRatio;
        if (timerBarFill != null) timerBarFill.fillAmount = timerRatio;
    }

    private void RefreshAll()
    {
        HandleHpChanged(bm.EnemyHp, bm.EnemyMaxHp);
        RefreshChain();
    }

    private void HandleHpChanged(int current, int max)
    {
        if (enemyNameText != null) enemyNameText.text = bm != null ? bm.EnemyName : "";
        if (enemyHpText != null) enemyHpText.text = $"{current} / {max}";
        float ratio = max > 0 ? (float)current / max : 0f;
        if (enemyHpSlider != null)
        {
            enemyHpSlider.maxValue = 1f;
            enemyHpSlider.value = ratio;
        }
        if (enemyHpBarFill != null) enemyHpBarFill.fillAmount = ratio;
    }

    private void RefreshChain()
    {
        if (bm == null) return;

        if (chainColorText != null)
        {
            if (!bm.HasChainColor)
            {
                chainColorText.text = "自由";
                chainColorText.color = Color.white;
            }
            else
            {
                chainColorText.text = ColorLabel(bm.ChainColor);
                chainColorText.color = ColorTint(bm.ChainColor);
            }
        }

        if (chainPipsText != null)
        {
            string pips = "";
            for (int i = 0; i < 4; i++) pips += i < bm.ChainPips ? "●" : "○";
            chainPipsText.text = pips;
        }

        if (linkLevelText != null) linkLevelText.text = bm.LinkLevelLabel;
        if (baseDamageText != null) baseDamageText.text = $"基礎威力 {bm.BaseDamage}";
    }

    private void HandleCountdownTick(int number)
    {
        ShowCenter(number.ToString(), Color.white, 44);
    }

    private void HandleFightStarted()
    {
        ShowCenter("FIGHT!!", new Color(1f, 0.85f, 0.2f, 1f), 56);
    }

    private void ShowCenter(string text, Color color, int fontSize)
    {
        if (countdownText == null) return;
        if (centerRoutine != null) StopCoroutine(centerRoutine);
        centerRoutine = StartCoroutine(ShowCenterRoutine(text, color, fontSize));
    }

    private IEnumerator ShowCenterRoutine(string text, Color color, int fontSize)
    {
        countdownText.gameObject.SetActive(true);
        countdownText.text = text;
        countdownText.color = color;
        countdownText.fontSize = fontSize;
        yield return new WaitForSeconds(0.7f);
        countdownText.gameObject.SetActive(false);
    }

    // ダメージポップアップ (仕様 3.6): 通常=白, 特大=ゴールド拡大, ∞特大=赤で特大
    private void HandleDamageDealt(int damage, bool isBurst, bool isLinkBurst)
    {
        if (damagePopupText == null) return;
        if (popupRoutine != null) StopCoroutine(popupRoutine);
        popupRoutine = StartCoroutine(PopupRoutine(damage, isBurst, isLinkBurst));
    }

    private IEnumerator PopupRoutine(int damage, bool isBurst, bool isLinkBurst)
    {
        damagePopupText.gameObject.SetActive(true);
        damagePopupText.text = $"{damage}";
        if (isLinkBurst)
        {
            damagePopupText.color = new Color(1f, 0.25f, 0.2f);
            damagePopupText.fontSize = 64;
        }
        else if (isBurst)
        {
            damagePopupText.color = new Color(1f, 0.85f, 0.2f);
            damagePopupText.fontSize = 52;
        }
        else
        {
            damagePopupText.color = Color.white;
            damagePopupText.fontSize = 36;
        }

        yield return new WaitForSeconds(0.6f);
        damagePopupText.gameObject.SetActive(false);
    }

    private void ShowResult(string text, Color color)
    {
        if (resultText == null) return;
        resultText.gameObject.SetActive(true);
        resultText.text = text;
        resultText.color = color;
    }

    private string ColorLabel(CardColor color)
    {
        switch (color)
        {
            case CardColor.Red: return "赤(炎)";
            case CardColor.Green: return "緑(風)";
            case CardColor.Blue: return "青(氷)";
            case CardColor.White: return "白(万能)";
            default: return "自由";
        }
    }

    private Color ColorTint(CardColor color)
    {
        switch (color)
        {
            case CardColor.Red: return new Color(0.95f, 0.3f, 0.35f);
            case CardColor.Green: return new Color(0.2f, 0.8f, 0.45f);
            case CardColor.Blue: return new Color(0.15f, 0.55f, 1.0f);
            case CardColor.White: return new Color(0.95f, 0.95f, 0.95f);
            default: return Color.white;
        }
    }
}
