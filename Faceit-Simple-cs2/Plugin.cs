using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace FaceitSimpleCs2;

// Port of the CS:GO SourceMod plugin "Faceit Warmup" 2.0 (faceit_warmup.sp) to CounterStrikeSharp.
// On warmup: money is set to StartMoney (after every purchase or after spawn),
// dropped weapons are removed every N seconds, mp_weapons_allow_typecount is unlimited.
// sm_fw_warmup_alltalk of the original is dropped on purpose.
public partial class Plugin : BasePlugin
{
    public override string ModuleName => "Faceit-Simple-cs2";
    public override string ModuleVersion => "0.1.1";
    public override string ModuleAuthor => "Rimmer";
    public override string ModuleDescription => "Faceit-style warmup, knife round and side vote";

    public FaceitSimpleConfig Config { get; private set; } = new();

    private string ConfigDir =>
        Path.GetFullPath(Path.Combine(ModuleDirectory, "..", "..", "configs", "plugins", ModuleName));

    private Timer? _cleanupTimer;
    private bool _isWarmup;

    public override void Load(bool hotReload)
    {
        Config = ConfigFile.Load(ConfigDir, Logger);

        RegisterListener<Listeners.OnMapStart>(_ =>
        {
            _isWarmup = false;
            MatchReset();
            HudReset();
            StartCleanupTimer();
            StartTick();
        });
        RegisterListener<Listeners.OnMapEnd>(() =>
        {
            StopCleanupTimer();
            MatchReset();
            HudReset();
        });
        RegisterListener<Listeners.OnClientDisconnect>(slot =>
        {
            HudForgetSlot(slot);
            _votes.Remove(slot);
            if (slot >= 0 && slot < _released.Length)
            {
                _released[slot] = false;
                _offered[slot] = false;
                _teamAt[slot] = 0f;
            }
        });
        RegisterListener<Listeners.OnCustomHudClicked>(OnHudClick);
        RegisterVoteCommands();

        StartCleanupTimer();
        StartTick();
        if (hotReload)
        {
            Server.NextFrame(() =>
            {
                ApplyRoundState();
                // Joined in the middle of a match: nothing to vote about.
                if (!IsWarmupNow()) State = Phase.Done;
            });
        }
    }

    public override void Unload(bool hotReload)
    {
        StopCleanupTimer();
        _tick?.Kill();
        _tick = null;
        MatchReset();
        HudRemove();
    }

    private Timer? _tick;

    private void StartTick()
    {
        _tick?.Kill();
        _tick = AddTimer(0.25f, () =>
        {
            MatchTick();
            VoteTick();
        }, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);
    }

    // ---------------------------------------------------------------- warmup state

    private static CCSGameRules? GameRules()
    {
        try
        {
            return Utilities.GetGameRules();
        }
        catch
        {
            return null;
        }
    }

    private static bool IsWarmupNow()
    {
        try
        {
            return GameRules()?.WarmupPeriod ?? false;
        }
        catch
        {
            return false;
        }
    }

    // eventRoundStart: mp_weapons_allow_typecount -1 on warmup, 2 after it.
    private void ApplyRoundState()
    {
        _isWarmup = IsWarmupNow();
        if (!Config.Enabled || !Config.ControlTypeCount) return;
        int value = _isWarmup ? Config.WarmupTypeCount : Config.MatchTypeCount;
        Server.ExecuteCommand($"mp_weapons_allow_typecount {value}");
        Dbg("round start: warmup={Warmup}, mp_weapons_allow_typecount {Value}", _isWarmup, value);
    }

