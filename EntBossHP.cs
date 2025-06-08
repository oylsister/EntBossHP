using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.UserMessages;
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

        public static double CurrentTime = 0;
        public static double[] LastShootHitbox = new double[65];
        public static double LastForceShowBossHP = 0;
        public static CBaseEntity[] LastShootBreakable = new CBaseEntity[65];

        public override void Load(bool hotReload)
        {
            HookEntityOutput("math_counter", "OutValue", CounterOut);
            HookEntityOutput("func_physbox_multiplayer", "OnDamaged", BreakableOut);
            HookEntityOutput("func_physbox", "OnHealthChanged", BreakableOut);
            HookEntityOutput("func_breakable", "OnHealthChanged", BreakableOut);
            HookEntityOutput("prop_dynamic", "OnHealthChanged", Hitbox_Hook);

            RegisterEventHandler<EventRoundStart>(OnRoundStart);
            //RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
            RegisterListener<OnClientDisconnect>(OnClientDisconnect);
            RegisterListener<OnMapStart>(MapStart);
            RegisterListener<OnEntityCreated>(OnEntityCreated);
        }

        public void MapStart(string mapname)
        {
            CurrentTime = 0;
            LastForceShowBossHP = 0;
        }

        public void OnClientDisconnect(int playerslot)
        {
            LastShootBreakable[playerslot] = null;
            LastShootHitbox[playerslot] = 0;
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
            if(activator == null || !activator.IsValid)
                return HookResult.Continue;

            if(caller == null || !caller.IsValid)
                return HookResult.Continue;

            var client = player(activator);

            if(client == null)
                return HookResult.Continue;

            if(client.Slot <= 65)
            {
                if(LastShootHitbox[client.Slot] < Server.EngineTime - 0.1)
                    return HookResult.Continue;

                var entityname = caller.Entity.Name;

                if (string.IsNullOrEmpty(entityname) || string.IsNullOrWhiteSpace(entityname))
                    entityname = "HP";

                CMathCounter prop = new(caller.Handle);
                var values = (int)Math.Round(GetMathCounterValue(caller.Handle));

                LastShootBreakable[client.Slot] = prop;

                if(values > 0)
                {
                    Print_BHud(client, prop, entityname, values);
                }
            }

            return HookResult.Continue;
        }

        public HookResult BreakableOut(CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller, CVariant value, float delay)
        {
            if(activator == null || !activator.IsValid)
                return HookResult.Continue;

            if(caller == null || !caller.IsValid)
                return HookResult.Continue;

            var client = player(activator);

            if(client == null)
                return HookResult.Continue;

            LastShootHitbox[client.Slot] = Server.EngineTime;

            if(client.Slot <= 65)
            {
                if(LastShootHitbox[client.Slot] < Server.EngineTime - 0.1)
                    return HookResult.Continue;

                var entityname = caller.Entity.Name;

                if (string.IsNullOrEmpty(entityname) || string.IsNullOrWhiteSpace(entityname))
                    entityname = "HP";

                CBreakable prop = new(caller.Handle);
                var values = prop.Health;

                if(values > 0 && values < 99999)
                {
                    // we set specific entity here after confirm that is not 
                    LastShootBreakable[client.Slot] = prop;
                    Print_BHud(client, prop, entityname, values);
                }
            }

            return HookResult.Continue;
        }

        public HookResult Hitbox_Hook(CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller, CVariant value, float delay)
        {
            if(activator == null || !activator.IsValid)
                return HookResult.Continue;

            if(caller == null || !caller.IsValid)
                return HookResult.Continue;

            var client = player(activator);

            if(client == null)
                return HookResult.Continue;

            LastShootHitbox[client.Slot] = Server.EngineTime;

            if(client.Slot <= 65)
            {
                if(LastShootHitbox[client.Slot] < Server.EngineTime - 0.1)
                    return HookResult.Continue;

                var entityname = caller.Entity.Name;

                if (string.IsNullOrEmpty(entityname) || string.IsNullOrWhiteSpace(entityname))
                    entityname = "HP";

                CBreakable prop = new(caller.Handle);
                var values = prop.Health;

                if(values > 0 && values < 99999)
                {
                    // we set specific entity here after confirm that is not 
                    LastShootBreakable[client.Slot] = prop;
                    Print_BHud(client, prop, entityname, values);
                }
            }

            return HookResult.Continue;
        }

        public void Print_BHud(CCSPlayerController client, CBaseEntity entity, string name, int hp)
        {
            CurrentTime = Server.EngineTime;

            if(LastShootHitbox[client.Slot] > CurrentTime - 3.0 && LastForceShowBossHP + 0.1 < CurrentTime || hp == 0)
            {
                LastForceShowBossHP = CurrentTime;
                int count = 0;
                int CTCount = 0;

                for(int i = 0; i < Server.MaxPlayers + 1; i++)
                {
                    CCSPlayerController player = Utilities.GetPlayerFromSlot(i);

                    if(player == null || !player.IsValid || player.Connected != PlayerConnectedState.PlayerConnected)
                        continue;
                        
                    if(player.Team == CsTeam.CounterTerrorist)
                    {
                        CTCount++;

                        if(LastShootHitbox[player.Slot] > CurrentTime - 7.0 && LastShootBreakable[player.Slot] == entity)
                            count++;
                    }
                }

                // if there are a lot of player shooting at it.
                if(count > CTCount / 2)
                {
                    PrintToCenterAll($"{name}: {hp}");
                }

                // just showing at single player.
                else
                {
                    for(int i = 0; i < Server.MaxPlayers + 1; i++)
                    {
                        CCSPlayerController player = Utilities.GetPlayerFromSlot(i);

                        if(player == null || !player.IsValid || player.Connected != PlayerConnectedState.PlayerConnected)
                            continue;

                        if(LastShootHitbox[player.Slot] > CurrentTime - 7.0 && LastShootBreakable[player.Slot] == entity)
                            player.PrintToCenter($"{name}: {hp}");
                    }
                }

                LastForceShowBossHP = CurrentTime;
            }
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
            var rp = new RecipientFilter();
            rp.AddAllPlayers();

            var um = UserMessage.FromId(323);
            um.Recipients = rp;
            um.SetString("message", text);
            um.Send();
        }

        private static unsafe float GetMathCounterValue(nint handle)
        {
            var offset = Schema.GetSchemaOffset("CMathCounter", "m_OutValue");
            return *(float*)IntPtr.Add(handle, offset + 24);
        }
    }
}
