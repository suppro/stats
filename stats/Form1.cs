using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace stats
{
    public partial class Form1 : Form
    {
        // WinAPI функции
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int nSize, out int lpNumberOfBytesWritten);

        [DllImport("kernel32.dll")]
        static extern bool CloseHandle(IntPtr hObject);

        const uint PROCESS_VM_READ = 0x0010;
        const uint PROCESS_VM_WRITE = 0x0020;
        const uint PROCESS_QUERY_INFORMATION = 0x0400;

        // Базовые адреса
        private static readonly int PLAYER_BASE_OFFSET = 0x01592584;
        private static readonly int MOB_BASE_OFFSET = 0x01592550;

        // Оффсеты персонажа (прямые адреса)
        private static readonly int PLAYER_HP_OFFSET = 0xFC0;
        private static readonly int PLAYER_MP_OFFSET = 0xFC4;
        private static readonly int PLAYER_X_OFFSET = 0x2B70;
        private static readonly int PLAYER_Y_OFFSET = 0x2B74;
        private static readonly int PLAYER_Z_OFFSET = 0x2B78;
        private static readonly int PLAYER_TARGET_OFFSET = 0x101C;
        private static readonly int PLAYER_AUTO_ATTACK_SET1_OFFSET = 0x3094;
        private static readonly int PLAYER_AUTO_ATTACK_SET2_OFFSET = 0x3098;

        // Оффсеты для списка мобов
        private static readonly int MOB_LIST_OFFSET = -0x100000;
        private static readonly int MAX_MOB_COUNT = 1000000;

        // Оффсеты внутри структуры моба
        private static readonly int MOB_ID_OFFSET = 0x0C;
        private static readonly int MOB_HP_OFFSET = 0x40;
        private static readonly int MOB_X_OFFSET = 0x1BF0;
        private static readonly int MOB_Y_OFFSET = 0x1BF4;
        private static readonly int MOB_Z_OFFSET = 0x1BF8;
        private static readonly int MOB_UNIQUE_ID_OFFSET = 0x98;

        private Timer updateTimer;
        private Timer attackTimer;
        private Process gameProcess;
        private IntPtr hProcess;

        // Словарь для имен мобов (ID -> Name)
        private Dictionary<int, string> mobNames = new Dictionary<int, string>();

        // Состояние атаки
        private bool isAttacking = false;
        private int targetMobCount = 0;
        private int killedMobCount = 0;
        private MobData? currentTarget = null;

        // Labels для персонажа
        private Label lblPlayerHP;
        private Label lblPlayerMP;
        private Label lblPlayerX;
        private Label lblPlayerY;
        private Label lblPlayerZ;

        // Labels для моба
        private Label lblMobID;
        private Label lblMobHP;
        private Label lblMobX;
        private Label lblMobY;
        private Label lblMobZ;

        // Label для расстояния
        private Label lblDistance;

        // Label для статуса
        private Label lblStatus;

        // UI элементы для атаки
        private TextBox txtMobCount;
        private Button btnStart;
        private Button btnStop;
        private Label lblTargetName;
        private Label lblKilledCount;

        public Form1()
        {
            InitializeComponent();
            LoadMobNames();
            SetupForm();
            StartUpdateTimer();
        }

        private void SetupForm()
        {
            this.Text = "Stats";
            this.Size = new Size(500, 600);
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

            // Координаты персонажа
            lblPlayerX = new Label
            {
                Text = "X: ---",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11F),
                AutoSize = true,
                Location = new Point(30, yPos)
            };
            this.Controls.Add(lblPlayerX);
            yPos += 22;

            lblPlayerY = new Label
            {
                Text = "Y: ---",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11F),
                AutoSize = true,
                Location = new Point(30, yPos)
            };
            this.Controls.Add(lblPlayerY);
            yPos += 22;

            lblPlayerZ = new Label
            {
                Text = "Z: ---",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11F),
                AutoSize = true,
                Location = new Point(30, yPos)
            };
            this.Controls.Add(lblPlayerZ);
            yPos += 35;

            // Заголовок моба
            Label lblMobTitle = new Label
            {
                Text = "=== БЛИЖАЙШИЙ МОБ ===",
                ForeColor = Color.Orange,
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(20, yPos)
            };
            this.Controls.Add(lblMobTitle);
            yPos += 35;

            // ID моба
            lblMobID = new Label
            {
                Text = "ID: ---",
                ForeColor = Color.Yellow,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(30, yPos)
            };
            this.Controls.Add(lblMobID);
            yPos += 25;

            // HP моба
            lblMobHP = new Label
            {
                Text = "HP: ---",
                ForeColor = Color.Red,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(30, yPos)
            };
            this.Controls.Add(lblMobHP);
            yPos += 25;

            // Координаты моба
            lblMobX = new Label
            {
                Text = "X: ---",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11F),
                AutoSize = true,
                Location = new Point(30, yPos)
            };
            this.Controls.Add(lblMobX);
            yPos += 22;

            lblMobY = new Label
            {
                Text = "Y: ---",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11F),
                AutoSize = true,
                Location = new Point(30, yPos)
            };
            this.Controls.Add(lblMobY);
            yPos += 22;

            lblMobZ = new Label
            {
                Text = "Z: ---",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11F),
                AutoSize = true,
                Location = new Point(30, yPos)
            };
            this.Controls.Add(lblMobZ);
            yPos += 35;

            // Расстояние
            lblDistance = new Label
            {
                Text = "Расстояние: ---",
                ForeColor = Color.Lime,
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(20, yPos)
            };
            this.Controls.Add(lblDistance);
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

            // Поле для количества мобов
            Label lblMobCountLabel = new Label
            {
                Text = "Убить мобов:",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11F),
                AutoSize = true,
                Location = new Point(30, yPos)
            };
            this.Controls.Add(lblMobCountLabel);

            txtMobCount = new TextBox
            {
                Text = "10",
                Size = new Size(80, 25),
                Location = new Point(130, yPos - 2),
                BackColor = Color.DarkGray,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F)
            };
            this.Controls.Add(txtMobCount);
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

            // Имя текущей цели
            Label lblTargetLabel = new Label
            {
                Text = "Цель:",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11F),
                AutoSize = true,
                Location = new Point(30, yPos)
            };
            this.Controls.Add(lblTargetLabel);

            lblTargetName = new Label
            {
                Text = "---",
                ForeColor = Color.Yellow,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(80, yPos)
            };
            this.Controls.Add(lblTargetName);
            yPos += 30;

            // Счетчик убитых
            lblKilledCount = new Label
            {
                Text = "Убито: 0 / 0",
                ForeColor = Color.Lime,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(30, yPos)
            };
            this.Controls.Add(lblKilledCount);
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
                Process[] processes = Process.GetProcessesByName("R2Client");
                if (processes.Length == 0)
                {
                    UpdateStatus("Игра не найдена", Color.Red);
                    ClearAllData();
                    return;
                }
                gameProcess = processes[0];
                hProcess = OpenProcess(PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_QUERY_INFORMATION, false, gameProcess.Id);
                if (hProcess == IntPtr.Zero)
                {
                    UpdateStatus("Нет доступа (запусти от админа)", Color.Red);
                    ClearAllData();
                    return;
                }
            }

            UpdateStatus($"Подключено: R2Client.exe (PID: {gameProcess.Id})", Color.Lime);

            // Читаем данные персонажа
            var playerData = ReadPlayerData();
            UpdatePlayerData(playerData);

            // Читаем список мобов и находим ближайшего (если не атакуем)
            if (!isAttacking)
            {
                var nearestMob = FindNearestMob(playerData);
                if (nearestMob.HasValue)
                {
                    UpdateMobData(nearestMob.Value);
                    if (playerData.IsValid)
                    {
                        float distance = CalculateDistance(playerData.X, playerData.Y, playerData.Z, 
                            nearestMob.Value.X, nearestMob.Value.Y, nearestMob.Value.Z);
                        lblDistance.Text = $"Расстояние: {distance:F2}";
                        lblDistance.ForeColor = Color.Lime;
                    }
                }
                else
                {
                    ClearMobData();
                    lblDistance.Text = "Расстояние: ---";
                    lblDistance.ForeColor = Color.Gray;
                }
            }
            else
            {
                // Во время атаки обновляем данные текущей цели
                if (currentTarget.HasValue)
                {
                    UpdateMobData(currentTarget.Value);
                    if (playerData.IsValid)
                    {
                        float distance = CalculateDistance(playerData.X, playerData.Y, playerData.Z, 
                            currentTarget.Value.X, currentTarget.Value.Y, currentTarget.Value.Z);
                        lblDistance.Text = $"Расстояние: {distance:F2}";
                        lblDistance.ForeColor = Color.Lime;
                    }
                }
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
            lblPlayerX.Text = "X: ---";
            lblPlayerY.Text = "Y: ---";
            lblPlayerZ.Text = "Z: ---";
            ClearMobData();
            lblDistance.Text = "Расстояние: ---";
        }

        private void ClearMobData()
        {
            lblMobID.Text = "ID: ---";
            lblMobHP.Text = "HP: ---";
            lblMobX.Text = "X: ---";
            lblMobY.Text = "Y: ---";
            lblMobZ.Text = "Z: ---";
        }

        private void UpdatePlayerData(PlayerData data)
        {
            if (data.IsValid)
            {
                lblPlayerHP.Text = $"HP: {data.HP}";
                lblPlayerHP.ForeColor = Color.Red;
                lblPlayerMP.Text = $"MP: {data.MP}";
                lblPlayerMP.ForeColor = Color.Blue;
                lblPlayerX.Text = $"X: {data.X:F2}";
                lblPlayerX.ForeColor = Color.White;
                lblPlayerY.Text = $"Y: {data.Y:F2}";
                lblPlayerY.ForeColor = Color.White;
                lblPlayerZ.Text = $"Z: {data.Z:F2}";
                lblPlayerZ.ForeColor = Color.White;
            }
            else
            {
                lblPlayerHP.Text = "HP: ---";
                lblPlayerMP.Text = "MP: ---";
                lblPlayerX.Text = "X: ---";
                lblPlayerY.Text = "Y: ---";
                lblPlayerZ.Text = "Z: ---";
            }
        }

        private void UpdateMobData(MobData data)
        {
            if (data.IsValid)
            {
                lblMobID.Text = $"ID: {data.ID}";
                lblMobID.ForeColor = Color.Yellow;
                lblMobHP.Text = $"HP: {data.HP}";
                lblMobHP.ForeColor = Color.Red;
                lblMobX.Text = $"X: {data.X:F2}";
                lblMobX.ForeColor = Color.White;
                lblMobY.Text = $"Y: {data.Y:F2}";
                lblMobY.ForeColor = Color.White;
                lblMobZ.Text = $"Z: {data.Z:F2}";
                lblMobZ.ForeColor = Color.White;
            }
            else
            {
                ClearMobData();
            }
        }

        private IntPtr GetModuleBaseAddress(string moduleName)
        {
            if (gameProcess == null) return IntPtr.Zero;

            try
            {
                foreach (ProcessModule module in gameProcess.Modules)
                {
                    if (module.ModuleName.Equals(moduleName, StringComparison.OrdinalIgnoreCase))
                    {
                        return module.BaseAddress;
                    }
                }
            }
            catch
            {
                return IntPtr.Zero;
            }

            return IntPtr.Zero;
        }

        private IntPtr ReadPointer(IntPtr address)
        {
            if (hProcess == IntPtr.Zero) return IntPtr.Zero;

            byte[] buffer = new byte[IntPtr.Size];
            int bytesRead;

            if (ReadProcessMemory(hProcess, address, buffer, IntPtr.Size, out bytesRead) && bytesRead == IntPtr.Size)
            {
                if (IntPtr.Size == 4)
                {
                    return (IntPtr)BitConverter.ToUInt32(buffer, 0);
                }
                else
                {
                    return (IntPtr)BitConverter.ToUInt64(buffer, 0);
                }
            }

            return IntPtr.Zero;
        }

        private int ReadInt32(IntPtr address)
        {
            if (hProcess == IntPtr.Zero) return 0;

            byte[] buffer = new byte[4];
            int bytesRead;

            if (ReadProcessMemory(hProcess, address, buffer, 4, out bytesRead) && bytesRead == 4)
            {
                return BitConverter.ToInt32(buffer, 0);
            }

            return 0;
        }

        private bool WriteInt32(IntPtr address, int value)
        {
            if (hProcess == IntPtr.Zero) return false;

            byte[] buffer = BitConverter.GetBytes(value);
            int bytesWritten;

            return WriteProcessMemory(hProcess, address, buffer, 4, out bytesWritten) && bytesWritten == 4;
        }

        private float ReadFloat(IntPtr address)
        {
            if (hProcess == IntPtr.Zero) return 0f;

            byte[] buffer = new byte[4];
            int bytesRead;

            if (ReadProcessMemory(hProcess, address, buffer, 4, out bytesRead) && bytesRead == 4)
            {
                return BitConverter.ToSingle(buffer, 0);
            }

            return 0f;
        }

        private PlayerData ReadPlayerData()
        {
            PlayerData data = new PlayerData { IsValid = false };

            try
            {
                // Получаем базовый адрес модуля
                IntPtr moduleBase = GetModuleBaseAddress("R2Client.exe");
                if (moduleBase == IntPtr.Zero) return data;

                // Вычисляем адрес указателя: R2Client.exe + 0x01592584
                IntPtr basePtrAddress = IntPtr.Add(moduleBase, PLAYER_BASE_OFFSET);

                // Читаем указатель по этому адресу
                IntPtr playerBase = ReadPointer(basePtrAddress);
                if (playerBase == IntPtr.Zero) return data;

                // Читаем данные персонажа относительно указателя
                // HP и MP - 4 байта (int32), координаты - float
                data.HP = ReadInt32(IntPtr.Add(playerBase, PLAYER_HP_OFFSET));
                data.MP = ReadInt32(IntPtr.Add(playerBase, PLAYER_MP_OFFSET));
                data.X = ReadFloat(IntPtr.Add(playerBase, PLAYER_X_OFFSET));
                data.Y = ReadFloat(IntPtr.Add(playerBase, PLAYER_Y_OFFSET));
                data.Z = ReadFloat(IntPtr.Add(playerBase, PLAYER_Z_OFFSET));

                // Проверяем валидность данных
                if (data.HP > 0 || data.MP > 0 || (data.X != 0f || data.Y != 0f || data.Z != 0f))
                {
                    data.IsValid = true;
                }
            }
            catch
            {
                data.IsValid = false;
            }

            return data;
        }

        private List<MobData> ReadMobList()
        {
            List<MobData> mobs = new List<MobData>();

            try
            {
                // Получаем базовый адрес модуля
                IntPtr moduleBase = GetModuleBaseAddress("R2Client.exe");
                if (moduleBase == IntPtr.Zero) return mobs;

                // Вычисляем базовый адрес для списка мобов: R2Client.exe + 0x01592550
                IntPtr mobBasePtr = IntPtr.Add(moduleBase, MOB_BASE_OFFSET);

                // Читаем указатель по базовому адресу
                IntPtr basePtr = ReadPointer(mobBasePtr);
                if (basePtr == IntPtr.Zero) return mobs;

                // Вычисляем адрес массива указателей на мобов: basePtr + 0xAC
                IntPtr mobListPtr = IntPtr.Add(basePtr, MOB_LIST_OFFSET);

                // Проходим по массиву указателей (каждый указатель 4 байта для 32-bit процесса)
                for (int i = 0; i < MAX_MOB_COUNT; i++)
                {
                    // Читаем указатель на моба из массива
                    IntPtr ptrAddr = IntPtr.Add(mobListPtr, i * 4);
                    IntPtr mobAddr = ReadPointer(ptrAddr);

                    if (mobAddr != IntPtr.Zero)
                    {
                        // Читаем ID моба
                        int id = ReadInt32(IntPtr.Add(mobAddr, MOB_ID_OFFSET));

                        // Проверяем, что ID валидный (больше 0)
                        if (id > 0)
                        {
                            MobData mob = new MobData
                            {
                                IsValid = true,
                                ID = id,
                                UniqueID = ReadInt32(IntPtr.Add(mobAddr, MOB_UNIQUE_ID_OFFSET)),
                                HP = ReadInt32(IntPtr.Add(mobAddr, MOB_HP_OFFSET)),
                                X = ReadFloat(IntPtr.Add(mobAddr, MOB_X_OFFSET)),
                                Y = ReadFloat(IntPtr.Add(mobAddr, MOB_Y_OFFSET)),
                                Z = ReadFloat(IntPtr.Add(mobAddr, MOB_Z_OFFSET))
                            };

                            mobs.Add(mob);
                        }
                    }
                }
            }
            catch
            {
                // В случае ошибки возвращаем пустой список
            }

            return mobs;
        }

        private MobData? FindNearestMob(PlayerData player)
        {
            if (!player.IsValid) return null;

            List<MobData> mobs = ReadMobList();
            if (mobs.Count == 0) return null;

            MobData? nearestMob = null;
            float minDistance = float.MaxValue;

            foreach (var mob in mobs)
            {
                // Пропускаем невалидных и мертвых мобов
                if (!mob.IsValid || mob.HP <= 0) continue;

                float distance = CalculateDistance(player.X, player.Y, player.Z, mob.X, mob.Y, mob.Z);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestMob = mob;
                }
            }

            return nearestMob;
        }

        private float CalculateDistance(float x1, float y1, float z1, float x2, float y2, float z2)
        {
            float dx = x2 - x1;
            float dy = y2 - y1;
            float dz = z2 - z1;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private void LoadMobNames()
        {
            mobNames.Clear();
            try
            {
                string filePath = Path.Combine(Application.StartupPath, "id-name.txt");
                if (!File.Exists(filePath))
                {
                    // Пробуем найти файл в папке со сборкой
                    filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "id-name.txt");
                }

                if (File.Exists(filePath))
                {
                    string[] lines = File.ReadAllLines(filePath);
                    foreach (string line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        // Формат: "85 = Упырь"
                        int equalsIndex = line.IndexOf('=');
                        if (equalsIndex > 0)
                        {
                            string idStr = line.Substring(0, equalsIndex).Trim();
                            string name = line.Substring(equalsIndex + 1).Trim();

                            if (int.TryParse(idStr, out int id))
                            {
                                mobNames[id] = name;
                            }
                        }
                    }
                }
            }
            catch
            {
                // Игнорируем ошибки загрузки
            }
        }

        private string GetMobName(int mobId)
        {
            return mobNames.ContainsKey(mobId) ? mobNames[mobId] : $"ID: {mobId}";
        }

        private void BtnStart_Click(object sender, EventArgs e)
        {
            if (!int.TryParse(txtMobCount.Text, out targetMobCount) || targetMobCount <= 0)
            {
                MessageBox.Show("Введите корректное количество мобов!", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            killedMobCount = 0;
            isAttacking = true;
            currentTarget = null;

            btnStart.Enabled = false;
            btnStop.Enabled = true;
            txtMobCount.Enabled = false;

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

            UpdateStatus($"Атака начата. Цель: убить {targetMobCount} мобов", Color.Lime);
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
            txtMobCount.Enabled = true;

            // Сбрасываем цель
            ClearTarget();

            UpdateStatus($"Атака остановлена. Убито: {killedMobCount} / {targetMobCount}", Color.Orange);
            lblTargetName.Text = "---";
            lblKilledCount.Text = $"Убито: {killedMobCount} / {targetMobCount}";
        }

        private void AttackTimer_Tick(object sender, EventArgs e)
        {
            if (!isAttacking || gameProcess == null || gameProcess.HasExited)
            {
                StopAttack();
                return;
            }

            // Проверяем, есть ли текущая цель и жива ли она
            if (currentTarget.HasValue)
            {
                // Обновляем данные текущей цели
                var playerData = ReadPlayerData();
                var mobs = ReadMobList();
                var targetMob = mobs.FirstOrDefault(m => m.UniqueID == currentTarget.Value.UniqueID);

                if (!targetMob.IsValid || targetMob.HP <= 0)
                {
                    // Моб убит
                    killedMobCount++;
                    currentTarget = null;
                    ClearTarget();

                    lblKilledCount.Text = $"Убито: {killedMobCount} / {targetMobCount}";

                    if (killedMobCount >= targetMobCount)
                    {
                        // Достигли цели
                        StopAttack();
                        UpdateStatus($"Атака завершена! Убито: {killedMobCount} мобов", Color.Lime);
                        return;
                    }
                }
                else
                {
                    // Обновляем имя цели
                    lblTargetName.Text = GetMobName(targetMob.ID);
                    return; // Продолжаем атаковать текущую цель
                }
            }

            // Ищем новую цель
            var player = ReadPlayerData();
            if (!player.IsValid)
            {
                return;
            }

            var nearestMob = FindNearestMob(player);
            if (nearestMob.HasValue && nearestMob.Value.HP > 0)
            {
                // Атакуем нового моба
                currentTarget = nearestMob;
                AttackMob(nearestMob.Value);
                lblTargetName.Text = GetMobName(nearestMob.Value.ID);
            }
            else
            {
                lblTargetName.Text = "Цель не найдена";
            }
        }

        private void AttackMob(MobData mob)
        {
            try
            {
                IntPtr moduleBase = GetModuleBaseAddress("R2Client.exe");
                if (moduleBase == IntPtr.Zero) return;

                // Вычисляем адрес указателя персонажа
                IntPtr basePtrAddress = IntPtr.Add(moduleBase, PLAYER_BASE_OFFSET);
                IntPtr playerBase = ReadPointer(basePtrAddress);
                if (playerBase == IntPtr.Zero) return;

                // Записываем уникальный ID моба в TARGET
                WriteInt32(IntPtr.Add(playerBase, PLAYER_TARGET_OFFSET), mob.UniqueID);

                // Записываем значения для атаки
                WriteInt32(IntPtr.Add(playerBase, PLAYER_AUTO_ATTACK_SET1_OFFSET), 3);
                WriteInt32(IntPtr.Add(playerBase, PLAYER_AUTO_ATTACK_SET2_OFFSET), 65536);
            }
            catch
            {
                // Игнорируем ошибки
            }
        }

        private void ClearTarget()
        {
            try
            {
                IntPtr moduleBase = GetModuleBaseAddress("R2Client.exe");
                if (moduleBase == IntPtr.Zero) return;

                IntPtr basePtrAddress = IntPtr.Add(moduleBase, PLAYER_BASE_OFFSET);
                IntPtr playerBase = ReadPointer(basePtrAddress);
                if (playerBase == IntPtr.Zero) return;

                // Сбрасываем цель
                WriteInt32(IntPtr.Add(playerBase, PLAYER_TARGET_OFFSET), 0);
                WriteInt32(IntPtr.Add(playerBase, PLAYER_AUTO_ATTACK_SET1_OFFSET), 0);
                WriteInt32(IntPtr.Add(playerBase, PLAYER_AUTO_ATTACK_SET2_OFFSET), 0);
            }
            catch
            {
                // Игнорируем ошибки
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

            if (hProcess != IntPtr.Zero)
            {
                CloseHandle(hProcess);
            }
            base.OnFormClosed(e);
        }
    }

    // Структуры данных
    public struct PlayerData
    {
        public bool IsValid;
        public int HP;
        public int MP;
        public float X;
        public float Y;
        public float Z;
    }

    public struct MobData
    {
        public bool IsValid;
        public int ID;
        public int UniqueID;
        public int HP;
        public float X;
        public float Y;
        public float Z;
    }
}
