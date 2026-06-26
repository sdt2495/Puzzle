using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
    public const int MachingCount = 3;

    private enum GameState
    {
        Idle,
        PieceMove,
        MatchCheck,
        DeletePiece,
        FillPiece,
    }

    [SerializeField] private Board board;
    [SerializeField] private Text stateText;

    [SerializeField]
    private EnemyHP enemy;

    private GameState currentState;
    private Piece selectedPiece;

    private bool isPressing;
    private Vector2 pointerPos;

    private int comboCount;
    private void Start()
    {
        board.InitializeBoard(6, 5);
        currentState = GameState.Idle;

        comboCount = 0;
    }

    private void Update()
    {
        UpdatePointer(); // ★入力統一

        switch (currentState)
        {
            case GameState.Idle:
                Idle();
                break;

            case GameState.PieceMove:
                PieceMove();
                break;

            case GameState.MatchCheck:
                MatchCheck();
                break;

            case GameState.DeletePiece:
                DeletePiece();
                break;

            case GameState.FillPiece:
                FillPiece();
                break;
        }

        if (stateText != null)
            stateText.text = currentState.ToString();
    }

    //-------------------------------------------------------
    // ★ここがマウス＋タッチ統合
    //-------------------------------------------------------
    private void UpdatePointer()
    {
        isPressing = false;

        // マウス
        if (Mouse.current != null)
        {
            if (Mouse.current.leftButton.isPressed)
            {
                isPressing = true;
                pointerPos = Mouse.current.position.ReadValue();
            }
        }

        // タッチ
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            isPressing = true;
            pointerPos = Touchscreen.current.primaryTouch.position.ReadValue();
        }
    }

    //-------------------------------------------------------
    private void Idle()
    {
        if (WasPressedThisFrame())
        {
            selectedPiece = board.GetNearestPiece(pointerPos);
            currentState = GameState.PieceMove;
        }
    }

    private void PieceMove()
    {
        if (isPressing)
        {
            Piece piece = board.GetNearestPiece(pointerPos);

            if (piece != null && piece != selectedPiece)
            {
                board.SwitchPiece(selectedPiece, piece);
                selectedPiece = piece;
            }
        }

        if (WasReleasedThisFrame())
        {
            currentState = GameState.MatchCheck;
        }
    }

    //-------------------------------------------------------
    private bool WasPressedThisFrame()
    {
        return Mouse.current != null &&
               Mouse.current.leftButton.wasPressedThisFrame;
    }

    private bool WasReleasedThisFrame()
    {
        return Mouse.current != null &&
               Mouse.current.leftButton.wasReleasedThisFrame;
    }

    //-------------------------------------------------------
    private void MatchCheck()
    {
        if (board.HasMatch())
        {
            comboCount++;

            currentState = GameState.DeletePiece;
        }
        else
        {
            comboCount = 0;

            currentState = GameState.Idle;
        }
    }

    private void DeletePiece()
    {
        int deleteCount =
            board.DeleteMatchPiece();


        int damage =
            deleteCount * 10 * comboCount;


        enemy.TakeDamage(damage);


        Debug.Log(
            comboCount +
            " Combo  " +
            damage +
            " Damage");


        currentState =
            GameState.FillPiece;
    }

    private void FillPiece()
    {
        board.FillPiece();
        currentState = GameState.MatchCheck;
    }
}