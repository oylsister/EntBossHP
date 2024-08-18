using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Utils;

namespace EntBossHP
{
    public class EntBossHP : BasePlugin
    {
        public override string ModuleName => "EntBossHP";
        public override string ModuleVersion => "1.1";
        public override string ModuleAuthor => "Oylsister, Kxrnl";

        public Dictionary<CCSPlayerController, ClientDisplayData> ClientDisplayDatas { get; set; } = new Dictionary<CCSPlayerController, ClientDisplayData>();
        public Dictionary<CEntityInstance, EntityData> EntityDatas { get; set; } = new Dictionary<CEntityInstance, EntityData>();
        public double CurrentTime;
        public double LastForceShowBossHP;

        public override void Load(bool hotReload)
        {
            HookEntityOutput("math_counter", "OutValue", CounterOut);
            HookEntityOutput("func_physbox_multiplayer", "OnDamaged", BreakableOut);
            HookEntityOutput("func_physbox", "OnHealthChanged", BreakableOut);
            HookEntityOutput("func_breakable", "OnHealthChanged", BreakableOut);
            HookEntityOutput("prop_dynamic", "OnHealthChanged", Hitbox_Hook);

            RegisterEventHandler<EventRoundStart>(OnRoundStart);
            RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
            RegisterListener<Listeners.OnClientDisconnect>(OnClientDisconnect);
            RegisterListener<Listeners.OnTick>(OnGameFrame);

            if(hotReload)
            {
                foreach(var player in Utilities.GetPlayers())
                    ClientDisplayDatas.Add(player, new());
            }
        }

        public HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
        {
            if (@event.Userid.IsBot || @event.Userid.IsHLTV)
                return HookResult.Continue;

            ClientDisplayDatas.Add(@event.Userid, new());
            return HookResult.Continue;
        }

        public void OnClientDisconnect(int playerslot)
        {
            var client = Utilities.GetPlayerFromSlot(playerslot);

            if (client.IsBot || client.IsHLTV)
                return;

            if (ClientDisplayDatas.ContainsKey(client))
                ClientDisplayDatas.Remove(client);

            if (EntityDatas.Count > 0)
            {
                foreach (var entity in EntityDatas)
                {
                    if (entity.Value.Playerhit.Contains(client))
                        entity.Value.Playerhit.Remove(client);
                }
            }
        }

        public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
        {
            EntityDatas.Clear();
            return HookResult.Continue;
        }

        public HookResult CounterOut(CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller, CVariant value, float delay)
        {
            if (!activator.IsValid || !ClientDisplayDatas.ContainsKey(player(activator)))
                return HookResult.Continue;

            if (!caller.IsValid)
                return HookResult.Continue;

            if (activator.DesignerName != "player")
                return HookResult.Continue;

            if (!EntityDatas.ContainsKey(caller))
                EntityDatas.Add(caller, new(caller));

            var entityname = caller.Entity.Name;

            if (string.IsNullOrEmpty(entityname) || string.IsNullOrWhiteSpace(entityname))
                entityname = "HP";

            CMathCounter prop = new(caller.Handle);

            var hp = (int)GetMathCounterValue(caller.Handle);

            if (hp < 0)
                Math.Abs(hp);

            if (EntityDatas[caller].Playerhit.Contains(player(activator)))
                EntityDatas[caller].Playerhit.Add(player(activator));

            EntityDatas[caller].Name = entityname;
            EntityDatas[caller].Health = hp;

            if (EntityDatas[caller].MaxHealth <= EntityDatas[caller].Health)
                EntityDatas[caller].MaxHealth = EntityDatas[caller].Health;

            EntityDatas[caller].LastHit = Server.EngineTime;

            // Server.PrintToChatAll($"{caller.Entity.Name}: {hp}");

            if (hp > 500000)
                return HookResult.Continue;

            if (player(activator) == null)
                return HookResult.Continue;

            ClientDisplayDatas[player(activator)].EntitiyHit = caller;
            ClientDisplayDatas[player(activator)].BossName = caller.Entity.Name;
            ClientDisplayDatas[player(activator)].BossHP = hp;
            ClientDisplayDatas[player(activator)].LastShootHitBox = Server.EngineTime;

            // Server.PrintToChatAll($"activator = {activator.DesignerName} | caller = {caller.DesignerName}");

            return HookResult.Continue;
        }

        public HookResult BreakableOut(CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller, CVariant value, float delay)
        {
            if(!activator.IsValid || !ClientDisplayDatas.ContainsKey(player(activator)))
                return HookResult.Continue;

            if (!caller.IsValid)
                return HookResult.Continue;

            if (activator.DesignerName != "player")
                return HookResult.Continue;

            if (!EntityDatas.ContainsKey(caller))
                EntityDatas.Add(caller, new(caller));

            CBreakable prop = new CBreakable(caller.Handle);

            var entityname = caller.Entity.Name;

            if (string.IsNullOrEmpty(entityname) || string.IsNullOrWhiteSpace(entityname))
                entityname = "HP";

            if (!prop.IsValid || prop == null)
                return HookResult.Continue;

            var hp = prop!.Health;

            if (hp < 0)
                hp = 0;

            if (EntityDatas[caller].Playerhit.Contains(player(activator)))
                EntityDatas[caller].Playerhit.Add(player(activator));

            EntityDatas[caller].Name = entityname;
            EntityDatas[caller].Health = hp;

            if (EntityDatas[caller].MaxHealth <= EntityDatas[caller].Health)
                EntityDatas[caller].MaxHealth = EntityDatas[caller].Health;

            EntityDatas[caller].LastHit = Server.EngineTime;

            // Server.PrintToChatAll($"{caller.Entity.Name}: {hp}");

            if (hp > 500000)
                return HookResult.Continue;

            if (player(activator) == null)
                return HookResult.Continue;

            ClientDisplayDatas[player(activator)].EntitiyHit = caller;
            ClientDisplayDatas[player(activator)].BossName = caller.Entity.Name;
            ClientDisplayDatas[player(activator)].BossHP = hp > 0 ? hp : 0;
            ClientDisplayDatas[player(activator)].LastShootHitBox = Server.EngineTime;


            //Server.PrintToChatAll($"{caller.Entity.Name}: {hp}");

            return HookResult.Continue;
        }

