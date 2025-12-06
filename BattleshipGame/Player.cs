using System.Collections.Generic;
using System.Linq;

namespace BattleshipGame
{
    public class Player
    {
        public Cell[,] Grid { get; set; } = new Cell[10, 10];
        public List<Ship> Ships { get; set; } = new List<Ship>();

        public Player()
        {
            for (int x = 0; x < 10; x++)
                for (int y = 0; y < 10; y++)
                    Grid[x, y] = new Cell { X = x, Y = y };
        }

        public bool AllShipsSunk()
        {
            return Ships.All(s => s.IsSunk);
        }
    }
}
