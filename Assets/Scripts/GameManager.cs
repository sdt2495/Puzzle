
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PuzzleBoardと連携して、コンボ・敵への攻撃・カメラ移動を管理する。
///
/// ・PuzzleBoardから消去結果を受け取る
/// ・消去したオーブの種類に対応するプレイヤーで攻撃する
/// ・コンボ数に応じてダメージを増加させる
/// ・敵撃破後にカメラを次の敵へ移動する
/// ・すべての敵を倒したらゲームクリアにする
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    // 3個以上で消去する
    public const int MatchingCount = 3;

    [Header("盤面")]
    [SerializeField] private PuzzleBoard puzzleBoard;
    [SerializeField] private Text stateText;

    [Header("オーブとプレイヤーの対応")]
    [Tooltip("PuzzleBoardのオーブ番号とPiece.PieceKindを同じ順番で設定する")]
    [SerializeField] private Piece.PieceKind[] orbTypeToPieceKind;

    [Header("敵")]
    [SerializeField] private EnemyHP[] enemies;

    [Header("プレイヤー")]
    [SerializeField] private PlayerManager playerManager;

    [Header("カメラ")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Transform[] cameraPoints;

    [Min(0.01f)]
    [SerializeField] private float cameraMoveTime = 1.5f;

    [Header("攻撃設定")]
    [Min(0)]
    [SerializeField] private int damagePerPiece = 10;

    // 現在の敵の配列番号
    private int currentEnemyIndex;

    // 直近の連鎖で成立したコンボ数
    private int comboCount;

    // カメラ移動中かどうか
    private bool isCameraMoving;

    // ゲームクリア済みかどうか
    private bool isGameClear;

    // 現在の敵を取得する
    private EnemyHP CurrentEnemy
    {
        get
        {
            if (enemies == null ||
                currentEnemyIndex < 0 ||
                currentEnemyIndex >= enemies.Length)
            {
                return null;
            }

            return enemies[currentEnemyIndex];
        }
    }

    // ============================================================
    // 初期化
    // ============================================================

    private void Awake()
    {
        // GameManagerの重複を確認する
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("GameManagerが複数存在しています。", this);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        // PuzzleBoardの参照を確認する
        if (puzzleBoard == null)
        {
            Debug.LogError(
                "GameManager：PuzzleBoardが設定されていません。",
                this
            );

            enabled = false;
            return;
        }

        // PlayerManagerの参照を確認する
        if (playerManager == null)
        {
            Debug.LogError(
                "GameManager：PlayerManagerが設定されていません。",
                this
            );

            enabled = false;
            return;
        }

        // カメラが未設定ならMainCameraを取得する
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        // ゲームの状態を初期化する
        currentEnemyIndex = 0;
        comboCount = 0;
        isCameraMoving = false;
        isGameClear = false;

        // PuzzleBoardから消去結果を受け取る
        puzzleBoard.OnMatchesResolved += OnMatchesResolved;

        UpdateStateText("Idle");
    }

    private void OnDestroy()
    {
        // イベント登録を解除する
        if (puzzleBoard != null)
        {
            puzzleBoard.OnMatchesResolved -= OnMatchesResolved;
        }

        // 自分自身の参照だけを解除する
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // ============================================================
    // オーブ番号の変換
    // ============================================================

    /// <summary>
    /// PuzzleBoardから受け取ったオーブ番号を、
    /// 対応するPiece.PieceKindに変換する。
    ///
    /// 例：
    /// orbTypeToPieceKindの0番目はオーブ番号0の対応先。
    /// orbTypeToPieceKindの1番目はオーブ番号1の対応先。
    /// </summary>
    private bool TryConvertOrbType(
        int orbType,
        out Piece.PieceKind pieceKind)
    {
        if (orbTypeToPieceKind != null &&
            orbType >= 0 &&
            orbType < orbTypeToPieceKind.Length)
        {
            pieceKind = orbTypeToPieceKind[orbType];
            return true;
        }

        pieceKind = default(Piece.PieceKind);
        return false;
    }

    // ============================================================
    // PuzzleBoardからの消去結果受け取り
    // ============================================================

    /// <summary>
    /// 消去されたオーブの種類ごとの個数とコンボ数を受け取る。
    ///
    /// clearedCountsのキー：オーブの種類番号
    /// clearedCountsの値：その種類の消去数
    /// resolvedComboCount：連鎖全体のコンボ数
    /// </summary>
    private void OnMatchesResolved(
        Dictionary<int, int> clearedCounts,
        int resolvedComboCount)
    {
        // ゲームクリア後は処理しない
        if (isGameClear || clearedCounts == null)
        {
            return;
        }

        // コンボ数を更新する
        comboCount = resolvedComboCount;

        UpdateStateText(
            comboCount > 0
                ? comboCount + " Combo"
                : "Idle"
        );

        // コンボが成立していなければ攻撃しない
        if (comboCount <= 0)
        {
            return;
        }

        // 攻撃対象の敵を取得する
        EnemyHP enemy = CurrentEnemy;

        if (enemy == null)
        {
            Debug.LogWarning(
                "GameManager：攻撃対象の敵が設定されていません。",
                this
            );

            return;
        }

        // 消去されたオーブの種類ごとに攻撃する
        foreach (KeyValuePair<int, int> pair in clearedCounts)
        {
            int orbType = pair.Key;
            int count = pair.Value;

            // 消去数が0以下ならスキップする
            if (count <= 0)
            {
                continue;
            }

            // オーブ番号をプレイヤーの種類に変換する
            if (!TryConvertOrbType(orbType, out Piece.PieceKind pieceKind))
            {
                Debug.LogWarning(
                    "GameManager：オーブ番号 " + orbType +
                    " の対応先が設定されていません。",
                    this
                );

                continue;
            }

            // 対応するプレイヤーを取得する
            PlayerAttack player = playerManager.GetPlayer(pieceKind);

            if (player == null)
            {
                Debug.LogWarning(
                    pieceKind + " に対応するプレイヤーが見つかりません。",
                    this
                );

                continue;
            }

            // 消去数 × 1個あたりのダメージ × コンボ数
            int damage = count * damagePerPiece * comboCount;

            // 敵にダメージを与える
            player.Attack(enemy, damage);

            Debug.Log(
                pieceKind + "：" +
                count + "個消去 / " +
                comboCount + "コンボ / " +
                damage + "ダメージ"
            );
        }
    }

    // ============================================================
    // 敵撃破後の処理
    // ============================================================

    /// <summary>
    /// 現在の敵が倒されたときに呼び出す。
    /// EnemyHP側からGameManager.Instance.NextEnemy()を呼び出す。
    /// </summary>
    public void NextEnemy()
    {
        // カメラ移動中やクリア後は処理しない
        if (isCameraMoving || isGameClear)
        {
            return;
        }

        // 次の敵へ進む
        currentEnemyIndex++;

        // すべての敵を倒したか確認する
        if (enemies == null ||
            currentEnemyIndex >= enemies.Length)
        {
            isGameClear = true;
            comboCount = 0;

            UpdateStateText("Game Clear");
            Debug.Log("Game Clear");

            return;
        }

        // カメラ参照を確認する
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (mainCamera == null)
        {
            Debug.LogError(
                "GameManager：MainCameraが見つかりません。",
                this
            );

            return;
        }

        // 次の敵のカメラ位置を確認する
        if (cameraPoints == null ||
            currentEnemyIndex >= cameraPoints.Length ||
            cameraPoints[currentEnemyIndex] == null)
        {
            Debug.LogError(
                "GameManager：次の敵のcameraPointsが設定されていません。",
                this
            );

            return;
        }

        // カメラを次の敵へ移動する
        StartCoroutine(MoveCamera());
    }

    /// <summary>
    /// カメラを次の敵の位置へ移動する。
    /// </summary>
    private IEnumerator MoveCamera()
    {
        // 必要な参照を確認する
        if (mainCamera == null ||
            cameraPoints == null ||
            currentEnemyIndex >= cameraPoints.Length ||
            cameraPoints[currentEnemyIndex] == null)
        {
            isCameraMoving = false;
            yield break;
        }

        isCameraMoving = true;
        UpdateStateText("CameraMove");

        // 移動開始位置と回転を保存する
        Vector3 startPosition = mainCamera.transform.position;
        Quaternion startRotation = mainCamera.transform.rotation;

        Transform target = cameraPoints[currentEnemyIndex];

        Vector3 endPosition = target.position;
        Quaternion endRotation = target.rotation;

        float duration = Mathf.Max(cameraMoveTime, 0.01f);
        float timer = 0f;

        // 時間をかけてカメラを移動する
        while (timer < duration)
        {
            // 移動中に参照が破棄されていないか確認する
            if (mainCamera == null || target == null)
            {
                isCameraMoving = false;
                yield break;
            }

            timer += Time.deltaTime;

            float t = Mathf.Clamp01(timer / duration);

            mainCamera.transform.position =
                Vector3.Lerp(startPosition, endPosition, t);

            mainCamera.transform.rotation =
                Quaternion.Slerp(startRotation, endRotation, t);

            yield return null;
        }

        // 最終位置を正確に設定する
        if (mainCamera != null && target != null)
        {
            mainCamera.transform.position = endPosition;
            mainCamera.transform.rotation = endRotation;
        }

        isCameraMoving = false;
        UpdateStateText("Idle");
    }

    // ============================================================
    // 状態表示
    // ============================================================

    /// <summary>
    /// 状態表示用のUIテキストを更新する。
    /// </summary>
    private void UpdateStateText(string message)
    {
        if (stateText != null)
        {
            stateText.text = message;
        }
    }

    // ============================================================
    // 外部から状態を取得するためのプロパティ
    // ============================================================

    public int CurrentEnemyIndex
    {
        get { return currentEnemyIndex; }
    }

    public int ComboCount
    {
        get { return comboCount; }
    }

    public bool IsCameraMoving
    {
        get { return isCameraMoving; }
    }

    public bool IsGameClear
    {
        get { return isGameClear; }
    }
}