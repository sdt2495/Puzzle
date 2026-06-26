using System;

namespace NIKEE
{
    class Program
    {
        static void Main()
        {
            // ★ここを変えるだけで盤面サイズを変更できる
            Board board = new Board(5, 6);   // ← まずは 5×6
            // Board board = new Board(7, 6); // ← 次は 7×6 にしたい時

            while (true)
            {
                Console.Clear();
                board.Draw();

                Console.WriteLine("\nSelect tile (x y) or 'q' to quit:");
                string input = Console.ReadLine();

                if (input == "q")
                    break;

                var parts = input.Split(' ');
                if (parts.Length != 2)
                    continue;

                if (int.TryParse(parts[0], out int x) &&
                    int.TryParse(parts[1], out int y))
                {
                    board.ToggleTile(x, y);
                }
            }
        }
    }

    public class Board
    {
        private Tile[,] tiles;
        private int width;
        private int height;

        public Board(int width, int height)
        {
            this.width = width;
            this.height = height;

            tiles = new Tile[width, height];

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    tiles[x, y] = new Tile();
                }
            }
        }

        public void Draw()
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Console.Write(tiles[x, y].IsOn ? "■ " : "□ ");
                }
                Console.WriteLine();
            }
        }

        public void ToggleTile(int x, int y)
        {
            if (x < 0 || x >= width || y < 0 || y >= height)
                return;

            tiles[x, y].Toggle();
        }
    }

    public class Tile
    {
        public bool IsOn { get; private set; }

        public Tile()
        {
            IsOn = false;
        }

        public void Toggle()
        {
            IsOn = !IsOn;
        }
    }
}
