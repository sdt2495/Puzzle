using UnityEngine;

public class AttackProjectile : MonoBehaviour
{
    [Header("移動速度")]
    [SerializeField] private float moveSpeed = 12f;

    [Header("命中距離")]
    [SerializeField] private float hitDistance = 0.3f;

    [Header("命中エフェクト")]
    [SerializeField] private GameObject hitEffect;

    [Header("自動削除時間")]
    [SerializeField] private float lifeTime = 5f;

    private EnemyHP target;
    private int damage;

    //--------------------------------------------------
    // 初期化
    //--------------------------------------------------
    public void Initialize(EnemyHP enemy, int attackDamage)
    {
        target = enemy;
        damage = attackDamage;

        Destroy(gameObject, lifeTime);
    }

    //--------------------------------------------------
    private void Update()
    {
        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 targetPos = target.transform.position;

        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPos,
            moveSpeed * Time.deltaTime);

        // 飛ぶ方向を向く
        Vector3 dir = targetPos - transform.position;

        if (dir.sqrMagnitude > 0.001f)
        {
            transform.rotation =
                Quaternion.LookRotation(dir);
        }

        if (Vector3.Distance(transform.position, targetPos) <= hitDistance)
        {
            Hit();
        }
    }

    //--------------------------------------------------
    private void Hit()
    {
        if (target != null)
        {
            target.TakeDamage(damage);
        }

        if (hitEffect != null)
        {
            Instantiate(
                hitEffect,
                transform.position,
                Quaternion.identity);
        }

        Destroy(gameObject);
    }
}