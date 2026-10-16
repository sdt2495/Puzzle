using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// パズドラ風ドロップパズルの盤面を管理するクラス。
///
/// ・ドラッグによるドロップの入れ替え（斜め移動対応）
/// ・操作時間制限
/// ・縦横3個以上で消去
/// ・落下、補充、連鎖
/// ・コンボ表示、操作時間バー
/// ・GameManagerへ色ごとの消去数とコンボ数を通知
/// </summary>
public class PuzzleBoard : MonoBehaviour
{
    [Header("盤面設定")]
    [Min(1)] public int columns = 6;
    [Min(1)] public int rows = 5;
    [Min(0.01f)] public float cellSize = 1f;

    [Header("画面レイアウト")]
    public bool boardAtBottom = true;
    [Min(0f)] public float sideMargin = 0f;
    [Min(0f)] public float bottomMargin = 0.3f;

    [Header("ドロップの色")]
    public Color[] orbColors =
    {
        new Color(0.95f, 0.30f, 0.30f), // 0：火
        new Color(0.30f, 0.55f, 1.00f), // 1：水
        new Color(0.35f, 0.85f, 0.40f), // 2：木
        new Color(1.00f, 0.85f, 0.30f), // 3：光
        new Color(0.70f, 0.40f, 0.95f), // 4：闇
        new Color(1.00f, 0.50f, 0.75f)  // 5：回復
    };

    [Header("ドロップ画像")]
    public Sprite[] orbSprites;
    public bool tintSprites = false;

    [Header("操作設定")]
    [Min(0.01f)] public float moveTimeLimit = 4f;

    [Range(0.2f, 0.5f)]
    public float swapRadius = 0.42f;

    [Header("演出設定")]
    [Min(0.01f)] public float swapSpeed = 25f;
    [Min(0.01f)] public float fallSpeed = 14f;
    [Min(0.01f)] public float clearDuration = 0.3f;

    [Range(0.5f, 1f)]
    public float orbScale = 0.95f;

    // ============================================================
    // GameManager連携
    // ============================================================

    /// <summary>
    /// 消去数とコンボ数をGameManagerへ通知するイベント。
    /// 第1引数：ドロップ種類ごとの消去数
    /// 第2引数：コンボ数
    /// </summary>
    public event Action<Dictionary<int, int>, int> OnMatchesResolved;

    private enum State
    {
        Idle,
        Dragging,
        Resolving
    }

    private class Orb
    {
        public int type;
        public Transform tr;
        public SpriteRenderer sr;
        public Vector3 target;
        public float speed;
        public float baseScale;
    }

    private int TypeCount
    {
        get
        {
            if (orbSprites != null && orbSprites.Length > 0)
            {
                return orbSprites.Length;
            }

            return orbColors != null ? orbColors.Length : 0;
        }
    }

    private Orb[,] grid;
    private State state = State.Idle;

    private Camera cam;
    private Sprite orbSprite;
    private Sprite tileSprite;

    private Orb heldOrb;
    private Vector2Int heldCell;

    private bool moveStarted;
    private float moveTimer;

    private int comboCount;
    private float comboShowTimer;
    private GUIStyle comboStyle;

    // 連鎖全体で消去したドロップ数
    private readonly Dictionary<int, int> lastMatchCounts =
        new Dictionary<int, int>();

    private int lastScreenWidth;
    private int lastScreenHeight;

    private bool backgroundCreated;

    private const int OrbTextureSize = 128;
    private const int RandomTypeAttempts = 100;
    private const int MinimumMatchCount = 3;
    private const float ComboDisplayDuration = 1.5f;
    private const float CameraZOffset = -10f;
    private const float PointerZDistance = 10f;
    private const float SelectedScaleMultiplier = 1.15f;
    private const float SelectedAlpha = 0.85f;

    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

