using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace stats
{
    public static class KeyboardHelper
    {
        private static Random random = new Random();

        // Новый метод с SendInput для более естественной имитации
        public static void SendKeyPressNatural(IntPtr hWnd, ushort virtualKey)
        {
            try
            {
                // Используем SendInput для более естественной имитации нажатия клавиши
                WinAPI.INPUT[] inputs = new WinAPI.INPUT[2];

                // KEYDOWN
                inputs[0] = new WinAPI.INPUT
                {
                    type = WinAPI.INPUT_KEYBOARD,
                    U = new WinAPI.INPUTUNION
                    {
                        ki = new WinAPI.KEYBDINPUT
                        {
                            wVk = virtualKey,
                            wScan = 0,
                            dwFlags = 0,
                            time = 0,
                            dwExtraInfo = IntPtr.Zero
                        }
                    }
                };

                // KEYUP (с случайной задержкой между нажатием и отпусканием)
                int keyDownDelay = 15 + random.Next(0, 16); // 15-30мс (имитация человеческой задержки)
                Thread.Sleep(keyDownDelay);

                inputs[1] = new WinAPI.INPUT
                {
                    type = WinAPI.INPUT_KEYBOARD,
                    U = new WinAPI.INPUTUNION
                    {
                        ki = new WinAPI.KEYBDINPUT
                        {
                            wVk = virtualKey,
                            wScan = 0,
                            dwFlags = WinAPI.KEYEVENTF_KEYUP,
                            time = 0,
                            dwExtraInfo = IntPtr.Zero
                        }
                    }
                };

                WinAPI.SendInput(2, inputs, Marshal.SizeOf(typeof(WinAPI.INPUT)));
            }
            catch
            {
                // Если SendInput не работает, используем PostMessage как fallback
                SendKeyPressFallback(hWnd, virtualKey);
            }
        }

        // Метод использует SendInput для более естественной имитации (с fallback на PostMessage)
        public static void SendKeyPress(IntPtr hWnd, ushort virtualKey)
        {
            try
            {
                // Пытаемся использовать SendInput для более естественной имитации
                WinAPI.INPUT[] inputs = new WinAPI.INPUT[2];

                // KEYDOWN
                inputs[0] = new WinAPI.INPUT
                {
                    type = WinAPI.INPUT_KEYBOARD,
                    U = new WinAPI.INPUTUNION
                    {
                        ki = new WinAPI.KEYBDINPUT
                        {
                            wVk = virtualKey,
                            wScan = 0,
                            dwFlags = 0,
                            time = 0,
                            dwExtraInfo = IntPtr.Zero
                        }
                    }
                };

                // KEYUP (с случайной задержкой между нажатием и отпусканием)
                int keyDownDelay = 15 + random.Next(0, 16); // 15-30мс (имитация человеческой задержки)
                Thread.Sleep(keyDownDelay);

                inputs[1] = new WinAPI.INPUT
                {
                    type = WinAPI.INPUT_KEYBOARD,
                    U = new WinAPI.INPUTUNION
                    {
                        ki = new WinAPI.KEYBDINPUT
                        {
                            wVk = virtualKey,
                            wScan = 0,
                            dwFlags = WinAPI.KEYEVENTF_KEYUP,
                            time = 0,
                            dwExtraInfo = IntPtr.Zero
                        }
                    }
                };

                uint result = WinAPI.SendInput(2, inputs, Marshal.SizeOf(typeof(WinAPI.INPUT)));
                if (result == 0)
                {
                    // Если SendInput не сработал, используем PostMessage как fallback
                    SendKeyPressFallback(hWnd, virtualKey);
                }
            }
            catch
            {
                // Если произошла ошибка, используем PostMessage как fallback
                SendKeyPressFallback(hWnd, virtualKey);
            }
        }

        // Fallback метод (для совместимости)
        private static void SendKeyPressFallback(IntPtr hWnd, ushort virtualKey)
        {
            try
            {
                int keyDownDelay = 15 + random.Next(0, 16);
                WinAPI.PostMessage(hWnd, WinAPI.WM_KEYDOWN, (IntPtr)virtualKey, IntPtr.Zero);
                Thread.Sleep(keyDownDelay);
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
                    // Пробуем найти процесс по имени
                    string processName = gameProcess.ProcessName;
                    Process[] processes = Process.GetProcessesByName(processName);
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
                    logCallback?.Invoke($"Сбор лута: нажатие E {i + 1}/{count}");
                    if (i < count - 1)
                    {
                        // Случайная задержка между нажатиями: 450-550мс (±10%)
                        int delay = 450 + random.Next(0, 101);
                        Thread.Sleep(delay);
                    }
                }
                logCallback?.Invoke($"Сбор лута завершен: отправлено {count} нажатий E");
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
                    // Пробуем найти процесс по имени
                    string processName = gameProcess.ProcessName;
                    Process[] processes = Process.GetProcessesByName(processName);
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
                    SendKeyPress(hWnd, WinAPI.VK_3);
                    logCallback?.Invoke("Баф: нажата клавиша 3");
                }
            }
            catch (Exception ex)
            {
                logCallback?.Invoke($"Ошибка при отправке бафа: {ex.Message}");
            }
        }

        // Зажать комбинацию Shift+E для подсветки лута (держать зажатым)
        // Используем PostMessage - отправляем KEYDOWN для обеих клавиш без отпускания
        public static void PressShiftE(IntPtr hWnd)
        {
            try
            {
                if (hWnd == IntPtr.Zero) return;

                // Нажимаем Shift (без отпускания)
                WinAPI.PostMessage(hWnd, WinAPI.WM_KEYDOWN, (IntPtr)WinAPI.VK_SHIFT, new IntPtr(0x002A0001));
                
                // Небольшая задержка чтобы система обработала Shift
                Thread.Sleep(50);
                
                // Нажимаем E (без отпускания, система должна понять что Shift уже нажат)
                WinAPI.PostMessage(hWnd, WinAPI.WM_KEYDOWN, (IntPtr)WinAPI.VK_E, new IntPtr(0x001E0001));
            }
            catch
            {
                // Игнорируем ошибки
            }
        }

        // Отпустить комбинацию Shift+E
        public static void ReleaseShiftE(IntPtr hWnd)
        {
            try
            {
                if (hWnd == IntPtr.Zero) return;

                // Отпускаем E
                WinAPI.PostMessage(hWnd, WinAPI.WM_KEYUP, (IntPtr)WinAPI.VK_E, new IntPtr(0xC01E0001));
                
                // Небольшая задержка
                Thread.Sleep(50);
                
                // Отпускаем Shift
                WinAPI.PostMessage(hWnd, WinAPI.WM_KEYUP, (IntPtr)WinAPI.VK_SHIFT, new IntPtr(0xC02A0001));
            }
            catch
            {
                // Игнорируем ошибки
            }
        }
    }
}
