using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Runtime.CompilerServices;
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
                boss.MathCounterHitMode = mathcounter.MathCounterMode;
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
                boss.MathCounterHitMode = hpbar.MathCounterMode;
                boss.MathCounterName = hpbar.MathCounter;

                boss.IteratorEntity = null;
                boss.IteratorHitMode = hpbar.IteratorMode;
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

            ResetBossHP();

            return HookResult.Continue;
        }

        private void ResetBossHP()
        {
            foreach(var boss in breakableBosses)
            {
                if(boss == null) continue;

                boss.Health = 0;
                boss.MaxHealth = 0;
                boss.BreakableEntity = null;
                boss.LastHit = 0f;
            }

            foreach(var boss in mathCounterBosses)
            {
                if(boss == null) continue ;

                boss.Health = 0;
                boss.MaxHealth = 0;
                boss.MathCounterEntity = null;
                boss.LastHit = 0f;
            }

            foreach (var boss in hpBarBosses)
            {
                if (boss == null) continue;

                boss.Health = 0;
                boss.MaxHealth = 0;
                boss.MathCounterEntity = null;
                boss.BackUpEntity = null;
                boss.BackupValue = 0f;
                boss.IteratorValue = 0f;
                boss.IteratorEntity = null;
                boss.LastHit = 0f;
            }
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
            if (entity == null)
                return;

            if (string.IsNullOrEmpty(entity.Entity.Name) || string.IsNullOrWhiteSpace(entity.Entity.Name))
                return;

            foreach (var boss in mathCounterBosses)
            {
                if (boss.MathCounterName == entity.Entity.Name)
                {
                    boss.MathCounterEntity = entity;

                    // if math_counter increment when get hit.
                    if (boss.MathCounterHitMode == 2)
                    {
                        var counter = new CMathCounter(entity.Handle);
                        boss.MaxHealth = (int)Math.Round(counter.Max);
                    }

                    // normal mode
                    else if (boss.MathCounterHitMode == 1)
                    {
                        var counter = GetMathCounterValue(entity.Handle);
                        boss.MaxHealth = (int)Math.Round(counter);
                    }

                    // if other value is assign or is -1.
                    else
                    {
                        var counter = new CMathCounter(entity.Handle);

                        // check if the outvalue is same as hitmin or not.
                        if (counter.HitMin)
                            boss.MaxHealth = (int)Math.Round(GetMathCounterValue(entity.Handle));

                        else
                            boss.MaxHealth = (int)Math.Round(counter.Max);
                    }
                }
            }

            foreach (var boss in hpBarBosses)
            {
                if (entity.Entity.Name == boss.MathCounterName)
                {
                    boss.MathCounterEntity = entity;

                    if(boss.MathCounterHitMode == -1)
                    {
                        var counter = new CMathCounter(entity.Handle);

                        if (counter.HitMin)
                            boss.MathCounterHitMode = 2;

                        else
                            boss.MathCounterHitMode = 1;
                    }
                }

                if (entity.Entity.Name == boss.IteratorName)
                {
                    boss.IteratorEntity = entity;
                    var iteratorMath = new CMathCounter(entity.Handle);

                    if (boss.IteratorHitMode == 2)
                        boss.IteratorValue = iteratorMath.Max;

                    else if (boss.IteratorHitMode == 1)
                        boss.IteratorValue = GetMathCounterValue(entity.Handle);

                    else
                    {
                        if(iteratorMath.HitMin)
                            boss.IteratorValue = iteratorMath.Max;

                        else
                            boss.IteratorValue = GetMathCounterValue(entity.Handle);
                    }
                }

                if(!string.IsNullOrEmpty(boss.BackupName) && !string.IsNullOrWhiteSpace(boss.BackupName))
                {
                    if (entity.Entity.Name == boss.BackupName)
                    {
                        boss.BackUpEntity = entity;
                        boss.BackupValue = GetMathCounterValue(entity.Handle);
                    }
                }
            }
        }

        public HookResult CounterOut(CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller, CVariant value, float delay)
        {
            if (activator == null)
                return HookResult.Continue;

            var client = player(activator);

            if (client == null)
                return HookResult.Continue;

            if (!activator.IsValid || !ClientDisplayDatas.ContainsKey(client))
                return HookResult.Continue;

            if (!caller.IsValid)
                return HookResult.Continue;

            if (!EntityDatas.ContainsKey(caller))
                EntityDatas.Add(caller, new(caller));

            var entityname = caller.Entity.Name;

            if (string.IsNullOrEmpty(entityname) || string.IsNullOrWhiteSpace(entityname))
                entityname = "HP";

            CMathCounter prop = new(caller.Handle);

            //var hp = (int)Math.Round(GetMathCounterValue(caller.Handle));
            var TheOutput = new CEntityOutputTemplate_float(output.Handle);
            var hp = (int)Math.Round(TheOutput.OutValue);

            if (!EntityDatas[caller].Playerhit.Contains(client))
                EntityDatas[caller].Playerhit.Add(client);

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

                        if (boss.MathCounterHitMode == 1)
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

                foreach (var boss in hpBarBosses)
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

                        if(boss.MathCounterHitMode == 1)
                        {
                            boss.Health = (int)Math.Round((hp - prop.Min) + (boss.IteratorValue * boss.BackupValue));
                        }

                        else
                        {
                            boss.Health = (int)Math.Round((prop.Max - hp) + (boss.IteratorValue * boss.BackupValue));
                        }

                        if(boss.MaxHealth < boss.Health)
                            boss.MaxHealth = boss.Health;

                        activeBosses.Add(boss);
                    }
                }
            }

            if (hp > 500000)
                return HookResult.Continue;

            ClientDisplayDatas[client].EntitiyHit = caller;
            ClientDisplayDatas[client].BossName = caller.Entity.Name;
            ClientDisplayDatas[client].BossHP = hp;
            ClientDisplayDatas[client].LastShootHitBox = Server.EngineTime;

            // Server.PrintToChatAll($"activator = {activator.DesignerName} | caller = {caller.DesignerName}");

            return HookResult.Continue;
        }

        public HookResult BreakableOut(CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller, CVariant value, float delay)
        {
            if (activator == null)
                return HookResult.Continue;

            var client = player(activator);

            if (client == null)
                return HookResult.Continue;

            if (!activator.IsValid || !ClientDisplayDatas.ContainsKey(client))
                return HookResult.Continue;

            if (!caller.IsValid)
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

            if (hp < 99999)
            {

                if (!EntityDatas[caller].Playerhit.Contains(client))
                    EntityDatas[caller].Playerhit.Add(client);

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

                ClientDisplayDatas[client].EntitiyHit = caller;
                ClientDisplayDatas[client].BossName = caller.Entity.Name;
                ClientDisplayDatas[client].BossHP = hp;
                ClientDisplayDatas[client].LastShootHitBox = Server.EngineTime;

                //Server.PrintToChatAll($"{caller.Entity.Name}: {hp}");
            }

            return HookResult.Continue;
        }

        public HookResult Hitbox_Hook(CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller, CVariant value, float delay)
        {
            if (activator == null)
                return HookResult.Continue;

            var client = player(activator);

            if (client == null)
                return HookResult.Continue;

            if (!activator.IsValid || !ClientDisplayDatas.ContainsKey(client))
                return HookResult.Continue;

            if (!caller.IsValid)
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

            if (hp < 99999)
            {
                if (!EntityDatas[caller].Playerhit.Contains(client))
                    EntityDatas[caller].Playerhit.Add(client);

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

                ClientDisplayDatas[client].EntitiyHit = caller;
                ClientDisplayDatas[client].BossName = caller.Entity.Name;
                ClientDisplayDatas[client].BossHP = hp;
                ClientDisplayDatas[client].LastShootHitBox = Server.EngineTime;

                //Server.PrintToChatAll($"{caller.Entity.Name}: {hp}");
            }

            return HookResult.Continue;
        }

        public void OnGameFrame()
        {
            // proceed check for entity hit.
            EntityOnFrame();

            if (activeBosses != null && activeBosses.Count <= 0)
                BossHPOnFrame();
        }

        private void EntityOnFrame()
        {
            // if there is bossHP active now, don't show until it's clear;
            if (activeBosses != null && activeBosses.Count > 0)
                return;

            // why bother to show if there is no one shooting it.
            if (EntityDatas != null && EntityDatas.Count < 0)
                return;

            foreach(var entity in EntityDatas.Values)
            {
                // better check than let it pass through
                if (entity == null)
                    continue;

                // is entity name on config file?
                if(IsEntityInBossHP(entity.Entity))
                    continue;

                // get player count that hit this entity.
                var playerCount = entity.Playerhit.Count;

                // compare player actual hit number with all ct player dived by 2.
                bool overCount = playerCount > Utilities.GetPlayers().Where(player => player.Team == CsTeam.CounterTerrorist).Count() / 2;

                // if there is a lot player hitting boss, then show them all!
                if (overCount && entity.LastHit > Server.EngineTime - 5.0f)
                    Print_BHudAll(entity);

                if (entity.Playerhit.Count < 0)
                    continue;

                // Remove Player from list of Entity hit so they can count.
                foreach(var player in entity.Playerhit)
                {
                    if(ClientDisplayDatas.ContainsKey(player))
                    {
                        var clientData = ClientDisplayDatas[player];

                        if(clientData.EntitiyHit == entity.Entity)
                        {
                            // player doesn't hit entity more than 2 seconds then remove it.
                            if (clientData.LastShootHitBox < Server.EngineTime - 5.0f)
                                entity.Playerhit.Remove(player);

                            // if not then
                            else
                            {
                                // if no player hit it enough then let's just print hp to specific player that hit it.
                                if(!overCount && clientData.LastShootHitBox > Server.EngineTime - 5.0f)
                                    Print_BHudGlobal(player, entity);
                            }
                        }
                    }
                }
            }
        }

        private void BossHPOnFrame()
        {
            // There is no active bosses then why show lol. 
            if (activeBosses.Count <= 0)
                return;

            Print_BossHP();
        }

        int PlayerCount;

        private void Print_BHudGlobal(CCSPlayerController client, EntityData data)
        { 
            if (client == null) return;

            client.PrintToCenterHtml($"{data.Name}: {data.Health}");
        }

        private void Print_BHudAll(EntityData data)
        {
            PrintToCenterHtmlAll($"{data.Name}: {data.Health}");
        }

        private void Print_BHudLocal(CCSPlayerController client, ClientDisplayData data)
        {
            if (client == null) return;

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

        public bool IsEntityInBossHP(CEntityInstance entity)
        {
            if (entity == null) return false;

            foreach (var boss in breakableBosses)
            {
                if(entity.Entity.Name == boss.BreakableEntityName)
                    return true;
            }

            foreach (var boss in mathCounterBosses)
            {
                if(entity.Entity.Name == boss.MathCounterName)
                    return true;
            }

            foreach (var boss in hpBarBosses)
            {
                if (entity.Entity.Name == boss.MathCounterName)
                    return true;
            }

            return false;
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
                // player null lol
                if (player == null) continue;

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

public class CEntityOutputTemplate_float : NativeObject
{
    public CEntityOutputTemplate_float(IntPtr pointer) : base(pointer) { }
    public unsafe float OutValue => Unsafe.Add(ref *(float*)Handle, 6);
}
