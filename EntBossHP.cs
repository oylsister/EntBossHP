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
        public override string ModuleVersion => "1.5";
        public override string ModuleAuthor => "Oylsister, Credits to Kxrnl, DarkerZ [RUS]";

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
        }

        public void MapStart(string mapname)
        {

        }

        public HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
        {
            if (@event.Userid.IsBot || @event.Userid.IsHLTV)
                return HookResult.Continue;

            
            return HookResult.Continue;
        }

        public void OnClientDisconnect(int playerslot)
        {
            var client = Utilities.GetPlayerFromSlot(playerslot);

            if (client.IsBot || client.IsHLTV)
                return;

            
        }

        public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
        {

            return HookResult.Continue;
        }

        public void OnEntityCreated(CEntityInstance entity)
        {

        }

        public HookResult CounterOut(CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller, CVariant value, float delay)
        {
            if (caller == null)
                return HookResult.Continue;

            if (activator.DesignerName != "player")
                return HookResult.Continue;

            var client = player(activator);

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
                            boss.MaxHealth = (int)Math.Round(prop.Max);
                            boss.Health = boss.MaxHealth - values;
                        }

                        if (boss.LastHP > boss.Health)
                        {
                            Print_BossHP();

                            if (activator != null && client != null && activeBosses.ContainsKey(caller.Entity.Name))
                                Print_SingleBossHP(client, activeBosses[caller.Entity.Name]);
                        }

                        boss.LastHP = boss.Health;

                        if (!activeBosses.ContainsKey(boss.MathCounterName))
                        {
                            //Server.PrintToChatAll($"{caller.Entity.Name} get added to list!");
                            activeBosses.Add(boss.MathCounterName, boss);
                        }

                        else
                        {
                            //Server.PrintToChatAll($"{caller.Entity.Name} get updated");
                            activeBosses[boss.MathCounterName].Health = boss.Health;
                            activeBosses[boss.MathCounterName].MaxHealth = boss.MaxHealth;
                            activeBosses[boss.MathCounterName].LastHit = boss.LastHit;
                        }
                    }
                }

                foreach (var boss in hpBarBosses)
                {
                    if (caller.Entity.Name == boss.MathCounterName)
                    {
                        boss.MathCounterEntity = caller;
                        boss.LastHit = Server.EngineTime;

                        if (boss.MathCounterHitMode == 1)
                        {
                            boss.Health = (int)Math.Round((values - prop.Min) + ((boss.IteratorValue - 1) * boss.BackupValue));
                        }

                        else
                        {
                            boss.Health = (int)Math.Round((prop.Max - values) + ((boss.IteratorValue - 1) * boss.BackupValue));
                        }

                        if (boss.MaxHealth < boss.Health)
                            boss.MaxHealth = boss.Health;

                        if (boss.LastHP > boss.Health)
                        {
                            Print_BossHP();

                            if (activator != null && client != null && activeBosses.ContainsKey(caller.Entity.Name))
                                Print_SingleBossHP(client, boss);
                        }

                        boss.LastHP = boss.Health;

                        if (!activeBosses.ContainsKey(boss.MathCounterName))
                            activeBosses.Add(boss.MathCounterName, boss);

                        else
                        {
                            activeBosses[boss.MathCounterName].Health = boss.Health;
                            activeBosses[boss.MathCounterName].MaxHealth = boss.MaxHealth;
                            activeBosses[boss.MathCounterName].LastHit = boss.LastHit;
                        }
                    }

                    if (caller.Entity.Name == boss.IteratorName)
                    {
                        if (boss.IteratorHitMode == 1)
                            boss.IteratorValue = values - prop.Min;

                        else
                            boss.IteratorValue = prop.Max - values;
                    }

                    if (caller.Entity.Name == boss.BackupName)
                    {
                        boss.BackupValue = values;
                    }
                }
            }

            if (!EntityDatas.ContainsKey(caller))
                EntityDatas.Add(caller, new(caller));

            EntityDatas[caller].Name = entityname;
            EntityDatas[caller].Health = values;
            EntityDatas[caller].LastHit = Server.EngineTime;

            if (activator == null)
                return HookResult.Continue;

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

            if (activator == null)
                return HookResult.Continue;

            var client = player(activator);

            if (client == null)
                return HookResult.Continue;

            CBreakable prop = new CBreakable(caller.Handle);

            ClientDisplayDatas[client].LastShootHitBox = Server.EngineTime;

            var entityname = caller.Entity.Name;

            if (string.IsNullOrEmpty(entityname) || string.IsNullOrWhiteSpace(entityname))
                entityname = "HP";

            if (!prop.IsValid || prop == null)
                return HookResult.Continue;

            var hp = prop!.Health;

            if (hp <= 0)
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
                                hp = 0;
                            }

                            boss.BreakableEntity = caller;
                            boss.LastHit = Server.EngineTime;
                            boss.Health = hp;

                            if (boss.Health > boss.MaxHealth)
                            {
                                boss.MaxHealth = boss.Health;
                            }

                            if (boss.LastHP > boss.Health)
                            {
                                Print_BossHP();

                                if (activator != null && client != null && activeBosses.ContainsKey(caller.Entity.Name))
                                    Print_SingleBossHP(client, boss);
                            }

                            boss.LastHP = boss.Health;

                            if (!activeBosses.ContainsKey(boss.BreakableEntityName))
                                activeBosses.Add(boss.BreakableEntityName, boss);

                            else
                            {
                                activeBosses[boss.BreakableEntityName].Health = boss.Health;
                                activeBosses[boss.BreakableEntityName].MaxHealth = boss.MaxHealth;
                                activeBosses[boss.BreakableEntityName].LastHit = boss.LastHit;
                            }
                        }
                    }
                }

                if (!EntityDatas.ContainsKey(caller))
                    EntityDatas.Add(caller, new(caller));

                // entity section
                EntityDatas[caller].Name = entityname;
                EntityDatas[caller].Health = hp;
                EntityDatas[caller].LastHit = Server.EngineTime;

                if (activator == null)
                    return HookResult.Continue;

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

            var client = player(activator);

            CBreakable prop = new CBreakable(caller.Handle);

            ClientDisplayDatas[client].LastShootHitBox = Server.EngineTime;

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
                                hp = 0;
                            }

                            boss.BreakableEntity = caller;
                            boss.LastHit = Server.EngineTime;
                            boss.Health = hp;

                            if (boss.Health > boss.MaxHealth)
                            {
                                boss.MaxHealth = boss.Health;
                            }

                            if (boss.LastHP > boss.Health)
                            {
                                Print_BossHP();

                                if (activator != null && client != null && activeBosses.ContainsKey(caller.Entity.Name))
                                    Print_SingleBossHP(client, boss);
                            }

                            boss.LastHP = boss.Health;

                            if (!activeBosses.ContainsKey(boss.BreakableEntityName))
                                activeBosses.Add(boss.BreakableEntityName, boss);

                            else
                            {
                                activeBosses[boss.BreakableEntityName].Health = boss.Health;
                                activeBosses[boss.BreakableEntityName].MaxHealth = boss.MaxHealth;
                                activeBosses[boss.BreakableEntityName].LastHit = boss.LastHit;
                            }
                        }
                    }
                }

                if (!EntityDatas.ContainsKey(caller))
                    EntityDatas.Add(caller, new(caller));

                // entity section
                EntityDatas[caller].Name = entityname;
                EntityDatas[caller].Health = hp;
                EntityDatas[caller].LastHit = Server.EngineTime;

                if (activator == null)
                    return HookResult.Continue;

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

                if (activeBosses == null || (activeBosses != null && activeBosses.Count < 1))
                    Print_BHud(EntityDatas[caller]);

                //Server.PrintToChatAll($"{caller.Entity.Name}: {hp}");
            }

            return HookResult.Continue;
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
            foreach (var player in Utilities.GetPlayers())
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
    }
}

public class CEntityOutputTemplate_float : NativeObject
{
    public CEntityOutputTemplate_float(IntPtr pointer) : base(pointer) { }
    public unsafe float OutValue => Unsafe.Add(ref *(float*)Handle, 6);
}
