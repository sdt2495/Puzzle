using UnityEngine;

public class Board : MonoBehaviour
{
    public int width = 5;
    public int height = 6;
    public GameObject orbPrefab;

    private Orb[,] orbs;

    void Start()
    {
        CreateBoard();
    }

    void CreateBoard()
    {
        orbs = new Orb[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3 pos = new Vector3(x, y, 0);
                GameObject obj = Instantiate(orbPrefab, pos, Quaternion.identity);
                orbs[x, y] = obj.GetComponent<Orb>();
                orbs[x, y].Init(x, y);
            }
        }
    }

    public void Swap(int x1, int y1, int x2, int y2)
    {
        var a = orbs[x1, y1];
        var b = orbs[x2, y2];

        orbs[x1, y1] = b;
        orbs[x2, y2] = a;

        a.SetPosition(x2, y2);
        b.SetPosition(x1, y1);
    }
}
