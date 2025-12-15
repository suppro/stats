namespace stats
{
    public class ServerOffsets
    {
        public string ProcessName; // Имя процесса (R2Client или PNGame)
        public int BaseOffset;
        public bool UseFirstPointer;
        public int FirstPointerOffset;
        public bool UseSecondPointer; // Для TW и PN: нужно читать указатель дважды
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
        // Значения для атаки (set1=3, set2=65536 для обоих серверов)
        public int AttackValue1;
        public int AttackValue2;
        // Значения для сброса (0,0 для обоих серверов)
        public int ResetValue1;
        public int ResetValue2;

        public static readonly ServerOffsets Default = new ServerOffsets
        {
            ProcessName = "R2Client",
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
            AttackValue1 = 3,
            AttackValue2 = 65536,
            ResetValue1 = 0,
            ResetValue2 = 0
        };

        public static readonly ServerOffsets Tw = new ServerOffsets
        {
            ProcessName = "R2Client",
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
            AttackSet1Offset = 0x2FE4, // Новые офсеты для атаки
            AttackSet2Offset = 0x2FE8,
            MobSignature = 56124016, // Новая сигнатура для tw
            // Офсеты мобов для TW (от адреса моба 04A69FC0)
            MobIdOffset = 0x0C,        // 04A69FCC - 04A69FC0
            MobHpOffset = 0x40,        // 04A6A000 - 04A69FC0
            MobXOffset = 0x2AC4,       // 04A6CA84 - 04A69FC0
            MobYOffset = 0x2AC8,       // 04A6CA88 - 04A69FC0
            MobZOffset = 0x2ACC,       // 04A6CA8C - 04A69FC0
            MobUniqueIdOffset = 0xA8,  // 04A6A068 - 04A69FC0
            AttackValue1 = 3,          // Такие же значения как на Default
            AttackValue2 = 65536,
            ResetValue1 = 0,           // Такие же значения сброса как на Default
            ResetValue2 = 0
        };

        public static readonly ServerOffsets Pn = new ServerOffsets
        {
            ProcessName = "PNGame",
            BaseOffset = 0x037BB5C0,
            UseFirstPointer = true,
            FirstPointerOffset = 0x60,
            UseSecondPointer = true, // Для PN: читаем указатель дважды
            // "PNGame.exe"+037BB5C0 -> указатель -> +60 -> указатель -> +офсеты
            HpOffset = 0x40,
            MpOffset = 0x44,
            XOffset = 0x2D84,
            YOffset = 0x2D88,
            ZOffset = 0x2D8C,
            TargetOffset = 0xAC,
            AttackSet1Offset = 0x32A4,
            AttackSet2Offset = 0x32A8,
            MobSignature = 59719308, // Сигнатура для PN
            // Офсеты мобов для PN
            MobIdOffset = 0x0C,
            MobHpOffset = 0x40,
            MobXOffset = 0x2D84,
            MobYOffset = 0x2D88,
            MobZOffset = 0x2D8C,
            MobUniqueIdOffset = 0xA8,
            AttackValue1 = 3,
            AttackValue2 = 65536,
            ResetValue1 = 0,
            ResetValue2 = 0
        };
    }
}
