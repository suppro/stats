using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace stats
{
    public partial class Form1 : Form
    {
        private Timer updateTimer;
        private Timer attackTimer;
        private Timer buffTimer;
        private Process gameProcess;
        private GameController gameController;

        // Состояние атаки
        private bool isAttacking = false;
        private int killedMobCount = 0;
        private MobData? currentTarget = null;
        private bool enableLootCollection = false;
        private bool enableAutoHeal = false;
        private bool enableBuff = false;
        private bool enableLootHighlight = false; // Подсветка лута (Shift+E)
        private bool isLooting = false; // Флаг процесса сбора лута
        private DateTime lootStartTime = DateTime.MinValue; // Время начала сбора лута
        private Random random = new Random(); // Генератор случайных чисел для задержек
        
        // Для периодических пауз (имитация отвлечения)
        private DateTime lastPauseTime = DateTime.MinValue;
        private bool isPausing = false; // Флаг активной паузы
        
        // Максимальное HP персонажа (для расчета процентов)
        private int playerMaxHP = 0;
        
        // Порог низкого HP для определения остаточных данных мертвого моба
        // Если HP в этом диапазоне И координаты (0,0,0), то это остаточные данные от мертвого моба
        private const int MAX_RESIDUAL_HP = 15;
        
        // Кэш для списка мобов (адреса статичные, сканируем только 1 раз при старте)
        private bool addressesScanned = false;
        private bool connectionLogged = false;

        // Labels для персонажа
        private Label lblPlayerHP;
        private Label lblPlayerMP;
        private Label lblPlayerId;
        
        // Label для моба
        private Label lblMobHP;
        private Label lblMobTargetId;

        // Label для статуса
        private Label lblStatus;

        // UI элементы для атаки
        private ComboBox cmbServer;
        private TextBox txtMobIds;
        private CheckBox chkLootCollection;
        private CheckBox chkAutoHeal;
        private CheckBox chkBuff;
        private CheckBox chkLootHighlight;
        private Button btnStart;
        private Button btnStop;
        private Button btnCopyLogs;
        private Button btnClearLogs;
        private Label lblKilledCount;
        private ListBox listBoxLogs;
        
        // Выбранный сервер
        private string selectedServer = "default";
        
        // Список ID мобов для атаки
        private HashSet<int> targetMobIds = new HashSet<int>();

        public Form1()
        {
            InitializeComponent();
            SetupForm();
            StartUpdateTimer();
        }

        private void SetupForm()
        {
            // Изменено название окна для снижения заметности
            this.Text = "System Monitor";
            this.Size = new Size(790, 800);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.BackColor = Color.Black;

            int yPos = 20;

            // Заголовок персонажа
            Label lblPlayerTitle = new Label
            {
                Text = "=== ПЕРСОНАЖ ===",
                ForeColor = Color.Cyan,
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(20, yPos)
            };
            this.Controls.Add(lblPlayerTitle);
            yPos += 35;

            // HP персонажа
            lblPlayerHP = new Label
            {
                Text = "HP: ---",
                ForeColor = Color.Red,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(30, yPos)
            };
            this.Controls.Add(lblPlayerHP);
            yPos += 25;

            // MP персонажа
            lblPlayerMP = new Label
            {
                Text = "MP: ---",
                ForeColor = Color.Blue,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(30, yPos)
            };
            this.Controls.Add(lblPlayerMP);
            yPos += 25;

            // ID персонажа
            lblPlayerId = new Label
            {
                Text = "Player ID: ---",
                ForeColor = Color.LightBlue,
                Font = new Font("Segoe UI", 10F),
                AutoSize = true,
                Location = new Point(30, yPos)
            };
            this.Controls.Add(lblPlayerId);
            yPos += 40;

            // HP моба
            lblMobHP = new Label
            {
                Text = "Моб HP: ---",
                ForeColor = Color.Orange,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(30, yPos)
            };
            this.Controls.Add(lblMobHP);
            yPos += 25;

            // TargetId моба
            lblMobTargetId = new Label
            {
                Text = "Target ID: ---",
                ForeColor = Color.Yellow,
                Font = new Font("Segoe UI", 10F),
                AutoSize = true,
                Location = new Point(30, yPos)
            };
            this.Controls.Add(lblMobTargetId);
            yPos += 40;

            // Выбор сервера
            Label lblServer = new Label
            {
                Text = "Сервер:",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F),
                AutoSize = true,
                Location = new Point(30, yPos)
            };
            this.Controls.Add(lblServer);
            yPos += 25;

            cmbServer = new ComboBox
            {
                Size = new Size(150, 25),
                Location = new Point(30, yPos),
                BackColor = Color.DarkGray,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbServer.Items.Add("default");
            cmbServer.Items.Add("tw");
            cmbServer.Items.Add("pn");
            cmbServer.SelectedIndex = 0;
            cmbServer.SelectedIndexChanged += CmbServer_SelectedIndexChanged;
            this.Controls.Add(cmbServer);
            yPos += 35;

            // Статус
            lblStatus = new Label
            {
                Text = "Ожидание игры...",
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 10F),
                AutoSize = true,
                Location = new Point(20, yPos)
            };
            this.Controls.Add(lblStatus);
            yPos += 35;

            // Заголовок атаки
            Label lblAttackTitle = new Label
            {
                Text = "=== АВТОАТАКА ===",
                ForeColor = Color.Red,
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(20, yPos)
            };
            this.Controls.Add(lblAttackTitle);
            yPos += 35;

            // Поле для ввода ID мобов
            Label lblMobIdsLabel = new Label
            {
                Text = "ID мобов (через запятую):",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F),
                AutoSize = true,
                Location = new Point(30, yPos)
            };
            this.Controls.Add(lblMobIdsLabel);
            yPos += 25;

            txtMobIds = new TextBox
            {
                Text = "",
                Size = new Size(250, 25),
                Location = new Point(30, yPos),
                BackColor = Color.DarkGray,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F)
            };
            this.Controls.Add(txtMobIds);
            yPos += 35;

            // Чекбокс сбора лута
            chkLootCollection = new CheckBox
            {
                Text = "Сбор лута (E после убийства)",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11F),
                AutoSize = true,
                Location = new Point(30, yPos),
                BackColor = Color.Black
            };
            this.Controls.Add(chkLootCollection);
            yPos += 35;

            // Чекбокс автохила
            chkAutoHeal = new CheckBox
            {
                Text = "Автохил (1 при HP ≤ 50%, до 80%)",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11F),
                AutoSize = true,
                Location = new Point(30, yPos),
                BackColor = Color.Black
            };
            this.Controls.Add(chkAutoHeal);
            yPos += 35;

            // Чекбокс бафа
            chkBuff = new CheckBox
            {
                Text = "Баф (клавиша 3 раз в минуту)",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11F),
                AutoSize = true,
                Location = new Point(30, yPos),
                BackColor = Color.Black
            };
            chkBuff.CheckedChanged += ChkBuff_CheckedChanged;
            this.Controls.Add(chkBuff);
            yPos += 35;

            // Чекбокс подсветки лута
            chkLootHighlight = new CheckBox
            {
                Text = "Подсветка лута (Shift+E зажато)",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11F),
                AutoSize = true,
                Location = new Point(30, yPos),
                BackColor = Color.Black
            };
            chkLootHighlight.CheckedChanged += ChkLootHighlight_CheckedChanged;
            this.Controls.Add(chkLootHighlight);
            yPos += 35;

            // Кнопки старт/стоп
            btnStart = new Button
            {
                Text = "СТАРТ",
                Size = new Size(100, 35),
                Location = new Point(30, yPos),
                BackColor = Color.Green,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat
            };
            btnStart.Click += BtnStart_Click;
            this.Controls.Add(btnStart);

            btnStop = new Button
            {
                Text = "ОСТАНОВИТЬ",
                Size = new Size(120, 35),
                Location = new Point(140, yPos),
                BackColor = Color.Red,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Enabled = false
            };
            btnStop.Click += BtnStop_Click;
            this.Controls.Add(btnStop);
            yPos += 45;

            // Счетчик убитых
            lblKilledCount = new Label
            {
                Text = "Убито: 0",
                ForeColor = Color.Lime,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(30, yPos)
            };
            this.Controls.Add(lblKilledCount);
            yPos += 35;

            // Окно логов
            Label lblLogsTitle = new Label
            {
                Text = "Логи:",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(30, yPos)
            };
            this.Controls.Add(lblLogsTitle);
            yPos += 25;

            listBoxLogs = new ListBox
            {
                Size = new Size(700, 150),
                Location = new Point(30, yPos),
                BackColor = Color.DarkGray,
                ForeColor = Color.White,
                Font = new Font("Consolas", 9F),
                IntegralHeight = false,
                SelectionMode = SelectionMode.MultiExtended
            };
            this.Controls.Add(listBoxLogs);
            yPos += 160;

            // Кнопки для работы с логами
            btnCopyLogs = new Button
            {
                Text = "Копировать логи",
                Size = new Size(120, 30),
                Location = new Point(30, yPos),
                BackColor = Color.DarkBlue,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F),
                FlatStyle = FlatStyle.Flat
            };
            btnCopyLogs.Click += BtnCopyLogs_Click;
            this.Controls.Add(btnCopyLogs);

            btnClearLogs = new Button
            {
                Text = "Очистить логи",
                Size = new Size(120, 30),
                Location = new Point(160, yPos),
                BackColor = Color.DarkRed,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F),
                FlatStyle = FlatStyle.Flat
            };
            btnClearLogs.Click += BtnClearLogs_Click;
            this.Controls.Add(btnClearLogs);
        }

        private void StartUpdateTimer()
        {
            updateTimer = new Timer
            {
                Interval = 200
            };
            updateTimer.Tick += UpdateTimer_Tick;
            updateTimer.Start();
        }

        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            // Получаем процесс
            if (gameProcess == null || gameProcess.HasExited)
            {
                connectionLogged = false; // Сбрасываем флаг при потере подключения
                
                // Определяем имя процесса в зависимости от выбранного сервера
                string targetProcessName = GetProcessNameForServer(selectedServer);
                Process[] processes = Process.GetProcessesByName(targetProcessName);
                
                if (processes.Length == 0)
                {
                    UpdateStatus($"Игра не найдена ({targetProcessName})", Color.Red);
                    ClearAllData();
                    gameController?.Close();
                    gameController = null;
                    gameProcess = null;
                    return;
                }
                gameProcess = processes[0];
            }

            // Создаем или обновляем GameController
            if (gameController == null || !gameController.IsValid)
            {
                gameController?.Close();
                gameController = new GameController(gameProcess, selectedServer);
                if (!gameController.IsValid)
                {
                    UpdateStatus("Нет доступа (запусти от админа)", Color.Red);
                    ClearAllData();
                    return;
                }
            }

            // Обновляем сервер, если изменился
            gameController.SetServer(selectedServer);

            string processName = GetProcessNameForServer(selectedServer);
            UpdateStatus($"Подключено: {processName}.exe (PID: {gameProcess.Id})", Color.Lime);
            
            // Логируем подключение только один раз
            if (!connectionLogged)
            {
                AddLog($"Подключено к процессу {processName}.exe (PID: {gameProcess.Id})");
                connectionLogged = true;
            }

            // Если подсветка лута включена, убеждаемся что клавиши зажаты
            if (enableLootHighlight)
            {
                try
                {
                    IntPtr hWnd = gameProcess.MainWindowHandle;
                    if (hWnd != IntPtr.Zero)
                    {
                        KeyboardHelper.PressShiftE(hWnd);
                    }
                }
                catch
                {
                    // Игнорируем ошибки
                }
            }

            // Читаем данные персонажа
            if (gameController != null)
            {
                var playerData = gameController.ReadPlayerData();
                UpdatePlayerData(playerData);
            }
        }

        private void UpdateStatus(string text, Color color)
        {
            lblStatus.Text = text;
            lblStatus.ForeColor = color;
        }

        private void ClearAllData()
        {
            lblPlayerHP.Text = "HP: ---";
            lblPlayerMP.Text = "MP: ---";
            lblPlayerId.Text = "Player ID: ---";
            lblMobHP.Text = "Моб HP: ---";
            lblMobTargetId.Text = "Target ID: ---";
        }

        private void UpdatePlayerData(PlayerData data)
        {
            if (data.IsValid)
            {
                // Обновляем максимальное HP, если текущее больше сохраненного
                if (data.HP > playerMaxHP)
                {
                    playerMaxHP = data.HP;
                }
                
                lblPlayerHP.Text = $"HP: {data.HP}";
                lblPlayerHP.ForeColor = Color.Red;
                lblPlayerMP.Text = $"MP: {data.MP}";
                lblPlayerMP.ForeColor = Color.Blue;
                lblPlayerId.Text = $"Player ID: {data.PlayerId}";
            }
            else
            {
                lblPlayerHP.Text = "HP: ---";
                lblPlayerMP.Text = "MP: ---";
                lblPlayerId.Text = "Player ID: ---";
            }
        }

        private void AddLog(string message)
        {
            if (listBoxLogs == null) return;

            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string logMessage = $"[{timestamp}] {message}";

            if (listBoxLogs.InvokeRequired)
            {
                listBoxLogs.Invoke((MethodInvoker)delegate
                {
                    listBoxLogs.Items.Insert(0, logMessage);
                    if (listBoxLogs.Items.Count > 100)
                    {
                        listBoxLogs.Items.RemoveAt(listBoxLogs.Items.Count - 1);
                    }
                });
            }
            else
            {
                listBoxLogs.Items.Insert(0, logMessage);
                if (listBoxLogs.Items.Count > 100)
                {
                    listBoxLogs.Items.RemoveAt(listBoxLogs.Items.Count - 1);
                }
            }
        }

        private void CmbServer_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbServer.SelectedItem != null)
            {
                selectedServer = cmbServer.SelectedItem.ToString();
                if (gameController != null)
                {
                    gameController.SetServer(selectedServer);
                }
                // Сбрасываем подключение, чтобы переподключиться к правильному процессу
                connectionLogged = false;
                gameController?.Close();
                gameController = null;
                gameProcess = null;
                AddLog($"Выбран сервер: {selectedServer}");
            }
        }

        private string GetProcessNameForServer(string server)
        {
            if (server == "pn")
                return ServerOffsets.Pn.ProcessName;
            else if (server == "tw")
                return ServerOffsets.Tw.ProcessName;
            else
                return ServerOffsets.Default.ProcessName;
        }


        private void AutoHeal()
        {
            try
            {
                if (gameController == null || !gameController.IsValid) return;

                var playerData = gameController.ReadPlayerData();
                if (!playerData.IsValid)
                {
                    return;
                }

                // Обновляем максимальное HP, если текущее больше сохраненного
                if (playerData.HP > playerMaxHP)
                {
                    playerMaxHP = playerData.HP;
                }

                // Если максимальное HP еще не определено (меньше 100), используем текущее как максимум
                if (playerMaxHP < 100)
                {
                    playerMaxHP = Math.Max(playerMaxHP, playerData.HP);
                }

                // Начинаем хил от 50% и меньше, лечим до 80%
                double hpPercent = playerMaxHP > 0 ? (double)playerData.HP / playerMaxHP * 100.0 : 0;
                
                if (hpPercent <= 50.0)
                {
                    if (gameProcess == null || gameProcess.HasExited)
                    {
                        return;
                    }

                    int maxHealAttempts = 20; // Максимум попыток лечения
                    int attempts = 0;
                    double targetPercent = 80.0; // Цель - 80%
                    int targetHP = (int)(playerMaxHP * targetPercent / 100.0);

                    while (hpPercent < targetPercent && attempts < maxHealAttempts)
                    {
                        KeyboardHelper.SendKey1(gameProcess);
                        // Случайная задержка между нажатиями: 450-550мс (±10%)
                        int healDelay = 450 + random.Next(0, 101);
                        System.Threading.Thread.Sleep(healDelay);

                        // Обновляем данные персонажа
                        playerData = gameController.ReadPlayerData();
                        if (!playerData.IsValid)
                        {
                            break;
                        }

                        // Пересчитываем процент
                        hpPercent = playerMaxHP > 0 ? (double)playerData.HP / playerMaxHP * 100.0 : 0;

                        attempts++;
                    }

                    if (hpPercent >= targetPercent)
                    {
                        AddLog($"Автохил: HP восстановлено до {playerData.HP} ({hpPercent:F1}%)");
                    }
                    else if (attempts >= maxHealAttempts)
                    {
                        AddLog($"Автохил: достигнут лимит попыток ({maxHealAttempts}), текущий HP: {playerData.HP} ({hpPercent:F1}%)");
                    }
                }
            }
            catch (Exception ex)
            {
                AddLog($"ОШИБКА при автохиле: {ex.Message}");
            }
        }


        private void BtnStart_Click(object sender, EventArgs e)
        {
            // Парсим ID мобов из текстового поля
            targetMobIds.Clear();
            string idsText = txtMobIds.Text.Trim();
            if (!string.IsNullOrEmpty(idsText))
            {
                string[] ids = idsText.Split(new char[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string idStr in ids)
                {
                    if (int.TryParse(idStr.Trim(), out int id))
                    {
                        targetMobIds.Add(id);
                    }
                }
            }

            killedMobCount = 0;
            isAttacking = true;
            currentTarget = null;
            enableLootCollection = chkLootCollection.Checked;
            enableAutoHeal = chkAutoHeal.Checked;
            enableBuff = chkBuff.Checked;

            btnStart.Enabled = false;
            btnStop.Enabled = true;
            chkLootCollection.Enabled = false;
            chkAutoHeal.Enabled = false;
            chkBuff.Enabled = false;
            chkLootHighlight.Enabled = false;
            txtMobIds.Enabled = false;

            if (targetMobIds.Count > 0)
            {
                AddLog($"Старт атаки. Целевые ID: {string.Join(", ", targetMobIds)}");
            }
            else
            {
                AddLog("Старт атаки. Атакуем всех мобов (ID не указаны)");
            }

            // Сканируем адреса мобов только 1 раз при старте (список статичный)
            if (!addressesScanned || gameController == null)
            {
                UpdateStatus("Сканирование памяти...", Color.Yellow);
                Application.DoEvents(); // Обновляем UI

                // Запускаем сканирование в отдельном потоке, чтобы не блокировать UI
                System.Threading.Tasks.Task.Run(() =>
                {
                    if (gameController == null) return;
                    
                    gameController.ScanMobs();
                    
                    this.Invoke((MethodInvoker)delegate
                    {
                        addressesScanned = true;
                        
                        // Проверяем, что адреса найдены (через чтение списка мобов)
                        var testMobs = gameController.ReadMobList(targetMobIds, AddLog);
                        
                        if (testMobs.Count > 0 || addressesScanned)
                        {
                            UpdateStatus($"Атака начата!", Color.Lime);
                            
                            // Запускаем таймер атаки с случайным интервалом
                            if (attackTimer == null)
                            {
                                attackTimer = new Timer();
                                attackTimer.Tick += AttackTimer_Tick;
                            }
                            // Случайный интервал: 450-550мс (±10%)
                            attackTimer.Interval = 450 + random.Next(0, 101);
                            attackTimer.Start();
                            
                            // Запускаем баф, если включен
                            StartBuffTimerIfEnabled();
                        }
                        else
                        {
                            UpdateStatus("Мобы не найдены. Проверьте игру.", Color.Red);
                            AddLog("ОШИБКА: Мобы не найдены при сканировании!");
                            btnStart.Enabled = true;
                            btnStop.Enabled = false;
                            chkLootCollection.Enabled = true;
                            chkAutoHeal.Enabled = true;
                            chkBuff.Enabled = true;
                            txtMobIds.Enabled = true;
                            isAttacking = false;
                        }
                    });
                });
            }
            else
            {
                // Адреса уже отсканированы, сразу запускаем атаку
                UpdateStatus($"Атака начата!", Color.Lime);
                AddLog($"Используются кэшированные адреса");
                
                if (attackTimer == null)
                {
                    attackTimer = new Timer();
                    attackTimer.Tick += AttackTimer_Tick;
                }
                // Случайный интервал: 450-550мс (±10%)
                attackTimer.Interval = 450 + random.Next(0, 101);
                attackTimer.Start();
                
                // Запускаем баф, если включен
                StartBuffTimerIfEnabled();
            }
        }

        private void BtnStop_Click(object sender, EventArgs e)
        {
            StopAttack();
        }

        private void StopAttack()
        {
            isAttacking = false;
            currentTarget = null;

            if (attackTimer != null)
            {
                attackTimer.Stop();
            }

            // Останавливаем баф при остановке атаки
            if (buffTimer != null)
            {
                buffTimer.Stop();
            }

            btnStart.Enabled = true;
            btnStop.Enabled = false;
            chkLootCollection.Enabled = true;
            chkAutoHeal.Enabled = true;
            chkBuff.Enabled = true;
            chkLootHighlight.Enabled = true;
            txtMobIds.Enabled = true;

            // Сбрасываем цель (но не очищаем кэш адресов - они статичные)
            gameController?.ClearTarget();

            UpdateStatus($"Атака остановлена. Убито мобов: {killedMobCount}", Color.Orange);
            lblKilledCount.Text = $"Убито: {killedMobCount}";
            AddLog($"Атака остановлена. Всего убито: {killedMobCount}");
        }

        private void AttackTimer_Tick(object sender, EventArgs e)
        {
            if (!isAttacking || gameProcess == null || gameProcess.HasExited)
            {
                StopAttack();
                return;
            }

            if (gameController == null || !gameController.IsValid)
            {
                StopAttack();
                return;
            }

            // Периодические паузы (имитация отвлечения игрока)
            // Каждые 10-15 минут делаем паузу на 10-30 секунд
            if (!isPausing && (DateTime.Now - lastPauseTime).TotalMinutes >= 10 + random.Next(0, 6))
            {
                isPausing = true;
                int pauseDuration = 10000 + random.Next(0, 20001); // 10-30 секунд
                AddLog($"Пауза (имитация отвлечения): {pauseDuration / 1000} сек");
                System.Threading.Thread.Sleep(pauseDuration);
                lastPauseTime = DateTime.Now;
                isPausing = false;
                AddLog("Пауза завершена, продолжаем");
            }

            // Обновляем интервал таймера с случайной вариацией для следующей итерации
            if (attackTimer != null)
            {
                attackTimer.Interval = 450 + random.Next(0, 101); // 450-550мс
            }

            // Если идет сбор лута, не начинаем новую атаку
            if (isLooting)
            {
                // Проверяем, прошло ли достаточно времени для сбора лута (2 нажатия * 0.7 сек = 1.4 секунды + буфер)
                if ((DateTime.Now - lootStartTime).TotalSeconds >= 3.5) // Wait 3.5s
                {
                    isLooting = false;
                    AddLog("Сбор лута завершен, продолжаем атаку");
                }
                else
                {
                    return; // Ждем завершения сбора лута
                }
            }

            // Проверяем HP и лечим в процессе атаки, если включено
            if (enableAutoHeal)
            {
                AutoHeal();
            }

            // Сначала проверяем, есть ли мобы с агро (приоритет выше текущей цели)
            PlayerData player = gameController.ReadPlayerData();
            if (player.IsValid && player.PlayerId > 0)
            {
                var mobsWithAgro = gameController.FindMobsWithAgro(player, targetMobIds);
                if (mobsWithAgro.Count > 0 && (!currentTarget.HasValue || mobsWithAgro[0].UniqueID != currentTarget.Value.UniqueID))
                {
                    // Есть мобы с агро и текущая цель не среди них - переключаемся на ближайшего с агро
                    var nearestAgroMob = mobsWithAgro[0];
                    currentTarget = nearestAgroMob;
                    var mobAddr = gameController.FindMobAddressByUniqueId(nearestAgroMob.UniqueID);
                    gameController.AttackMob(nearestAgroMob);
                    
                    lblMobHP.Text = $"Моб HP: {nearestAgroMob.HP} [АГРО]";
                    lblMobHP.ForeColor = Color.Red;
                    
                    string mobName = gameController.GetMobName(nearestAgroMob.ID);
                    AddLog($"Переключение на моба с агро: {mobName} (ID: {nearestAgroMob.ID}, HP: {nearestAgroMob.HP})");
                    return;
                }
            }

            // Проверяем, есть ли текущая цель и жива ли она
            if (currentTarget.HasValue)
            {
                // Находим адрес текущей цели
                var targetAddr = gameController.FindMobAddressByUniqueId(currentTarget.Value.UniqueID);

                if (targetAddr.HasValue && targetAddr.Value != IntPtr.Zero)
                {
                    // Читаем данные текущей цели напрямую (без полного сканирования)
                    var targetMob = gameController.ReadMobByAddress(targetAddr.Value);

                    // Проверка: моб считается мертвым если:
                    // 1. Данные невалидны
                    // 2. HP <= 0 (основная проверка)
                    // 3. HP очень низкое (1-15) И координаты (0,0,0) - это остаточные данные от мертвого моба
                    bool isMobDead = !targetMob.HasValue 
                        || targetMob.Value.HP <= 0
                        || (targetMob.Value.HP > 0 && targetMob.Value.HP <= MAX_RESIDUAL_HP 
                            && targetMob.Value.X == 0f && targetMob.Value.Y == 0f && targetMob.Value.Z == 0f);

                    if (!isMobDead)
                    {
                        // Моб жив - но сначала проверяем, нет ли мобов с агро (приоритет выше)
                            if (player.IsValid && player.PlayerId > 0)
                            {
                                var mobsWithAgro = gameController.FindMobsWithAgro(player, targetMobIds);
                                // Если есть мобы с агро и текущая цель не является мобом с агро, переключаемся
                                if (mobsWithAgro.Count > 0 && targetMob.Value.TargetId != player.PlayerId)
                                {
                                    var nearestAgroMob = mobsWithAgro[0];
                                    currentTarget = nearestAgroMob;
                                    gameController.AttackMob(nearestAgroMob);
                                    
                                    lblMobHP.Text = $"Моб HP: {nearestAgroMob.HP} [АГРО]";
                                    lblMobHP.ForeColor = Color.Red;
                                    lblMobTargetId.Text = $"Target ID: {nearestAgroMob.TargetId}";
                                    
                                    string mobName = gameController.GetMobName(nearestAgroMob.ID);
                                    AddLog($"Переключение на моба с агро: {mobName} (ID: {nearestAgroMob.ID}, HP: {nearestAgroMob.HP})");
                                    return;
                                }
                            }
                            
                            // Если текущая цель - моб с агро, или мобов с агро нет, продолжаем атаковать текущую цель
                            bool isCurrentTargetAgro = (player.IsValid && player.PlayerId > 0 && targetMob.Value.TargetId == player.PlayerId);
                            lblMobHP.Text = $"Моб HP: {targetMob.Value.HP}" + (isCurrentTargetAgro ? " [АГРО]" : "");
                            lblMobHP.ForeColor = isCurrentTargetAgro ? Color.Red : Color.Orange;
                            lblMobTargetId.Text = $"Target ID: {targetMob.Value.TargetId}";
                            return; // Продолжаем атаковать текущую цель
                    }
                    else
                    {
                        // Моб мертв: HP = 0 или остаточные данные (низкое HP + нулевые координаты)
                        int lastHP = targetMob.HasValue ? targetMob.Value.HP : 0;
                        int lastTargetId = targetMob.HasValue ? targetMob.Value.TargetId : 0;
                        lblMobHP.Text = $"Моб HP: {lastHP}";
                        lblMobTargetId.Text = $"Target ID: {lastTargetId}";
                        
                        int killedMobId = currentTarget.Value.ID;
                        string mobName = gameController.GetMobName(killedMobId);
                        
                        killedMobCount++;
                        currentTarget = null;
                        gameController.ClearTarget();

                        AddLog($"Моб убит: {mobName} (ID: {killedMobId}, HP: {lastHP})");

                        lblKilledCount.Text = $"Убито: {killedMobCount}";
                        
                        // Отправляем клавишу E для сбора лута, если включено (синхронно, до поиска новой цели)
                        if (enableLootCollection)
                        {
                            // Задержка перед сбором лута (имитация реакции игрока на убийство моба)
                            int lootDelay = 100 + random.Next(0, 201); // 100-300мс
                            System.Threading.Thread.Sleep(lootDelay);
                            
                            isLooting = true;
                            lootStartTime = DateTime.Now;
                            AddLog("Начинаем сбор лута...");
                            // Выполняем сбор лута синхронно, чтобы не начинать атаку следующего моба
                            KeyboardHelper.SendKeyE(gameProcess, 5, AddLog);
                            // Ждем завершения сбора лута с небольшой дополнительной паузой
                            System.Threading.Thread.Sleep(800 + random.Next(0, 401)); // 800-1200мс
                            isLooting = false;
                            AddLog("Сбор лута завершен");
                        }
                        
                        // Добавляем случайную задержку перед поиском нового моба (уменьшена для реалистичности)
                        int delayMs = 100 + random.Next(0, 151); // 100-250 мс
                        System.Threading.Thread.Sleep(delayMs);
                        
                        // После сбора лута и задержки - выходим, следующая итерация найдет новую цель
                        return;
                    }
                }
                else
                {
                    // Адрес не найден - проверяем HP из currentTarget
                    // Если HP > 0, продолжаем искать этот моб, иначе считаем убитым
                    if (currentTarget.HasValue && currentTarget.Value.HP > 0)
                    {
                        // Моб еще может быть жив, но адрес временно не найден - продолжаем атаковать
                        return;
                    }
                    
                    // Моб убит (HP был 0)
                    int killedMobId = currentTarget.Value.ID;
                    string mobName = gameController.GetMobName(killedMobId);
                    
                    killedMobCount++;
                    currentTarget = null;
                    gameController.ClearTarget();

                    AddLog($"Моб убит (исчез из памяти): {mobName} (ID: {killedMobId})");
                    
                    lblMobHP.Text = "Моб HP: ---";
                    lblMobTargetId.Text = "Target ID: ---";
                    lblKilledCount.Text = $"Убито: {killedMobCount}";
                    
                    // Отправляем клавишу E для сбора лута, если включено (синхронно, до поиска новой цели)
                    if (enableLootCollection)
                    {
                        // Задержка перед сбором лута (имитация реакции игрока на убийство моба)
                        int lootDelay = 100 + random.Next(0, 201); // 100-300мс
                        System.Threading.Thread.Sleep(lootDelay);
                        
                        isLooting = true;
                        lootStartTime = DateTime.Now;
                        AddLog("Начинаем сбор лута...");
                        // Выполняем сбор лута синхронно, чтобы не начинать атаку следующего моба
                        KeyboardHelper.SendKeyE(gameProcess, 2, AddLog);
                        // Ждем завершения сбора лута с небольшой дополнительной паузой
                        System.Threading.Thread.Sleep(1500 + random.Next(0, 501)); // 1500-2000мс
                        isLooting = false;
                        AddLog("Сбор лута завершен");
                    }
                    
                    // Добавляем случайную задержку 0.1-0.5 сек перед поиском нового моба
                    int delayMs = random.Next(100, 501); // 100-500 мс
                    System.Threading.Thread.Sleep(delayMs);
                    
                    // После сбора лута и задержки - выходим, следующая итерация найдет новую цель
                    return;
                }
            }

            // Ищем новую цель (player уже прочитан выше для проверки агро)
            // Если player невалидный, перечитываем
            if (!player.IsValid)
            {
                player = gameController.ReadPlayerData();
                if (!player.IsValid)
                {
                    return;
                }
            }

            // Случайные "ошибки": иногда (1-2% случаев) пропускаем моба
            if (random.Next(100) < 2)
            {
                AddLog("Пропущен моб (имитация ошибки игрока)");
                System.Threading.Thread.Sleep(200 + random.Next(0, 301)); // Небольшая пауза
                return;
            }

            var nearestMob = gameController.FindNearestMob(player, targetMobIds, random);
            // Проверка: моб должен быть валидным и иметь HP > 0, и не быть остаточными данными
            bool isValidMob = nearestMob.HasValue 
                && nearestMob.Value.HP > 0
                && !(nearestMob.Value.HP <= MAX_RESIDUAL_HP 
                    && nearestMob.Value.X == 0f && nearestMob.Value.Y == 0f && nearestMob.Value.Z == 0f);
            if (isValidMob)
            {
                // Находим адрес моба по UniqueID
                var mobAddr = gameController.FindMobAddressByUniqueId(nearestMob.Value.UniqueID);
                
                // Добавляем случайную задержку перед началом атаки (имитация реакции игрока)
                // 100-250мс (было 100-500мс, уменьшено для реалистичности)
                int delayMs = 100 + random.Next(0, 151);
                System.Threading.Thread.Sleep(delayMs);
                
                // Атакуем нового моба
                currentTarget = nearestMob;
                gameController.AttackMob(nearestMob.Value);
                
                // Обновляем отображение HP моба
                lblMobHP.Text = $"Моб HP: {nearestMob.Value.HP}";
                lblMobHP.ForeColor = Color.Orange;
                lblMobTargetId.Text = $"Target ID: {nearestMob.Value.TargetId}";
                
                string mobName = gameController.GetMobName(nearestMob.Value.ID);
                string addrInfo = mobAddr.HasValue ? mobAddr.Value.ToInt64().ToString("X") : "N/A";
                string agroInfo = (player.PlayerId > 0 && nearestMob.Value.TargetId == player.PlayerId) ? " [АГРО!]" : "";
                AddLog($"Атака: {mobName} (ID: {nearestMob.Value.ID}, HP: {nearestMob.Value.HP}, UniqueID: {nearestMob.Value.UniqueID}{agroInfo}, Адрес: {addrInfo})");
            }
            else
            {
                // Цель не найдена - логируем только периодически, чтобы не спамить
                if (DateTime.Now.Second % 5 == 0) // Раз в 5 секунд
                {
                    AddLog("Цель не найдена. Ожидание...");
                }
            }
        }


        private void ChkBuff_CheckedChanged(object sender, EventArgs e)
        {
            enableBuff = chkBuff != null && chkBuff.Checked;
            
            // Баф будет запущен только при старте автоатаки, здесь только устанавливаем флаг
            if (enableBuff)
            {
                AddLog("Баф будет включен при старте автоатаки");
            }
            else
            {
                // Останавливаем таймер, если он запущен
                if (buffTimer != null)
                {
                    buffTimer.Stop();
                }
                AddLog("Баф выключен");
            }
        }

        private void BuffTimer_Tick(object sender, EventArgs e)
        {
            // Баф работает только во время автоатаки
            if (!enableBuff || !isAttacking || gameProcess == null || gameProcess.HasExited)
            {
                if (buffTimer != null)
                {
                    buffTimer.Stop();
                }
                return;
            }

            KeyboardHelper.SendKey3(gameProcess, AddLog);
            
            // Обновляем интервал таймера с случайной вариацией для следующего бафа
            // Интервал: 55-65 секунд (±5 секунд от 60 секунд)
            if (buffTimer != null && random != null)
            {
                buffTimer.Interval = 55000 + random.Next(0, 10001); // 55-65 секунд (±5 сек)
            }
        }

        private void StartBuffTimerIfEnabled()
        {
            if (enableBuff && isAttacking)
            {
                // Запускаем таймер для бафа с вариацией интервала
                if (buffTimer == null)
                {
                    buffTimer = new Timer();
                    buffTimer.Tick += BuffTimer_Tick;
                }
                // Случайный интервал: 55-65 секунд (±5 секунд от 60 секунд)
                buffTimer.Interval = 55000 + random.Next(0, 10001);
                buffTimer.Start();
                AddLog("Баф включен (клавиша 3 каждые 55-65 секунд)");
            }
        }

        private void ChkLootHighlight_CheckedChanged(object sender, EventArgs e)
        {
            enableLootHighlight = chkLootHighlight != null && chkLootHighlight.Checked;
            
            if (gameProcess == null || gameProcess.HasExited)
            {
                return;
            }

            try
            {
                IntPtr hWnd = gameProcess.MainWindowHandle;
                if (hWnd == IntPtr.Zero)
                {
                    // Пробуем найти процесс по имени
                    string processName = gameProcess.ProcessName;
                    Process[] processes = Process.GetProcessesByName(processName);
                    if (processes.Length > 0)
                    {
                        hWnd = processes[0].MainWindowHandle;
                    }
                }

                if (hWnd != IntPtr.Zero)
                {
                    if (enableLootHighlight)
                    {
                        // Зажимаем Shift+E (держим зажатым)
                        KeyboardHelper.PressShiftE(hWnd);
                        AddLog($"Подсветка лута включена (Shift+E зажато, hWnd: {hWnd.ToInt64():X})");
                    }
                    else
                    {
                        // Отпускаем Shift+E
                        KeyboardHelper.ReleaseShiftE(hWnd);
                        AddLog("Подсветка лута выключена");
                    }
                }
                else
                {
                    AddLog("ОШИБКА: Не удалось найти окно игры для подсветки лута");
                }
            }
            catch (Exception ex)
            {
                AddLog($"Ошибка при изменении подсветки лута: {ex.Message}");
            }
        }

        private void BtnCopyLogs_Click(object sender, EventArgs e)
        {
            try
            {
                if (listBoxLogs == null || listBoxLogs.Items.Count == 0)
                {
                    return;
                }

                // Если выбраны элементы, копируем только их, иначе все
                if (listBoxLogs.SelectedItems.Count > 0)
                {
                    var selectedText = new System.Text.StringBuilder();
                    foreach (var item in listBoxLogs.SelectedItems)
                    {
                        selectedText.AppendLine(item.ToString());
                    }
                    Clipboard.SetText(selectedText.ToString());
                    AddLog($"Скопировано {listBoxLogs.SelectedItems.Count} строк в буфер обмена");
                }
                else
                {
                    // Копируем все логи
                    var allText = new System.Text.StringBuilder();
                    for (int i = listBoxLogs.Items.Count - 1; i >= 0; i--)
                    {
                        allText.AppendLine(listBoxLogs.Items[i].ToString());
                    }
                    Clipboard.SetText(allText.ToString());
                    AddLog($"Скопировано {listBoxLogs.Items.Count} строк в буфер обмена");
                }
            }
            catch (Exception ex)
            {
                AddLog($"Ошибка при копировании логов: {ex.Message}");
            }
        }

        private void BtnClearLogs_Click(object sender, EventArgs e)
        {
            if (listBoxLogs != null)
            {
                listBoxLogs.Items.Clear();
                AddLog("Логи очищены");
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (isAttacking)
            {
                StopAttack();
            }

            if (attackTimer != null)
            {
                attackTimer.Stop();
                attackTimer.Dispose();
            }

            if (buffTimer != null)
            {
                buffTimer.Stop();
                buffTimer.Dispose();
            }

            // Отпускаем Shift+E при закрытии формы, если было включено
            if (enableLootHighlight && gameProcess != null && !gameProcess.HasExited)
            {
                try
                {
                    IntPtr hWnd = gameProcess.MainWindowHandle;
                    if (hWnd != IntPtr.Zero)
                    {
                        KeyboardHelper.ReleaseShiftE(hWnd);
                    }
                }
                catch
                {
                    // Игнорируем ошибки
                }
            }

            gameController?.Close();
            base.OnFormClosed(e);
        }
    }

}
