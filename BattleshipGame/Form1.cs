using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BattleshipGame
{
    public partial class Form1 : Form
    {
        private GameManager game = new GameManager();
        private NetworkManager network = new NetworkManager();
        private Button[,] playerButtons = new Button[10, 10];
        private Button[,] enemyButtons = new Button[10, 10];

        private bool playWithBot = false;
        private Random rnd = new Random();
        private List<Point> botAvailableShots = new List<Point>();

        // Для ручной расстановки
        private int selectedShipSize = 4;
        private Orientation currentOrientation = Orientation.Horizontal;
        private Dictionary<int, int> shipLimits = new Dictionary<int, int> { { 4, 1 }, { 3, 2 }, { 2, 3 }, { 1, 4 } };
        private Dictionary<int, int> shipsPlaced = new Dictionary<int, int> { { 4, 0 }, { 3, 0 }, { 2, 0 }, { 1, 0 } };

        public Form1()
        {
            InitializeComponent();
            InitGrids();
            network.MessageReceived += Network_MessageReceived;
        }

        private void InitGrids()
        {
            int size = 30;

            for (int x = 0; x < 10; x++)
            {
                for (int y = 0; y < 10; y++)
                {
                    var btn = new Button
                    {
                        Location = new Point(x * size, y * size),
                        Size = new Size(size, size),
                        BackColor = Color.LightBlue,
                        Tag = new Point(x, y)
                    };
                    btn.MouseDown += PlayerGrid_MouseDown;
                    playerButtons[x, y] = btn;
                    Controls.Add(btn);

                    var ebtn = new Button
                    {
                        Location = new Point(350 + x * size, y * size),
                        Size = new Size(size, size),
                        BackColor = Color.LightBlue,
                        Tag = new Point(x, y)
                    };
                    ebtn.Click += EnemyGrid_Click;
                    enemyButtons[x, y] = ebtn;
                    Controls.Add(ebtn);
                }
            }

            var ipBox = new TextBox { Location = new Point(40, 400), Width = 100, Text = "127.0.0.1" };
            Controls.Add(ipBox);
            var portBox = new TextBox { Location = new Point(200, 400), Width = 60, Text = "5000" };
            Controls.Add(portBox);

            var hostBtn = new Button { Location = new Point(10, 430), Size = new Size(100, 30), Text = "Создать игру" };
            hostBtn.Click += async (s, e) =>
            {
                int port = int.Parse(portBox.Text);
                await network.StartServer(port);
                game.State = GameState.MyTurn;
            };
            Controls.Add(hostBtn);

            var joinBtn = new Button { Location = new Point(120, 430), Size = new Size(100, 30), Text = "Подключиться" };
            joinBtn.Click += async (s, e) =>
            {
                int port = int.Parse(portBox.Text);
                string ip = ipBox.Text;
                await network.ConnectToServer(ip, port);
                game.State = GameState.EnemyTurn;
            };
            Controls.Add(joinBtn);

            var botBtn = new Button { Location = new Point(230, 430), Size = new Size(120, 30), Text = "Игра с ботом" };
            botBtn.Click += (s, e) =>
            {
                playWithBot = true;
                game.State = GameState.MyTurn;
                ResetShipsPlaced();
                InitBotShots();
                AutoPlaceShips();
                PlaceBotShips();
            };
            Controls.Add(botBtn);

            var autoBtn = new Button { Location = new Point(360, 430), Size = new Size(120, 30), Text = "Авто-расстановка" };
            autoBtn.Click += (s, e) =>
            {
                ResetShipsPlaced();
                AutoPlaceShips();
            };
            Controls.Add(autoBtn);

            var sizeLabel = new Label { Location = new Point(500, 400), Text = "Размер: 4", AutoSize = true };
            Controls.Add(sizeLabel);

            var sizeBox = new NumericUpDown { Location = new Point(560, 400), Minimum = 1, Maximum = 4, Value = 4 };
            sizeBox.ValueChanged += (s, e) =>
            {
                selectedShipSize = (int)sizeBox.Value;
                sizeLabel.Text = $"Размер: {selectedShipSize}";
            };
            Controls.Add(sizeBox);
        }

        private void ResetShipsPlaced()
        {
            foreach (var key in shipLimits.Keys.ToList())
                shipsPlaced[key] = 0;
        }

        private void PlayerGrid_MouseDown(object sender, MouseEventArgs e)
        {
            Button btn = sender as Button;
            Point pos = (Point)btn.Tag;
            int x = pos.X;
            int y = pos.Y;

            if (e.Button == MouseButtons.Right)
            {
                currentOrientation = currentOrientation == Orientation.Horizontal ? Orientation.Vertical : Orientation.Horizontal;
                return;
            }

            if (shipsPlaced[selectedShipSize] >= shipLimits[selectedShipSize])
            {
                MessageBox.Show($"Все корабли размера {selectedShipSize} уже размещены");
                return;
            }

            if (CanPlaceShipPlayer(x, y, selectedShipSize, currentOrientation))
            {
                PlaceShipPlayer(x, y, selectedShipSize, currentOrientation);
                shipsPlaced[selectedShipSize]++;
            }
        }

        private bool CanPlaceShipPlayer(int x, int y, int size, Orientation orientation)
        {
            for (int i = 0; i < size; i++)
            {
                int cx = x + (orientation == Orientation.Horizontal ? i : 0);
                int cy = y + (orientation == Orientation.Vertical ? i : 0);
                if (cx >= 10 || cy >= 10) return false;
                if (game.LocalPlayer.Grid[cx, cy].State != CellState.Empty) return false;
            }
            return true;
        }

        private void PlaceShipPlayer(int x, int y, int size, Orientation orientation)
        {
            Ship ship = new Ship { Orientation = orientation };
            for (int i = 0; i < size; i++)
            {
                int cx = x + (orientation == Orientation.Horizontal ? i : 0);
                int cy = y + (orientation == Orientation.Vertical ? i : 0);
                var cell = game.LocalPlayer.Grid[cx, cy];
                cell.State = CellState.Ship;
                cell.Ship = ship;
                ship.Cells.Add(cell);
                playerButtons[cx, cy].BackColor = Color.Gray;
            }
            game.LocalPlayer.Ships.Add(ship);
        }

        private void AutoPlaceShips()
        {
            for (int x = 0; x < 10; x++)
                for (int y = 0; y < 10; y++)
                {
                    game.LocalPlayer.Grid[x, y].State = CellState.Empty;
                    playerButtons[x, y].BackColor = Color.LightBlue;
                }
            game.LocalPlayer.Ships.Clear();
            ResetShipsPlaced();

            int[] shipSizes = { 4, 3, 3, 2, 2, 2, 1, 1, 1, 1 };
            foreach (int size in shipSizes)
            {
                bool placed = false;
                while (!placed)
                {
                    int x = rnd.Next(0, 10);
                    int y = rnd.Next(0, 10);
                    bool horizontal = rnd.Next(0, 2) == 0;
                    if (CanPlaceShipPlayer(x, y, size, horizontal ? Orientation.Horizontal : Orientation.Vertical))
                    {
                        PlaceShipPlayer(x, y, size, horizontal ? Orientation.Horizontal : Orientation.Vertical);
                        shipsPlaced[size]++;
                        placed = true;
                    }
                }
            }
        }

        private void PlaceBotShips()
        {
            game.RemotePlayer.Ships.Clear();
            for (int x = 0; x < 10; x++)
                for (int y = 0; y < 10; y++)
                    game.RemotePlayer.Grid[x, y].State = CellState.Empty;

            int[] shipSizes = { 4, 3, 3, 2, 2, 2, 1, 1, 1, 1 };
            foreach (int size in shipSizes)
            {
                bool placed = false;
                while (!placed)
                {
                    int x = rnd.Next(0, 10);
                    int y = rnd.Next(0, 10);
                    bool horizontal = rnd.Next(0, 2) == 0;
                    if (CanPlaceShipBot(x, y, size, horizontal))
                    {
                        PlaceShipBot(x, y, size, horizontal);
                        placed = true;
                    }
                }
            }
        }

        private bool CanPlaceShipBot(int x, int y, int size, bool horizontal)
        {
            for (int i = 0; i < size; i++)
            {
                int cx = x + (horizontal ? i : 0);
                int cy = y + (horizontal ? 0 : i);
                if (cx >= 10 || cy >= 10) return false;
                if (game.RemotePlayer.Grid[cx, cy].State != CellState.Empty) return false;
            }
            return true;
        }

        private void PlaceShipBot(int x, int y, int size, bool horizontal)
        {
            Ship ship = new Ship { Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical };
            for (int i = 0; i < size; i++)
            {
                int cx = x + (horizontal ? i : 0);
                int cy = y + (horizontal ? 0 : i);
                var cell = game.RemotePlayer.Grid[cx, cy];
                cell.State = CellState.Ship;
                cell.Ship = ship;
                ship.Cells.Add(cell);
            }
            game.RemotePlayer.Ships.Add(ship);
        }

        private void InitBotShots()
        {
            botAvailableShots.Clear();
            for (int x = 0; x < 10; x++)
                for (int y = 0; y < 10; y++)
                    botAvailableShots.Add(new Point(x, y));
        }

        private async Task PlayerShotBot(int x, int y)
        {
            var cell = game.RemotePlayer.Grid[x, y];
            if (cell.State == CellState.Miss || cell.State == CellState.Hit || cell.State == CellState.Sunk) return;

            if (cell.State == CellState.Ship)
            {
                cell.State = CellState.Hit;
                if (cell.Ship.IsSunk) cell.Ship.MarkSunk();
            }
            else cell.State = CellState.Miss;

            RefreshField();

            if (game.CheckWinner())
            {
                MessageBox.Show("Вы победили!");
                ResetGame();
                return;
            }

            await Task.Delay(500);
            BotShot();
        }

        private void BotShot()
        {
            if (botAvailableShots.Count == 0) return;

            int index = rnd.Next(botAvailableShots.Count);
            Point shot = botAvailableShots[index];
            botAvailableShots.RemoveAt(index);

            var cell = game.LocalPlayer.Grid[shot.X, shot.Y];
            if (cell.State == CellState.Ship)
            {
                cell.State = CellState.Hit;
                if (cell.Ship.IsSunk) cell.Ship.MarkSunk();
            }
            else cell.State = CellState.Miss;

            RefreshField();

            if (game.CheckWinner())
            {
                MessageBox.Show("Бот победил!");
                ResetGame();
                return;
            }

            game.State = GameState.MyTurn;
        }

        private void ResetGame()
        {
            foreach (var cell in game.LocalPlayer.Grid)
                cell.State = CellState.Empty;
            foreach (var cell in game.RemotePlayer.Grid)
                cell.State = CellState.Empty;
            RefreshField(clear: true);
            ResetShipsPlaced();
            game.State = GameState.Placement;
        }

        private async void EnemyGrid_Click(object sender, EventArgs e)
        {
            if (game.State != GameState.MyTurn) return;

            Button btn = sender as Button;
            Point pos = (Point)btn.Tag;
            int x = pos.X;
            int y = pos.Y;

            if (playWithBot)
            {
                await PlayerShotBot(x, y);
            }
            else
            {
                var msg = new { type = "Shot", x, y };
                await network.SendMessage(msg);
                game.State = GameState.EnemyTurn;
            }
        }

        private void Network_MessageReceived(string msg)
        {
            Invoke(() =>
            {
                var doc = System.Text.Json.JsonDocument.Parse(msg);
                var type = doc.RootElement.GetProperty("type").GetString();

                if (type == "Shot")
                {
                    int x = doc.RootElement.GetProperty("x").GetInt32();
                    int y = doc.RootElement.GetProperty("y").GetInt32();

                    var cell = game.LocalPlayer.Grid[x, y];
                    if (cell.State == CellState.Empty) cell.State = CellState.Miss;
                    else if (cell.State == CellState.Ship)
                    {
                        cell.State = CellState.Hit;
                        if (cell.Ship.IsSunk) cell.Ship.MarkSunk();
                    }

                    RefreshField();

                    _ = network.SendMessage(new { type = "Result", x, y, result = cell.State.ToString() });

                    if (game.CheckWinner())
                    {
                        MessageBox.Show("Вы проиграли!");
                        ResetGame();
                    }
                    else game.State = GameState.MyTurn;
                }
                else if (type == "Result")
                {
                    int x = doc.RootElement.GetProperty("x").GetInt32();
                    int y = doc.RootElement.GetProperty("y").GetInt32();
                    string result = doc.RootElement.GetProperty("result").GetString();

                    enemyButtons[x, y].BackColor = result switch
                    {
                        "Miss" => Color.White,
                        "Hit" => Color.Red,
                        "Sunk" => Color.DarkRed,
                        _ => Color.LightBlue
                    };

                    if (game.CheckWinner())
                    {
                        MessageBox.Show("Вы победили!");
                        ResetGame();
                    }
                }
            });
        }

        private void RefreshField(bool clear = false)
        {
            for (int x = 0; x < 10; x++)
                for (int y = 0; y < 10; y++)
                {
                    var cell = game.LocalPlayer.Grid[x, y];
                    playerButtons[x, y].BackColor = clear ? Color.LightBlue : cell.State switch
                    {
                        CellState.Empty => Color.LightBlue,
                        CellState.Ship => Color.Gray,
                        CellState.Miss => Color.White,
                        CellState.Hit => Color.Red,
                        CellState.Sunk => Color.DarkRed,
                        _ => Color.LightBlue
                    };

                    var ecell = game.RemotePlayer.Grid[x, y];
                    enemyButtons[x, y].BackColor = clear ? Color.LightBlue : ecell.State switch
                    {
                        CellState.Empty => Color.LightBlue,
                        CellState.Ship => Color.LightBlue, // скрываем корабли
                        CellState.Miss => Color.White,
                        CellState.Hit => Color.Red,
                        CellState.Sunk => Color.DarkRed,
                        _ => Color.LightBlue
                    };
                }
        }
    }
}
