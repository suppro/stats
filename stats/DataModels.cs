namespace stats
{
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
        public int UniqueID2; // Не используется (оставлено для совместимости)
        public int HP;
        public float X;
        public float Y;
        public float Z;
    }
}
