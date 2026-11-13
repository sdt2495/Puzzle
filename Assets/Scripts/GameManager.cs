
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// プレイヤーの攻撃管理と敵のターン管理。
///
/// ・パズルの消去数に応じて敵を攻撃
/// ・コンボ数に応じてダメージ増加
/// ・プレイヤーが3ターン行動すると敵が攻撃
/// ・敵の攻撃でPlayerのHPを減らす
/// </summary>
public class GameManager : MonoBehaviour
{
    [Header("連携するオブジェクト")]
    [SerializeField] private PuzzleBoard puzzleBoard;
    [SerializeField] private PlayerAttack[] playerAttacks;
    [SerializeField] private EnemyHP targetEnemy;
    [SerializeField] private PlayerHP playerHP;

    [Header("プレイヤー攻撃設定")]
    [SerializeField] private int damagePerOrb = 10;
    [SerializeField] private float comboDamageMultiplier = 0.5f;

    [Header("敵の攻撃設定")]
    [SerializeField] private int playerTurnsPerEnemyAttack = 3;
    [SerializeField] private int enemyAttackDamage = 20;

    [Header("ログ設定")]
    [SerializeField] private bool logDetails = true;

    // 現在のプレイヤーターン数
    private int playerTurnCount = 0;

    private void Awake()
    {
        if (puzzleBoard == null)
        {
            puzzleBoard = FindObjectOfType<PuzzleBoard>();
        }

        if (targetEnemy == null)
        {
            targetEnemy = FindObjectOfType<EnemyHP>();
        }

        if (playerHP == null)
        {
            playerHP = FindObjectOfType<PlayerHP>();
        }

        if (playerAttacks == null || playerAttacks.Length == 0)
        {
            playerAttacks = FindObjectsOfType<PlayerAttack>();
        }
    }

    private void OnEnable()
    {
        SubscribePuzzleBoard();
    }

    private void Start()
    {
        SubscribePuzzleBoard();
    }

    private void OnDisable()
    {
        if (puzzleBoard != null)
        {
            puzzleBoard.OnMatchesResolved -= OnMatchesResolved;
        }
    }

    private void SubscribePuzzleBoard()
    {
        if (puzzleBoard == null)
        {
            puzzleBoard = FindObjectOfType<PuzzleBoard>();
        }

        if (puzzleBoard == null)
        {
            Debug.LogError("GameManager：PuzzleBoardが見つかりません。", this);
            return;
        }

        // 多重登録を防ぐ
        puzzleBoard.OnMatchesResolved -= OnMatchesResolved;
        puzzleBoard.OnMatchesResolved += OnMatchesResolved;
    }

    /// <summary>
    /// パズルの消去と連鎖が終了したときに呼ばれる。
    /// </summary>
    private void OnMatchesResolved(
        Dictionary<int, int> matchCounts,
        int comboCount)
    {
        if (matchCounts == null || matchCounts.Count == 0)
        {
            return;
        }

        if (playerHP == null)
        {
            playerHP = FindObjectOfType<PlayerHP>();
        }

        // Playerが倒れている場合は処理しない
        if (playerHP != null && playerHP.IsDead)
        {
            return;
        }

        if (targetEnemy == null)
        {
            targetEnemy = FindObjectOfType<EnemyHP>();
        }

        if (targetEnemy == null)
        {
            Debug.LogError("GameManager：EnemyHPが見つかりません。", this);
            return;
        }

        if (playerAttacks == null || playerAttacks.Length == 0)
        {
            playerAttacks = FindObjectsOfType<PlayerAttack>();
        }

        if (playerAttacks == null || playerAttacks.Length == 0)
        {
            Debug.LogError("GameManager：PlayerAttackが見つかりません。", this);
            return;
        }

        // 消去したオーブの合計数
        int totalOrbs = 0;

        foreach (KeyValuePair<int, int> pair in matchCounts)
        {
            totalOrbs += pair.Value;
        }

        // コンボ倍率を計算
        float comboMultiplier =
            1f + Mathf.Max(0, comboCount - 1) * comboDamageMultiplier;

        int damage = Mathf.Max(
            1,
            Mathf.RoundToInt(totalOrbs * damagePerOrb * comboMultiplier)
        );

        if (logDetails)
        {
            Debug.Log(
                $"GameManager：消去数={totalOrbs}, " +
                $"コンボ={comboCount}, ダメージ={damage}"
            );
        }

        // 消去した色に対応するPlayerだけ攻撃
        bool attackIssued = false;

        foreach (PlayerAttack playerAttack in playerAttacks)
        {
            if (playerAttack == null)
            {
                continue;
            }

            int type = (int)playerAttack.pieceKind;

            if (!matchCounts.TryGetValue(type, out int matchedCount) ||
                matchedCount <= 0)
            {
                continue;
            }

            int pieceDamage = Mathf.Max(
                1,
                Mathf.RoundToInt(
                    matchedCount * damagePerOrb * comboMultiplier
                )
            );

            if (logDetails)
            {
                Debug.Log(
                    $"GameManager：{playerAttack.gameObject.name} " +
                    $"が攻撃。ダメージ={pieceDamage}"
                );
            }

            playerAttack.Attack(targetEnemy, pieceDamage);
            attackIssued = true;
        }

        if (!attackIssued && logDetails)
        {
            Debug.LogWarning(
                "GameManager：消去した色とPlayerAttackのpieceKindが一致しません。"
            );
        }

        // プレイヤーの行動が終了したのでターンを加算
        EndPlayerTurn();
    }

    /// <summary>
    /// プレイヤーのターン終了処理。
    /// </summary>
    private void EndPlayerTurn()
    {
        playerTurnCount++;

        if (logDetails)
        {
            Debug.Log(
                $"プレイヤーターン：{playerTurnCount}/" +
                $"{playerTurnsPerEnemyAttack}"
            );
        }

        // 設定したターン数に到達したら敵が攻撃
        if (playerTurnCount >= Mathf.Max(1, playerTurnsPerEnemyAttack))
        {
            playerTurnCount = 0;
            EnemyAttack();
        }
    }

    /// <summary>
    /// 敵の攻撃処理。
    /// </summary>
    private void EnemyAttack()
    {
        if (playerHP == null)
        {
            playerHP = FindObjectOfType<PlayerHP>();
        }

        if (playerHP == null)
        {
            Debug.LogError("GameManager：PlayerHPが見つかりません。");
            return;
        }

        if (playerHP.IsDead)
        {
            return;
        }

        if (logDetails)
        {
            Debug.Log(
                $"敵の攻撃！ Playerに{enemyAttackDamage}ダメージ！"
            );
        }

        playerHP.TakeDamage(enemyAttackDamage);
    }

    /// <summary>
    /// 外部から攻撃を実行したい場合にも使用できる。
    /// </summary>
    public void RequestAttack(int damage)
    {
        if (targetEnemy == null)
        {
            targetEnemy = FindObjectOfType<EnemyHP>();
        }

        if (targetEnemy == null)
        {
            Debug.LogError("GameManager：攻撃対象のEnemyHPがありません。");
            return;
        }

        if (playerAttacks == null || playerAttacks.Length == 0)
        {
            playerAttacks = FindObjectsOfType<PlayerAttack>();
        }

        if (playerAttacks == null || playerAttacks.Length == 0)
        {
            Debug.LogError("GameManager：PlayerAttackがありません。");
            return;
        }

        foreach (PlayerAttack playerAttack in playerAttacks)
        {
            if (playerAttack != null)
            {
                playerAttack.Attack(
                    targetEnemy,
                    Mathf.Max(1, damage)
                );
            }
        }
    }
}
