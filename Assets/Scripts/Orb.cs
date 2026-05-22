using UnityEngine;

public class Orb : MonoBehaviour
{
    public int x;
    public int y;

    private Board board;

    void Start()
    {
        board = FindObjectOfType<Board>();
    }

    public void Init(int x, int y)
    {
        this.x = x;
        this.y = y;
    }

    public void SetPosition(int newX, int newY)
    {
        x = newX;
        y = newY;
        transform.position = new Vector3(x, y, 0);
    }

    void OnMouseDrag()
    {
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        worldPos.z = 0;

        // 近いマスを探す
        int targetX = Mathf.RoundToInt(worldPos.x);
        int targetY = Mathf.RoundToInt(worldPos.y);

        // 範囲外なら無視
        if (targetX < 0 || targetX >= board.width ||
            targetY < 0 || targetY >= board.height)
            return;

        // 別マスなら入れ替え
        if (targetX != x || targetY != y)
        {
            board.Swap(x, y, targetX, targetY);
        }
    }
}
