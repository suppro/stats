using System;
using System.Diagnostics;

namespace stats
{
    public static class KeyboardHelper
    {
        public static void SendKeyPress(IntPtr hWnd, ushort virtualKey)
        {
            try
            {
                // Используем PostMessage для отправки клавиши в окно игры
                // Это работает даже если окно не в фокусе
                WinAPI.PostMessage(hWnd, WinAPI.WM_KEYDOWN, (IntPtr)virtualKey, IntPtr.Zero);
                System.Threading.Thread.Sleep(10); // Небольшая задержка между нажатием и отпусканием
                WinAPI.PostMessage(hWnd, WinAPI.WM_KEYUP, (IntPtr)virtualKey, IntPtr.Zero);
            }
            catch
            {
                // Игнорируем ошибки
            }
        }

        public static void SendKeyE(Process gameProcess, int count, Action<string> logCallback = null)
        {
            try
            {
                if (gameProcess == null || gameProcess.HasExited)
                {
                    logCallback?.Invoke("ОШИБКА: Процесс игры не найден для отправки клавиши E");
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
                    logCallback?.Invoke("ОШИБКА: Не удалось найти окно игры для отправки клавиши E");
                    return;
                }

                for (int i = 0; i < count; i++)
                {
                    SendKeyPress(hWnd, WinAPI.VK_E);
                    if (i < count - 1)
                    {
                        System.Threading.Thread.Sleep(700); // Пауза 0.7 сек между нажатиями
                    }
                }
            }
            catch (Exception ex)
            {
                logCallback?.Invoke($"ОШИБКА при отправке клавиши E: {ex.Message}");
            }
        }

        public static void SendKey1(Process gameProcess, Action<string> logCallback = null)
        {
            try
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

                SendKeyPress(hWnd, WinAPI.VK_1);
            }
            catch
            {
                // Игнорируем ошибки
            }
        }

        public static void SendKey3(Process gameProcess, Action<string> logCallback = null)
        {
            try
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

                if (hWnd != IntPtr.Zero)
                {
                    SendKeyPress(hWnd, WinAPI.VK_3);
                    logCallback?.Invoke("Баф: нажата клавиша 3");
                }
            }
            catch (Exception ex)
            {
                logCallback?.Invoke($"Ошибка при отправке бафа: {ex.Message}");
            }
        }
    }
}
