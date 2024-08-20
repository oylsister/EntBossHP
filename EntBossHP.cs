using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using static CounterStrikeSharp.API.Core.Listeners;

namespace EntBossHP
{
    public class EntBossHP : BasePlugin
    {
        public override string ModuleName => "EntBossHP";
        public override string ModuleVersion => "1.1";
        public override string ModuleAuthor => "Oylsister, Kxrnl";

        public Dictionary<CCSPlayerController, ClientDisplayData> ClientDisplayDatas { get; set; } = new Dictionary<CCSPlayerController, ClientDisplayData>();
        public Dictionary<CEntityInstance, EntityData> EntityDatas { get; set; } = new Dictionary<CEntityInstance, EntityData>();

        public List<BreakableBoss> breakableBosses = new List<BreakableBoss>();
        public List<MathCounterBoss> mathCounterBosses = new List<MathCounterBoss>();
        public List<HPBarBoss> hpBarBosses = new List<HPBarBoss>();

        public List<BossData> activeBosses;
        bool configLoaded = false;

        public BossConfig BossConfigs;
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
            RegisterListener<OnClientDisconnect>(OnClientDisconnect);
            RegisterListener<OnTick>(OnGameFrame);
            RegisterListener<OnMapStart>(MapStart);
            RegisterListener<OnEntityCreated>(OnEntityCreated);

            if (hotReload)
            {
                foreach(var player in Utilities.GetPlayers())
                    ClientDisplayDatas.Add(player, new());

                MapStart(Server.MapName);
            }
        }

        public void MapStart(string mapname)
        {
            var configPath = Path.Combine(ModuleDirectory, $"../../configs/bosshp/{mapname}.jsonc");

            if(!File.Exists(configPath))
            {
                Logger.LogInformation($"Couldn't Find {configPath}");
                configLoaded = false;
                return;
            }
            
            BossConfigs = JsonConvert.DeserializeObject<BossConfig>(File.ReadAllText(configPath));
            Logger.LogInformation($"Loaded Boss Config {configPath}");
            configLoaded = true;

            BossDataLoading();
            activeBosses = new();
        }

