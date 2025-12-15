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
        private bool isLooting = false; // Флаг процесса сбора лута
        private DateTime lootStartTime = DateTime.MinValue; // Время начала сбора лута
        private Random random = new Random(); // Генератор случайных чисел для задержек
        
        // Кэш для списка мобов (адреса статичные, сканируем только 1 раз при старте)
        private bool addressesScanned = false;
        private bool connectionLogged = false;

        // Labels для персонажа
        private Label lblPlayerHP;
        private Label lblPlayerMP;
        
        // Label для моба
        private Label lblMobHP;

        // Label для статуса
        private Label lblStatus;

        // UI элементы для атаки
        private ComboBox cmbServer;
        private TextBox txtMobIds;
        private CheckBox chkLootCollection;
        private CheckBox chkAutoHeal;
        private CheckBox chkBuff;
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
            //название окна не менять
            this.Text = "Stats";
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
                Text = "Автохил (1 при HP < 80, до HP > 120)",
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
            lblMobHP.Text = "Моб HP: ---";
        }

        private void UpdatePlayerData(PlayerData data)
        {
            if (data.IsValid)
            {
                lblPlayerHP.Text = $"HP: {data.HP}";
                lblPlayerHP.ForeColor = Color.Red;
                lblPlayerMP.Text = $"MP: {data.MP}";
                lblPlayerMP.ForeColor = Color.Blue;
            }
            else
            {
                lblPlayerHP.Text = "HP: ---";
                lblPlayerMP.Text = "MP: ---";
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

                // Если HP меньше 80, лечим до тех пор, пока не станет больше 120
                if (playerData.HP < 80)
                {
                    if (gameProcess == null || gameProcess.HasExited)
                    {
                        return;
                    }

                    int maxHealAttempts = 20; // Максимум попыток лечения
                    int attempts = 0;

                    while (playerData.HP <= 120 && attempts < maxHealAttempts)
                    {
                        KeyboardHelper.SendKey1(gameProcess);
                        System.Threading.Thread.Sleep(500); // Пауза 0.5 сек между нажатиями

                        // Обновляем данные персонажа
                        playerData = gameController.ReadPlayerData();
                        if (!playerData.IsValid)
                        {
                            break;
                        }

                        attempts++;
                    }

                    if (playerData.HP > 120)
                    {
                        AddLog($"Автохил: HP восстановлено до {playerData.HP}");
                    }
                    else if (attempts >= maxHealAttempts)
                    {
                        AddLog($"Автохил: достигнут лимит попыток ({maxHealAttempts}), текущий HP: {playerData.HP}");
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

            btnStart.Enabled = false;
            btnStop.Enabled = true;
            chkLootCollection.Enabled = false;
            chkAutoHeal.Enabled = false;
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
                            
                            // Запускаем таймер атаки
                            if (attackTimer == null)
                            {
                                attackTimer = new Timer
                                {
                                    Interval = 500 // Проверяем каждые 500мс
                                };
                                attackTimer.Tick += AttackTimer_Tick;
                            }
                            attackTimer.Start();
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
                    attackTimer = new Timer
                    {
                        Interval = 500
                    };
                    attackTimer.Tick += AttackTimer_Tick;
                }
                attackTimer.Start();
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

            btnStart.Enabled = true;
            btnStop.Enabled = false;
            chkLootCollection.Enabled = true;
            chkAutoHeal.Enabled = true;
            chkBuff.Enabled = true;
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

            // Проверяем, есть ли текущая цель и жива ли она
            if (currentTarget.HasValue)
            {
                // Находим адрес текущей цели
                var targetAddr = gameController.FindMobAddressByUniqueId(currentTarget.Value.UniqueID);

                if (targetAddr.HasValue && targetAddr.Value != IntPtr.Zero)
                {
                    // Читаем данные текущей цели напрямую (без полного сканирования)
                    var targetMob = gameController.ReadMobByAddress(targetAddr.Value);

                    // Обновляем отображение HP моба и проверяем, жив ли моб
                    if (targetMob.HasValue && targetMob.Value.HP > 0)
                    {
                        // Моб жив - обновляем отображение и продолжаем атаковать
                        lblMobHP.Text = $"Моб HP: {targetMob.Value.HP}";
                        lblMobHP.ForeColor = Color.Orange;
                        return; // Продолжаем атаковать текущую цель
                    }
                    else
                    {
                        // HP = 0 или моб не найден - моб убит
                        lblMobHP.Text = "Моб HP: 0";
                        
                        int killedMobId = currentTarget.Value.ID;
                        string mobName = gameController.GetMobName(killedMobId);
                        
                        killedMobCount++;
                        currentTarget = null;
                        gameController.ClearTarget();

                        AddLog($"Моб убит: {mobName} (ID: {killedMobId})");

                        lblKilledCount.Text = $"Убито: {killedMobCount}";
                        
                        // Отправляем клавишу E для сбора лута, если включено (синхронно, до поиска новой цели)
                        if (enableLootCollection)
                        {
                            isLooting = true;
                            lootStartTime = DateTime.Now;
                            AddLog("Начинаем сбор лута...");
                            // Выполняем сбор лута синхронно, чтобы не начинать атаку следующего моба
                            KeyboardHelper.SendKeyE(gameProcess, 4, AddLog);
                            // Ждем завершения сбора лута (2 нажатия * 0.7 сек + буфер)
                            System.Threading.Thread.Sleep(1000); // Дополнительная пауза для завершения
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
                    lblKilledCount.Text = $"Убито: {killedMobCount}";
                    
                    // Отправляем клавишу E для сбора лута, если включено (синхронно, до поиска новой цели)
                    if (enableLootCollection)
                    {
                        isLooting = true;
                        lootStartTime = DateTime.Now;
                        AddLog("Начинаем сбор лута...");
                        // Выполняем сбор лута синхронно, чтобы не начинать атаку следующего моба
                        KeyboardHelper.SendKeyE(gameProcess, 2, AddLog);
                        // Ждем завершения сбора лута (2 нажатия * 0.7 сек + буфер)
                        System.Threading.Thread.Sleep(2000); // Дополнительная пауза для завершения
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

            // Ищем новую цель
            var player = gameController.ReadPlayerData();
            if (!player.IsValid)
            {
                return;
            }

            var nearestMob = gameController.FindNearestMob(player, targetMobIds);
            if (nearestMob.HasValue && nearestMob.Value.HP > 0)
            {
                // Находим адрес моба по UniqueID
                var mobAddr = gameController.FindMobAddressByUniqueId(nearestMob.Value.UniqueID);
                
                // Добавляем случайную задержку 0.1-0.5 сек перед началом атаки нового моба
                int delayMs = random.Next(100, 501); // 100-500 мс
                System.Threading.Thread.Sleep(delayMs);
                
                // Атакуем нового моба
                currentTarget = nearestMob;
                gameController.AttackMob(nearestMob.Value);
                
                // Обновляем отображение HP моба
                lblMobHP.Text = $"Моб HP: {nearestMob.Value.HP}";
                lblMobHP.ForeColor = Color.Orange;
                
                string mobName = gameController.GetMobName(nearestMob.Value.ID);
                string addrInfo = mobAddr.HasValue ? mobAddr.Value.ToInt64().ToString("X") : "N/A";
                AddLog($"Атака: {mobName} (ID: {nearestMob.Value.ID}, HP: {nearestMob.Value.HP}, UniqueID: {nearestMob.Value.UniqueID}, Адрес: {addrInfo})");
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
            
            if (enableBuff)
            {
                // Запускаем таймер для бафа (раз в минуту = 60000 мс)
                if (buffTimer == null)
                {
                    buffTimer = new Timer
                    {
                        Interval = 60000 // 1 минута
                    };
                    buffTimer.Tick += BuffTimer_Tick;
                }
                buffTimer.Start();
                AddLog("Баф включен (клавиша 3 раз в минуту)");
            }
            else
            {
                // Останавливаем таймер
                if (buffTimer != null)
                {
                    buffTimer.Stop();
                }
                AddLog("Баф выключен");
            }
        }

        private void BuffTimer_Tick(object sender, EventArgs e)
        {
            if (!enableBuff || gameProcess == null || gameProcess.HasExited)
            {
                if (buffTimer != null)
                {
                    buffTimer.Stop();
                }
                return;
            }

            KeyboardHelper.SendKey3(gameProcess, AddLog);
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

            gameController?.Close();
            base.OnFormClosed(e);
        }
    }

}
