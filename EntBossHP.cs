using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Utils;

namespace EntBossHP
{
    public class EntBossHP : BasePlugin
    {
        public override string ModuleName => "EntBossHP";
        public override string ModuleVersion => "1.0";
        public override string ModuleAuthor => "Oylsister, Kxrnl";

        public Dictionary<CCSPlayerController, ClientDisplayData> ClientDisplayDatas { get; set; } = new Dictionary<CCSPlayerController, ClientDisplayData>();
        public double CurrentTime;
        public double LastForceShowBossHP;

        public override void Load(bool hotReload)
        {
            HookEntityOutput("math_counter", "OutValue", CounterOut);
            HookEntityOutput("func_physbox_multiplayer", "OnDamaged", BreakableOut);
            HookEntityOutput("func_physbox", "OnHealthChanged", BreakableOut);
            HookEntityOutput("func_breakable", "OnHealthChanged", BreakableOut);
            HookEntityOutput("prop_dynamic", "OnHealthChanged", Hitbox_Hook);

            RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
            RegisterListener<Listeners.OnClientDisconnect>(OnClientDisconnect);
            RegisterListener<Listeners.OnTick>(OnGameFrame);
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

            if(ClientDisplayDatas.ContainsKey(client))
                ClientDisplayDatas.Remove(client);
        }

        private unsafe float GetMathCounterValue(nint handle)
        {
            var offset = Schema.GetSchemaOffset("CMathCounter", "m_OutValue");
            return *(float*)IntPtr.Add(handle, offset + 24);
        }

        public HookResult CounterOut(CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller, CVariant value, float delay)
        {
            /*
            if (!caller.IsValid)
                return HookResult.Continue;

            if (caller.DesignerName != "math_counter")
                return HookResult.Continue;

            var entityname = caller.Entity.Name;
            var hp = GetMathCounterValue(caller.Handle);

            if (player(activator) == null)
                return HookResult.Continue;

            if (ClientLastShootHitBox[player(activator)] > Server.EngineTime - 0.2f)
            {
                ClientEntityHit[player(activator)] = caller;
                ClientEntityNameHit[player(activator)] = caller.Entity.Name;

                if (hp > 0)
                {
                    Print_BHUD(player(activator), caller, entityname, (int)Math.Round(hp));
                }
            }
            */
            Server.PrintToChatAll($"activator = {activator.DesignerName} | caller = {caller.DesignerName}");

            return HookResult.Continue;
        }

        public HookResult BreakableOut(CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller, CVariant value, float delay)
        {
            if (!activator.IsValid || !ClientDisplayDatas.ContainsKey(player(activator)))
                return HookResult.Continue;

            if (!caller.IsValid)
                return HookResult.Continue;

            string entityname;
            CBreakable prop = new CBreakable(caller.Handle);

            if(caller.Entity.Name.Length <= 0)
                entityname = "HP";

            else
                entityname = caller.Entity.Name;

            if (!prop.IsValid || prop == null)
                return HookResult.Continue;

            var hp = prop!.Health;

            Server.PrintToChatAll($"{caller.Entity.Name}: {hp}");

            if (hp > 500000)
                return HookResult.Continue;

            if (player(activator) == null)
                return HookResult.Continue;

            if (ClientDisplayDatas[player(activator)].LastShootHitBox > Server.EngineTime - 2f)
            {
                ClientDisplayDatas[player(activator)].EntitiyHit = caller;
                ClientDisplayDatas[player(activator)].BossName = caller.Entity.Name;
                ClientDisplayDatas[player(activator)].BossHP = hp;
            }

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

            var entityname = caller.Entity.Name;
            CBreakable prop = new CBreakable(caller.Handle);

            if (entityname.Length <= 0)
                entityname = "HP";

            if (!prop.IsValid || prop == null)
                return HookResult.Continue;

            var hp = prop!.Health;

            Server.PrintToChatAll($"{caller.Entity.Name}: {hp}");

            if (hp > 500000)
                return HookResult.Continue;

            if (player(activator) == null)
                return HookResult.Continue;

            if (ClientDisplayDatas[player(activator)].LastShootHitBox > Server.EngineTime - 2f)
            {
                ClientDisplayDatas[player(activator)].EntitiyHit = caller;
                ClientDisplayDatas[player(activator)].BossName = entityname;
                ClientDisplayDatas[player(activator)].BossHP = hp;
            }

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
                    Print_BHud(client, ClientDisplayDatas[client]);
            }
        }

        private void Print_BHud(CCSPlayerController client, ClientDisplayData data)
        {
            client.PrintToCenterHtml($"{data.BossName}: {data.BossHP}");
        }

        /*
        void Print_BHUD(CCSPlayerController client, CEntityInstance entity, string name, int hp)
        {
            CurrentTime = Server.EngineTime;

            if (ClientLastShootHitBox[client] > CurrentTime - 3.0f && LastForceShowBossHP + 0.1f < CurrentTime || hp == 0)
            {
                var playercount = 0;
                var CTcount = 0;

                foreach (var player in Utilities.GetPlayers())
                {
                    if (player.Team == CsTeam.CounterTerrorist)
                    {
                        CTcount++;
                        if (ClientLastShootHitBox[player] > CurrentTime - 7.0 && ClientEntityHit[player] == entity && name == ClientEntityNameHit[player])
                        {
                            playercount++;
                        }
                    }
                }

                if (playercount > CTcount / 2)
                {
                    foreach (var player in Utilities.GetPlayers())
                    {
                        player.PrintToCenter($"{name}: {hp}");
                    }
                }
                else
                {
                    foreach (var player in Utilities.GetPlayers())
                    {
                        if (ClientLastShootHitBox[player] > CurrentTime - 7.0f && ClientEntityHit[player] == entity && name == ClientEntityNameHit[player])
                        {
                            player.PrintToCenter($"{name}: {hp}");
                        }
                    }
                }

                LastForceShowBossHP = CurrentTime;
            }
        }
        */

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
    }
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
