using CounterStrikeSharp.API.Core;

namespace EntBossHP
{
    public class ClientDisplayData
    {
        public ClientDisplayData()
        {
            _lastShootHitBox = 0.0f;
            _entitiyHit = null;
            _bossName = null;
            _bossHP = 0;
        }

        private double _lastShootHitBox;
        private CEntityInstance _entitiyHit;
        private string _bossName;
        private int _bossHP;

        public double LastShootHitBox
        {
            get { return _lastShootHitBox; }
            set { _lastShootHitBox = value; }
        }

        public CEntityInstance EntitiyHit
        {
            get { return _entitiyHit; }
            set { _entitiyHit = value; }
        }

        public string BossName
        {
            get { return _bossName; }
            set { _bossName = value; }
        }

        public int BossHP
        {
            get { return _bossHP; }
            set { _bossHP = value; }
        }
    }
}
