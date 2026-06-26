using System;
using UnityEngine;
using UnityEngine.UI;

public class Board : MonoBehaviour
{
    [SerializeField] private GameObject piecePrefab;

    // ★ 追加：生成先のUIパネル
    [SerializeField] private RectTransform boardPanel;

    private Piece[,] board;
    private int width;
    private int height;
    private int pieceWidth;

    //-------------------------------------------------------
    public void InitializeBoard(int boardWidth, int boardHeight)
    {
        width = boardWidth;
        height = boardHeight;

        pieceWidth = 100; // UIなら固定でOK

        board = new Piece[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                CreatePiece(new Vector2(x, y), false);
            }
        }
    }

    //-------------------------------------------------------
    private void CreatePiece(Vector2 pos, bool animate = false)
    {
        GameObject obj = Instantiate(piecePrefab);

        obj.transform.SetParent(boardPanel, false);

        RectTransform rt = obj.GetComponent<RectTransform>();


        var kind =
            (Piece.PieceKind)UnityEngine.Random.Range(
            0,
            System.Enum.GetNames(
            typeof(Piece.PieceKind)).Length);


        Piece piece = obj.GetComponent<Piece>();

        piece.SetSize(pieceWidth);
        piece.SetKind(kind);

        board[(int)pos.x, (int)pos.y] = piece;


        Vector2 targetPos = GetPieceAnchoredPos(pos);


        if (animate)
        {
            rt.anchoredPosition =
                targetPos + Vector2.up * 300f;

            StartCoroutine(
                piece.MoveTo(targetPos, 0.2f));
        }
        else
        {
            rt.anchoredPosition = targetPos;
        }
    }

    //-------------------------------------------------------
    public Piece GetNearestPiece(Vector3 screenPos)
    {
        float min = float.MaxValue;
        Piece nearest = null;

        foreach (var p in board)
        {
            if (p == null) continue;

            RectTransform rt = p.GetComponent<RectTransform>();

            Vector2 localPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                boardPanel,
                screenPos,
                null,
                out localPos
            );

            float d = Vector2.Distance(localPos, rt.anchoredPosition);

            if (d < min)
            {
                min = d;
                nearest = p;
            }
        }

        return nearest;
    }

    //-------------------------------------------------------
    public void SwitchPiece(Piece p1, Piece p2)
    {
        if (p1 == null || p2 == null) return;

        RectTransform r1 = p1.GetComponent<RectTransform>();
        RectTransform r2 = p2.GetComponent<RectTransform>();

        Vector2 temp = r1.anchoredPosition;
        r1.anchoredPosition = r2.anchoredPosition;
        r2.anchoredPosition = temp;

        Vector2 p1Pos = GetPieceBoardPos(p1);
        Vector2 p2Pos = GetPieceBoardPos(p2);

        board[(int)p1Pos.x, (int)p1Pos.y] = p2;
        board[(int)p2Pos.x, (int)p2Pos.y] = p1;
    }

    //-------------------------------------------------------
    public bool HasMatch()
    {
        foreach (var p in board)
        {
            if (p != null && IsMatchPiece(p))
                return true;
        }
        return false;
    }

    public int DeleteMatchPiece()
    {
        int deleteCount = 0;

        foreach (var p in board)
        {
            if (p != null)
            {
                p.deleteFlag = IsMatchPiece(p);
            }
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Piece p = board[x, y];

                if (p != null && p.deleteFlag)
                {
                    deleteCount++;

                    Destroy(p.gameObject);
                    board[x, y] = null;
                }
            }
        }

        return deleteCount;
    }
    public void FillPiece()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                FillPiece(new Vector2(x, y));
            }
        }
    }

    //-------------------------------------------------------
    private Vector2 GetPieceBoardPos(Piece piece)
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (board[x, y] == piece)
                    return new Vector2(x, y);
            }
        }
        return Vector2.zero;
    }

    private bool IsMatchPiece(Piece piece)
    {
        Vector2 pos = GetPieceBoardPos(piece);
        var kind = piece.GetKind();

        int v =
            GetSameKindPieceNum(kind, pos, Vector2.up) +
            GetSameKindPieceNum(kind, pos, Vector2.down) + 1;

        int h =
            GetSameKindPieceNum(kind, pos, Vector2.left) +
            GetSameKindPieceNum(kind, pos, Vector2.right) + 1;

        return v >= GameManager.MachingCount ||
               h >= GameManager.MachingCount;
    }

    private int GetSameKindPieceNum(Piece.PieceKind kind, Vector2 pos, Vector2 dir)
    {
        int count = 0;

        while (true)
        {
            pos += dir;

            if (!IsInBoard(pos)) break;

            if (board[(int)pos.x, (int)pos.y].GetKind() == kind)
                count++;
            else
                break;
        }

        return count;
    }

    private bool IsInBoard(Vector2 pos)
    {
        return pos.x >= 0 && pos.y >= 0 &&
               pos.x < width && pos.y < height;
    }

    private void FillPiece(Vector2 pos)
    {
        var piece = board[(int)pos.x, (int)pos.y];

        if (piece != null && !piece.deleteFlag)
            return;

        Vector2 check = pos + Vector2.up;

        while (IsInBoard(check))
        {
            var p = board[(int)check.x, (int)check.y];

            if (p != null && !p.deleteFlag)
            {
                board[(int)pos.x, (int)pos.y] = p;
                board[(int)check.x, (int)check.y] = null;


                StartCoroutine(
                    p.MoveTo(
                        GetPieceAnchoredPos(pos),
                        0.15f));


                return;
            }

            check += Vector2.up;
        }

        CreatePiece(pos, true);
    }

    private Vector2 GetPieceAnchoredPos(Vector2 pos)
    {
        float offsetX = -(width - 1) * pieceWidth * 0.5f;
        float offsetY = -(height - 1) * pieceWidth * 0.5f;

        return new Vector2(
            pos.x * pieceWidth + offsetX,
            pos.y * pieceWidth + offsetY
        );
    }
}