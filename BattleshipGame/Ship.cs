using System.Collections.Generic;
using System.Linq;

namespace BattleshipGame
{
    public enum Orientation { Horizontal, Vertical }

    public class Ship
    {
        public List<Cell> Cells { get; set; } = new List<Cell>();
        public Orientation Orientation { get; set; } = Orientation.Horizontal;
        public bool IsSunk => Cells.All(c => c.State == CellState.Hit || c.State == CellState.Sunk);

        public void MarkSunk()
        {
            foreach (var cell in Cells)
                cell.State = CellState.Sunk;
        }
    }
}
