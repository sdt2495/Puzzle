using UnityEngine;

public class PlayerAttack : MonoBehaviour
{
    public Piece.PieceKind pieceKind;

    [SerializeField] private Transform firePoint;
    [SerializeField] private AttackProjectile projectilePrefab;

    public void Attack(EnemyHP enemy, int damage)
    {
        Debug.Log(gameObject.name + " Attack!");

        if (enemy == null)
        {
            Debug.Log("Enemy NULL");
            return;
        }

        if (projectilePrefab == null)
        {
            Debug.LogError("ProjectilePrefab Ç™ê›íËÇ≥ÇÍÇƒÇ¢Ç‹ÇπÇÒ");
            return;
        }

        if (firePoint == null)
        {
            Debug.LogError("FirePoint Ç™ê›íËÇ≥ÇÍÇƒÇ¢Ç‹ÇπÇÒ");
            return;
        }

        AttackProjectile projectile =
            Instantiate(projectilePrefab,
                        firePoint.position,
                        Quaternion.identity);

        projectile.Initialize(enemy, damage);
    }
}