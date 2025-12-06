namespace BattleshipGame
{
    public enum GameState { Placement, Waiting, MyTurn, EnemyTurn, GameOver }

    public class GameManager
    {
        public Player LocalPlayer { get; set; } = new Player();
        public Player RemotePlayer { get; set; } = new Player();
        public GameState State { get; set; } = GameState.Placement;

        public void MakeMove(int x, int y)
        {
            var targetCell = RemotePlayer.Grid[x, y];
            if (targetCell.State == CellState.Empty)
                targetCell.State = CellState.Miss;
            else if (targetCell.State == CellState.Ship)
            {
                targetCell.State = CellState.Hit;
                if (targetCell.Ship.IsSunk)
                    targetCell.Ship.MarkSunk();
            }
        }

        public bool CheckWinner()
        {
            if (RemotePlayer.AllShipsSunk() || LocalPlayer.AllShipsSunk())
            {
                State = GameState.GameOver;
                return true;
            }
            return false;
        }
    }
}
