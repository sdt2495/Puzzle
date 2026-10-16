
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// パズルの1個のピースを管理するクラス。
///
/// ・ピースの種類を管理する
/// ・画像を設定する
/// ・ピースの大きさを設定する
/// ・指定位置までアニメーション移動する
/// ・消去対象かどうかを管理する
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class Piece : MonoBehaviour
{
    public enum PieceKind
    {
        Red,
        Blue,
        Green,
        Yellow,
        Purple,
        Heal
    }

    [Header("画像設定")]
    [SerializeField] private Image pieceImage;

    [Tooltip("PieceKindの順番に合わせて画像を登録")]
    [SerializeField] private Sprite[] pieceSprites;

    [Header("色設定")]
    [SerializeField]
    private Color[] pieceColors =
    {
        new Color(0.95f, 0.30f, 0.30f),
        new Color(0.30f, 0.55f, 1.00f),
        new Color(0.35f, 0.85f, 0.40f),
        new Color(1.00f, 0.85f, 0.30f),
        new Color(0.70f, 0.40f, 0.95f),
        new Color(1.00f, 0.50f, 0.75f)
    };

    [Header("表示設定")]
    [Min(1f)]
    [SerializeField] private float pieceSize = 100f;

    [Header("移動設定")]
    [Min(0.01f)]
    [SerializeField] private float defaultMoveDuration = 0.15f;

    /// <summary>
    /// Boardから参照する消去フラグ。
    /// </summary>
    [HideInInspector]
    public bool deleteFlag;

    private PieceKind kind;
    private RectTransform rectTransform;
    private Coroutine moveCoroutine;

    public PieceKind GetKind()
    {
        return kind;
    }

    public RectTransform GetRectTransform()
    {
        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }

        return rectTransform;
    }

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        if (pieceImage == null)
        {
            pieceImage = GetComponent<Image>();
        }

        SetSize(pieceSize);
    }

    /// <summary>
    /// ピースの種類と画像を設定する。
    /// </summary>
    public void SetKind(PieceKind newKind)
    {
        kind = newKind;

        if (pieceImage == null)
        {
            pieceImage = GetComponent<Image>();
        }

        if (pieceImage == null)
        {
            Debug.LogError(
                "Piece：Imageが設定されていません。",
                this
            );
            return;
        }

        int index = (int)kind;

        if (pieceSprites != null &&
            index >= 0 &&
            index < pieceSprites.Length &&
            pieceSprites[index] != null)
        {
            pieceImage.sprite = pieceSprites[index];
            pieceImage.color = Color.white;
        }
        else if (pieceColors != null &&
                 index >= 0 &&
                 index < pieceColors.Length)
        {
            pieceImage.sprite = null;
            pieceImage.color = pieceColors[index];
        }
    }

    /// <summary>
    /// ピースの幅と高さを設定する。
    /// </summary>
    public void SetSize(float size)
    {
        pieceSize = Mathf.Max(1f, size);

        if (GetRectTransform() != null)
        {
            rectTransform.sizeDelta =
                new Vector2(pieceSize, pieceSize);
        }
    }

    /// <summary>
    /// 指定位置まで滑らかに移動する。
    /// </summary>
    public IEnumerator MoveTo(
        Vector2 targetPosition,
        float duration)
    {
        RectTransform rt = GetRectTransform();

        if (rt == null)
        {
            yield break;
        }

        if (moveCoroutine != null)
        {
            StopCoroutine(moveCoroutine);
            moveCoroutine = null;
        }

        duration = duration > 0f
            ? duration
            : defaultMoveDuration;

        Vector2 startPosition = rt.anchoredPosition;
        float timer = 0f;

        while (timer < duration)
        {
            if (rt == null)
            {
                yield break;
            }

            timer += Time.deltaTime;

            float progress = Mathf.Clamp01(timer / duration);

            // SmoothStepで移動開始と終了を滑らかにする
            float easedProgress =
                Mathf.SmoothStep(0f, 1f, progress);

            rt.anchoredPosition = Vector2.Lerp(
                startPosition,
                targetPosition,
                easedProgress
            );

            yield return null;
        }

        if (rt != null)
        {
            rt.anchoredPosition = targetPosition;
        }

        moveCoroutine = null;
    }

    private void OnDestroy()
    {
        if (moveCoroutine != null)
        {
            StopCoroutine(moveCoroutine);
            moveCoroutine = null;
        }
    }
}