    private void Start()
    {
        if (columns < 1 || rows < 1 || cellSize <= 0f)
        {
            Debug.LogError("PuzzleBoard：盤面設定が不正です。", this);
            enabled = false;
            return;
        }

        if (TypeCount <= 0)
        {
            Debug.LogError(
                "PuzzleBoard：ドロップ画像またはドロップ色を設定してください。",
                this
            );

            enabled = false;
            return;
        }

        cam = Camera.main;

        if (cam == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cam = cameraObject.AddComponent<Camera>();
            cam.tag = "MainCamera";
        }

        SetupCamera();

        orbSprite = CreateOrbSprite(OrbTextureSize);

        tileSprite = Sprite.Create(
            Texture2D.whiteTexture,
            new Rect(0f, 0f, 4f, 4f),
            new Vector2(0.5f, 0.5f),
            4f
        );

        InitializeBoard(columns, rows);
    }

    /// <summary>
    /// 盤面を初期化する。
    /// GameManagerなどから呼び出せる。
    /// </summary>
    public void InitializeBoard(int boardColumns, int boardRows)
    {
        if (boardColumns < 1 || boardRows < 1)
        {
            Debug.LogError("PuzzleBoard：行数・列数は1以上にしてください。");
            return;
        }

        columns = boardColumns;
        rows = boardRows;

        StopAllCoroutines();

        // 既存のドロップを削除
        if (grid != null)
        {
            for (int x = 0; x < grid.GetLength(0); x++)
            {
                for (int y = 0; y < grid.GetLength(1); y++)
                {
                    if (grid[x, y] != null && grid[x, y].tr != null)
                    {
                        Destroy(grid[x, y].tr.gameObject);
                    }
                }
            }
        }

        // 既存の背景を削除
        Transform oldBackground = transform.Find("BoardBackground");

        if (oldBackground != null)
        {
            Destroy(oldBackground.gameObject);
        }

        backgroundCreated = false;

        if (tileSprite != null)
        {
            CreateBackground();
        }

        heldOrb = null;
        heldCell = Vector2Int.zero;
        moveStarted = false;
        moveTimer = 0f;
        comboCount = 0;
        comboShowTimer = 0f;
        lastMatchCounts.Clear();
        state = State.Idle;

        grid = new Orb[columns, rows];

        for (int x = 0; x < columns; x++)
        {
            for (int y = 0; y < rows; y++)
            {
                int type = RandomTypeWithoutMatch(x, y);
                grid[x, y] = CreateOrb(type, CellToWorld(x, y));
            }
        }

        if (cam != null)
        {
            UpdateCameraSize();
        }
    }

    private void SetupCamera()
    {
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.08f, 0.12f);

