using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace stats
{
    public class GameController
    {
        private MemoryReader memoryReader;
        private Process gameProcess;
        private ServerOffsets currentOffsets;
        private List<IntPtr> cachedMobAddresses = new List<IntPtr>();
        private Dictionary<int, string> mobNames = new Dictionary<int, string>();

        public GameController(Process process, string server)
        {
            gameProcess = process;
            memoryReader = new MemoryReader(process);
            SetServer(server);
            LoadMobNames();
        }

        public void SetServer(string server)
        {
            if (server == "tw")
                currentOffsets = ServerOffsets.Tw;
            else if (server == "pn")
                currentOffsets = ServerOffsets.Pn;
            else
                currentOffsets = ServerOffsets.Default;
        }

        public bool IsValid => memoryReader != null && memoryReader.IsValid;

        public IntPtr GetPlayerBaseAddress()
        {
            string moduleName = currentOffsets.ProcessName + ".exe";
            IntPtr moduleBase = memoryReader.GetModuleBaseAddress(moduleName);
            if (moduleBase == IntPtr.Zero) return IntPtr.Zero;

            IntPtr basePtrAddress = IntPtr.Add(moduleBase, currentOffsets.BaseOffset);
            IntPtr firstPtr = memoryReader.ReadPointer(basePtrAddress);
            if (firstPtr == IntPtr.Zero) return IntPtr.Zero;

            if (currentOffsets.UseFirstPointer)
            {
                if (currentOffsets.UseSecondPointer)
                {
                    IntPtr secondPtrAddress = IntPtr.Add(firstPtr, currentOffsets.FirstPointerOffset);
                    IntPtr secondPtr = memoryReader.ReadPointer(secondPtrAddress);
                    if (secondPtr == IntPtr.Zero) return IntPtr.Zero;
                    return secondPtr;
                }
                else
                {
                    return IntPtr.Add(firstPtr, currentOffsets.FirstPointerOffset);
                }
            }

            return firstPtr;
        }

        public PlayerData ReadPlayerData()
        {
            PlayerData data = new PlayerData { IsValid = false, PlayerId = 0 };

            try
            {
                IntPtr playerBase = GetPlayerBaseAddress();
                if (playerBase == IntPtr.Zero) return data;

                data.HP = memoryReader.ReadInt32(IntPtr.Add(playerBase, currentOffsets.HpOffset));
                data.MP = memoryReader.ReadInt32(IntPtr.Add(playerBase, currentOffsets.MpOffset));
                data.X = memoryReader.ReadFloat(IntPtr.Add(playerBase, currentOffsets.XOffset));
                data.Y = memoryReader.ReadFloat(IntPtr.Add(playerBase, currentOffsets.YOffset));
                data.Z = memoryReader.ReadFloat(IntPtr.Add(playerBase, currentOffsets.ZOffset));
                data.PlayerId = memoryReader.ReadInt32(IntPtr.Add(playerBase, currentOffsets.PlayerIdOffset));

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

        public void ScanMobs()
        {
            cachedMobAddresses = memoryReader.ScanMemoryForValue(currentOffsets.MobSignature);
        }

        public List<MobData> ReadMobList(HashSet<int> targetMobIds, Action<string> logCallback = null)
        {
            List<MobData> mobs = new List<MobData>();

            try
            {
                if (!memoryReader.IsValid || cachedMobAddresses.Count == 0) return mobs;

                int validMobsCount = 0;
                int invalidMobsCount = 0;
                int invalidIdHpCount = 0;
                int invalidUniqueIdCount = 0;
                int invalidCoordsCount = 0;
                int debugCount = 0;

                foreach (IntPtr mobAddr in cachedMobAddresses)
                {
                    int id = memoryReader.ReadInt32(IntPtr.Add(mobAddr, currentOffsets.MobIdOffset));
                    int hp = memoryReader.ReadInt32(IntPtr.Add(mobAddr, currentOffsets.MobHpOffset));

                    if (debugCount < 5 && logCallback != null)
                    {
                        long uniqueId = memoryReader.ReadInt64(IntPtr.Add(mobAddr, currentOffsets.MobUniqueIdOffset));
                        float x = memoryReader.ReadFloat(IntPtr.Add(mobAddr, currentOffsets.MobXOffset));
                        float y = memoryReader.ReadFloat(IntPtr.Add(mobAddr, currentOffsets.MobYOffset));
                        float z = memoryReader.ReadFloat(IntPtr.Add(mobAddr, currentOffsets.MobZOffset));
                        logCallback($"[DEBUG] Адрес моба: {mobAddr.ToInt64():X}");
                        logCallback($"[DEBUG]   ID offset {currentOffsets.MobIdOffset:X} = {id}, HP offset {currentOffsets.MobHpOffset:X} = {hp}");
                        logCallback($"[DEBUG]   UniqueID offset {currentOffsets.MobUniqueIdOffset:X} = {uniqueId}");
                        logCallback($"[DEBUG]   XYZ offsets ({currentOffsets.MobXOffset:X}, {currentOffsets.MobYOffset:X}, {currentOffsets.MobZOffset:X}) = ({x:F2}, {y:F2}, {z:F2})");
                        debugCount++;
                    }

                    if (id > 0 && id < 10000 && hp > 0 && hp < 10000)
                    {
                        long uniqueId = memoryReader.ReadInt64(IntPtr.Add(mobAddr, currentOffsets.MobUniqueIdOffset));

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
                            UniqueID2 = 0,
                            HP = hp,
                            X = memoryReader.ReadFloat(IntPtr.Add(mobAddr, currentOffsets.MobXOffset)),
                            Y = memoryReader.ReadFloat(IntPtr.Add(mobAddr, currentOffsets.MobYOffset)),
                            Z = memoryReader.ReadFloat(IntPtr.Add(mobAddr, currentOffsets.MobZOffset)),
                            TargetId = memoryReader.ReadInt32(IntPtr.Add(mobAddr, currentOffsets.MobTargetIdOffset))
                        };

                        // Проверяем координаты и HP: моб должен иметь HP > 0 и не быть остаточными данными
                        bool isNotResidual = !(mob.HP > 0 && mob.HP <= MAX_RESIDUAL_HP && mob.X == 0f && mob.Y == 0f && mob.Z == 0f);
                        if ((mob.X != 0f || mob.Y != 0f || mob.Z != 0f) && mob.HP > 0 && isNotResidual)
                        {
                            if (targetMobIds.Count == 0 || targetMobIds.Contains(mob.ID))
                            {
                                mobs.Add(mob);
                                validMobsCount++;
                            }
                        }
                        else
                        {
                            invalidCoordsCount++;
                            invalidMobsCount++;
                        }
                    }
                    else
                    {
                        invalidIdHpCount++;
                        invalidMobsCount++;
                    }
                }

                if (logCallback != null)
                {
                    logCallback($"Сканирование мобов: найдено адресов: {cachedMobAddresses.Count}, валидных: {validMobsCount}");
                    logCallback($"  - Отфильтровано по ID/HP: {invalidIdHpCount}");
                    logCallback($"  - Отфильтровано по UniqueID: {invalidUniqueIdCount}");
                    logCallback($"  - Отфильтровано по координатам: {invalidCoordsCount}");
                }
            }
            catch
            {
            }

            return mobs;
        }

        public MobData? ReadMobByAddress(IntPtr mobAddr)
        {
            try
            {
                if (!memoryReader.IsValid || mobAddr == IntPtr.Zero) return null;

                int id = memoryReader.ReadInt32(IntPtr.Add(mobAddr, currentOffsets.MobIdOffset));
                int hp = memoryReader.ReadInt32(IntPtr.Add(mobAddr, currentOffsets.MobHpOffset));

                if (id <= 0 || id >= 10000 || hp <= 0 || hp >= 10000)
                {
                    return null;
                }

                long uniqueId = memoryReader.ReadInt64(IntPtr.Add(mobAddr, currentOffsets.MobUniqueIdOffset));
                if (uniqueId <= 0)
                {
                    return null;
                }

                MobData mob = new MobData
                {
                    IsValid = true,
                    ID = id,
                    UniqueID = uniqueId,
                    UniqueID2 = 0,
                    HP = hp,
                    X = memoryReader.ReadFloat(IntPtr.Add(mobAddr, currentOffsets.MobXOffset)),
                    Y = memoryReader.ReadFloat(IntPtr.Add(mobAddr, currentOffsets.MobYOffset)),
                    Z = memoryReader.ReadFloat(IntPtr.Add(mobAddr, currentOffsets.MobZOffset)),
                    TargetId = memoryReader.ReadInt32(IntPtr.Add(mobAddr, currentOffsets.MobTargetIdOffset))
                };

                return mob;
            }
            catch
            {
                return null;
            }
        }

        // Порог низкого HP для определения остаточных данных мертвого моба
        private const int MAX_RESIDUAL_HP = 15;

        // Найти всех мобов с агро (атакуют игрока)
        public List<MobData> FindMobsWithAgro(PlayerData player, HashSet<int> targetMobIds)
        {
            List<MobData> mobsWithAgro = new List<MobData>();
            
            if (!player.IsValid || player.PlayerId <= 0) return mobsWithAgro;
            
            List<MobData> mobs = ReadMobList(targetMobIds);
            foreach (var mob in mobs)
            {
                // Проверка: моб должен быть валидным, иметь HP > 0, и не быть остаточными данными
                if (!mob.IsValid || mob.HP <= 0 
                    || (mob.HP <= MAX_RESIDUAL_HP && mob.X == 0f && mob.Y == 0f && mob.Z == 0f)) continue;
                
                // Проверяем агро: если TargetId моба равен PlayerId игрока, значит моб нас атакует
                if (mob.TargetId == player.PlayerId)
                {
                    mobsWithAgro.Add(mob);
                }
            }

            // Сортируем по расстоянию
            mobsWithAgro.Sort((a, b) =>
            {
                float distA = CalculateDistance(player.X, player.Y, player.Z, a.X, a.Y, a.Z);
                float distB = CalculateDistance(player.X, player.Y, player.Z, b.X, b.Y, b.Z);
                return distA.CompareTo(distB);
            });

            return mobsWithAgro;
        }

        public MobData? FindNearestMob(PlayerData player, HashSet<int> targetMobIds, Random random = null)
        {
            if (!player.IsValid) return null;

            List<MobData> mobs = ReadMobList(targetMobIds);
            if (mobs.Count == 0) return null;

            // Разделяем мобов на две группы: с агро и без агро
            List<(MobData mob, float distance)> mobsWithAgro = new List<(MobData, float)>();
            List<(MobData mob, float distance)> mobsWithoutAgro = new List<(MobData, float)>();

            foreach (var mob in mobs)
            {
                // Проверка: моб должен быть валидным, иметь HP > 0, и не быть остаточными данными
                if (!mob.IsValid || mob.HP <= 0 
                    || (mob.HP <= MAX_RESIDUAL_HP && mob.X == 0f && mob.Y == 0f && mob.Z == 0f)) continue;

                float distance = CalculateDistance(player.X, player.Y, player.Z, mob.X, mob.Y, mob.Z);
                
                // Проверяем агро: если TargetId моба равен PlayerId игрока, значит моб нас атакует
                if (player.PlayerId > 0 && mob.TargetId == player.PlayerId)
                {
                    mobsWithAgro.Add((mob, distance));
                }
                else
                {
                    mobsWithoutAgro.Add((mob, distance));
                }
            }

            // Приоритет 1: Если есть мобы с агро, выбираем ближайшего из них
            if (mobsWithAgro.Count > 0)
            {
                mobsWithAgro.Sort((a, b) => a.distance.CompareTo(b.distance));
                return mobsWithAgro[0].mob; // Всегда выбираем ближайшего с агро
            }

            // Приоритет 2: Если мобов с агро нет, выбираем из обычных мобов
            if (mobsWithoutAgro.Count == 0) return null;

            mobsWithoutAgro.Sort((a, b) => a.distance.CompareTo(b.distance));

            // Случайное отклонение: 15% случаев выбираем не самого близкого моба
            // (выбираем из 3 ближайших для реалистичности)
            if (random != null && mobsWithoutAgro.Count > 1 && random.Next(100) < 15)
            {
                int randomIndex = random.Next(0, Math.Min(3, mobsWithoutAgro.Count));
                return mobsWithoutAgro[randomIndex].mob;
            }

            // В остальных случаях выбираем ближайшего
            return mobsWithoutAgro[0].mob;
        }

        public void AttackMob(MobData mob)
        {
            try
            {
                IntPtr playerBase = GetPlayerBaseAddress();
                if (playerBase == IntPtr.Zero) return;

                IntPtr targetAddr = IntPtr.Add(playerBase, currentOffsets.TargetOffset);
                memoryReader.WriteInt32(targetAddr, (int)mob.UniqueID);

                IntPtr attack1Addr = IntPtr.Add(playerBase, currentOffsets.AttackSet1Offset);
                IntPtr attack2Addr = IntPtr.Add(playerBase, currentOffsets.AttackSet2Offset);
                memoryReader.WriteInt32(attack1Addr, currentOffsets.AttackValue1);
                memoryReader.WriteInt32(attack2Addr, currentOffsets.AttackValue2);
            }
            catch
            {
            }
        }

        public void ClearTarget()
        {
            try
            {
                IntPtr playerBase = GetPlayerBaseAddress();
                if (playerBase == IntPtr.Zero) return;

                memoryReader.WriteInt32(IntPtr.Add(playerBase, currentOffsets.TargetOffset), 0);

                memoryReader.WriteInt32(IntPtr.Add(playerBase, currentOffsets.AttackSet1Offset), currentOffsets.ResetValue1);
                memoryReader.WriteInt32(IntPtr.Add(playerBase, currentOffsets.AttackSet2Offset), currentOffsets.ResetValue2);
            }
            catch
            {
            }
        }

        public IntPtr? FindMobAddressByUniqueId(long uniqueId)
        {
            foreach (var addr in cachedMobAddresses)
            {
                long id = memoryReader.ReadInt64(IntPtr.Add(addr, currentOffsets.MobUniqueIdOffset));
                if (id == uniqueId)
                {
                    return addr;
                }
            }
            return null;
        }

        public string GetMobName(int mobId)
        {
            return mobNames.ContainsKey(mobId) ? mobNames[mobId] : $"Моб {mobId}";
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
                    filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "id-name.txt");
                }

                if (File.Exists(filePath))
                {
                    string[] lines = File.ReadAllLines(filePath);
                    foreach (string line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;

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
            }
        }

        public void Close()
        {
            memoryReader?.Close();
        }
    }
}
