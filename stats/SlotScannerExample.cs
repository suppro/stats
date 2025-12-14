using System;
using System.Collections.Generic;

namespace stats
{
    /// <summary>
    /// Пример использования сканера слотов
    /// 
    /// Этот файл показывает, как использовать методы ScanSlots для автоматического
    /// обнаружения и чтения слотов формата "число/число" из памяти процесса.
    /// </summary>
    public class SlotScannerExample
    {
        /// <summary>
        /// Пример использования сканера слотов с автоматическим определением stride
        /// </summary>
        public static void ExampleUsage()
        {
            // Создаём экземпляр формы (или используем существующий)
            Form1 form = new Form1();

            // Адрес первого слота (пример из задания)
            IntPtr firstSlotAddress = (IntPtr)0x0376E578;

            // ВАРИАНТ 1: Автоматическое определение stride
            Console.WriteLine("=== Автоматическое определение stride ===");
            var slots = form.ScanSlots(firstSlotAddress, maxSlots: 50);

            if (slots.Count > 0)
            {
                // Выводим информацию о найденных слотах
                string output = form.FormatSlotsOutput(slots);
                Console.WriteLine(output);

                // Или обрабатываем каждый слот индивидуально:
                foreach (var slot in slots)
                {
                    if (slot.IsValid)
                    {
                        Console.WriteLine($"Слот #{slot.Index}: Адрес = 0x{slot.Address:X}, Значение = \"{slot.Value}\"");

                        // Парсим значение (например, "15/240")
                        var parts = slot.Value.Split('/');
                        if (parts.Length == 2)
                        {
                            if (int.TryParse(parts[0], out int num1) && int.TryParse(parts[1], out int num2))
                            {
                                Console.WriteLine($"  -> Первое число: {num1}, Второе число: {num2}");
                            }
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine("Слоты не найдены!");
            }

            Console.WriteLine("\n" + new string('=', 60) + "\n");

            // ВАРИАНТ 2: Использование известного stride
            Console.WriteLine("=== Использование известного stride (например, 16 байт) ===");
            var slotsWithStride = form.ScanSlots(firstSlotAddress, maxSlots: 50, stride: 16);

            int validCount = 0;
            foreach (var slot in slotsWithStride)
            {
                if (slot.IsValid)
                {
                    validCount++;
                    Console.WriteLine($"[{slot.Index:D3}] 0x{slot.Address:X} = \"{slot.Value}\"");
                }
            }

            Console.WriteLine($"\nНайдено валидных слотов: {validCount}");

            Console.WriteLine("\n" + new string('=', 60) + "\n");

            // ВАРИАНТ 3: Обработка только валидных слотов
            Console.WriteLine("=== Только валидные слоты ===");
            foreach (var slot in slots)
            {
                if (!slot.IsValid) continue;

                // Ваша логика обработки
                Console.WriteLine($"Слот {slot.Index}: {slot.Value}");
            }
        }

        /// <summary>
        /// Пример определения stride вручную
        /// </summary>
        public static void ExampleDetectStride()
        {
            Form1 form = new Form1();

            // Адрес первого слота
            IntPtr firstSlotAddress = (IntPtr)0x0376E578;

            // Сканируем слоты без указания stride - он определится автоматически
            var slots = form.ScanSlots(firstSlotAddress, maxSlots: 10);

            if (slots.Count >= 2 && slots[0].IsValid && slots[1].IsValid)
            {
                // Вычисляем stride на основе найденных адресов
                long stride = slots[1].Address.ToInt64() - slots[0].Address.ToInt64();
                Console.WriteLine($"Определённый stride: {stride} байт (0x{stride:X} hex)");
            }
        }
    }
}

