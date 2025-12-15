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

        [DllImport("kernel32.dll")]
        static extern int VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

        [DllImport("user32.dll")]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern uint SendInput(uint nInputs, [MarshalAs(UnmanagedType.LPArray), In] INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        const uint WM_KEYDOWN = 0x0100;
        const uint WM_KEYUP = 0x0101;

        [StructLayout(LayoutKind.Sequential)]
        struct INPUT
        {
            public uint type;
            public INPUTUNION U;
        }

        [StructLayout(LayoutKind.Explicit)]
        struct INPUTUNION
        {
            [FieldOffset(0)]
            public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        const uint INPUT_KEYBOARD = 1;
        const uint KEYEVENTF_KEYUP = 0x0002;
        const ushort VK_E = 0x45;
        const ushort VK_1 = 0x31;

        [StructLayout(LayoutKind.Sequential)]
        struct MEMORY_BASIC_INFORMATION
        {
            public IntPtr BaseAddress;
            public IntPtr AllocationBase;
            public uint AllocationProtect;
            public IntPtr RegionSize;
            public uint State;
            public uint Protect;
            public uint Type;
        }

        const uint PROCESS_VM_READ = 0x0010;
        const uint PROCESS_VM_WRITE = 0x0020;
        const uint PROCESS_QUERY_INFORMATION = 0x0400;
        const uint MEM_COMMIT = 0x1000;
        const uint PAGE_READONLY = 0x02;
        const uint PAGE_READWRITE = 0x04;
        const uint PAGE_EXECUTE_READ = 0x20;
        const uint PAGE_EXECUTE_READWRITE = 0x40;

        private class ServerOffsets
        {
            public int BaseOffset;
            public bool UseFirstPointer;
            public int FirstPointerOffset;
            public bool UseSecondPointer; // Для TW: нужно читать указатель дважды
            public int HpOffset;
            public int MpOffset;
            public int XOffset;
            public int YOffset;
            public int ZOffset;
            public int TargetOffset;
            public int AttackSet1Offset;
            public int AttackSet2Offset;
            public int MobSignature;
            // Офсеты мобов
            public int MobIdOffset;
            public int MobHpOffset;
            public int MobXOffset;
            public int MobYOffset;
            public int MobZOffset;
            public int MobUniqueIdOffset;
            public int MobUniqueId2Offset; // Второе уникальное ID (только для TW)
            public int TargetOffset2; // Второй офсет для таргета (только для TW)
            // Значения для атаки (для default: set1=3, set2=65536; для tw: set1=3, set2=257)
            public int AttackValue1;
            public int AttackValue2;
            // Значения для сброса (для default: 0,0; для tw: 65536,0)
            public int ResetValue1;
            public int ResetValue2;
        }

        // Конфигурации офсетов для разных серверов
        private static readonly ServerOffsets OffsetsDefault = new ServerOffsets
        {
            BaseOffset = 0x01592584,
            UseFirstPointer = false,
            FirstPointerOffset = 0,
            UseSecondPointer = false,
            HpOffset = 0xFC0,
            MpOffset = 0xFC4,
            XOffset = 0x2B70,
            YOffset = 0x2B74,
            ZOffset = 0x2B78,
            TargetOffset = 0x101C,
            AttackSet1Offset = 0x3094,
            AttackSet2Offset = 0x3098,
            MobSignature = 24831816, // Старая сигнатура для default
            MobIdOffset = 0x0C,
            MobHpOffset = 0x40,
            MobXOffset = 0x1BF0,
            MobYOffset = 0x1BF4,
            MobZOffset = 0x1BF8,
            MobUniqueIdOffset = 0x98,
            MobUniqueId2Offset = 0, // Нет второго уникального ID для default
            TargetOffset2 = 0, // Нет второго таргета для default
            AttackValue1 = 3,
            AttackValue2 = 65536,
            ResetValue1 = 0,
            ResetValue2 = 0
        };

        private static readonly ServerOffsets OffsetsTw = new ServerOffsets
        {
            BaseOffset = 0x034182B8,
            UseFirstPointer = true,
            FirstPointerOffset = 0x60,
            UseSecondPointer = true, // Для TW: читаем указатель дважды
            // "R2Client.exe"+034182B8 -> указатель -> +60 -> указатель -> +офсеты
            HpOffset = 0x40,
            MpOffset = 0x44,
            XOffset = 0x2AE0,
            YOffset = 0x2AE4,
            ZOffset = 0x2AE8,
            TargetOffset = 0xAC,
            AttackSet1Offset = 0x2AEC, // не в бою = 65536, в бою = 3
            AttackSet2Offset = 0x2AAC, // не в бою = 0, в бою = 257
            MobSignature = 56124016, // Новая сигнатура для tw
            // Офсеты мобов для TW (от адреса моба 04A69FC0)
            MobIdOffset = 0x0C,        // 04A69FCC - 04A69FC0
            MobHpOffset = 0x40,        // 04A6A000 - 04A69FC0
            MobXOffset = 0x2AC4,       // 04A6CA84 - 04A69FC0
            MobYOffset = 0x2AC8,       // 04A6CA88 - 04A69FC0
            MobZOffset = 0x2ACC,       // 04A6CA8C - 04A69FC0
            MobUniqueIdOffset = 0xA8,  // 04A6A068 - 04A69FC0
            MobUniqueId2Offset = 0xC0, // 04A6A080 - 04A69FC0 (второе уникальное ID)
            TargetOffset2 = 0xC8,      // Второй офсет для таргета (от playerBase)
            AttackValue1 = 3,      // для 0x2AEC (записываем 3)
            AttackValue2 = 257,    // для 0x2AAC
            ResetValue1 = 65536,   // для 0x2AEC (вернуть в не бой)
            ResetValue2 = 0        // для 0x2AAC (вернуть в не бой)
        };

        private Timer updateTimer;
        private Timer attackTimer;
        private Process gameProcess;
        private IntPtr hProcess;

        // Словарь для имен мобов (ID -> Name)
        private Dictionary<int, string> mobNames = new Dictionary<int, string>();

        // Состояние атаки
        private bool isAttacking = false;
        private int killedMobCount = 0;
        private MobData? currentTarget = null;
        private bool enableLootCollection = false;
        private bool enableAutoHeal = false;
        
        // Кэш для списка мобов (адреса статичные, сканируем только 1 раз при старте)
        private List<IntPtr> cachedMobAddresses = new List<IntPtr>();
        private bool addressesScanned = false;
        private bool connectionLogged = false;
        private DateTime lastDebugLogTime = DateTime.MinValue;

        // Labels для персонажа
        private Label lblPlayerHP;
        private Label lblPlayerMP;

        // Label для статуса
        private Label lblStatus;

        // UI элементы для атаки
        private ComboBox cmbServer;
        private TextBox txtMobIds;
        private CheckBox chkLootCollection;
        private CheckBox chkAutoHeal;
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
            LoadMobNames();
            SetupForm();
            StartUpdateTimer();
        }

        private void SetupForm()
        {
            //название окна не менять
            this.Text = "Stats";
            this.Size = new Size(800, 700);
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
            
            // Логируем подключение только один раз
            if (!connectionLogged)
            {
                AddLog($"Подключено к процессу R2Client.exe (PID: {gameProcess.Id})");
                connectionLogged = true;
            }

            // Читаем данные персонажа
            var playerData = ReadPlayerData();
            UpdatePlayerData(playerData);

            // Обновляем только данные персонажа
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

        private long ReadInt64(IntPtr address)
        {
            if (hProcess == IntPtr.Zero) return 0;

            byte[] buffer = new byte[8];
            int bytesRead;

            if (ReadProcessMemory(hProcess, address, buffer, 8, out bytesRead) && bytesRead == 8)
            {
                return BitConverter.ToInt64(buffer, 0);
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

        private ServerOffsets GetCurrentOffsets()
        {
            return selectedServer == "tw" ? OffsetsTw : OffsetsDefault;
        }

        private IntPtr GetPlayerBaseAddress(IntPtr moduleBase)
        {
            if (moduleBase == IntPtr.Zero) return IntPtr.Zero;

            var cfg = GetCurrentOffsets();
            IntPtr basePtrAddress = IntPtr.Add(moduleBase, cfg.BaseOffset);
            
            // Читаем первый указатель
            IntPtr firstPtr = ReadPointer(basePtrAddress);
            if (firstPtr == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            if (cfg.UseFirstPointer)
            {
                if (cfg.UseSecondPointer)
                {
                    // Для tw: "R2Client.exe"+034182B8 -> указатель -> +60 -> указатель
                    IntPtr secondPtrAddress = IntPtr.Add(firstPtr, cfg.FirstPointerOffset);
                    IntPtr secondPtr = ReadPointer(secondPtrAddress);
                    if (secondPtr == IntPtr.Zero) return IntPtr.Zero;
                    return secondPtr;
                }
                else
                {
                    // Просто прибавляем офсет к первому указателю
                    return IntPtr.Add(firstPtr, cfg.FirstPointerOffset);
                }
            }

            return firstPtr;
        }

        private PlayerData ReadPlayerData()
        {
            PlayerData data = new PlayerData { IsValid = false };

            try
            {
                // Получаем базовый адрес модуля
                IntPtr moduleBase = GetModuleBaseAddress("R2Client.exe");
                if (moduleBase == IntPtr.Zero) return data;

                var cfg = GetCurrentOffsets();
                IntPtr playerBase = GetPlayerBaseAddress(moduleBase);

                if (playerBase == IntPtr.Zero)
                {
                    // Логируем ошибку только раз в 5 секунд
                    if ((DateTime.Now - lastDebugLogTime).TotalSeconds >= 5.0)
                    {
                        AddLog($"[ОШИБКА] PlayerBase = Zero, не удалось получить адрес персонажа (сервер: {selectedServer})");
                        lastDebugLogTime = DateTime.Now;
                    }
                    return data;
                }

                // Читаем данные персонажа относительно указателя
                // HP и MP - 4 байта (int32), координаты - float
                IntPtr hpAddr = IntPtr.Add(playerBase, cfg.HpOffset);
                IntPtr mpAddr = IntPtr.Add(playerBase, cfg.MpOffset);
                IntPtr xAddr = IntPtr.Add(playerBase, cfg.XOffset);
                
                data.HP = ReadInt32(hpAddr);
                data.MP = ReadInt32(mpAddr);
                data.X = ReadFloat(xAddr);
                data.Y = ReadFloat(IntPtr.Add(playerBase, cfg.YOffset));
                data.Z = ReadFloat(IntPtr.Add(playerBase, cfg.ZOffset));

                // Проверяем валидность данных
                if (data.HP > 0 || data.MP > 0 || (data.X != 0f || data.Y != 0f || data.Z != 0f))
                {
                    data.IsValid = true;
                }
                else
                {
                    // Логируем ошибки только раз в 5 секунд
                    if ((DateTime.Now - lastDebugLogTime).TotalSeconds >= 5.0)
                    {
                        AddLog($"[ОШИБКА] Данные персонажа невалидны: HP={data.HP}, MP={data.MP}, XYZ=({data.X:F2}, {data.Y:F2}, {data.Z:F2})");
                        AddLog($"[ОШИБКА] Адреса: PlayerBase={playerBase.ToInt64():X}, HP адрес={hpAddr.ToInt64():X} (offset {cfg.HpOffset:X}), MP адрес={mpAddr.ToInt64():X} (offset {cfg.MpOffset:X})");
                        lastDebugLogTime = DateTime.Now;
                    }
                }
            }
            catch
            {
                data.IsValid = false;
            }

            return data;
        }
        
        private void CmbServer_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbServer.SelectedItem != null)
            {
                selectedServer = cmbServer.SelectedItem.ToString();
                AddLog($"Выбран сервер: {selectedServer}");
            }
        }

        private List<MobData> ReadMobList()
        {
            List<MobData> mobs = new List<MobData>();

            try
            {
                if (hProcess == IntPtr.Zero) return mobs;

                // Используем кэшированные адреса (сканируем только 1 раз при старте)
                if (cachedMobAddresses.Count == 0)
                {
                    return mobs; // Адреса еще не отсканированы
                }

                // Для каждого найденного адреса читаем данные моба
                int validMobsCount = 0;
                int invalidMobsCount = 0;
                int invalidIdHpCount = 0;
                int invalidUniqueIdCount = 0;
                int invalidCoordsCount = 0;
                int debugCount = 0; // Логируем только первые 5 адресов
                
                foreach (IntPtr mobAddr in cachedMobAddresses)
                {
                    // Читаем ID и HP моба для предварительной проверки
                    var cfg = GetCurrentOffsets();
                    int id = ReadInt32(IntPtr.Add(mobAddr, cfg.MobIdOffset));
                    int hp = ReadInt32(IntPtr.Add(mobAddr, cfg.MobHpOffset));
                    
                    // Логируем первые несколько адресов для отладки
                    if (debugCount < 5)
                    {
                        int uniqueId = ReadInt32(IntPtr.Add(mobAddr, cfg.MobUniqueIdOffset));
                        float x = ReadFloat(IntPtr.Add(mobAddr, cfg.MobXOffset));
                        float y = ReadFloat(IntPtr.Add(mobAddr, cfg.MobYOffset));
                        float z = ReadFloat(IntPtr.Add(mobAddr, cfg.MobZOffset));
                        AddLog($"[DEBUG] Адрес моба: {mobAddr.ToInt64():X}");
                        AddLog($"[DEBUG]   ID offset {cfg.MobIdOffset:X} = {id}, HP offset {cfg.MobHpOffset:X} = {hp}");
                        AddLog($"[DEBUG]   UniqueID offset {cfg.MobUniqueIdOffset:X} = {uniqueId}");
                        AddLog($"[DEBUG]   XYZ offsets ({cfg.MobXOffset:X}, {cfg.MobYOffset:X}, {cfg.MobZOffset:X}) = ({x:F2}, {y:F2}, {z:F2})");
                        debugCount++;
                    }

                    // Проверяем, что это действительно моб: ID и HP должны быть > 0 и < 10000
                    if (id > 0 && id < 10000 && hp > 0 && hp < 10000)
                    {
                        // Читаем UniqueID для проверки (int64)
                        long uniqueId = ReadInt64(IntPtr.Add(mobAddr, cfg.MobUniqueIdOffset));
                        
                        // UniqueID не может быть 0 - это обязательное поле
                        if (uniqueId <= 0)
                        {
                            invalidUniqueIdCount++;
                            invalidMobsCount++;
                            continue;
                        }
                        
                        MobData mob = new MobData
                        {
                            IsValid = true,
                            ID = id,
                            UniqueID = uniqueId,
                            UniqueID2 = cfg.MobUniqueId2Offset > 0 ? (int)ReadInt64(IntPtr.Add(mobAddr, cfg.MobUniqueId2Offset)) : 0,
                            HP = hp,
                            X = ReadFloat(IntPtr.Add(mobAddr, cfg.MobXOffset)),
                            Y = ReadFloat(IntPtr.Add(mobAddr, cfg.MobYOffset)),
                            Z = ReadFloat(IntPtr.Add(mobAddr, cfg.MobZOffset))
                        };

                        // Проверяем валидность координат
                        // Координаты должны быть не все нули (хотя бы одна координата != 0)
                        if (mob.X != 0f || mob.Y != 0f || mob.Z != 0f)
                        {
                            mobs.Add(mob);
                            validMobsCount++;
                            
                            // Логируем первый валидный моб для проверки
                            if (validMobsCount == 1)
                            {
                                AddLog($"[DEBUG] Первый валидный моб: ID={mob.ID}, HP={mob.HP}, UniqueID={mob.UniqueID}, XYZ=({mob.X:F2}, {mob.Y:F2}, {mob.Z:F2})");
                            }
                        }
                        else
                        {
                            invalidCoordsCount++;
                            invalidMobsCount++;
                            
                            // Логируем первый моб с нулевыми координатами
                            if (invalidCoordsCount == 1)
                            {
                                AddLog($"[DEBUG] Первый моб с нулевыми координатами: ID={mob.ID}, HP={mob.HP}, UniqueID={mob.UniqueID}, XYZ=({mob.X:F2}, {mob.Y:F2}, {mob.Z:F2})");
                            }
                        }
                    }
                    else
                    {
                        invalidIdHpCount++;
                        invalidMobsCount++;
                    }
                }
                
                // Логируем статистику фильтрации
                AddLog($"Сканирование мобов: найдено адресов: {cachedMobAddresses.Count}, валидных: {validMobsCount}");
                AddLog($"  - Отфильтровано по ID/HP: {invalidIdHpCount}");
                AddLog($"  - Отфильтровано по UniqueID: {invalidUniqueIdCount}");
                AddLog($"  - Отфильтровано по координатам: {invalidCoordsCount}");
            }
            catch
            {
                // В случае ошибки возвращаем пустой список
            }

            return mobs;
        }

        private MobData? ReadMobByAddress(IntPtr mobAddr)
        {
            try
            {
                if (hProcess == IntPtr.Zero || mobAddr == IntPtr.Zero) return null;

                var cfg = GetCurrentOffsets();
                
                // Читаем ID и HP для проверки валидности
                int id = ReadInt32(IntPtr.Add(mobAddr, cfg.MobIdOffset));
                int hp = ReadInt32(IntPtr.Add(mobAddr, cfg.MobHpOffset));

                // Проверяем, что это действительно моб: ID и HP должны быть > 0 и < 10000
                if (id <= 0 || id >= 10000 || hp <= 0 || hp >= 10000)
                {
                    return null;
                }

                // Читаем UniqueID и проверяем, что он не 0 (int64)
                long uniqueId = ReadInt64(IntPtr.Add(mobAddr, cfg.MobUniqueIdOffset));
                if (uniqueId <= 0)
                {
                    return null; // UniqueID не может быть 0 или отрицательным
                }

                MobData mob = new MobData
                {
                    IsValid = true,
                    ID = id,
                    UniqueID = uniqueId,
                            UniqueID2 = cfg.MobUniqueId2Offset > 0 ? (int)ReadInt64(IntPtr.Add(mobAddr, cfg.MobUniqueId2Offset)) : 0,
                    HP = hp,
                    X = ReadFloat(IntPtr.Add(mobAddr, cfg.MobXOffset)),
                    Y = ReadFloat(IntPtr.Add(mobAddr, cfg.MobYOffset)),
                    Z = ReadFloat(IntPtr.Add(mobAddr, cfg.MobZOffset))
                };

                return mob;
            }
            catch
            {
                return null;
            }
        }

        private List<IntPtr> ScanMemoryForValue(int value)
        {
            List<IntPtr> addresses = new List<IntPtr>();

            try
            {
                if (hProcess == IntPtr.Zero) return addresses;

                byte[] searchBytes = BitConverter.GetBytes(value);
                IntPtr currentAddress = IntPtr.Zero;
                MEMORY_BASIC_INFORMATION mbi = new MEMORY_BASIC_INFORMATION();

                // Сканируем все регионы памяти процесса
                while (VirtualQueryEx(hProcess, currentAddress, out mbi, (uint)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION))) != 0)
                {
                    // Проверяем, что регион закоммичен и доступен для чтения
                    if (mbi.State == MEM_COMMIT &&
                        (mbi.Protect == PAGE_READONLY || mbi.Protect == PAGE_READWRITE ||
                         mbi.Protect == PAGE_EXECUTE_READ || mbi.Protect == PAGE_EXECUTE_READWRITE))
                    {
                        int regionSize = (int)mbi.RegionSize;
                        // Ограничиваем размер региона для избежания проблем с большими регионами
                        if (regionSize > 0 && regionSize < 100 * 1024 * 1024) // До 100MB
                        {
                            // Сканируем регион на наличие значения
                            byte[] buffer = new byte[regionSize];
                            int bytesRead;

                            if (ReadProcessMemory(hProcess, mbi.BaseAddress, buffer, regionSize, out bytesRead) && bytesRead > 0)
                            {
                                // Ищем значение в буфере
                                for (int i = 0; i <= bytesRead - 4; i++)
                                {
                                    if (buffer[i] == searchBytes[0] &&
                                        buffer[i + 1] == searchBytes[1] &&
                                        buffer[i + 2] == searchBytes[2] &&
                                        buffer[i + 3] == searchBytes[3])
                                    {
                                        IntPtr foundAddress = IntPtr.Add(mbi.BaseAddress, i);
                                        addresses.Add(foundAddress);
                                    }
                                }
                            }
                        }
                    }

                    // Переходим к следующему региону
                    currentAddress = IntPtr.Add(mbi.BaseAddress, (int)mbi.RegionSize);
                    
                    // Защита от бесконечного цикла
                    if (currentAddress.ToInt64() <= mbi.BaseAddress.ToInt64())
                        break;
                }
            }
            catch (Exception ex)
            {
                // В случае ошибки возвращаем найденные адреса
                System.Diagnostics.Debug.WriteLine($"Ошибка при сканировании памяти: {ex.Message}");
            }

            return addresses;
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

                // Фильтруем по ID, если указаны целевые ID
                if (targetMobIds.Count > 0 && !targetMobIds.Contains(mob.ID))
                {
                    continue;
                }

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

        private void SendKeyEToGame(int count)
        {
            try
            {
                if (gameProcess == null || gameProcess.HasExited)
                {
                    AddLog("ОШИБКА: Процесс игры не найден для отправки клавиши E");
                    return;
                }

                // Находим главное окно процесса
                IntPtr hWnd = gameProcess.MainWindowHandle;
                if (hWnd == IntPtr.Zero)
                {
                    // Пробуем найти окно процесса заново
                    Process[] processes = Process.GetProcessesByName("R2Client");
                    if (processes.Length > 0)
                    {
                        hWnd = processes[0].MainWindowHandle;
                    }
                }

                if (hWnd == IntPtr.Zero)
                {
                    AddLog("ОШИБКА: Не удалось найти окно игры для отправки клавиши E");
                    return;
                }

                // НЕ активируем окно игры - отправляем клавиши в фоновом режиме
                // Отправляем клавишу E указанное количество раз
                for (int i = 0; i < count; i++)
                {
                    SendKeyPress(hWnd, VK_E);
                    if (i < count - 1) // Не делаем паузу после последнего нажатия
                    {
                        System.Threading.Thread.Sleep(500); // Пауза 0.7 сек между нажатиями
                    }
                }
            }
            catch (Exception ex)
            {
                AddLog($"ОШИБКА при отправке клавиши E: {ex.Message}");
            }
        }

        private void AutoHeal()
        {
            try
            {
                var playerData = ReadPlayerData();
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

                    IntPtr hWnd = gameProcess.MainWindowHandle;
                    if (hWnd == IntPtr.Zero)
                    {
                        Process[] processes = Process.GetProcessesByName("R2Client");
                        if (processes.Length > 0)
                        {
                            hWnd = processes[0].MainWindowHandle;
                        }
                    }

                    if (hWnd == IntPtr.Zero)
                    {
                        return;
                    }

                    // НЕ активируем окно - отправляем клавиши в фоновом режиме
                    int maxHealAttempts = 20; // Максимум попыток лечения
                    int attempts = 0;

                    while (playerData.HP <= 120 && attempts < maxHealAttempts)
                    {
                        SendKeyPress(hWnd, VK_1);
                        System.Threading.Thread.Sleep(500); // Пауза 0.5 сек между нажатиями

                        // Обновляем данные персонажа
                        playerData = ReadPlayerData();
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

        private void SendKeyPress(IntPtr hWnd, ushort virtualKey)
        {
            try
            {
                // Используем PostMessage для отправки клавиши в окно игры
                // Это работает даже если окно не в фокусе
                PostMessage(hWnd, WM_KEYDOWN, (IntPtr)virtualKey, IntPtr.Zero);
                System.Threading.Thread.Sleep(10); // Небольшая задержка между нажатием и отпусканием
                PostMessage(hWnd, WM_KEYUP, (IntPtr)virtualKey, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                AddLog($"ОШИБКА при отправке клавиши: {ex.Message}");
            }
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
            if (!addressesScanned || cachedMobAddresses.Count == 0)
            {
                UpdateStatus("Сканирование памяти...", Color.Yellow);
                Application.DoEvents(); // Обновляем UI

                // Запускаем сканирование в отдельном потоке, чтобы не блокировать UI
                System.Threading.Tasks.Task.Run(() =>
                {
                    var cfg = GetCurrentOffsets();
                    var addresses = ScanMemoryForValue(cfg.MobSignature);
                    
                    this.Invoke((MethodInvoker)delegate
                    {
                        cachedMobAddresses = addresses;
                        addressesScanned = true;
                        
                        if (cachedMobAddresses.Count > 0)
                        {
                            UpdateStatus($"Найдено адресов мобов: {cachedMobAddresses.Count}. Атака начата!", Color.Lime);
                            AddLog($"Найдено адресов мобов: {cachedMobAddresses.Count}");
                            
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
                            txtMobIds.Enabled = true;
                            isAttacking = false;
                        }
                    });
                });
            }
            else
            {
                // Адреса уже отсканированы, сразу запускаем атаку
                UpdateStatus($"Атака начата! Найдено адресов: {cachedMobAddresses.Count}", Color.Lime);
                AddLog($"Используются кэшированные адреса: {cachedMobAddresses.Count}");
                
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
            txtMobIds.Enabled = true;

            // Сбрасываем цель (но не очищаем кэш адресов - они статичные)
            ClearTarget();

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

            // Проверяем, есть ли текущая цель и жива ли она
            if (currentTarget.HasValue)
            {
                // Находим адрес текущей цели в кэше
                IntPtr? targetAddr = null;
                foreach (var addr in cachedMobAddresses)
                {
                    var mob = ReadMobByAddress(addr);
                    if (mob.HasValue && mob.Value.UniqueID == currentTarget.Value.UniqueID)
                    {
                        targetAddr = addr;
                        break;
                    }
                }

                if (targetAddr != null && targetAddr.Value != IntPtr.Zero)
                {
                    // Читаем данные текущей цели напрямую (без полного сканирования)
                    var targetMob = ReadMobByAddress(targetAddr.Value);

                    if (!targetMob.HasValue || targetMob.Value.HP <= 0)
                    {
                        // Моб убит
                        int killedMobId = currentTarget.Value.ID;
                        string mobName = GetMobName(killedMobId);
                        
                        killedMobCount++;
                        currentTarget = null;
                        ClearTarget();

                        AddLog($"Моб убит: {mobName} (ID: {killedMobId})");

                        // Отправляем клавишу E для сбора лута, если включено
                        if (enableLootCollection)
                        {
                            AddLog($"Сбор лута (E) - enableLootCollection = {enableLootCollection}");
                            SendKeyEToGame(2);
                            SendKeyEToGame(2);
                        }
                        else
                        {
                            AddLog($"Сбор лута пропущен - enableLootCollection = {enableLootCollection}");
                        }

                        // Автохил после убийства моба, если включено
                        if (enableAutoHeal)
                        {
                            AutoHeal();
                        }

                        lblKilledCount.Text = $"Убито: {killedMobCount}";
                    }
                    else
                    {
                        // Продолжаем атаковать текущую цель
                        return;
                    }
                }
                else
                {
                    // Адрес не найден в кэше - моб исчез (убит или деспавнился)
                    int killedMobId = currentTarget.Value.ID;
                    string mobName = GetMobName(killedMobId);
                    
                    killedMobCount++;
                    currentTarget = null;
                    ClearTarget();

                    AddLog($"Моб убит (исчез из памяти): {mobName} (ID: {killedMobId})");

                    // Отправляем клавишу E для сбора лута, если включено
                    if (enableLootCollection)
                    {
                        AddLog($"Сбор лута (E)");
                        SendKeyEToGame(2);
                        SendKeyEToGame(2);
                    }

                    // Автохил после убийства моба, если включено
                    if (enableAutoHeal)
                    {
                        AutoHeal();
                    }

                    lblKilledCount.Text = $"Убито: {killedMobCount}";
                }
            }

            // Ищем новую цель из кэшированного списка адресов
            var player = ReadPlayerData();
            if (!player.IsValid)
            {
                return;
            }

            // Используем кэшированные адреса (не сканируем заново)
            var nearestMob = FindNearestMob(player);
            if (nearestMob.HasValue && nearestMob.Value.HP > 0)
            {
                // Находим адрес моба по UniqueID
                IntPtr mobAddr = IntPtr.Zero;
                var cfg = GetCurrentOffsets();
                foreach (var addr in cachedMobAddresses)
                {
                    long uniqueId = ReadInt64(IntPtr.Add(addr, cfg.MobUniqueIdOffset));
                    if (uniqueId == nearestMob.Value.UniqueID)
                    {
                        mobAddr = addr;
                        break;
                    }
                }
                
                // Атакуем нового моба
                currentTarget = nearestMob;
                AttackMob(nearestMob.Value);
                string mobName = GetMobName(nearestMob.Value.ID);
                string uniqueId2Info = nearestMob.Value.UniqueID2 > 0 ? $", UniqueID2: {nearestMob.Value.UniqueID2}" : "";
                AddLog($"Атака: {mobName} (ID: {nearestMob.Value.ID}, HP: {nearestMob.Value.HP}, UniqueID: {nearestMob.Value.UniqueID}{uniqueId2Info}, Адрес: {mobAddr.ToInt64():X})");
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

        private void AttackMob(MobData mob)
        {
            try
            {
                IntPtr moduleBase = GetModuleBaseAddress("R2Client.exe");
                if (moduleBase == IntPtr.Zero) return;

                IntPtr playerBase = GetPlayerBaseAddress(moduleBase);
                if (playerBase == IntPtr.Zero) return;

                var cfg = GetCurrentOffsets();

                // Записываем уникальный ID моба в TARGET (приводим long к int, так как WriteInt32 принимает int)
                IntPtr targetAddr = IntPtr.Add(playerBase, cfg.TargetOffset);
                WriteInt32(targetAddr, (int)mob.UniqueID);
                AddLog($"[АТАКА] Target адрес: {targetAddr.ToInt64():X} (offset {cfg.TargetOffset:X}), записано UniqueID: {mob.UniqueID}");

                // Для TW сервера записываем второе уникальное ID
                if (cfg.TargetOffset2 > 0 && mob.UniqueID2 > 0)
                {
                    IntPtr targetAddr2 = IntPtr.Add(playerBase, cfg.TargetOffset2);
                    WriteInt32(targetAddr2, mob.UniqueID2);
                    AddLog($"[АТАКА] Target2 адрес: {targetAddr2.ToInt64():X} (offset {cfg.TargetOffset2:X}), записано UniqueID2: {mob.UniqueID2}");
                }

                // Записываем значения для атаки (разные для разных серверов)
                IntPtr attack1Addr = IntPtr.Add(playerBase, cfg.AttackSet1Offset);
                IntPtr attack2Addr = IntPtr.Add(playerBase, cfg.AttackSet2Offset);
                
                // Читаем текущие значения перед записью
                int currentValue1 = ReadInt32(attack1Addr);
                int currentValue2 = ReadInt32(attack2Addr);
                
                WriteInt32(attack1Addr, cfg.AttackValue1);
                WriteInt32(attack2Addr, cfg.AttackValue2);
                
                // Проверяем, что записалось
                int writtenValue1 = ReadInt32(attack1Addr);
                int writtenValue2 = ReadInt32(attack2Addr);
                
                AddLog($"[АТАКА] Attack1 адрес: {attack1Addr.ToInt64():X} (offset {cfg.AttackSet1Offset:X}), было: {currentValue1}, записано: {cfg.AttackValue1}, прочитано: {writtenValue1}");
                AddLog($"[АТАКА] Attack2 адрес: {attack2Addr.ToInt64():X} (offset {cfg.AttackSet2Offset:X}), было: {currentValue2}, записано: {cfg.AttackValue2}, прочитано: {writtenValue2}");
                AddLog($"[АТАКА] PlayerBase: {playerBase.ToInt64():X}, сервер: {selectedServer}");
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

                IntPtr playerBase = GetPlayerBaseAddress(moduleBase);
                if (playerBase == IntPtr.Zero) return;

                var cfg = GetCurrentOffsets();

                // Сбрасываем цель
                WriteInt32(IntPtr.Add(playerBase, cfg.TargetOffset), 0);
                
                // Для TW сервера сбрасываем второй таргет
                if (cfg.TargetOffset2 > 0)
                {
                    WriteInt32(IntPtr.Add(playerBase, cfg.TargetOffset2), 0);
                }
                
                // Возвращаем значения в состояние "не в бою" (разные для разных серверов)
                WriteInt32(IntPtr.Add(playerBase, cfg.AttackSet1Offset), cfg.ResetValue1);
                WriteInt32(IntPtr.Add(playerBase, cfg.AttackSet2Offset), cfg.ResetValue2);
            }
            catch
            {
                // Игнорируем ошибки
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
        public long UniqueID; // int64 для правильного отображения
        public int UniqueID2; // Второе уникальное ID (только для TW)
        public int HP;
        public float X;
        public float Y;
        public float Z;
    }
}
