using CounterStrikeSharp.API.Core;

namespace EntBossHP
{
    public class EntityData
    {
        public EntityData(CEntityInstance entity)
        {
            Entity = entity;
            Playerhit = new List<CCSPlayerController>();
            Health = 0;
            Name = "HP";
            LastHit = 0;
            Entity = entity;
        }

        public CEntityInstance Entity;
        public List<CCSPlayerController> Playerhit;
        public int Health;
        public string Name;
        public double LastHit;
    }
}
