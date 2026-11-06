
using UnityEngine;

public class AttackProjectile : MonoBehaviour
{
    [Header("移動設定")]
    [Min(0.01f)][SerializeField] private float moveSpeed = 12f;
    [Min(0.01f)][SerializeField] private float hitDistance = 0.3f;

    [Header("命中エフェクト")]
    [SerializeField] private GameObject hitEffect;

    [Header("自動削除")]
    [Min(0.1f)][SerializeField] private float lifeTime = 5f;

    private EnemyHP target;
    private int damage;
    private bool initialized;
    private bool hasHit;

    public void Initialize(EnemyHP enemy, int attackDamage)
    {
        target = enemy;
        damage = Mathf.Max(0, attackDamage);
        initialized = true;
        hasHit = false;

        Destroy(gameObject, Mathf.Max(0.1f, lifeTime));

        if (target == null)
            Debug.LogWarning("AttackProjectile: 対象の敵がありません。");
    }

    private void Update()
    {
        if (!initialized || hasHit)
            return;

        if (target == null || target.IsDead)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 targetPosition = target.transform.position;
        Vector3 difference = targetPosition - transform.position;

        if (difference.sqrMagnitude <= hitDistance * hitDistance)
        {
            Hit();
            return;
        }

        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            moveSpeed * Time.deltaTime);

        if (difference.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(difference);
    }

    private void Hit()
    {
        if (target != null)
        {
            Debug.Log(
                $"[命中] 敵={target.name}, ダメージ={damage}"
            );

            target.TakeDamage(damage);
        }
        else
        {
            Debug.LogWarning("[命中失敗] 攻撃対象が存在しません");
        }

        if (hitEffect != null)
        {
            Instantiate(
                hitEffect,
                transform.position,
                Quaternion.identity
            );
        }

        Destroy(gameObject);
    }
}
