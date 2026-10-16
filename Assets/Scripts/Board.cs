
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// UI方式のパズル盤面を管理するクラス。
///
/// ・ドラッグによるピースの入れ替え（斜め移動対応）
/// ・操作時間制限
/// ・縦横3個以上のピース消去
/// ・落下、補充、連鎖
/// ・コンボ表示、操作時間バー
/// ・色ごとの消去数とコンボ数をGameManagerへ通知
/// </summary>
public class Board : MonoBehaviour
{
    [Header("盤面設定")]
    [Min(1)]
    [SerializeField] private int width = 6;

    [Min(1)]
    [SerializeField] private int height = 5;

    [Min(1f)]
    [SerializeField] private float pieceWidth = 100f;

    [Header("UI設定")]
    [SerializeField] private GameObject piecePrefab;
    [SerializeField] private RectTransform boardPanel;

    [Tooltip("背景用Image。不要なら未設定でOK")]
    [SerializeField] private Image boardBackground;

    [Header("操作設定")]
    [Min(0.01f)]
    [SerializeField] private float moveTimeLimit = 4f;

    [Range(0.2f, 0.5f)]
    [SerializeField] private float swapRadius = 0.42f;

    [Header("移動演出")]
    [Min(0.01f)]
    [SerializeField] private float swapDuration = 0.08f;

    [Min(0.01f)]
    [SerializeField] private float fallDuration = 0.15f;

    [Min(0.01f)]
    [SerializeField] private float clearDuration = 0.3f;

    [Header("消去設定")]
    [Min(3)]
    [SerializeField] private int minimumMatchCount = 3;

    [Header("コンボ表示")]
    [SerializeField] private Text comboText;

    [Min(0.1f)]
    [SerializeField] private float comboDisplayDuration = 1.5f;

    [Header("操作時間バー")]
    [SerializeField] private Image timeBar;
    [SerializeField] private Image timeBarBackground;

    [Header("初期盤面")]
    [Tooltip("初期盤面に3個以上の並びができないようにする")]
    [SerializeField] private bool preventInitialMatches = true;

    [Min(1)]
    [SerializeField] private int randomTypeAttempts = 100;

    /// <summary>
    /// 連鎖終了時にGameManagerへ通知するイベント。
    /// Dictionaryのキーはピースの種類、値は総消去数。
    /// </summary>
    public event Action<Dictionary<Piece.PieceKind, int>, int>
        OnMatchesResolved;

    private enum BoardState
    {
        Idle,
        Dragging,
        Resolving
    }

    private Piece[,] board;
    private BoardState state = BoardState.Idle;

    private Piece heldPiece;
    private Vector2Int heldCell;
    private Vector2 dragOffset;

    private bool moveStarted;
    private float moveTimer;
    private float comboTimer;
    private int comboCount;

    private Camera uiCamera;
    private Canvas parentCanvas;

    private readonly Dictionary<Piece.PieceKind, int> lastMatchCounts =
        new Dictionary<Piece.PieceKind, int>();

    private const float DraggingScale = 1.1f;
    private const float DraggingAlpha = 0.85f;
    private const float DraggingMoveSpeed = 1f;
    private const float SpawnOffsetCells = 1f;
    private const float TimeBarFullAmount = 1f;

    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

    public int Width => width;
    public int Height => height;

    private void Awake()
    {
        if (boardPanel == null)
        {
            Debug.LogError(
                "Board：Board Panelが設定されていません。",
                this
            );
            enabled = false;
            return;
        }

        if (piecePrefab == null)
        {
            Debug.LogError(
                "Board：Piece Prefabが設定されていません。",
                this
            );
            enabled = false;
            return;
        }

        parentCanvas = boardPanel.GetComponentInParent<Canvas>();

        if (parentCanvas != null &&
            parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            uiCamera = parentCanvas.worldCamera;
        }

        if (comboText != null)
        {
            comboText.gameObject.SetActive(false);
        }

        if (timeBar != null)
        {
            timeBar.fillAmount = TimeBarFullAmount;
        }

        if (timeBarBackground != null)
        {
            timeBarBackground.gameObject.SetActive(false);
        }
    }

    private void Start()
    {
        InitializeBoard(width, height);
    }