        public HookResult Hitbox_Hook(CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller, CVariant value, float delay)
        {
            if (!activator.IsValid || !ClientDisplayDatas.ContainsKey(player(activator)))
                return HookResult.Continue;

            if (!caller.IsValid)
                return HookResult.Continue;

            if (activator.DesignerName != "player")
                return HookResult.Continue;

            if (!EntityDatas.ContainsKey(caller))
                EntityDatas.Add(caller, new(caller));

            CBreakable prop = new CBreakable(caller.Handle);

            var entityname = caller.Entity.Name;

            if (string.IsNullOrEmpty(entityname) || string.IsNullOrWhiteSpace(entityname))
                entityname = "HP";

            if (!prop.IsValid || prop == null)
                return HookResult.Continue;

            var hp = prop!.Health;

            if (hp < 0)
                hp = 0;

            if (EntityDatas[caller].Playerhit.Contains(player(activator)))
                EntityDatas[caller].Playerhit.Add(player(activator));

            EntityDatas[caller].Name = entityname;
            EntityDatas[caller].Health = hp;

            if (EntityDatas[caller].MaxHealth <= EntityDatas[caller].Health)
                EntityDatas[caller].MaxHealth = EntityDatas[caller].Health;

            EntityDatas[caller].LastHit = Server.EngineTime;

            // Server.PrintToChatAll($"{caller.Entity.Name}: {hp}");

            if (hp > 500000)
                return HookResult.Continue;

            if (player(activator) == null)
                return HookResult.Continue;

            ClientDisplayDatas[player(activator)].EntitiyHit = caller;
            ClientDisplayDatas[player(activator)].BossName = caller.Entity.Name;
            ClientDisplayDatas[player(activator)].BossHP = hp;
            ClientDisplayDatas[player(activator)].LastShootHitBox = Server.EngineTime;

            //Server.PrintToChatAll($"{caller.Entity.Name}: {hp}");

            return HookResult.Continue;
        }

        public void OnGameFrame()
        {
            foreach(var client in Utilities.GetPlayers())
            {
                if(client == null)
                    continue;

                if (client.IsBot || client.IsHLTV)
                    continue;

                if (!ClientDisplayDatas.ContainsKey(client))
                    continue;

                if (ClientDisplayDatas[client].LastShootHitBox > Server.EngineTime - 2f)
                {
                    if (EntityDatas.ContainsKey(ClientDisplayDatas[client].EntitiyHit))
                        Print_BHudGlobal(client, EntityDatas[ClientDisplayDatas[client].EntitiyHit]);

                    else
                        Print_BHudLocal(client, ClientDisplayDatas[client]);
                }
            }
        }

        int PlayerCount;

        private void Print_BHudGlobal(CCSPlayerController client, EntityData data)
        {
            PlayerCount = 0;

            foreach (var player in Utilities.GetPlayers())
            {
                if(player.Team == CsTeam.CounterTerrorist)
                    PlayerCount++;
            }

            // if there is alot of player hitting a same entity then print showing all player
            if(EntityDatas[data.Entity].Playerhit.Count > PlayerCount / 2)
            {
                PrintToCenterHtmlAll($"{data.Name}: {data.Health}");
                return;
            }

            // yeah just one
            else
                client.PrintToCenterHtml($"{data.Name}: {data.Health}");
        }

        private void Print_BHudLocal(CCSPlayerController client, ClientDisplayData data)
        {
            client.PrintToCenterHtml($"{data.BossName}: {data.BossHP}");
        }

        public static CCSPlayerController player(CEntityInstance instance)
        {
            if (instance == null)
            {
                return null;
            }

            if (instance.DesignerName != "player")
            {
                return null;
            }

            // grab the pawn index
            int player_index = (int)instance.Index;

            // grab player controller from pawn
            CCSPlayerPawn player_pawn = Utilities.GetEntityFromIndex<CCSPlayerPawn>(player_index);

            // pawn valid
            if (player_pawn == null || !player_pawn.IsValid)
            {
                return null;
            }

            // controller valid
            if (player_pawn.OriginalController == null || !player_pawn.OriginalController.IsValid)
            {
                return null;
            }

            // any further validity is up to the caller
            return player_pawn.OriginalController.Value;
        }

        void PrintToCenterHtmlAll(string text)
        {
            foreach(var player in Utilities.GetPlayers())
            {
                player.PrintToCenterHtml(text);
            }
        }

        private unsafe float GetMathCounterValue(nint handle)
        {
            var offset = Schema.GetSchemaOffset("CMathCounter", "m_OutValue");
            return *(float*)IntPtr.Add(handle, offset + 24);
        }
    }
}

public class EntityData
{
    public EntityData(CEntityInstance entity)
    {
        Entity = entity;
        Playerhit = new List<CCSPlayerController>();
        Health = 0;
        MaxHealth = 0;
        Name = "HP";
        LastHit = 0;
        Entity = entity;
    }

    public CEntityInstance Entity;
    public List<CCSPlayerController> Playerhit;
    public int Health;
    public int MaxHealth;
    public string Name;
    public double LastHit;
}

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
        set {  _lastShootHitBox = value; }
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
