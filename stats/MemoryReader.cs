using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace stats
{
    public class MemoryReader
    {
        private Process gameProcess;
        private IntPtr hProcess;

        public MemoryReader(Process process)
        {
            gameProcess = process;
            if (gameProcess != null && !gameProcess.HasExited)
            {
                hProcess = WinAPI.OpenProcess(
                    WinAPI.PROCESS_VM_READ | WinAPI.PROCESS_VM_WRITE | WinAPI.PROCESS_QUERY_INFORMATION,
                    false,
                    gameProcess.Id
                );
            }
        }

        public bool IsValid => hProcess != IntPtr.Zero && gameProcess != null && !gameProcess.HasExited;

        public IntPtr GetModuleBaseAddress(string moduleName)
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

        public IntPtr ReadPointer(IntPtr address)
        {
            if (hProcess == IntPtr.Zero) return IntPtr.Zero;

            byte[] buffer = new byte[IntPtr.Size];
            int bytesRead;

            if (WinAPI.ReadProcessMemory(hProcess, address, buffer, IntPtr.Size, out bytesRead) && bytesRead == IntPtr.Size)
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

        public int ReadInt32(IntPtr address)
        {
            if (hProcess == IntPtr.Zero) return 0;

            byte[] buffer = new byte[4];
            int bytesRead;

            if (WinAPI.ReadProcessMemory(hProcess, address, buffer, 4, out bytesRead) && bytesRead == 4)
            {
                return BitConverter.ToInt32(buffer, 0);
            }

            return 0;
        }

        public long ReadInt64(IntPtr address)
        {
            if (hProcess == IntPtr.Zero) return 0;

            byte[] buffer = new byte[8];
            int bytesRead;

            if (WinAPI.ReadProcessMemory(hProcess, address, buffer, 8, out bytesRead) && bytesRead == 8)
            {
                return BitConverter.ToInt64(buffer, 0);
            }

            return 0;
        }

        public float ReadFloat(IntPtr address)
        {
            if (hProcess == IntPtr.Zero) return 0f;

            byte[] buffer = new byte[4];
            int bytesRead;

            if (WinAPI.ReadProcessMemory(hProcess, address, buffer, 4, out bytesRead) && bytesRead == 4)
            {
                return BitConverter.ToSingle(buffer, 0);
            }

            return 0f;
        }

        public bool WriteInt32(IntPtr address, int value)
        {
            if (hProcess == IntPtr.Zero) return false;

            byte[] buffer = BitConverter.GetBytes(value);
            int bytesWritten;

            return WinAPI.WriteProcessMemory(hProcess, address, buffer, 4, out bytesWritten) && bytesWritten == 4;
        }

        public List<IntPtr> ScanMemoryForValue(int value)
        {
            List<IntPtr> addresses = new List<IntPtr>();

            try
            {
                if (hProcess == IntPtr.Zero) return addresses;

                byte[] searchBytes = BitConverter.GetBytes(value);
                IntPtr currentAddress = IntPtr.Zero;
                WinAPI.MEMORY_BASIC_INFORMATION mbi = new WinAPI.MEMORY_BASIC_INFORMATION();

                while (WinAPI.VirtualQueryEx(hProcess, currentAddress, out mbi, (uint)System.Runtime.InteropServices.Marshal.SizeOf(mbi)) != 0)
                {
                    // Проверяем только коммитнутые регионы с нужными правами
                    if (mbi.State == WinAPI.MEM_COMMIT &&
                        (mbi.Protect == WinAPI.PAGE_READONLY || mbi.Protect == WinAPI.PAGE_READWRITE ||
                         mbi.Protect == WinAPI.PAGE_EXECUTE_READ || mbi.Protect == WinAPI.PAGE_EXECUTE_READWRITE))
                    {
                        IntPtr regionStart = mbi.BaseAddress;
                        int regionSize = (int)mbi.RegionSize;

                        // Сканируем регион
                        byte[] buffer = new byte[regionSize];
                        int bytesRead;

                        if (WinAPI.ReadProcessMemory(hProcess, regionStart, buffer, regionSize, out bytesRead))
                        {
                            // Ищем значение в буфере
                            for (int i = 0; i <= bytesRead - searchBytes.Length; i += 4) // Шаг 4 байта (выравнивание)
                            {
                                bool match = true;
                                for (int j = 0; j < searchBytes.Length; j++)
                                {
                                    if (buffer[i + j] != searchBytes[j])
                                    {
                                        match = false;
                                        break;
                                    }
                                }

                                if (match)
                                {
                                    addresses.Add(IntPtr.Add(regionStart, i));
                                }
                            }
                        }
                    }

                    // Переходим к следующему региону
                    currentAddress = IntPtr.Add(mbi.BaseAddress, (int)mbi.RegionSize);
                }
            }
            catch
            {
                // В случае ошибки возвращаем то, что нашли
            }

            return addresses;
        }

        public void Close()
        {
            if (hProcess != IntPtr.Zero)
            {
                WinAPI.CloseHandle(hProcess);
                hProcess = IntPtr.Zero;
            }
        }
    }
}
