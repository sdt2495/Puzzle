using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class Piece : MonoBehaviour
{
    public bool deleteFlag;

    private Image thisImage;
    private RectTransform thisRectTransform;
    private PieceKind kind;

    private void Awake()
    {
        thisImage = GetComponent<Image>();
        thisRectTransform = GetComponent<RectTransform>();

        deleteFlag = false;
    }

    public IEnumerator MoveTo(Vector2 target, float duration)
    {
        Vector2 start = thisRectTransform.anchoredPosition;

        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;

            float t = timer / duration;

            // EaseOut
            t = 1f - Mathf.Pow(1f - t, 3f);

            thisRectTransform.anchoredPosition =
                Vector2.Lerp(start, target, t);

            yield return null;
        }

        thisRectTransform.anchoredPosition = target;
    }

    // «««Šù‘¶ƒR[ƒh«««

    public void SetKind(PieceKind pieceKind)
    {
        kind = pieceKind;
        SetColor();
    }

    public PieceKind GetKind()
    {
        return kind;
    }

    public void SetSize(int size)
    {
        thisRectTransform.sizeDelta = Vector2.one * size;
    }

    private void SetColor()
    {
        switch (kind)
        {
            case PieceKind.Red:
                thisImage.color = Color.red;
                break;

            case PieceKind.Blue:
                thisImage.color = Color.blue;
                break;

            case PieceKind.Green:
                thisImage.color = Color.green;
                break;

            case PieceKind.Yellow:
                thisImage.color = Color.yellow;
                break;

            case PieceKind.Black:
                thisImage.color = Color.black;
                break;

            case PieceKind.Magenta:
                thisImage.color = Color.magenta;
                break;
        }
    }

    public enum PieceKind
    {
        Red,
        Blue,
        Green,
        Yellow,
        Black,
        Magenta
    }
}