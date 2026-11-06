
using UnityEngine;
using UnityEngine.UI;
using System;

public class EnemyHP : MonoBehaviour
{
    [Header("HP設定")]
    [SerializeField] private int maxHP = 100;
    [SerializeField] private int currentHP = 100;

    [Header("HPバー（Sliderを使用する場合）")]
    [SerializeField] private Slider hpSlider;

    [Header("HP表示（任意）")]
    [SerializeField] private Text hpText;

    [Header("死亡時の設定")]
    [SerializeField] private bool destroyOnDeath = false;
    [SerializeField] private float destroyDelay = 1f;

    private bool isDead;

    public int MaxHP => maxHP;
    public int CurrentHP => currentHP;
    public bool IsDead => isDead;

    public event Action<int, int> OnHPChanged;
    public event Action OnDeath;

    private void Awake()
    {
        maxHP = Mathf.Max(1, maxHP);
        currentHP = Mathf.Clamp(currentHP, 0, maxHP);

        isDead = currentHP <= 0;

        UpdateHPUI();
    }

    /// <summary>
    /// 攻撃を受けたときに呼び出す。
    /// </summary>
    public void TakeDamage(int damage)
    {
        if (isDead)
        {
            Debug.Log("EnemyHP：敵はすでに倒れています。", this);
            return;
        }

        if (damage <= 0)
        {
            Debug.LogWarning(
                "EnemyHP：ダメージが0以下です。",
                this
            );
            return;
        }

        int beforeHP = currentHP;

        currentHP = Mathf.Max(0, currentHP - damage);

        Debug.Log(
            $"EnemyHP：{gameObject.name} が {damage} ダメージを受けました。" +
            $" HP：{beforeHP} → {currentHP}/{maxHP}",
            this
        );

        UpdateHPUI();

        OnHPChanged?.Invoke(currentHP, maxHP);

        if (currentHP <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// HPバーとHPテキストを更新する。
    /// </summary>
    private void UpdateHPUI()
    {
        if (hpSlider != null)
        {
            hpSlider.minValue = 0;
            hpSlider.maxValue = maxHP;
            hpSlider.value = currentHP;
        }

        if (hpText != null)
        {
            hpText.text = currentHP + " / " + maxHP;
        }
    }

    /// <summary>
    /// 回復処理。
    /// </summary>
    public void Heal(int amount)
    {
        if (isDead || amount <= 0)
        {
            return;
        }

        currentHP = Mathf.Min(maxHP, currentHP + amount);

        UpdateHPUI();

        OnHPChanged?.Invoke(currentHP, maxHP);

        Debug.Log(
            $"EnemyHP：{amount} 回復。現在HP={currentHP}/{maxHP}",
            this
        );
    }

    /// <summary>
    /// 敵の死亡処理。
    /// </summary>
    private void Die()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;

        Debug.Log($"EnemyHP：{gameObject.name} を倒しました！", this);

        OnDeath?.Invoke();

        if (destroyOnDeath)
        {
            Destroy(gameObject, destroyDelay);
        }
    }

    /// <summary>
    /// HPを最大まで戻す。
    /// </summary>
    public void ResetHP()
    {
        currentHP = maxHP;
        isDead = false;

        UpdateHPUI();

        OnHPChanged?.Invoke(currentHP, maxHP);
    }
}
