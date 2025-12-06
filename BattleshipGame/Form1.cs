using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;

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

        // Manual placement controls
        private int selectedShipSize = 4;
        private Orientation currentOrientation = Orientation.Horizontal;
        private Dictionary<int, int> shipLimits = new Dictionary<int, int> { { 4, 1 }, { 3, 2 }, { 2, 3 }, { 1, 4 } };
        private Dictionary<int, int> shipsPlaced = new Dictionary<int, int> { { 4, 0 }, { 3, 0 }, { 2, 0 }, { 1, 0 } };

        // UI controls references (so they are accessible)
        private TextBox ipBox;
        private TextBox portBox;
        private Button hostBtn;
        private Button joinBtn;
        private Button botBtn;
        private Button autoBtn;
        private Label sizeLabel;
        private NumericUpDown sizeBox;
        private Label statusLabel;

        public Form1()
        {
            InitializeComponent();
            BuildUiRuntime();
            network.MessageReceived += OnNetworkMessage;
        }

        // Build runtime UI (keeps Designer optional)
        private void BuildUiRuntime()
        {
            this.Text = "Battleship - Network";
            this.ClientSize = new Size(750, 520);

            int size = 30;
            for (int x = 0; x < 10; x++)
            {
                for (int y = 0; y < 10; y++)
                {
                    var btn = new Button
                    {
                        Location = new Point(10 + x * size, 10 + y * size),
                        Size = new Size(size, size),
                        BackColor = Color.LightBlue,
                        Tag = new Point(x, y)
                    };
                    btn.MouseDown += PlayerGrid_MouseDown;
                    playerButtons[x, y] = btn;
                    Controls.Add(btn);

                    var ebtn = new Button
                    {
                        Location = new Point(350 + x * size, 10 + y * size),
                        Size = new Size(size, size),
                        BackColor = Color.LightBlue,
                        Tag = new Point(x, y)
                    };
                    ebtn.Click += EnemyGrid_Click;
                    enemyButtons[x, y] = ebtn;
                    Controls.Add(ebtn);
                }
            }

            ipBox = new TextBox { Location = new Point(10, 330), Width = 120, Text = "127.0.0.1" };
            Controls.Add(ipBox);
            portBox = new TextBox { Location = new Point(140, 330), Width = 70, Text = "5000" };
            Controls.Add(portBox);

            hostBtn = new Button { Location = new Point(220, 325), Size = new Size(100, 30), Text = "Создать игру" };
            hostBtn.Click += async (s, e) => await StartHost();
            Controls.Add(hostBtn);

            joinBtn = new Button { Location = new Point(330, 325), Size = new Size(100, 30), Text = "Подключиться" };
            joinBtn.Click += async (s, e) => await StartJoin();
            Controls.Add(joinBtn);

            botBtn = new Button { Location = new Point(440, 325), Size = new Size(120, 30), Text = "Игра с ботом" };
            botBtn.Click += (s, e) => StartBotGame();
            Controls.Add(botBtn);

            autoBtn = new Button { Location = new Point(570, 325), Size = new Size(120, 30), Text = "Авто-расстановка" };
            autoBtn.Click += (s, e) => { ResetShipsPlaced(); AutoPlaceShips(); };
            Controls.Add(autoBtn);

            sizeLabel = new Label { Location = new Point(10, 370), Text = "Размер: 4", AutoSize = true };
            Controls.Add(sizeLabel);
            sizeBox = new NumericUpDown { Location = new Point(80, 370), Minimum = 1, Maximum = 4, Value = 4 };
            sizeBox.ValueChanged += (s, e) =>
            {
                selectedShipSize = (int)sizeBox.Value;
                sizeLabel.Text = $"Размер: {selectedShipSize}";
            };
            Controls.Add(sizeBox);

            statusLabel = new Label { Location = new Point(10, 400), Text = "Статус: Готов", AutoSize = true };
            Controls.Add(statusLabel);

            // initialize fields
            RefreshField(clear: true);
        }

        // ---------------- Network helpers ----------------

        private async Task StartHost()
        {
            if (!int.TryParse(portBox.Text, out int port))
            {
                MessageBox.Show("Некорректный порт");
                return;
            }
            try
            {
                statusLabel.Text = "Статус: Запуск сервера...";
                await network.StartServer(port);
                statusLabel.Text = "Статус: Клиент подключён (хост)";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось запустить сервер: " + ex.Message);
                statusLabel.Text = "Статус: Ошибка сервера";
            }
        }

        private async Task StartJoin()
        {
            if (!int.TryParse(portBox.Text, out int port))
            {
                MessageBox.Show("Некорректный порт");
                return;
            }
            try
            {
                statusLabel.Text = "Статус: Подключение...";
                await network.ConnectToServer(ipBox.Text.Trim(), port);
                statusLabel.Text = "Статус: Подключено (клиент)";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось подключиться: " + ex.Message);
                statusLabel.Text = "Статус: Ошибка подключения";
            }
        }

        // Обработка входящих сетевых сообщений (строка JSON)
        private void OnNetworkMessage(string json)
        {
            // парсим тип сообщения
            try
            {
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (!root.TryGetProperty("type", out var t)) return;
                string type = t.GetString();

                if (type == "Shot")
                {
                    int x = root.GetProperty("x").GetInt32();
                    int y = root.GetProperty("y").GetInt32();
                    HandleIncomingShot(x, y);
                }
                else if (type == "Result")
                {
                    int x = root.GetProperty("x").GetInt32();
                    int y = root.GetProperty("y").GetInt32();
                    string result = root.GetProperty("result").GetString();
                    HandleShotResult(x, y, result);
                }
                else if (type == "Reset")
                {
                    // соперник попросил сбросить игру
                    Invoke(() => { ResetGame(); });
                }
            }
            catch
            {
                // ignore malformed
            }
        }

        // Обработка входящего выстрела от противника
        private void HandleIncomingShot(int x, int y)
        {
            Invoke(async () =>
            {
                var cell = game.LocalPlayer.Grid[x, y];
                string result;
                if (cell.State == CellState.Ship)
                {
                    cell.State = CellState.Hit;
                    if (cell.Ship.IsSunk) cell.Ship.MarkSunk();
                    result = cell.State == CellState.Sunk ? "Sunk" : "Hit";
                }
                else if (cell.State == CellState.Empty)
                {
                    cell.State = CellState.Miss;
                    result = "Miss";
                }
                else
                {
                    // уже открыта — считаем промахом
                    result = cell.State == CellState.Ship ? "Hit" : "Miss";
                }

                RefreshField();

                // отправляем результат обратно сопернику
                await network.SendAsync(new { type = "Result", x = x, y = y, result = result });

                // проверяем победу
                if (game.CheckWinner())
                {
                    statusLabel.Text = "Статус: Вы проиграли";
                    MessageBox.Show("Все ваши корабли уничтожены — вы проиграли.");
                    ResetGame();
                }
                else
                {
                    // если промах — ход переходит к нам (т.е. противник сделал ход, теперь наш)
                    // но синхронизация ходов нужно контролировать в UI
                }
            });
        }

        // Обработка результата своего выстрела (пришло от соперника)
        private void HandleShotResult(int x, int y, string result)
        {
            Invoke(() =>
            {
                if (result == "Miss")
                    game.RemotePlayer.Grid[x, y].State = CellState.Miss;
                else if (result == "Hit")
                    game.RemotePlayer.Grid[x, y].State = CellState.Hit;
                else if (result == "Sunk")
                    game.RemotePlayer.Grid[x, y].State = CellState.Sunk;

                RefreshField();

                if (game.CheckWinner())
                {
                    statusLabel.Text = "Статус: Победа!";
                    MessageBox.Show("Вы уничтожили все корабли противника — победа!");
                    ResetGame();
                }
            });
        }

        // ---------------- Bot and placement ----------------

        private void StartBotGame()
        {
            playWithBot = true;
            ResetShipsPlaced();
            InitBotShots();
            AutoPlaceShips();     // размещаем нашего игрока случайно
            PlaceBotShips();      // размещаем бот-поле
            statusLabel.Text = "Статус: Игра против бота. Ваш ход.";
        }

        private void ResetShipsPlaced()
        {
            foreach (var key in shipLimits.Keys.ToList())
                shipsPlaced[key] = 0;
        }

        private void InitBotShots()
        {
            botAvailableShots.Clear();
            for (int x = 0; x < 10; x++)
                for (int y = 0; y < 10; y++)
                    botAvailableShots.Add(new Point(x, y));
        }

        // --- Player manual placement ---
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
                RefreshField();
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
                // Also prevent touching other ships (optional)
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int nx = cx + dx, ny = cy + dy;
                        if (nx >= 0 && ny >= 0 && nx < 10 && ny < 10)
                            if (game.LocalPlayer.Grid[nx, ny].State == CellState.Ship)
                                return false;
                    }
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
            }
            game.LocalPlayer.Ships.Add(ship);
        }

        // --- Auto placement ---
        private void AutoPlaceShips()
        {
            // очистка игрока
            for (int x = 0; x < 10; x++)
                for (int y = 0; y < 10; y++)
                {
                    game.LocalPlayer.Grid[x, y].State = CellState.Empty;
                    game.LocalPlayer.Grid[x, y].Ship = null;
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
            RefreshField();
        }

        // --- Bot placement ---
        private void PlaceBotShips()
        {
            // очистка удалённого
            for (int x = 0; x < 10; x++)
                for (int y = 0; y < 10; y++)
                {
                    game.RemotePlayer.Grid[x, y].State = CellState.Empty;
                    game.RemotePlayer.Grid[x, y].Ship = null;
                }
            game.RemotePlayer.Ships.Clear();

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

        // --- Player shot (either bot or network) ---
        private async void EnemyGrid_Click(object sender, EventArgs e)
        {
            if (game.State == GameState.GameOver) return;

            Button btn = sender as Button;
            Point pos = (Point)btn.Tag;
            int x = pos.X, y = pos.Y;

            // ignore clicking on already revealed cells
            var targetCell = game.RemotePlayer.Grid[x, y];
            if (targetCell.State == CellState.Miss || targetCell.State == CellState.Hit || targetCell.State == CellState.Sunk) return;

            if (playWithBot)
            {
                await PlayerShotBot(x, y);
                return;
            }

            // network mode: send Shot message
            if (!network.IsConnected)
            {
                MessageBox.Show("Нет сетевого соединения.");
                return;
            }

            // mark waiting visually optional
            statusLabel.Text = "Статус: Ожидание результата...";
            await network.SendAsync(new { type = "Shot", x = x, y = y });
            // actual result will arrive via network MessageReceived -> Result
        }

        // --- Player vs Bot shot logic ---
        private async Task PlayerShotBot(int x, int y)
        {
            var cell = game.RemotePlayer.Grid[x, y];
            if (cell.State == CellState.Ship)
            {
                cell.State = CellState.Hit;
                if (cell.Ship.IsSunk) cell.Ship.MarkSunk();
            }
            else if (cell.State == CellState.Empty)
            {
                cell.State = CellState.Miss;
            }

            RefreshField();

            if (game.RemotePlayer.AllShipsSunk())
            {
                MessageBox.Show("Вы победили!");
                ResetGame();
                return;
            }

            await Task.Delay(350);
            BotShot();
        }

        private void BotShot()
        {
            if (botAvailableShots.Count == 0) return;
            int idx = rnd.Next(botAvailableShots.Count);
            var p = botAvailableShots[idx];
            botAvailableShots.RemoveAt(idx);

            var cell = game.LocalPlayer.Grid[p.X, p.Y];
            if (cell.State == CellState.Ship)
            {
                cell.State = CellState.Hit;
                if (cell.Ship.IsSunk) cell.Ship.MarkSunk();
            }
            else cell.State = CellState.Miss;

            RefreshField();

            if (game.LocalPlayer.AllShipsSunk())
            {
                MessageBox.Show("Бот победил!");
                ResetGame();
                return;
            }

            // give turn back
            statusLabel.Text = "Статус: Ваш ход";
        }

        // --- Network incoming handling is in OnNetworkMessage above ---

        // Reset game: clear both fields, hide enemy ships, reset counters
        private void ResetGame()
        {
            playWithBot = false; // reset mode
            // clear player
            for (int x = 0; x < 10; x++)
                for (int y = 0; y < 10; y++)
                {
                    game.LocalPlayer.Grid[x, y].State = CellState.Empty;
                    game.LocalPlayer.Grid[x, y].Ship = null;
                }
            game.LocalPlayer.Ships.Clear();

            // clear remote
            for (int x = 0; x < 10; x++)
                for (int y = 0; y < 10; y++)
                {
                    game.RemotePlayer.Grid[x, y].State = CellState.Empty;
                    game.RemotePlayer.Grid[x, y].Ship = null;
                }
            game.RemotePlayer.Ships.Clear();

            ResetShipsPlaced();
            RefreshField(clear: true);
            statusLabel.Text = "Статус: Готов";
        }

        // Refresh UI field; if clear==true — show empty fields
        private void RefreshField(bool clear = false)
        {
            for (int x = 0; x < 10; x++)
                for (int y = 0; y < 10; y++)
                {
                    // player
                    var pc = game.LocalPlayer.Grid[x, y];
                    playerButtons[x, y].BackColor = clear ? Color.LightBlue : pc.State switch
                    {
                        CellState.Empty => Color.LightBlue,
                        CellState.Ship => Color.Gray,
                        CellState.Miss => Color.White,
                        CellState.Hit => Color.Red,
                        CellState.Sunk => Color.DarkRed,
                        _ => Color.LightBlue
                    };

                    // enemy: hide ships unless revealed (Hit/Sunk)
                    var ec = game.RemotePlayer.Grid[x, y];
                    enemyButtons[x, y].BackColor = clear ? Color.LightBlue : ec.State switch
                    {
                        CellState.Empty => Color.LightBlue,
                        CellState.Ship => Color.LightBlue, // keep hidden
                        CellState.Miss => Color.White,
                        CellState.Hit => Color.Red,
                        CellState.Sunk => Color.DarkRed,
                        _ => Color.LightBlue
                    };
                }
        }
    }
}