        private void BossDataLoading()
        {
            foreach(var breakable in BossConfigs.BreakableList)
            {
                BreakableBoss boss = new BreakableBoss();

                boss.BossName = breakable.Name;
                boss.Health = 0;
                boss.MaxHealth = 0;
                boss.LastHit = 0.0f;
                boss.Type = BossType.Breakable;

                boss.BreakableEntity = null;
                boss.BreakableEntityName = breakable.Breakable;

                breakableBosses.Add(boss);
            }

            foreach (var mathcounter in BossConfigs.MathCounterList)
            {
                MathCounterBoss boss = new MathCounterBoss();

                boss.BossName = mathcounter.Name;
                boss.Health = 0;
                boss.MaxHealth = 0;
                boss.LastHit = 0.0f;
                boss.Type = BossType.Breakable;

                boss.MathCounterEntity = null;
                boss.MathCounterHitMax = mathcounter.MathCounterMode == 2 ? true : false;
                boss.MathCounterName = mathcounter.MathCounter;

                mathCounterBosses.Add(boss);
            }

            foreach (var hpbar in BossConfigs.HPBarList)
            {
                HPBarBoss boss = new HPBarBoss();

                boss.BossName = hpbar.Name;
                boss.Health = 0;
                boss.MaxHealth = 0;
                boss.LastHit = 0.0f;
                boss.Type = BossType.Breakable;

                boss.MathCounterEntity = null;
                boss.MathCounterHitMax = hpbar.MathCounterMode == 2 ? true : false;
                boss.MathCounterName = hpbar.MathCounter;

                boss.IteratorEntity = null;
                boss.IteratorHitMax =  hpbar.IteratorMode == 2 ? true : false;
                boss.IteratorName = hpbar.Iterator;
                boss.IteratorValue = 0.0f;

                boss.BackUpEntity = null;
                boss.BackupName = hpbar.Backup;
                boss.BackupValue = 0.0f;

                mathCounterBosses.Add(boss);
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

            if (activeBosses != null)
                activeBosses.Clear();

            return HookResult.Continue;
        }

        public void OnEntityCreated(CEntityInstance entity)
        {
            if (!configLoaded)
                return;

            if (entity.DesignerName == "math_counter")
            {
                AddTimer(0.1f, () =>
                {
                    Timer_MathCounterInitial(entity);
                });
            }
        }

        public void Timer_MathCounterInitial(CEntityInstance entity)
        {
            foreach (var boss in mathCounterBosses)
            {
                if (boss.MathCounterEntity == entity)
                {
                    if (boss.MathCounterHitMax)
                    {
                        var counter = new CMathCounter(boss.MathCounterEntity.Handle);
                        boss.MaxHealth = (int)Math.Round(counter.Max);
                    }
                }
            }
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

            var hp = (int)Math.Round(GetMathCounterValue(caller.Handle));

            if (hp < 0)
                Math.Abs(hp);

            if (EntityDatas[caller].Playerhit.Contains(player(activator)))
                EntityDatas[caller].Playerhit.Add(player(activator));

            EntityDatas[caller].Name = entityname;
            EntityDatas[caller].Health = hp;
            EntityDatas[caller].LastHit = Server.EngineTime;

            // Server.PrintToChatAll($"{caller.Entity.Name}: {hp}");

            // Boss Data section
            if (configLoaded)
            {
                foreach (var boss in mathCounterBosses)
                {
                    if (caller.Entity.Name == boss.MathCounterName)
                    {
                        if (hp == 0)
                        {
                            if (activeBosses.Contains(boss))
                                activeBosses.Remove(boss);

                            continue;
                        }

                        boss.MathCounterEntity = caller;
                        boss.LastHit = Server.EngineTime;

                        if (!boss.MathCounterHitMax)
                        {
                            boss.Health = hp;
                            if (boss.Health > boss.MaxHealth)
                            {
                                boss.MaxHealth = boss.Health;
                            }
                        }
                        else
                        {
                            boss.Health = boss.MaxHealth - hp;
                        }

                        activeBosses.Add(boss);
                    }
                }
            }

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

            // entity section.
            EntityDatas[caller].Name = entityname;
            EntityDatas[caller].Health = hp;
            EntityDatas[caller].LastHit = Server.EngineTime;

            // Boss Data section.
            if (configLoaded)
            {
                foreach (var boss in breakableBosses)
                {
                    if (caller.Entity.Name == boss.BreakableEntityName)
                    {
                        if (hp == 0)
                        {
                            if (activeBosses.Contains(boss))
                                activeBosses.Remove(boss);

                            continue;
                        }

                        boss.BreakableEntity = caller;
                        boss.LastHit = Server.EngineTime;
                        boss.Health = hp;

                        if (boss.Health > boss.MaxHealth)
                        {
                            boss.MaxHealth = boss.Health;
                        }

                        activeBosses.Add(boss);
                    }
                }
            }

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

            // entity section
            EntityDatas[caller].Name = entityname;
            EntityDatas[caller].Health = hp;
            EntityDatas[caller].LastHit = Server.EngineTime;

            // Boss Data section.
            if (configLoaded)
            {
                foreach (var boss in breakableBosses)
                {
                    if (caller.Entity.Name == boss.BreakableEntityName)
                    {
                        if (hp == 0)
                        {
                            if (activeBosses.Contains(boss))
                                activeBosses.Remove(boss);

                            continue;
                        }

                        boss.BreakableEntity = caller;
                        boss.LastHit = Server.EngineTime;
                        boss.Health = hp;

                        if (boss.Health > boss.MaxHealth)
                        {
                            boss.MaxHealth = boss.Health;
                        }

                        activeBosses.Add(boss);
                    }
                }
            }

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

                if(activeBosses.Count > 0)
                {
                    Print_BossHP();
                }

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

        private void Print_BossHP()
        {
            string message = "";

            if(activeBosses.Count < 2)
            {
                message += $"{activeBosses[0].BossName} : {activeBosses[0].Health} | {activeBosses[0].MaxHealth}";
            }

            else if(activeBosses.Count > 2)
            {
                foreach (var boss in activeBosses)
                {
                    message += $"{boss.BossName} : {boss.Health} | {boss.MaxHealth}";
                }
            }

            PrintToCenterHtmlAll(message);
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