        UpdateCameraSize();
    }

    /// <summary>
    /// カメラサイズと初期位置を調整する。
    /// 毎フレーム呼ばないため、GameManagerのカメラ移動を妨げない。
    /// </summary>
    private void UpdateCameraSize()
    {
        if (cam == null)
        {
            return;
        }

        float aspect = Mathf.Max(
            (float)Screen.width / Mathf.Max(Screen.height, 1),
            0.01f
        );

        float boardHalfW =
            columns * cellSize * 0.5f + sideMargin * cellSize;

        float boardHalfH = rows * cellSize * 0.5f;

        if (boardAtBottom)
        {
            float size = Mathf.Max(
                boardHalfW / aspect,
                boardHalfH + bottomMargin * cellSize
            );

            cam.orthographicSize = size;

            float boardBottom =
                -boardHalfH - bottomMargin * cellSize;

            cam.transform.position = transform.position +
                new Vector3(0f, boardBottom + size, CameraZOffset);
        }
        else
        {
            float halfH = boardHalfH + cellSize;

            cam.orthographicSize = Mathf.Max(
                halfH,
                (boardHalfW + cellSize * 0.3f) / aspect
            );

            cam.transform.position = transform.position +
                new Vector3(0f, 0.5f * cellSize, CameraZOffset);
        }

        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
    }

    private void Update()
    {
        // 解像度が変わった場合だけカメラを再調整
        if (Screen.width != lastScreenWidth ||
            Screen.height != lastScreenHeight)
        {
            UpdateCameraSize();
        }

        HandleInput();
        AnimateOrbs();

        if (comboShowTimer > 0f && state != State.Resolving)
        {
            comboShowTimer -= Time.deltaTime;
        }
    }

    // ============================================================
    // 背景
    // ============================================================

    private void CreateBackground()
    {
        if (backgroundCreated || tileSprite == null)
        {
            return;
        }

        Transform bg = new GameObject("BoardBackground").transform;
        bg.SetParent(transform, false);

        for (int x = 0; x < columns; x++)
        {
            for (int y = 0; y < rows; y++)
            {
                GameObject go = new GameObject($"Tile_{x}_{y}");
                go.transform.SetParent(bg, false);
                go.transform.position = CellToWorld(x, y);
                go.transform.localScale = Vector3.one * cellSize;

                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = tileSprite;

                sr.color = (x + y) % 2 == 0
                    ? new Color(0.24f, 0.19f, 0.16f)
                    : new Color(0.18f, 0.14f, 0.12f);

                sr.sortingOrder = -10;
            }
        }

        backgroundCreated = true;
    }

    // ============================================================
    // ドロップ生成
    // ============================================================

    private Sprite CreateOrbSprite(int size)
    {
        Texture2D tex = new Texture2D(
            size, size, TextureFormat.RGBA32, false
        );

        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[size * size];

        float radius = size * 0.5f;
        Vector2 center = new Vector2(radius, radius);
        Vector2 highlight = new Vector2(size * 0.36f, size * 0.66f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);

                float distance = Vector2.Distance(p, center) / radius;
                float alpha = Mathf.Clamp01(
                    (1f - distance) * radius * 0.5f
                );

                float shade = Mathf.Lerp(
                    1f, 0.55f, distance * distance
                );

                float h = Mathf.Clamp01(
                    1f - Vector2.Distance(p, highlight) / (size * 0.22f)
                );

                float brightness = Mathf.Clamp01(shade + h * h * 0.6f);

                pixels[y * size + x] = new Color(
                    brightness, brightness, brightness, alpha
                );
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        return Sprite.Create(
            tex,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            size
        );
    }

    private Orb CreateOrb(int type, Vector3 pos)
    {
        GameObject go = new GameObject($"Orb_{type}");
        go.transform.SetParent(transform, false);
        go.transform.position = pos;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();

        Sprite custom = null;

        if (orbSprites != null &&
            type >= 0 &&
            type < orbSprites.Length)
        {
            custom = orbSprites[type];
        }

        Color fallbackColor = Color.white;

        if (orbColors != null && orbColors.Length > 0)
        {
            fallbackColor = orbColors[type % orbColors.Length];
        }

        if (custom != null)
        {
            sr.sprite = custom;
            sr.color = tintSprites ? fallbackColor : Color.white;
        }
        else
        {
            sr.sprite = orbSprite;
            sr.color = fallbackColor;
        }

        Vector2 spriteSize = sr.sprite.bounds.size;
        float fit = Mathf.Max(spriteSize.x, spriteSize.y);

        float baseScale = cellSize * orbScale /
            (fit > 0f ? fit : 1f);

        go.transform.localScale = Vector3.one * baseScale;

        return new Orb
        {
            type = type,
            tr = go.transform,
            sr = sr,
            target = pos,
            speed = fallSpeed,
            baseScale = baseScale
        };
    }

    /// <summary>
    /// 初期盤面で最初から縦横3個並ばないように種類を選ぶ。
    /// </summary>
    private int RandomTypeWithoutMatch(int x, int y)
    {
        for (int tries = 0; tries < RandomTypeAttempts; tries++)
        {
            int type = UnityEngine.Random.Range(0, TypeCount);

            if (x >= 2 &&
                grid[x - 1, y] != null &&
                grid[x - 2, y] != null &&
                grid[x - 1, y].type == type &&
                grid[x - 2, y].type == type)
            {
                continue;
            }

            if (y >= 2 &&
                grid[x, y - 1] != null &&
                grid[x, y - 2] != null &&
                grid[x, y - 1].type == type &&
                grid[x, y - 2].type == type)
            {
                continue;
            }

            return type;
        }

        return UnityEngine.Random.Range(0, TypeCount);
    }

    // ============================================================
    // 座標変換
    // ============================================================

    private Vector3 CellToWorld(int x, int y)
    {
        return transform.position + new Vector3(
            (x - (columns - 1) * 0.5f) * cellSize,
            (y - (rows - 1) * 0.5f) * cellSize,
            0f
        );
    }

    private Vector3 CellToWorld(Vector2Int cell)
    {
        return CellToWorld(cell.x, cell.y);
    }

    private Vector2Int WorldToCell(Vector3 world)
    {
        Vector3 local = world - transform.position;

        int x = Mathf.RoundToInt(
            local.x / cellSize + (columns - 1) * 0.5f
        );

        int y = Mathf.RoundToInt(
            local.y / cellSize + (rows - 1) * 0.5f
        );

        return new Vector2Int(
            Mathf.Clamp(x, 0, columns - 1),
            Mathf.Clamp(y, 0, rows - 1)
        );
    }

    // ============================================================
    // 入力
    // ============================================================

    private bool ReadPointer(
        out Vector2 pos,
        out bool down,
        out bool held)
    {
#if ENABLE_INPUT_SYSTEM
        Pointer pointer = Pointer.current;

        if (pointer == null)
        {
            pos = default;
            down = false;
            held = false;
            return false;
        }

        pos = pointer.position.ReadValue();
        down = pointer.press.wasPressedThisFrame;
        held = pointer.press.isPressed;
        return true;
#else
        pos = Input.mousePosition;
        down = Input.GetMouseButtonDown(0);
        held = Input.GetMouseButton(0);
        return true;
#endif
    }

    private void HandleInput()
    {
        if (state == State.Resolving || grid == null || cam == null)
        {
            return;
        }

        if (!ReadPointer(
            out Vector2 screenPos,
            out bool down,
            out bool held))
        {
            if (state == State.Dragging)
            {
                Release();
            }

            return;
        }

        Vector3 world = cam.ScreenToWorldPoint(
            new Vector3(screenPos.x, screenPos.y, PointerZDistance)
        );

        world.z = 0f;

        if (state == State.Idle)
        {
            if (down)
            {
                TryPick(world);
            }

            return;
        }

        if (!held)
        {
            Release();
            return;
        }

        DragTo(world);

        if (moveStarted)
        {
            moveTimer -= Time.deltaTime;

            if (moveTimer <= 0f)
            {
                Release();
            }
        }
    }

    private void TryPick(Vector3 world)
    {
        Vector3 local = world - transform.position;

        if (Mathf.Abs(local.x) > columns * cellSize * 0.5f ||
            Mathf.Abs(local.y) > rows * cellSize * 0.5f)
        {
            return;
        }

        Vector2Int cell = WorldToCell(world);
        heldOrb = grid[cell.x, cell.y];

        if (heldOrb == null || heldOrb.sr == null)
        {
            heldOrb = null;
            return;
        }

        heldCell = cell;
        moveStarted = false;
        state = State.Dragging;

        heldOrb.sr.sortingOrder = 10;
        heldOrb.tr.localScale =
            Vector3.one * heldOrb.baseScale * SelectedScaleMultiplier;

        Color color = heldOrb.sr.color;
        color.a = SelectedAlpha;
        heldOrb.sr.color = color;
    }

    private void DragTo(Vector3 world)
    {
        if (heldOrb == null)
        {
            return;
        }

        Vector3 local = world - transform.position;

        float maxX = (columns - 1) * 0.5f * cellSize;
        float maxY = (rows - 1) * 0.5f * cellSize;

        local.x = Mathf.Clamp(local.x, -maxX, maxX);
        local.y = Mathf.Clamp(local.y, -maxY, maxY);

        Vector3 pos = transform.position + local;
        heldOrb.tr.position = pos;

        Vector2Int target = WorldToCell(pos);

        if (target == heldCell)
        {
            return;
        }

        if ((CellToWorld(target) - pos).magnitude >
            cellSize * swapRadius)
        {
            return;
        }

        // 移動先までマスを順番に入れ替える
        while (heldCell != target)
        {
            Vector2Int step = new Vector2Int(
                Math.Sign(target.x - heldCell.x),
                Math.Sign(target.y - heldCell.y)
            );

            Vector2Int next = heldCell + step;

            if (next.x < 0 || next.x >= columns ||
                next.y < 0 || next.y >= rows)
            {
                break;
            }

            SwapHeldInto(next);
        }
    }

    private void SwapHeldInto(Vector2Int next)
    {
        Orb other = grid[next.x, next.y];

        grid[heldCell.x, heldCell.y] = other;

        if (other != null)
        {
            other.target = CellToWorld(heldCell);
            other.speed = swapSpeed;
        }

        grid[next.x, next.y] = heldOrb;
        heldCell = next;

        if (!moveStarted)
        {
            moveStarted = true;
            moveTimer = moveTimeLimit;
        }
    }

    private void Release()
    {
        if (heldOrb == null)
        {
            state = State.Idle;
            return;
        }

        if (heldOrb.sr != null)
        {
            heldOrb.sr.sortingOrder = 0;

            Color color = heldOrb.sr.color;
            color.a = 1f;
            heldOrb.sr.color = color;
        }

        if (heldOrb.tr != null)
        {
            heldOrb.tr.localScale =
                Vector3.one * heldOrb.baseScale;

            heldOrb.target = CellToWorld(heldCell);
            heldOrb.speed = swapSpeed;
        }

        heldOrb = null;

        if (moveStarted)
        {
            StartCoroutine(Resolve());
        }
        else
        {
            state = State.Idle;
        }
    }

    // ============================================================
    // ドロップ移動
    // ============================================================

    private void AnimateOrbs()
    {
        if (grid == null)
        {
            return;
        }

        for (int x = 0; x < columns; x++)
        {
            for (int y = 0; y < rows; y++)
            {
                Orb orb = grid[x, y];

                if (orb == null ||
                    orb == heldOrb ||
                    orb.tr == null)
                {
                    continue;
                }

                orb.tr.position = Vector3.MoveTowards(
                    orb.tr.position,
                    orb.target,
                    orb.speed * Time.deltaTime
                );
            }
        }
    }

    private bool AllSettled()
    {
        if (grid == null)
        {
            return true;
        }

        for (int x = 0; x < columns; x++)
        {
            for (int y = 0; y < rows; y++)
            {
                Orb orb = grid[x, y];

                if (orb != null &&
                    orb.tr != null &&
                    orb.tr.position != orb.target)
                {
                    return false;
                }
            }
        }

        return true;
    }

    // ============================================================
    // 消去・落下・連鎖
    // ============================================================

    private IEnumerator Resolve()
    {
        state = State.Resolving;
        comboCount = 0;
        lastMatchCounts.Clear();

        yield return new WaitUntil(AllSettled);

        while (true)
        {
            List<List<Vector2Int>> groups = FindMatchGroups();

            if (groups.Count == 0)
            {
                break;
            }

            foreach (List<Vector2Int> group in groups)
            {
                comboCount++;
                comboShowTimer = ComboDisplayDuration;

                yield return StartCoroutine(ClearGroup(group));
            }

            ApplyGravityAndRefill();

            yield return new WaitUntil(AllSettled);
        }

        if (comboCount > 0)
        {
            Debug.Log($"{comboCount} コンボ！");
        }

        // 連鎖終了後にGameManagerへ一度だけ通知する
        if (comboCount > 0)
        {
            OnMatchesResolved?.Invoke(
                new Dictionary<int, int>(lastMatchCounts),
                comboCount
            );
        }

        state = State.Idle;
    }

    /// <summary>
    /// 縦横3個以上のマッチを検出する。
    /// 隣接した同色の消去対象は同じグループにまとめる。
    /// </summary>
    private List<List<Vector2Int>> FindMatchGroups()
    {
        bool[,] matched = new bool[columns, rows];

        // 横方向の判定
        for (int y = 0; y < rows; y++)
        {
            int x = 0;

            while (x < columns)
            {
                if (grid[x, y] == null)
                {
                    x++;
                    continue;
                }

                int type = grid[x, y].type;
                int end = x + 1;

                while (end < columns &&
                       grid[end, y] != null &&
                       grid[end, y].type == type)
                {
                    end++;
                }

                if (end - x >= MinimumMatchCount)
                {
                    for (int i = x; i < end; i++)
                    {
                        matched[i, y] = true;
                    }
                }

                x = end;
            }
        }

        // 縦方向の判定
        for (int x = 0; x < columns; x++)
        {
            int y = 0;

            while (y < rows)
            {
                if (grid[x, y] == null)
                {
                    y++;
                    continue;
                }

                int type = grid[x, y].type;
                int end = y + 1;

                while (end < rows &&
                       grid[x, end] != null &&
                       grid[x, end].type == type)
                {
                    end++;
                }

                if (end - y >= MinimumMatchCount)
                {
                    for (int i = y; i < end; i++)
                    {
                        matched[x, i] = true;
                    }
                }

                y = end;
            }
        }

        // 隣接する同色の対象をグループ化
        List<List<Vector2Int>> groups =
            new List<List<Vector2Int>>();

        bool[,] visited = new bool[columns, rows];

        for (int x = 0; x < columns; x++)
        {
            for (int y = 0; y < rows; y++)
            {
                if (!matched[x, y] ||
                    visited[x, y] ||
                    grid[x, y] == null)
                {
                    continue;
                }

                int type = grid[x, y].type;

                List<Vector2Int> group =
                    new List<Vector2Int>();

                Queue<Vector2Int> queue =
                    new Queue<Vector2Int>();

                queue.Enqueue(new Vector2Int(x, y));
                visited[x, y] = true;

                while (queue.Count > 0)
                {
                    Vector2Int cell = queue.Dequeue();
                    group.Add(cell);

                    foreach (Vector2Int direction in Directions)
                    {
                        Vector2Int next = cell + direction;

                        if (next.x < 0 || next.x >= columns ||
                            next.y < 0 || next.y >= rows)
                        {
                            continue;
                        }

                        if (visited[next.x, next.y] ||
                            !matched[next.x, next.y])
                        {
                            continue;
                        }

                        if (grid[next.x, next.y] == null ||
                            grid[next.x, next.y].type != type)
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

    /// <summary>
    /// 対象ドロップを消去し、種類ごとの個数を集計する。
    /// </summary>
    private IEnumerator ClearGroup(List<Vector2Int> cells)
    {
        List<Orb> orbs = new List<Orb>();

        foreach (Vector2Int cell in cells)
        {
            Orb orb = grid[cell.x, cell.y];

            if (orb == null)
            {
                continue;
            }

            if (!lastMatchCounts.ContainsKey(orb.type))
            {
                lastMatchCounts[orb.type] = 0;
            }

            lastMatchCounts[orb.type]++;

            orbs.Add(orb);
            grid[cell.x, cell.y] = null;
        }

        float timer = 0f;

        while (timer < clearDuration)
        {
            timer += Time.deltaTime;

            float progress = clearDuration > 0f
                ? Mathf.Clamp01(timer / clearDuration)
                : 1f;

            foreach (Orb orb in orbs)
            {
                if (orb == null || orb.tr == null || orb.sr == null)
                {
                    continue;
                }

                orb.tr.localScale =
                    Vector3.one * orb.baseScale *
                    (1f + progress * 0.3f);

                Color color = orb.sr.color;
                color.a = 1f - progress;
                orb.sr.color = color;
            }

            yield return null;
        }

        foreach (Orb orb in orbs)
        {
            if (orb != null && orb.tr != null)
            {
                Destroy(orb.tr.gameObject);
            }
        }
    }

    /// <summary>
    /// ドロップを下に詰め、空いた場所を補充する。
    /// </summary>
    private void ApplyGravityAndRefill()
    {
        for (int x = 0; x < columns; x++)
        {
            int write = 0;

            // 既存ドロップを下へ詰める
            for (int y = 0; y < rows; y++)
            {
                Orb orb = grid[x, y];

                if (orb == null)
                {
                    continue;
                }

                if (y != write)
                {
                    grid[x, write] = orb;
                    grid[x, y] = null;

                    orb.target = CellToWorld(x, write);
                    orb.speed = fallSpeed;
                }

                write++;
            }

            // 上側からドロップを補充
            int spawned = 0;

            for (int y = write; y < rows; y++)
            {
                Vector3 spawnPosition =
                    CellToWorld(x, rows + spawned);

                int type = UnityEngine.Random.Range(0, TypeCount);

                Orb orb = CreateOrb(type, spawnPosition);

                orb.target = CellToWorld(x, y);
                orb.speed = fallSpeed;

                grid[x, y] = orb;
                spawned++;
            }
        }
    }

    // ============================================================
    // UI：操作時間バー・コンボ表示
    // ============================================================

    private float BoardTopGuiY()
    {
        if (cam == null)
        {
            return 0f;
        }

        Vector3 top = transform.position +
            new Vector3(0f, rows * cellSize * 0.5f, 0f);

        return Screen.height - cam.WorldToScreenPoint(top).y;
    }

    private void OnGUI()
    {
        if (cam == null)
        {
            return;
        }

        if (comboStyle == null)
        {
            comboStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };

            comboStyle.normal.textColor = Color.white;
        }

        float boardTop = BoardTopGuiY();

        // 操作時間バー
        if (state == State.Dragging && moveStarted)
        {
            float ratio = moveTimeLimit > 0f
                ? Mathf.Clamp01(moveTimer / moveTimeLimit)
                : 0f;

            float width = Screen.width * 0.9f;
            float height = Mathf.Max(8f, Screen.height * 0.012f);
            float x = (Screen.width - width) * 0.5f;
            float y = boardTop - height - Screen.height * 0.01f;

            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(
                new Rect(x, y, width, height),
                Texture2D.whiteTexture
            );

            GUI.color = Color.Lerp(
                new Color(1f, 0.3f, 0.3f),
                new Color(0.3f, 1f, 0.5f),
                ratio
            );

            GUI.DrawTexture(
                new Rect(x, y, width * ratio, height),
                Texture2D.whiteTexture
            );

            GUI.color = Color.white;
        }

        // コンボ表示
        if (comboShowTimer > 0f && comboCount > 0)
        {
            comboStyle.fontSize = Mathf.RoundToInt(
                Mathf.Min(
                    Screen.height * 0.045f,
                    Screen.width * 0.09f
                )
            );

            float labelHeight = comboStyle.fontSize * 1.5f;

            GUI.Label(
                new Rect(
                    0f,
                    boardTop - labelHeight - Screen.height * 0.03f,
                    Screen.width,
                    labelHeight
                ),
                $"{comboCount} COMBO",
                comboStyle
            );
        }
    }
}