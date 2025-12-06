namespace BattleshipGame
{
    public enum CellState { Empty, Ship, Miss, Hit, Sunk }

    public class Cell
    {
        public int X { get; set; }
        public int Y { get; set; }
        public CellState State { get; set; } = CellState.Empty;
        public Ship Ship { get; set; } = null;
    }
}
