
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

/// <summary>
/// PlayerのHP管理。
/// HPスライダーへの反映、ダメージ、死亡判定を行う。
/// </summary>
public class PlayerHP : MonoBehaviour
{
    [Header("HP設定")]
    [SerializeField] private int maxHP = 100;
    [SerializeField] private int currentHP = 100;

    [Header("HPスライダー")]
    [SerializeField] private Slider hpSlider;

    [Header("死亡時のイベント")]
    [SerializeField] private UnityEvent onDeath;

    [Header("ログ設定")]
    [SerializeField] private bool showLog = true;

    private bool isDead = false;

    public int MaxHP => maxHP;
    public int CurrentHP => currentHP;
    public bool IsDead => isDead;

    private void Start()
    {
        // HPの初期値を設定
        maxHP = Mathf.Max(1, maxHP);
        currentHP = Mathf.Clamp(currentHP, 0, maxHP);

        UpdateHPUI();

        if (currentHP <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// ダメージを受ける。
    /// </summary>
    public void TakeDamage(int damage)
    {
        if (isDead)
        {
            return;
        }

        if (damage <= 0)
        {
            return;
        }

        currentHP = Mathf.Max(0, currentHP - damage);

        UpdateHPUI();

        if (showLog)
        {
            Debug.Log(
                $"Player：{damage}ダメージ！ " +
                $"HP：{currentHP}/{maxHP}"
            );
        }

        if (currentHP <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// HPを回復する。
    /// </summary>
    public void Heal(int amount)
    {
        if (isDead || amount <= 0)
        {
            return;
        }

        currentHP = Mathf.Min(maxHP, currentHP + amount);

        UpdateHPUI();

        if (showLog)
        {
            Debug.Log(
                $"Player：{amount}回復！ " +
                $"HP：{currentHP}/{maxHP}"
            );
        }
    }

    /// <summary>
    /// HPスライダーを更新する。
    /// </summary>
    private void UpdateHPUI()
    {
        if (hpSlider == null)
        {
            return;
        }

        hpSlider.minValue = 0;
        hpSlider.maxValue = maxHP;
        hpSlider.value = currentHP;
    }

    /// <summary>
    /// Player死亡処理。
    /// </summary>
    private void Die()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;

        if (showLog)
        {
            Debug.Log("Playerが倒れました。");
        }

        onDeath?.Invoke();
    }
}
