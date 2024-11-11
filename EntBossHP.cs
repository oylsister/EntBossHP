using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
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
        public override string ModuleVersion => "1.4";
        public override string ModuleAuthor => "Oylsister, Credits to Kxrnl, DarkerZ [RUS]";

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

        public FakeConVar<bool> cvarEnableBhud = new("css_bosshp_enablebhud", "Enable bhud to print all entity that get damaged", true, ConVarFlags.FCVAR_NONE);

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
            RegisterListener<OnMapStart>(MapStart);
            RegisterListener<OnEntityCreated>(OnEntityCreated);

            AddCommand("boss_list", "", CommandBossList);
            // AddCommand("boss_text", "", CommandBossText);

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
                boss.Type = BossType.MathCounter;

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
                boss.Type = BossType.HPBar;

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

                hpBarBosses.Add(boss);
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

            if (configLoaded)
            {
                Server.PrintToChatAll($" {ChatColors.Olive}[{ChatColors.Lime}EntBossHP{ChatColors.Olive}] {ChatColors.White}The current map is supported by this plugin.");

                if (activeBosses != null)
                    activeBosses.Clear();

                ResetBossHP();
            }

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

        private void CommandBossList(CCSPlayerController client, CommandInfo info)
        {
            foreach(var boss in BossConfigs.MathCounterList)
            {
                info.ReplyToCommand($"Name: {boss.Name} | Counter: {boss.MathCounter} | Mode: {boss.MathCounterMode}");
            }

            foreach(var boss in mathCounterBosses)
            {
                info.ReplyToCommand($"Name: {boss.BossName} | Counter: {boss.MathCounterName} | Mode: {boss.MathCounterHitMode}");
            }
        }

        /*
        private void CommandBossText(CCSPlayerController client, CommandInfo info)
        {
            int size = int.Parse(info.GetArg(1));

            var hp = "BossName: 5000";
            var bar = "■■■■■■■■□□";

            //client.PrintToCenterHtml($"<span class=\"fontSize-m\">{hp}</span><br><span class=\"fontSize-l\">{bar}</span>", 5);

            EventShowSurvivalRespawnStatus eventShowSurvivalRespawnStatus = new EventShowSurvivalRespawnStatus(force: true);
            eventShowSurvivalRespawnStatus.LocToken = $"<span class=\"fontSize-m\">{hp}</span><br><span class=\"fontSize-l\">{bar}</span>";
            eventShowSurvivalRespawnStatus.Duration = 5;
            eventShowSurvivalRespawnStatus.Userid = client;
            eventShowSurvivalRespawnStatus.FireEventToClient(client);

            info.ReplyToCommand($"<span class=\"fontSize-m\">{hp}</span><br><span class=\"fontSize-l\">{bar}</span>");
        }
        */

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
                    var counter = new CMathCounter(entity.Handle);

                    boss.MathCounterStartValue = (int)Math.Round(GetMathCounterValue(entity.Handle));
                    boss.MathCounterMaxValue = (int)Math.Round(counter.Max);
                    boss.MathCounterMinValue = (int)Math.Round(counter.Min);

                    // if math_counter increment when get hit.
                    if (boss.MathCounterHitMode != 1 && boss.MathCounterHitMode != 2)
                    {
                        // check if the outvalue is same as hitmin or not.
                        if (counter.HitMin)
                            boss.MathCounterHitMode = 2;

                        else
                            boss.MathCounterHitMode = 1;
                    }
                }
            }

            foreach (var boss in hpBarBosses)
            {
                if (entity.Entity.Name == boss.MathCounterName)
                {
                    boss.MathCounterEntity = entity;
                    var counter = new CMathCounter(entity.Handle);

                    boss.MathCounterStartValue = (int)Math.Round(GetMathCounterValue(entity.Handle));
                    boss.MathCounterMaxValue = (int)Math.Round(counter.Max);
                    boss.MathCounterMinValue = (int)Math.Round(counter.Min);

                    if (boss.MathCounterHitMode != 1 && boss.MathCounterHitMode != 2)
                    {
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
                        if (iteratorMath.HitMin)
                        {
                            boss.IteratorHitMode = 2;
                            boss.IteratorValue = iteratorMath.Max;
                        }

                        else
                        {
                            boss.IteratorHitMode = 1;
                            boss.IteratorValue = GetMathCounterValue(entity.Handle);
                        }
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
            if (caller == null)
                return HookResult.Continue;

            if(activator.DesignerName != "player")
                return HookResult.Continue;

            var entityname = caller.Entity.Name;

            if (string.IsNullOrEmpty(entityname) || string.IsNullOrWhiteSpace(entityname))
                entityname = "HP";

            CMathCounter prop = new(caller.Handle);

            //var hp = (int)Math.Round(GetMathCounterValue(caller.Handle));
            var TheOutput = new CEntityOutputTemplate_float(output.Handle);
            var values = (int)Math.Round(TheOutput.OutValue);

            //Server.PrintToChatAll($"{caller.Entity.Name}: {values}");

            // Boss Data section
            if (configLoaded)
            {
                foreach (var boss in mathCounterBosses)
                {
                    if (caller.Entity.Name == boss.MathCounterName)
                    {
                        if (values == 0)
                        {
                            //Server.PrintToChatAll($"{caller.Entity.Name} is 0");

                            if (activeBosses.Contains(boss))
                            {
                                //Server.PrintToChatAll($"{caller.Entity.Name} get removed!");
                                activeBosses.Remove(boss);
                            }

                            continue;
                        }

                        boss.MathCounterEntity = caller;
                        boss.LastHit = Server.EngineTime;

                        if (boss.MathCounterHitMode == 1)
                        {
                            boss.Health = values;

                            if (boss.Health > boss.MaxHealth)
                            {
                                boss.MaxHealth = boss.Health;
                            }
                        }
                        else
                        {
                            boss.Health = boss.MaxHealth - values;
                        }

                        if (!activeBosses.Contains(boss))
                        {
                            //Server.PrintToChatAll($"{caller.Entity.Name} get added to list!");
                            activeBosses.Add(boss);
                        }

                        else
                        {
                            //Server.PrintToChatAll($"{caller.Entity.Name} get updated");
                            var index = activeBosses.IndexOf(boss);
                            activeBosses[index].Health = boss.Health;
                            activeBosses[index].MaxHealth = boss.MaxHealth;
                            activeBosses[index].LastHit = boss.LastHit;
                        }
                    }
                }

                foreach (var boss in hpBarBosses)
                {
                    if (caller.Entity.Name == boss.MathCounterName)
                    {
                        if (values == 0)
                        {
                            if (activeBosses.Contains(boss))
                                activeBosses.Remove(boss);

                            continue;
                        }

                        boss.MathCounterEntity = caller;
                        boss.LastHit = Server.EngineTime;

                        if(boss.MathCounterHitMode == 1)
                        {
                            boss.Health = (int)Math.Round((values - prop.Min) + ((boss.IteratorValue - 1) * boss.BackupValue));
                        }

                        else
                        {
                            boss.Health = (int)Math.Round((prop.Max - values) + ((boss.IteratorValue - 1) * boss.BackupValue));
                        }

                        if(boss.MaxHealth < boss.Health)
                            boss.MaxHealth = boss.Health;

                        if (!activeBosses.Contains(boss))
                            activeBosses.Add(boss);

                        else
                        {
                            var index = activeBosses.IndexOf(boss);
                            activeBosses[index].Health = boss.Health;
                            activeBosses[index].MaxHealth = boss.MaxHealth;
                            activeBosses[index].LastHit = boss.LastHit;
                        }
                    }

                    if (caller.Entity.Name == boss.IteratorName)
                    {
                        if(boss.IteratorHitMode == 1)
                            boss.IteratorValue = values - prop.Min;

                        else
                            boss.IteratorValue = prop.Max - values;
                    }

                    if (caller.Entity.Name == boss.BackupName)
                    {
                        boss.BackupValue = values;
                    }
                }
                Print_BossHP();
            }

            if (!EntityDatas.ContainsKey(caller))
                EntityDatas.Add(caller, new(caller));

            EntityDatas[caller].Name = entityname;
            EntityDatas[caller].Health = values;
            EntityDatas[caller].LastHit = Server.EngineTime;

            if (activator == null)
                return HookResult.Continue;

            var client = player(activator);

            if (client == null)
                return HookResult.Continue;

            if (!activator.IsValid || !ClientDisplayDatas.ContainsKey(client))
                return HookResult.Continue;

            if (values > 500000)
                return HookResult.Continue;

            if (!EntityDatas[caller].Playerhit.Contains(client))
                EntityDatas[caller].Playerhit.Add(client);

            ClientDisplayDatas[client].EntitiyHit = caller;
            ClientDisplayDatas[client].BossName = caller.Entity.Name;
            ClientDisplayDatas[client].BossHP = values;
            ClientDisplayDatas[client].LastShootHitBox = Server.EngineTime;

            if (activeBosses == null || (activeBosses != null && activeBosses.Count < 1))
                Print_BHud(EntityDatas[caller]);

            // Server.PrintToChatAll($"activator = {activator.DesignerName} | caller = {caller.DesignerName}");

            return HookResult.Continue;
        }

        public HookResult BreakableOut(CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller, CVariant value, float delay)
        {
            if (caller == null)
                return HookResult.Continue;

            if (activator.DesignerName != "player")
                return HookResult.Continue;

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
                // Boss Data section.
                if (configLoaded)
                {
                    foreach (var boss in breakableBosses)
                    {
                        if (caller.Entity.Name == boss.BreakableEntityName)
                        {
                            if (hp <= 0)
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

                            if (!activeBosses.Contains(boss))
                                activeBosses.Add(boss);

                            else
                            {
                                var index = activeBosses.IndexOf(boss);
                                activeBosses[index].Health = boss.Health;
                                activeBosses[index].MaxHealth = boss.MaxHealth;
                                activeBosses[index].LastHit = boss.LastHit;
                            }
                        }
                    }
                    Print_BossHP();
                }

                if (!EntityDatas.ContainsKey(caller))
                    EntityDatas.Add(caller, new(caller));

                // entity section
                EntityDatas[caller].Name = entityname;
                EntityDatas[caller].Health = hp;
                EntityDatas[caller].LastHit = Server.EngineTime;

                if (activator == null)
                    return HookResult.Continue;

                var client = player(activator);

                if (client == null)
                    return HookResult.Continue;

                if (!activator.IsValid || !ClientDisplayDatas.ContainsKey(client))
                    return HookResult.Continue;

                if (!EntityDatas[caller].Playerhit.Contains(client))
                    EntityDatas[caller].Playerhit.Add(client);

                // Server.PrintToChatAll($"{caller.Entity.Name}: {hp}");

                ClientDisplayDatas[client].EntitiyHit = caller;
                ClientDisplayDatas[client].BossName = caller.Entity.Name;
                ClientDisplayDatas[client].BossHP = hp;
                ClientDisplayDatas[client].LastShootHitBox = Server.EngineTime;

            if (activeBosses == null || (activeBosses != null && activeBosses.Count < 1))
                Print_BHud(EntityDatas[caller]);

                //Server.PrintToChatAll($"{caller.Entity.Name}: {hp}");
            }

            return HookResult.Continue;
        }

        public HookResult Hitbox_Hook(CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller, CVariant value, float delay)
        {
            if (caller == null)
                return HookResult.Continue;

            if (activator.DesignerName != "player")
                return HookResult.Continue;

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
                // Boss Data section.
                if (configLoaded)
                {
                    foreach (var boss in breakableBosses)
                    {
                        if (caller.Entity.Name == boss.BreakableEntityName)
                        {
                            if (hp <= 0)
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

                            if (!activeBosses.Contains(boss))
                                activeBosses.Add(boss);

                            else
                            {
                                var index = activeBosses.IndexOf(boss);
                                activeBosses[index].Health = boss.Health;
                                activeBosses[index].MaxHealth = boss.MaxHealth;
                                activeBosses[index].LastHit = boss.LastHit;
                            }
                        }
                    }

                    Print_BossHP();
                }

                if (!EntityDatas.ContainsKey(caller))
                    EntityDatas.Add(caller, new(caller));

                // entity section
                EntityDatas[caller].Name = entityname;
                EntityDatas[caller].Health = hp;
                EntityDatas[caller].LastHit = Server.EngineTime;

                if (activator == null)
                    return HookResult.Continue;

                var client = player(activator);

                if (client == null)
                    return HookResult.Continue;

                if (!activator.IsValid || !ClientDisplayDatas.ContainsKey(client))
                    return HookResult.Continue;

                if (!EntityDatas[caller].Playerhit.Contains(client))
                    EntityDatas[caller].Playerhit.Add(client);

                // Server.PrintToChatAll($"{caller.Entity.Name}: {hp}");

                ClientDisplayDatas[client].EntitiyHit = caller;
                ClientDisplayDatas[client].BossName = caller.Entity.Name;
                ClientDisplayDatas[client].BossHP = hp;
                ClientDisplayDatas[client].LastShootHitBox = Server.EngineTime;

            if (activeBosses == null || (activeBosses != null && activeBosses.Count < 1))
                Print_BHud(EntityDatas[caller]);

                //Server.PrintToChatAll($"{caller.Entity.Name}: {hp}");
            }

            return HookResult.Continue;
        }

        private void Print_BHud(EntityData entity)
        {
            // if feature is not enabled then don't do it.
            if (!IsBhudEnabled())
                return;

            // if there is bossHP active now, don't show until it's clear;
            if (activeBosses != null && activeBosses.Count > 0)
                return;

            // why bother to show if there is no one shooting it.
            if (EntityDatas != null && EntityDatas.Count < 0)
                return;

            // better check than let it pass through
            if (entity == null)
                return;

            // get player count that hit this entity.
            var playerCount = entity.Playerhit.Count;

            // compare player actual hit number with all ct player dived by 2.
            bool overCount = playerCount > Utilities.GetPlayers().Where(player => player.Team == CsTeam.CounterTerrorist).Count() / 2;

            // if there is a lot player hitting boss, then show them all!
            if (overCount && entity.LastHit > Server.EngineTime - 5.0f)
                Print_BHudAll(entity);

            if (entity.Playerhit.Count < 0)
                return;

            var removeList = new List<CCSPlayerController>();

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
                        {
                            // add them to the list that need to be delete. because editing the list during the loop might cause error.
                            removeList.Add(player);
                        }

                        // if not then
                        else
                        {
                            // if no player hit it enough then let's just print hp to specific player that hit it.
                            if (!overCount && clientData.LastShootHitBox > Server.EngineTime - 5.0f)
                                Print_BHudClient(player, entity);
                        }
                    }
                }
            }

            // loop delete player.
            foreach(var player in removeList)
            {
                // nah just pass it over.
                if(player == null) continue;

                // found it, delete it.
                if(entity.Playerhit.Contains(player))
                    entity.Playerhit.Remove(player);
            }
        }

        private void Print_BHudClient(CCSPlayerController client, EntityData data)
        { 
            if (client == null) return;

            client.PrintToCenter($"{data.Name}: {data.Health}");
        }

        private void Print_BHudAll(EntityData data)
        {
            PrintToCenterAll($"{data.Name}: {data.Health}");
        }

        private void Print_BHudLocal(CCSPlayerController client, ClientDisplayData data)
        {
            if (client == null) return;

            client.PrintToCenter($"{data.BossName}: {data.BossHP}");
        }

        private void Print_BossHP()
        {
            if (activeBosses.Count < 1)
                return;

            string message = "";

            foreach (var boss in activeBosses)
            {
                var count = 0;
                if (activeBosses.Count > 1)
                {
                    var percent = boss.Health / (boss.MaxHealth / 100);
                    message += $"{boss.BossName} : {boss.Health} ({percent}%)";

                    if(count < activeBosses.Count - 1)
                    {
                        message += "\n";
                    }

                    count++;
                }
                else
                {
                    message += $"{boss.BossName} : {boss.Health}\n{CalculateHPBar(boss.Health, boss.MaxHealth)}";
                }
            }

            PrintToCenterAll(message);
        }

        private string CalculateHPBar(int hp, int maxhp)
        {
            // 800 / 10 = 80
            var ratio = (float)maxhp / 10;

            // 720 / 80 = 9
            var hpbar = hp / ratio;

            if (hpbar >= 10f)
                return "■■■■■■■■■■";

            else if (hpbar < 10f && hpbar >= 9f)
                return "■■■■■■■■■□";

            else if (hpbar < 9f && hpbar >= 8f)
                return "■■■■■■■■□□";

            else if (hpbar < 8f && hpbar >= 7f)
                return "■■■■■■■□□□";

            else if (hpbar < 7f && hpbar >= 6f)
                return "■■■■■■□□□□";

            else if (hpbar < 6f && hpbar >= 5f)
                return "■■■■■□□□□□";

            else if (hpbar < 5f && hpbar >= 4f)
                return "■■■■□□□□□□";

            else if (hpbar < 4f && hpbar >= 3f)
                return "■■■□□□□□□□";

            else if (hpbar < 3f && hpbar >= 2f)
                return "■■□□□□□□□□";

            else if (hpbar < 2f && hpbar > 0f)
                return "■□□□□□□□□□";

            else if (hpbar <= 0f)
                return "□□□□□□□□□□";

            return "□□□□□□□□□□";
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

        void PrintToCenterAll(string text)
        {
            foreach(var player in Utilities.GetPlayers())
            {
                // player null lol
                if (player == null) continue;

                player.PrintToCenter(text);
            }
        }

        private unsafe float GetMathCounterValue(nint handle)
        {
            var offset = Schema.GetSchemaOffset("CMathCounter", "m_OutValue");
            return *(float*)IntPtr.Add(handle, offset + 24);
        }

        private bool IsBhudEnabled()
        {
            return cvarEnableBhud.Value;
        }
    }
}

public class CEntityOutputTemplate_float : NativeObject
{
    public CEntityOutputTemplate_float(IntPtr pointer) : base(pointer) { }
    public unsafe float OutValue => Unsafe.Add(ref *(float*)Handle, 6);
}