    /// <summary>
    /// 盤面を初期化する。
    /// GameManagerからも呼び出せる。
    /// </summary>
    public void InitializeBoard(int boardWidth, int boardHeight)
    {
        if (boardWidth < 1 || boardHeight < 1)
        {
            Debug.LogError(
                "Board：盤面の幅と高さは1以上にしてください。",
                this
            );
            return;
        }

        if (boardPanel == null || piecePrefab == null)
        {
            Debug.LogError(
                "Board：Board PanelまたはPiece Prefabが未設定です。",
                this
            );
            return;
        }

        StopAllCoroutines();

        width = boardWidth;
        height = boardHeight;

        // 既存のピースを削除
        if (board != null)
        {
            foreach (Piece piece in board)
            {
                if (piece != null)
                {
                    Destroy(piece.gameObject);
                }
            }
        }

        board = new Piece[width, height];

        heldPiece = null;
        heldCell = Vector2Int.zero;
        dragOffset = Vector2.zero;
        moveStarted = false;
        moveTimer = 0f;
        comboTimer = 0f;
        comboCount = 0;
        lastMatchCounts.Clear();
        state = BoardState.Idle;

        if (comboText != null)
        {
            comboText.gameObject.SetActive(false);
        }

        if (timeBar != null)
        {
            timeBar.fillAmount = TimeBarFullAmount;
        }

        if (timeBarBackground != null)
        {
            timeBarBackground.gameObject.SetActive(false);
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Piece.PieceKind kind =
                    preventInitialMatches
                    ? RandomKindWithoutMatch(x, y)
                    : RandomKind();

                CreatePiece(new Vector2Int(x, y), kind, false);
            }
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(boardPanel);
    }

    /// <summary>
    /// ランダムな種類を取得する。
    /// </summary>
    private Piece.PieceKind RandomKind()
    {
        int kindCount = Enum.GetValues(
            typeof(Piece.PieceKind)
        ).Length;

        return (Piece.PieceKind)UnityEngine.Random.Range(
            0,
            kindCount
        );
    }

    /// <summary>
    /// 初期盤面で最初から3個並ばない種類を選ぶ。
    /// </summary>
    private Piece.PieceKind RandomKindWithoutMatch(
        int x,
        int y)
    {
        for (int attempt = 0; attempt < randomTypeAttempts; attempt++)
        {
            Piece.PieceKind kind = RandomKind();

            bool horizontalMatch =
                x >= 2 &&
                board[x - 1, y] != null &&
                board[x - 2, y] != null &&
                board[x - 1, y].GetKind() == kind &&
                board[x - 2, y].GetKind() == kind;

            bool verticalMatch =
                y >= 2 &&
                board[x, y - 1] != null &&
                board[x, y - 2] != null &&
                board[x, y - 1].GetKind() == kind &&
                board[x, y - 2].GetKind() == kind;

            if (!horizontalMatch && !verticalMatch)
            {
                return kind;
            }
        }

        // 試行回数を超えた場合もランダムな種類を返す
        return RandomKind();
    }

    /// <summary>
    /// 指定マスにピースを生成する。
    /// </summary>
    private Piece CreatePiece(
        Vector2Int cell,
        Piece.PieceKind kind,
        bool animate)
    {
        GameObject obj = Instantiate(piecePrefab, boardPanel, false);

        obj.name = $"Piece_{cell.x}_{cell.y}_{kind}";

        Piece piece = obj.GetComponent<Piece>();

        if (piece == null)
        {
            Debug.LogError(
                "Board：Piece PrefabにPiece.csがありません。",
                obj
            );

            Destroy(obj);
            return null;
        }

        RectTransform rt = piece.GetRectTransform();

        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        piece.SetSize(pieceWidth);
        piece.SetKind(kind);
        piece.deleteFlag = false;

        board[cell.x, cell.y] = piece;

        Vector2 targetPosition = GetPieceAnchoredPos(cell);

        if (animate)
        {
            rt.anchoredPosition =
                targetPosition +
                Vector2.up * pieceWidth * SpawnOffsetCells;

            StartCoroutine(piece.MoveTo(
                targetPosition,
                fallDuration
            ));
        }
        else
        {
            rt.anchoredPosition = targetPosition;
        }

        return piece;
    }

    /// <summary>
    /// マス座標をUIのanchoredPositionに変換する。
    /// </summary>
    private Vector2 GetPieceAnchoredPos(Vector2Int cell)
    {
        float offsetX = -(width - 1) * pieceWidth * 0.5f;
        float offsetY = -(height - 1) * pieceWidth * 0.5f;

        return new Vector2(
            cell.x * pieceWidth + offsetX,
            cell.y * pieceWidth + offsetY
        );
    }

