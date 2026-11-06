
using UnityEngine;

public class PlayerAttack : MonoBehaviour
{
    [Header("プレイヤーの種類")]
    public Piece.PieceKind pieceKind;

    [Header("攻撃設定")]
    [SerializeField] private Transform firePoint;
    [SerializeField] private AttackProjectile projectilePrefab;

    public void Attack(EnemyHP enemy, int damage)
    {
        Debug.Log($"{gameObject.name}: Attack() 呼び出し。ダメージ={damage}");

        if (enemy == null || enemy.IsDead)
        {
            Debug.LogWarning("PlayerAttack: 攻撃対象の敵がいません。");
            return;
        }

        if (damage <= 0)
        {
            Debug.LogWarning("PlayerAttack: ダメージが0以下です。");
            return;
        }

        // 直接ダメージ
        enemy.TakeDamage(damage);

        Debug.Log(
            $"[攻撃] キャラクター={gameObject.name}, " +
            $"敵={enemy.name}, ダメージ={damage}"
        );
    }
}
