using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace stats
{
    /// <summary>
    /// Информация о найденном слоте
    /// </summary>
    public class SlotInfo
    {
        public int Index { get; set; }
        public IntPtr Address { get; set; }
        public string Value { get; set; }
        public bool IsValid { get; set; }
    }

    /// <summary>
    /// Сканер слотов для чтения данных формата "число/число" из памяти процесса
    /// </summary>
    public class SlotScanner
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll")]
        private static extern int VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORY_BASIC_INFORMATION
        {
            public IntPtr BaseAddress;
            public IntPtr AllocationBase;
            public uint AllocationProtect;
            public IntPtr RegionSize;
            public uint State;
            public uint Protect;
            public uint Type;
        }

        private const uint PROCESS_VM_READ = 0x0010;
        private const uint PAGE_READONLY = 0x02;
        private const uint PAGE_READWRITE = 0x04;
        private const uint PAGE_EXECUTE_READ = 0x20;
        private const uint PAGE_EXECUTE_READWRITE = 0x40;
        private const uint MEM_COMMIT = 0x1000;

        private static readonly Regex SlotPattern = new Regex(@"^\d+/\d+$", RegexOptions.Compiled);

        /// <summary>
        /// Проверяет, соответствует ли строка формату "число/число"
        /// </summary>
        private static bool IsValidSlotFormat(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            return SlotPattern.IsMatch(value.Trim());
        }

        /// <summary>
        /// Читает строку из памяти процесса, начиная с указанного адреса
        /// </summary>
        private static string ReadStringFromMemory(IntPtr hProcess, IntPtr address, int maxLength = 64)
        {
            byte[] buffer = new byte[maxLength];
            int bytesRead;

            if (!ReadProcessMemory(hProcess, address, buffer, maxLength, out bytesRead) || bytesRead == 0)
                return null;

            // Находим конец строки (нулевой байт)
            int nullIndex = Array.IndexOf(buffer, (byte)0);
            int length = nullIndex >= 0 ? nullIndex : bytesRead;

            try
            {
                return Encoding.ASCII.GetString(buffer, 0, length);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Определяет stride между слотами, сканируя память от первого слота
        /// </summary>
        private static int DetectStride(IntPtr hProcess, IntPtr firstSlotAddress, int maxStride = 512)
        {
            string firstSlotValue = ReadStringFromMemory(hProcess, firstSlotAddress);
            if (string.IsNullOrEmpty(firstSlotValue) || !IsValidSlotFormat(firstSlotValue))
                return -1;

            // Сканируем память вперёд, ищем второй слот
            for (int offset = 4; offset <= maxStride; offset += 4) // Шаг по 4 байта
            {
                IntPtr candidateAddress = IntPtr.Add(firstSlotAddress, offset);
                string candidateValue = ReadStringFromMemory(hProcess, candidateAddress);

                if (!string.IsNullOrEmpty(candidateValue) && IsValidSlotFormat(candidateValue))
                {
                    // Проверяем, что это не та же самая строка (может быть дубликат)
                    if (candidateValue != firstSlotValue)
                    {
                        return offset;
                    }
                }
            }

            return -1;
        }

        /// <summary>
        /// Сканирует память и находит все слоты формата "число/число"
        /// </summary>
        /// <param name="processName">Имя процесса (например, "R2Client")</param>
        /// <param name="firstSlotAddress">Адрес первого слота</param>
        /// <param name="maxSlots">Максимальное количество слотов для поиска</param>
        /// <param name="maxStride">Максимальный stride для определения (по умолчанию 512 байт)</param>
        /// <returns>Список найденных слотов</returns>
        public static List<SlotInfo> ScanSlots(string processName, IntPtr firstSlotAddress, int maxSlots = 100, int maxStride = 512)
        {
            List<SlotInfo> slots = new List<SlotInfo>();

            Process[] processes = Process.GetProcessesByName(processName);
            if (processes.Length == 0)
            {
                Console.WriteLine($"Процесс '{processName}' не найден");
                return slots;
            }

            Process targetProcess = processes[0];
            IntPtr hProcess = OpenProcess(PROCESS_VM_READ, false, targetProcess.Id);

            if (hProcess == IntPtr.Zero)
            {
                Console.WriteLine($"Не удалось открыть процесс (требуются права администратора)");
                return slots;
            }

            try
            {
                // Читаем первый слот для валидации
                string firstValue = ReadStringFromMemory(hProcess, firstSlotAddress);
                if (string.IsNullOrEmpty(firstValue) || !IsValidSlotFormat(firstValue))
                {
                    Console.WriteLine($"Первый слот по адресу 0x{firstSlotAddress.ToInt64():X8} не содержит валидных данных");
                    return slots;
                }

                Console.WriteLine($"Первый слот найден: '{firstValue}' по адресу 0x{firstSlotAddress.ToInt64():X8}");

                // Определяем stride
                int stride = DetectStride(hProcess, firstSlotAddress, maxStride);
                if (stride <= 0)
                {
                    Console.WriteLine($"Не удалось определить stride. Используем первый слот.");
                    slots.Add(new SlotInfo
                    {
                        Index = 0,
                        Address = firstSlotAddress,
                        Value = firstValue,
                        IsValid = true
                    });
                    return slots;
                }

                Console.WriteLine($"Определён stride: {stride} байт (0x{stride:X})");

                // Добавляем первый слот
                slots.Add(new SlotInfo
                {
                    Index = 0,
                    Address = firstSlotAddress,
                    Value = firstValue,
                    IsValid = true
                });

                // Читаем остальные слоты
                for (int i = 1; i < maxSlots; i++)
                {
                    IntPtr slotAddress = IntPtr.Add(firstSlotAddress, i * stride);
                    string slotValue = ReadStringFromMemory(hProcess, slotAddress);

                    if (string.IsNullOrEmpty(slotValue))
                    {
                        // Пустой слот - возможно, конец списка
                        break;
                    }

                    bool isValid = IsValidSlotFormat(slotValue);
                    slots.Add(new SlotInfo
                    {
                        Index = i,
                        Address = slotAddress,
                        Value = slotValue,
                        IsValid = isValid
                    });

                    // Если слот невалидный, продолжаем, но отмечаем это
                    if (!isValid)
                    {
                        break; // Прерываем, если нашли невалидный слот
                    }
                }

                Console.WriteLine($"Найдено слотов: {slots.Count}");
            }
            finally
            {
                CloseHandle(hProcess);
            }

            return slots;
        }

        /// <summary>
        /// Выводит информацию о найденных слотах в консоль
        /// </summary>
        public static void PrintSlots(List<SlotInfo> slots)
        {
            if (slots.Count == 0)
            {
                Console.WriteLine("Слоты не найдены");
                return;
            }

            Console.WriteLine("\n=== НАЙДЕННЫЕ СЛОТЫ ===");
            Console.WriteLine($"{"Индекс",-8} {"Адрес",-16} {"Значение",-20} {"Валидный"}");
            Console.WriteLine(new string('-', 60));

            foreach (var slot in slots)
            {
                string addressStr = $"0x{slot.Address.ToInt64():X8}";
                string validStr = slot.IsValid ? "✓" : "✗";
                Console.WriteLine($"{slot.Index,-8} {addressStr,-16} {slot.Value,-20} {validStr}");
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Сканирует слоты и возвращает результат в виде строки для отображения
        /// </summary>
        public static string ScanSlotsAsString(string processName, IntPtr firstSlotAddress, int maxSlots = 100)
        {
            var slots = ScanSlots(processName, firstSlotAddress, maxSlots);
            
            if (slots.Count == 0)
                return "Слоты не найдены";

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Найдено слотов: {slots.Count}");
            sb.AppendLine();

            foreach (var slot in slots)
            {
                string addressStr = $"0x{slot.Address.ToInt64():X8}";
                string validStr = slot.IsValid ? "✓" : "✗";
                sb.AppendLine($"[{slot.Index}] {addressStr}: {slot.Value} {validStr}");
            }

            return sb.ToString();
        }
    }
}
