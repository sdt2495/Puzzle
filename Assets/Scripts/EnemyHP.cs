using UnityEngine;
using UnityEngine.UI;

public class EnemyHP : MonoBehaviour
{
    [SerializeField] private Slider hpSlider;

    [SerializeField] private int maxHP = 100;

    private int currentHP;

    private bool isDead = false;

    private void Start()
    {
        currentHP = maxHP;

        if (hpSlider != null)
        {
            hpSlider.maxValue = maxHP;
            hpSlider.value = currentHP;
        }
    }

    public void TakeDamage(int damage)
    {
        // Šù‚É“|‚³‚ê‚Ä‚¢‚½‚ç‰½‚à‚µ‚È‚¢
        if (isDead)
            return;

        currentHP -= damage;

        if (currentHP < 0)
            currentHP = 0;

        if (hpSlider != null)
            hpSlider.value = currentHP;

        if (currentHP <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        isDead = true;

        // GameManager‚Ö’Ê’m
        if (GameManager.Instance != null)
        {
            GameManager.Instance.NextEnemy();
        }

        Destroy(gameObject);
    }
}