using CounterStrikeSharp.API.Core;

namespace EntBossHP
{
    public enum BossType
    {
        Invalid = -1,
        Breakable = 0,
        MathCounter = 1,
        HPBar = 2,
    }

    public class BossData
    {
        public string BossName { get; set; }
        public int Health { get; set; }
        public int MaxHealth { get; set; }
        public int LastHP { get; set; }
        public double LastHit { get; set; }
        public BossType Type { get; set; }
    }

    public class BreakableBoss : BossData
    {
        public CEntityInstance BreakableEntity { get; set; }
        public string BreakableEntityName { get; set; }
    }

    public class MathCounterBoss : BossData
    {
        public CEntityInstance MathCounterEntity { get; set; }
        public int MathCounterHitMode { get; set; } = -1;
        public string MathCounterName { get; set; }

        // More data
        public int MathCounterStartValue { get; set; }
        public int MathCounterMaxValue { get; set; }
        public int MathCounterMinValue { get; set; }
    }

    public class HPBarBoss : MathCounterBoss
    {
        public CEntityInstance IteratorEntity { get; set; }
        public string IteratorName { get; set; }
        public int IteratorHitMode { get; set; } = -1;
        public float IteratorValue { get; set; }
        public CEntityInstance BackUpEntity { get; set; }
        public string BackupName { get; set; }
        public float BackupValue { get; set; }
    }
}
