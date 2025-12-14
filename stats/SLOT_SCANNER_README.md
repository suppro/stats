# Сканер слотов для R2Client

Этот модуль позволяет автоматически находить и читать слоты формата "число/число" (например, "15/240") из памяти процесса R2Client.

## Основные возможности

1. **Автоматическое определение stride** между слотами
2. **Валидация формата** данных (проверка паттерна "число/число")
3. **Корректная обработка** повреждённых или пустых слотов
4. **Чтение N слотов** подряд автоматически

## Быстрый старт

### Простой пример

```csharp
// Создаём экземпляр формы
Form1 form = new Form1();

// Адрес первого слота (из вашего примера)
IntPtr firstSlotAddr = (IntPtr)0x0376E578;

// Сканируем слоты с автоматическим определением stride
var slots = form.ScanSlots(firstSlotAddr, maxSlots: 50);

// Выводим результаты
foreach (var slot in slots)
{
    if (slot.IsValid)
    {
        Console.WriteLine($"Слот #{slot.Index}: {slot.Value} @ 0x{slot.Address:X}");
    }
}
```

### С известным stride

```csharp
// Если вы уже знаете stride (например, 16 байт)
var slots = form.ScanSlots(firstSlotAddr, maxSlots: 50, stride: 16);
```

### С offset от базового адреса модуля

```csharp
// Если адрес задан как offset от базового адреса модуля
long offset = 0x0376E578;
var slots = form.ScanSlotsFromOffset(offset, maxSlots: 50);
```

## Форматированный вывод

```csharp
var slots = form.ScanSlots(firstSlotAddr, maxSlots: 50);
string output = form.FormatSlotsOutput(slots);
Console.WriteLine(output);
```

Вывод будет выглядеть примерно так:
```
Определённый stride: 16 байт (10 hex)
Найдено слотов: 5 валидных, 5 всего

  [000] Адрес: 0x376E578 | Значение: "15/240"
  [001] Адрес: 0x376E588 | Значение: "20/250"
  [002] Адрес: 0x376E598 | Значение: "10/200"
  [003] Адрес: 0x376E5A8 | Значение: "5/100"
  [004] Адрес: 0x376E5B8 | Значение: "30/300"
```

## Структура SlotInfo

Каждый слот представлен классом `SlotInfo`:

```csharp
public class SlotInfo
{
    public int Index { get; set; }          // Индекс слота (0, 1, 2, ...)
    public IntPtr Address { get; set; }     // Адрес слота в памяти
    public string Value { get; set; }       // Значение (например, "15/240")
    public bool IsValid { get; set; }       // Валидность формата
}
```

## Парсинг значений

```csharp
foreach (var slot in slots)
{
    if (slot.IsValid)
    {
        var parts = slot.Value.Split('/');
        if (parts.Length == 2)
        {
            if (int.TryParse(parts[0], out int num1) && 
                int.TryParse(parts[1], out int num2))
            {
                Console.WriteLine($"Первое число: {num1}, Второе: {num2}");
            }
        }
    }
}
```

## Как работает автоматическое определение stride

1. Читает первый слот и проверяет его валидность
2. Пробует различные значения stride (от 1 до 256 байт)
3. Для каждого stride проверяет несколько последовательных слотов
4. Находит stride, при котором все проверенные слоты валидны
5. Использует найденный stride для чтения остальных слотов

## Обработка ошибок

- Если процесс R2Client не найден, возвращается пустой список
- Если нет доступа к процессу (нужны права администратора), возвращается пустой список
- Невалидные слоты помечаются `IsValid = false`
- Сканирование останавливается после 3 невалидных слотов подряд

## Пример: Полный цикл обработки

```csharp
Form1 form = new Form1();
IntPtr firstSlotAddr = (IntPtr)0x0376E578;

// Сканируем слоты
var slots = form.ScanSlots(firstSlotAddr, maxSlots: 100);

if (slots.Count == 0)
{
    Console.WriteLine("Слоты не найдены. Проверьте:");
    Console.WriteLine("  1. Процесс R2Client запущен");
    Console.WriteLine("  2. Приложение запущено от администратора");
    Console.WriteLine("  3. Адрес первого слота корректен");
    return;
}

// Обрабатываем только валидные слоты
var validSlots = slots.Where(s => s.IsValid).ToList();

Console.WriteLine($"Найдено {validSlots.Count} валидных слотов из {slots.Count}");

foreach (var slot in validSlots)
{
    var parts = slot.Value.Split('/');
    if (parts.Length == 2 && 
        int.TryParse(parts[0], out int first) && 
        int.TryParse(parts[1], out int second))
    {
        // Ваша логика обработки
        Console.WriteLine($"Слот {slot.Index}: {first}/{second}");
    }
}
```

## Примечания

- Адреса могут быть абсолютными (как `0x0376E578`) или относительными от базового адреса модуля
- Максимальная длина строки слота по умолчанию: 64 байта
- Stride определяется автоматически, но можно указать его вручную для ускорения
- Все методы безопасно обрабатывают ошибки чтения памяти

