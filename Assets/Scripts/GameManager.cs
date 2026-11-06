
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("連携するオブジェクト")]
    [SerializeField] private PuzzleBoard puzzleBoard;
    [SerializeField] private PlayerAttack[] playerAttacks;
    [SerializeField] private EnemyHP targetEnemy;

    [Header("攻撃設定")]
    [SerializeField] private int damagePerOrb = 10;
    [SerializeField] private float comboDamageMultiplier = 0.5f;
    [SerializeField] private bool logDetails = true;

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
        // Awake時点で参照できなかった場合にも対応
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
            Debug.LogError(
                "GameManager：PuzzleBoardが見つかりません。",
                this
            );
            return;
        }

        // 多重登録を防ぐ
        puzzleBoard.OnMatchesResolved -= OnMatchesResolved;
        puzzleBoard.OnMatchesResolved += OnMatchesResolved;
    }

    /// <summary>
    /// PuzzleBoardで消去と連鎖が終了したときに呼ばれる。
    /// </summary>
    private void OnMatchesResolved(
        Dictionary<int, int> matchCounts,
        int comboCount)
    {
        if (matchCounts == null || matchCounts.Count == 0)
        {
            Debug.LogWarning("GameManager：消去数を受け取れませんでした。");
            return;
        }

        if (targetEnemy == null)
        {
            targetEnemy = FindObjectOfType<EnemyHP>();
        }

        if (targetEnemy == null)
        {
            Debug.LogError(
                "GameManager：EnemyHPが見つかりません。攻撃できません。",
                this
            );
            return;
        }

        if (playerAttacks == null || playerAttacks.Length == 0)
        {
            playerAttacks = FindObjectsOfType<PlayerAttack>();
        }

        if (playerAttacks == null || playerAttacks.Length == 0)
        {
            Debug.LogError(
                "GameManager：PlayerAttackが見つかりません。",
                this
            );
            return;
        }

        int totalOrbs = 0;

        foreach (KeyValuePair<int, int> pair in matchCounts)
        {
            totalOrbs += pair.Value;
        }

        // 3個消去なら基本ダメージ30。
        // コンボ数が増えると追加ダメージが加算される。
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
                $"コンボ={comboCount}, 計算ダメージ={damage}"
            );
        }

        // 消去した色に対応するキャラクターだけ攻撃させる。
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
                    $"が攻撃。色ID={type}, 消去数={matchedCount}, " +
                    $"ダメージ={pieceDamage}"
                );
            }

            playerAttack.Attack(targetEnemy, pieceDamage);
            attackIssued = true;
        }

        if (!attackIssued && logDetails)
        {
            Debug.LogWarning(
                "GameManager：消去した色とPlayerAttackのpieceKindが一致しません。" +
                " PieceKindとドロップの色IDを確認してください。",
                this
            );
        }
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
                playerAttack.Attack(targetEnemy, Mathf.Max(1, damage));
            }
        }
    }
}
