using System.Collections.Generic;
using UnityEngine;

public class PlayerManager : MonoBehaviour
{
    public static PlayerManager Instance;

    [System.Serializable]
    public class PlayerData
    {
        public Piece.PieceKind color;
        public PlayerAttack player;
    }

    [Header("プレイヤー一覧")]
    [SerializeField]
    private PlayerData[] players;

    private Dictionary<Piece.PieceKind, PlayerAttack> playerDic;

    private void Awake()
    {
        Instance = this;

        playerDic = new Dictionary<Piece.PieceKind, PlayerAttack>();

        foreach (PlayerData data in players)
        {
            if (data.player == null)
                continue;

            playerDic[data.color] = data.player;
        }
    }

    /// <summary>
    /// 色に対応するPlayerを取得
    /// </summary>
    public PlayerAttack GetPlayer(Piece.PieceKind color)
    {
        if (playerDic.ContainsKey(color))
            return playerDic[color];

        return null;
    }
}