    [GameEventHandler]
    public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        ApplyRoundState();
        MatchRoundStart();
        // A round restart removes the entity; a new one is made by the next vote tick.
        if (!HudAlive) HudReset();
        return HookResult.Continue;
    }

    // ---------------------------------------------------------------- money

    [GameEventHandler]
    public HookResult OnItemPurchase(EventItemPurchase @event, GameEventInfo info)
    {
        if (Config.Enabled && Config.MoneyMode == 0)
            SetWarmupMoneyNextFrame(@event.Userid);
        return HookResult.Continue;
    }

    [GameEventHandler(HookMode.Pre)]
    public HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        if (Config.Enabled && Config.MoneyMode == 1)
            SetWarmupMoneyNextFrame(@event.Userid);
        return HookResult.Continue;
    }

    // The game writes the money itself right after the event, so the value is set on the next frame.
    private void SetWarmupMoneyNextFrame(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid) return;
        uint index = player.Index;
        Server.NextFrame(() =>
        {
            var p = Utilities.GetPlayerFromIndex((int)index);
            if (p == null || !p.IsValid || !IsWarmupNow()) return;
            if (p.TeamNum <= (byte)CsTeam.Spectator) return;
            var money = p.InGameMoneyServices;
            if (money == null) return;
            money.Account = Config.StartMoney;
            Utilities.SetStateChanged(p, "CCSPlayerController", "m_pInGameMoneyServices");
        });
    }

    // ---------------------------------------------------------------- dropped weapons

    private void StartCleanupTimer()
    {
        StopCleanupTimer();
        if (Config.DeleteDroppedInterval <= 0f) return;
        _cleanupTimer = AddTimer(Config.DeleteDroppedInterval, RemoveDroppedWeapons,
            TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);
    }

    private void StopCleanupTimer()
    {
        _cleanupTimer?.Kill();
        _cleanupTimer = null;
    }

    // timerXUY: every weapon_* / item_* without an owner is removed while the warmup lasts.
    private void RemoveDroppedWeapons()
    {
        if (!Config.Enabled || !IsWarmupNow()) return;

        var victims = new List<CBaseEntity>();
        foreach (var entity in Utilities.GetAllEntities())
        {
            string? name = entity.DesignerName;
            if (name == null) continue;
            if (!name.StartsWith("weapon_", StringComparison.Ordinal) &&
                !name.StartsWith("item_", StringComparison.Ordinal)) continue;

            var item = entity.As<CBaseEntity>();
            if (item.OwnerEntity.IsValid) continue;
            victims.Add(item);
        }

        foreach (var item in victims)
        {
            if (item.IsValid) item.Remove();
        }
        if (victims.Count > 0) Dbg("removed {Count} dropped weapons", victims.Count);
    }

    // ---------------------------------------------------------------- server console

    [ConsoleCommand("css_faceitsimple", "Faceit-Simple-cs2 status; css_faceitsimple reload = read the config again")]
    [CommandHelper(whoCanExecute: CommandUsage.SERVER_ONLY)]
    public void OnStatus(CCSPlayerController? caller, CommandInfo info)
    {
        if (info.ArgCount > 1 && info.GetArg(1).Equals("reload", StringComparison.OrdinalIgnoreCase))
        {
            Config = ConfigFile.Load(ConfigDir, Logger);
            StartCleanupTimer();
            ApplyRoundState();
            info.ReplyToCommand($"[Faceit-Simple-cs2] {ConfigFile.FileName} reloaded.");
            return;
        }

        info.ReplyToCommand($"[Faceit-Simple-cs2] {ModuleVersion} enabled={Config.Enabled} warmup={IsWarmupNow()}");
        info.ReplyToCommand($"  StartMoney={Config.StartMoney} MoneyMode={Config.MoneyMode} DeleteDroppedInterval={Config.DeleteDroppedInterval}");
        info.ReplyToCommand($"  ControlTypeCount={Config.ControlTypeCount} Warmup={Config.WarmupTypeCount} Match={Config.MatchTypeCount}");
        info.ReplyToCommand($"  KnifeRoundMode={Config.KnifeRoundMode} phase={State} warmupLeft={WarmupLeft():0.0} votes={_votes.Count} winner={_knifeWinner} hud={(HudAlive ? "alive" : "none")}");
    }

    private void Dbg(string msg, params object?[] args)
    {
        if (Config.Debug) Logger.LogInformation(msg, args);
    }
}