    /// <summary>
    /// 画面座標を盤面のローカル座標に変換する。
    /// </summary>
    private bool TryGetBoardLocalPosition(
        Vector2 screenPosition,
        out Vector2 localPosition)
    {
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            boardPanel,
            screenPosition,
            uiCamera,
            out localPosition
        );
    }

    /// <summary>
    /// 画面座標に最も近いピースを取得する。
    /// </summary>
    public Piece GetNearestPiece(Vector3 screenPos)
    {
        if (board == null ||
            !TryGetBoardLocalPosition(screenPos, out Vector2 localPos))
        {
            return null;
        }

        Vector2Int cell = LocalToCell(localPos);

        if (!IsInBoard(cell))
        {
            return null;
        }

        return board[cell.x, cell.y];
    }

    /// <summary>
    /// ローカル座標を最寄りのマスに変換する。
    /// </summary>
    private Vector2Int LocalToCell(Vector2 localPosition)
    {
        float offsetX = -(width - 1) * pieceWidth * 0.5f;
        float offsetY = -(height - 1) * pieceWidth * 0.5f;

        int x = Mathf.RoundToInt(
            (localPosition.x - offsetX) / pieceWidth
        );

        int y = Mathf.RoundToInt(
            (localPosition.y - offsetY) / pieceWidth
        );

        return new Vector2Int(x, y);
    }

    private bool IsInBoard(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < width &&
               cell.y >= 0 && cell.y < height;
    }

    private void Update()
    {
        if (board == null)
        {
            return;
        }

        UpdateComboUI();

        if (state == BoardState.Resolving)
        {
            return;
        }

        HandleInput();
        UpdateTimeBar();
    }

    /// <summary>
    /// マウスまたはタッチ入力を処理する。
    /// </summary>
    private bool ReadPointer(
        out Vector2 position,
        out bool pressed,
        out bool held)
    {
#if ENABLE_INPUT_SYSTEM
        Pointer pointer = Pointer.current;

        if (pointer == null)
        {
            position = Vector2.zero;
            pressed = false;
            held = false;
            return false;
        }

        position = pointer.position.ReadValue();
        pressed = pointer.press.wasPressedThisFrame;
        held = pointer.press.isPressed;
        return true;
#else
        position = Input.mousePosition;
        pressed = Input.GetMouseButtonDown(0);
        held = Input.GetMouseButton(0);
        return true;
#endif
    }

    private void HandleInput()
    {
        if (!ReadPointer(
            out Vector2 screenPosition,
            out bool pressed,
            out bool held))
        {
            return;
        }

        if (!TryGetBoardLocalPosition(
            screenPosition,
            out Vector2 localPosition))
        {
            return;
        }

        if (state == BoardState.Idle)
        {
            if (pressed)
            {
                TryPickPiece(localPosition);
            }

            return;
        }

        if (state != BoardState.Dragging)
        {
            return;
        }

        if (!held)
        {
            ReleasePiece();
            return;
        }

        DragPiece(localPosition);

        if (moveStarted)
        {
            moveTimer -= Time.deltaTime;

            if (moveTimer <= 0f)
            {
                ReleasePiece();
            }
        }
    }

    private void TryPickPiece(Vector2 localPosition)
    {
        Vector2Int cell = LocalToCell(localPosition);

        if (!IsInBoard(cell))
        {
            return;
        }

        Piece piece = board[cell.x, cell.y];

        if (piece == null)
        {
            return;
        }

        heldPiece = piece;
        heldCell = cell;

        dragOffset =
            piece.GetRectTransform().anchoredPosition - localPosition;

        moveStarted = false;
        moveTimer = moveTimeLimit;
        state = BoardState.Dragging;

        heldPiece.transform.SetAsLastSibling();
        heldPiece.transform.localScale =
            Vector3.one * DraggingScale;

        SetPieceAlpha(heldPiece, DraggingAlpha);
    }

    private void DragPiece(Vector2 localPosition)
    {
        if (heldPiece == null)
        {
            ReleasePiece();
            return;
        }

        heldPiece.GetRectTransform().anchoredPosition =
            localPosition + dragOffset;

        Vector2Int targetCell = LocalToCell(localPosition);

        if (!IsInBoard(targetCell) || targetCell == heldCell)
        {
            return;
        }

        Vector2 targetPosition = GetPieceAnchoredPos(targetCell);

        float distance = Vector2.Distance(
            localPosition + dragOffset,
            targetPosition
        );

        if (distance > pieceWidth * swapRadius)
        {
            return;
        }

        // 横・縦・斜めの順に隣接マスを入れ替える
        while (heldCell != targetCell)
        {
            int stepX = Math.Sign(targetCell.x - heldCell.x);
            int stepY = Math.Sign(targetCell.y - heldCell.y);

            Vector2Int nextCell = new Vector2Int(
                heldCell.x + stepX,
                heldCell.y + stepY
            );

            if (!IsInBoard(nextCell))
            {
                break;
            }

            SwitchHeldPieceInto(nextCell);
        }
    }

    /// <summary>
    /// ドラッグ中のピースと指定マスを入れ替える。
    /// </summary>
    private void SwitchHeldPieceInto(Vector2Int nextCell)
    {
        Piece other = board[nextCell.x, nextCell.y];

        board[heldCell.x, heldCell.y] = other;

        if (other != null)
        {
            StartCoroutine(other.MoveTo(
                GetPieceAnchoredPos(heldCell),
                swapDuration
            ));
        }

        board[nextCell.x, nextCell.y] = heldPiece;
        heldCell = nextCell;

        if (!moveStarted)
        {
            moveStarted = true;
            moveTimer = moveTimeLimit;
        }
    }

    /// <summary>
    /// 既存のGameManagerなどからも利用できる入れ替え関数。
    /// </summary>
    public void SwitchPiece(Piece p1, Piece p2)
    {
        if (p1 == null || p2 == null || p1 == p2 || board == null)
        {
            return;
        }

        Vector2Int pos1 = GetPieceBoardPos(p1);
        Vector2Int pos2 = GetPieceBoardPos(p2);

        if (!IsInBoard(pos1) || !IsInBoard(pos2))
        {
            return;
        }

        board[pos1.x, pos1.y] = p2;
        board[pos2.x, pos2.y] = p1;

        StartCoroutine(p1.MoveTo(GetPieceAnchoredPos(pos2), swapDuration));
        StartCoroutine(p2.MoveTo(GetPieceAnchoredPos(pos1), swapDuration));
    }

    private void ReleasePiece()
    {
        if (heldPiece == null)
        {
            state = BoardState.Idle;
            return;
        }

        Piece releasedPiece = heldPiece;

        SetPieceAlpha(releasedPiece, 1f);
        releasedPiece.transform.localScale = Vector3.one;

        StartCoroutine(releasedPiece.MoveTo(
            GetPieceAnchoredPos(heldCell),
            swapDuration
        ));

        heldPiece = null;
        dragOffset = Vector2.zero;

        if (timeBarBackground != null)
        {
            timeBarBackground.gameObject.SetActive(false);
        }

        if (moveStarted)
        {
            StartCoroutine(ResolveBoard());
        }
        else
        {
            state = BoardState.Idle;
        }
    }

    private void SetPieceAlpha(Piece piece, float alpha)
    {
        if (piece == null)
        {
            return;
        }

        Image image = piece.GetComponent<Image>();

        if (image == null)
        {
            return;
        }

        Color color = image.color;
        color.a = alpha;
        image.color = color;
    }

    private void UpdateTimeBar()
    {
        bool show = state == BoardState.Dragging && moveStarted;

        if (timeBarBackground != null)
        {
            timeBarBackground.gameObject.SetActive(show);
        }

        if (timeBar == null)
        {
            return;
        }

        timeBar.gameObject.SetActive(show);

        if (show)
        {
            float ratio = moveTimeLimit > 0f
                ? Mathf.Clamp01(moveTimer / moveTimeLimit)
                : 0f;

            timeBar.fillAmount = ratio;

            timeBar.color = Color.Lerp(
                Color.red,
                Color.green,
                ratio
            );
        }
    }

    private void UpdateComboUI()
    {
        if (comboText == null)
        {
            return;
        }

        if (comboTimer > 0f && state != BoardState.Resolving)
        {
            comboTimer -= Time.deltaTime;
        }

        bool visible = comboTimer > 0f && comboCount > 0;
        comboText.gameObject.SetActive(visible);

        if (visible)
        {
            comboText.text = $"{comboCount} COMBO";
        }
    }

    // -------------------------------------------------------
    // 盤面の判定・消去
    // -------------------------------------------------------

    /// <summary>
    /// 縦横3個以上の並びがあるかを返す。
    /// </summary>
    public bool HasMatch()
    {
        if (board == null)
        {
            return false;
        }

        return FindMatchGroups().Count > 0;
    }

    /// <summary>
    /// 消去対象のピースを削除して、種類ごとの個数を返す。
    /// 連鎖処理中はResolveBoardから呼び出す。
    /// </summary>
    public Dictionary<Piece.PieceKind, int> DeleteMatchPiece()
    {
        Dictionary<Piece.PieceKind, int> result =
            CreateEmptyMatchCounts();

        if (board == null)
        {
            return result;
        }

        List<List<Vector2Int>> groups = FindMatchGroups();

        foreach (List<Vector2Int> group in groups)
        {
            foreach (Vector2Int cell in group)
            {
                Piece piece = board[cell.x, cell.y];

                if (piece == null)
                {
                    continue;
                }

                result[piece.GetKind()]++;

                piece.deleteFlag = true;
                Destroy(piece.gameObject);
                board[cell.x, cell.y] = null;
            }
        }

        return result;
    }

    private Dictionary<Piece.PieceKind, int> CreateEmptyMatchCounts()
    {
        Dictionary<Piece.PieceKind, int> result =
            new Dictionary<Piece.PieceKind, int>();

        foreach (Piece.PieceKind kind in
                 Enum.GetValues(typeof(Piece.PieceKind)))
        {
            result[kind] = 0;
        }

        return result;
    }

    /// <summary>
    /// マッチしたピースを色ごとの連結グループに分ける。
    /// </summary>
    private List<List<Vector2Int>> FindMatchGroups()
    {
        List<List<Vector2Int>> groups =
            new List<List<Vector2Int>>();

        if (board == null)
        {
            return groups;
        }

        bool[,] matched = new bool[width, height];
        bool[,] visited = new bool[width, height];

        // 横方向を判定
        for (int y = 0; y < height; y++)
        {
            int x = 0;

            while (x < width)
            {
                Piece first = board[x, y];

                if (first == null)
                {
                    x++;
                    continue;
                }

                Piece.PieceKind kind = first.GetKind();
                int end = x + 1;

                while (end < width &&
                       board[end, y] != null &&
                       board[end, y].GetKind() == kind)
                {
                    end++;
                }

                if (end - x >= minimumMatchCount)
                {
                    for (int i = x; i < end; i++)
                    {
                        matched[i, y] = true;
                    }
                }

                x = end;
            }
        }

        // 縦方向を判定
        for (int x = 0; x < width; x++)
        {
            int y = 0;

            while (y < height)
            {
                Piece first = board[x, y];

                if (first == null)
                {
                    y++;
                    continue;
                }

                Piece.PieceKind kind = first.GetKind();
                int end = y + 1;

                while (end < height &&
                       board[x, end] != null &&
                       board[x, end].GetKind() == kind)
                {
                    end++;
                }

                if (end - y >= minimumMatchCount)
                {
                    for (int i = y; i < end; i++)
                    {
                        matched[x, i] = true;
                    }
                }

                y = end;
            }
        }

        // 隣接する同じ種類のマッチ対象をグループ化
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (!matched[x, y] || visited[x, y] ||
                    board[x, y] == null)
                {
                    continue;
                }

                Piece.PieceKind kind = board[x, y].GetKind();

                List<Vector2Int> group = new List<Vector2Int>();
                Queue<Vector2Int> queue = new Queue<Vector2Int>();

                queue.Enqueue(new Vector2Int(x, y));
                visited[x, y] = true;

                while (queue.Count > 0)
                {
                    Vector2Int cell = queue.Dequeue();
                    group.Add(cell);

                    foreach (Vector2Int direction in Directions)
                    {
                        Vector2Int next = cell + direction;

                        if (!IsInBoard(next) ||
                            visited[next.x, next.y] ||
                            !matched[next.x, next.y])
                        {
                            continue;
                        }

                        Piece neighbor = board[next.x, next.y];

                        if (neighbor == null ||
                            neighbor.GetKind() != kind)
                        {
                            continue;
                        }

                        visited[next.x, next.y] = true;
                        queue.Enqueue(next);
                    }
                }

                groups.Add(group);
            }
        }

        return groups;
    }

    private Vector2Int GetPieceBoardPos(Piece piece)
    {
        if (board == null || piece == null)
        {
            return new Vector2Int(-1, -1);
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (board[x, y] == piece)
                {
                    return new Vector2Int(x, y);
                }
            }
        }

        return new Vector2Int(-1, -1);
    }

    // -------------------------------------------------------
    // 落下・補充・連鎖
    // -------------------------------------------------------

    private IEnumerator ResolveBoard()
    {
        state = BoardState.Resolving;
        comboCount = 0;
        comboTimer = 0f;
        lastMatchCounts.Clear();

        // ドラッグで動いたピースの移動を待つ
        yield return WaitForAllPiecesToSettle();

        while (true)
        {
            List<List<Vector2Int>> groups = FindMatchGroups();

            if (groups.Count == 0)
            {
                break;
            }

            comboCount++;
            comboTimer = comboDisplayDuration;

            foreach (List<Vector2Int> group in groups)
            {
                yield return StartCoroutine(ClearGroup(group));
            }

            ApplyGravityAndRefill();

            yield return WaitForAllPiecesToSettle();
        }

        if (comboCount > 0)
        {
            Debug.Log($"{comboCount} COMBO");

            // 連鎖全体の消去数とコンボ数を一度だけ通知
            OnMatchesResolved?.Invoke(
                new Dictionary<Piece.PieceKind, int>(lastMatchCounts),
                comboCount
            );
        }

        if (comboText != null)
        {
            comboText.gameObject.SetActive(comboTimer > 0f);
        }

        state = BoardState.Idle;
        moveStarted = false;
        moveTimer = 0f;
    }

    private IEnumerator ClearGroup(List<Vector2Int> cells)
    {
        List<Piece> piecesToClear = new List<Piece>();

        // 対象を盤面から外して、消去中の二重参照を防ぐ
        foreach (Vector2Int cell in cells)
        {
            Piece piece = board[cell.x, cell.y];

            if (piece == null)
            {
                continue;
            }

            Piece.PieceKind kind = piece.GetKind();

            if (!lastMatchCounts.ContainsKey(kind))
            {
                lastMatchCounts[kind] = 0;
            }

            lastMatchCounts[kind]++;

            piece.deleteFlag = true;
            piecesToClear.Add(piece);
            board[cell.x, cell.y] = null;
        }

        float timer = 0f;

        while (timer < clearDuration)
        {
            timer += Time.deltaTime;

            float progress = Mathf.Clamp01(timer / clearDuration);

            foreach (Piece piece in piecesToClear)
            {
                if (piece == null)
                {
                    continue;
                }

                piece.transform.localScale =
                    Vector3.one * (1f + progress * 0.3f);

                SetPieceAlpha(piece, 1f - progress);
            }

            yield return null;
        }

        foreach (Piece piece in piecesToClear)
        {
            if (piece != null)
            {
                Destroy(piece.gameObject);
            }
        }
    }

    /// <summary>
    /// 空いたマスを下に詰め、上側から新しいピースを生成する。
    /// </summary>
    public void FillPiece()
    {
        if (board == null)
        {
            return;
        }

        ApplyGravityAndRefill();
    }

    private void ApplyGravityAndRefill()
    {
        for (int x = 0; x < width; x++)
        {
            int writeY = 0;

            // 既存ピースを下から順に詰める
            for (int y = 0; y < height; y++)
            {
                Piece piece = board[x, y];

                if (piece == null || piece.deleteFlag)
                {
                    continue;
                }

                if (y != writeY)
                {
                    board[x, writeY] = piece;
                    board[x, y] = null;

                    StartCoroutine(piece.MoveTo(
                        GetPieceAnchoredPos(new Vector2Int(x, writeY)),
                        fallDuration
                    ));
                }

                writeY++;
            }

            // 上側の空きマスを新しいピースで補充
            for (int y = writeY; y < height; y++)
            {
                CreatePiece(
                    new Vector2Int(x, y),
                    RandomKind(),
                    true
                );
            }
        }
    }

    private IEnumerator WaitForAllPiecesToSettle()
    {
        // MoveToのアニメーションが終わるまで待機
        while (true)
        {
            bool moving = false;

            for (int x = 0; x < width && !moving; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Piece piece = board[x, y];

                    if (piece == null)
                    {
                        continue;
                    }

                    Vector2 target = GetPieceAnchoredPos(
                        new Vector2Int(x, y)
                    );

                    if (Vector2.Distance(
                        piece.GetRectTransform().anchoredPosition,
                        target) > 0.5f)
                    {
                        moving = true;
                        break;
                    }
                }
            }

            if (!moving)
            {
                yield break;
            }

            yield return null;
        }
    }
}
