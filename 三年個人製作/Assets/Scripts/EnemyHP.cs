using UnityEngine;
using UnityEngine.UI;

public class EnemyHP : MonoBehaviour
{
    public Slider hpSlider;

    public int maxHP = 100;
    private int currentHP;

    void Start()
    {
        currentHP = maxHP;

        hpSlider.maxValue = maxHP;
        hpSlider.value = currentHP;
    }

    public void TakeDamage(int damage)
    {
        currentHP -= damage;

        if (currentHP < 0)
            currentHP = 0;

        hpSlider.value = currentHP;

        if (currentHP <= 0)
        {
            Destroy(gameObject);
        }
    }
}