using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;

namespace FaceitSimpleCs2;

// Stage 2: knife vote at the end of the warmup -> knife round -> a paused warmup where the winners
// pick their side -> the match starts at 0:0 (mp_warmup_end resets the match).
public partial class Plugin
{
    internal enum Phase
    {
        Idle,         // warmup, no vote yet
        KnifeVote,    // warmup, "knife round?" on the card
        KnifeRound,   // first match round, knives only
        SideVote,     // paused warmup, the winners pick a side
        Done,         // the match is on; nothing until the next map
    }

    internal Phase State { get; private set; } = Phase.Idle;

    private bool _knifePending;                       // the round after the warmup is a knife round
    private CsTeam _knifeWinner = CsTeam.None;
    private float _sideVoteEnd;                       // Server.CurrentTime when the side vote ends
    private readonly Dictionary<int, int> _votes = new(); // slot -> 1 (yes / T) or 2 (no / CT)

    // Cvars changed for the knife round and the side vote warmup: name -> value before.
    private readonly Dictionary<string, string> _savedCvars = new(StringComparer.Ordinal);

    private void MatchReset()
    {
        // Map change or unload in the middle of the knife round: the remembered values come back.
        RestoreCvars();
        _knifeCvarNames.Clear();
        State = Phase.Idle;
        _knifePending = false;
        _knifeWinner = CsTeam.None;
        _votes.Clear();
    }

    private void SetPhase(Phase phase)
    {
        Dbg("phase {From} -> {To}", State, phase);
        State = phase;
        _votes.Clear();
        Array.Clear(_released);
    }

    // ---------------------------------------------------------------- cvars

    private void SetCvar(string name, string value)
    {
        if (!_savedCvars.ContainsKey(name))
        {
            string? before = null;
            try
            {
                ushort index = NativeAPI.GetConvarAccessIndexByName(name);
                if (index != 0) before = NativeAPI.GetConvarValueAsString(index);
            }
            catch { }
            if (before != null) _savedCvars[name] = before;
        }
        Server.ExecuteCommand($"{name} \"{value}\"");
    }

    private void RestoreCvar(string name)
    {
        if (_savedCvars.Remove(name, out var value))
            Server.ExecuteCommand($"{name} \"{value}\"");
    }

    private void RestoreCvars()
    {
        foreach (var (name, value) in _savedCvars)
            Server.ExecuteCommand($"{name} \"{value}\"");
        _savedCvars.Clear();
    }

    // ---------------------------------------------------------------- warmup and the knife vote

    private float WarmupLeft()
    {
        var rules = GameRules();
        if (rules == null || !rules.WarmupPeriod) return -1f;
        return rules.WarmupPeriodEnd - Server.CurrentTime;
    }

    // Called from the tick.
    private void MatchTick()
    {
        if (!Config.Enabled) return;
        bool warmup = IsWarmupNow();

        switch (State)
        {
            case Phase.Idle when warmup && Config.KnifeRoundMode == "vote":
            {
                float left = WarmupLeft();
                if (left > 0f && left <= Config.KnifeVoteBeforeEnd)
                {
                    SetPhase(Phase.KnifeVote);
                    AnnounceVote();
                }
                break;
            }
            case Phase.KnifeVote when !warmup:
                // warmup_end did not come (plugin reload, mp_warmup_end from another plugin).
                FinishKnifeVote();
                break;
            case Phase.SideVote:
                if (Server.CurrentTime >= _sideVoteEnd || AllWinnersVoted()) FinishSideVote();
                break;
        }
    }

    [GameEventHandler]
    public HookResult OnWarmupEnd(EventWarmupEnd @event, GameEventInfo info)
    {
        if (!Config.Enabled) return HookResult.Continue;
        if (State == Phase.KnifeVote) FinishKnifeVote();
        else if (State == Phase.Idle && Config.KnifeRoundMode == "always") PrepareKnifeRound();
        else if (State == Phase.Idle) State = Phase.Done;
        return HookResult.Continue;
    }

    private void FinishKnifeVote()
    {
        int yes = _votes.Values.Count(v => v == 1), no = _votes.Values.Count(v => v == 2);
        bool knife = yes > no || (yes == no && Config.KnifeVoteTieResult);
        Dbg("knife vote: yes={Yes} no={No} -> {Knife}", yes, no, knife);
        ChatAll(knife ? "chat.knife_yes" : "chat.knife_no", yes, no);
        if (knife) PrepareKnifeRound();
        else SetPhase(Phase.Done);
    }

    // Before the first round spawns: no C4, no pistols, no buying.
    private void PrepareKnifeRound()
    {
        SetPhase(Phase.KnifeRound);
        _knifePending = true;
        SetCvar("mp_give_player_c4", "0");
        SetCvar("mp_buytime", "0");
        SetCvar("mp_free_armor", "0");
        SetCvar("mp_t_default_secondary", "");
        SetCvar("mp_ct_default_secondary", "");
        SetCvar("mp_round_restart_delay", (Config.KnifeRoundEndDelay + 2f).ToString(System.Globalization.CultureInfo.InvariantCulture));

        // KnifeCvars (bhop by default). SetCvar remembers each value from before.
        _knifeCvarNames.Clear();
        foreach (var line in Config.KnifeCvars)
        {
            if (!ConfigFile.TryCvar(line, out var name, out var value)) continue;
            SetCvar(name, value);
            _knifeCvarNames.Add(name);
        }
    }

    private readonly List<string> _knifeCvarNames = new();

    // After the knife round: AfterKnifeCvars are set; a KnifeCvars cvar not listed there gets its
    // remembered value back. Empty AfterKnifeCvars - every KnifeCvars cvar gets its value back.
    private void ApplyAfterKnifeCvars()
    {
        var after = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in Config.AfterKnifeCvars)
        {
            if (!ConfigFile.TryCvar(line, out var name, out var value)) continue;
            _savedCvars.Remove(name);   // the configured value wins over the remembered one
            Server.ExecuteCommand($"{name} \"{value}\"");
            after.Add(name);
        }
        foreach (var name in _knifeCvarNames)
            if (!after.Contains(name)) RestoreCvar(name);
        _knifeCvarNames.Clear();
    }

    private bool KnifeHasBhop() => Config.KnifeCvars.Any(l =>
        ConfigFile.TryCvar(l, out var n, out var v) && v != "0" &&
        n is "sv_enablebunnyhopping" or "sv_autobunnyhopping");

    // ---------------------------------------------------------------- knife round

    private int _knifeRoundId;   // a new id per knife round start, so an old reset timer does nothing

    // Called from OnRoundStart.
    private void MatchRoundStart()
    {
        if (State != Phase.KnifeRound || !_knifePending || IsWarmupNow()) return;
        _knifePending = false;
        int id = ++_knifeRoundId;
        ChatAll("chat.knife_start");
        if (KnifeHasBhop()) ChatAll("chat.knife_bhop");
        foreach (var p in Utilities.GetPlayers()) KnifeOnly(p);

        // KnifeResetDelay: one more full reset a few seconds into the round. VIP and other plugins
        // give weapons, a healthshot, HP, armor and a helmet a moment after the spawn, and some
        // players grab a weapon right at the start; this takes all of it away once.
        if (Config.KnifeResetDelay > 0f)
        {
            AddTimer(Config.KnifeResetDelay, () =>
            {
                if (State != Phase.KnifeRound || _knifeRoundId != id || IsWarmupNow()) return;
                int count = 0;
                foreach (var p in Utilities.GetPlayers())
                    if (StripToKnife(p)) count++;
                Dbg("knife reset after {Delay} s: {Count} players", Config.KnifeResetDelay, count);
            });
        }
    }

    [GameEventHandler]
    public HookResult OnKnifeSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        if (State == Phase.KnifeRound && !IsWarmupNow()) KnifeOnly(@event.Userid);
        return HookResult.Continue;
    }

    // Next frame: the game gives the default loadout right after the spawn.
    private void KnifeOnly(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid) return;
        uint index = player.Index;
        Server.NextFrame(() =>
        {
            if (State != Phase.KnifeRound) return;
            StripToKnife(Utilities.GetPlayerFromIndex((int)index));
        });
    }

    // Everything but a knife goes; health, armor, helmet and money are set to the knife round values.
    private bool StripToKnife(CCSPlayerController? p)
    {
        if (p == null || !p.IsValid || !p.PawnIsAlive) return false;
        if (p.TeamNum <= (byte)CsTeam.Spectator) return false;
        var pawn = p.PlayerPawn.Value;
        if (pawn == null || !pawn.IsValid) return false;

        p.RemoveWeapons();
        p.GiveNamedItem("weapon_knife");

        pawn.Health = Config.KnifeHealth;
        Utilities.SetStateChanged(pawn, "CBaseEntity", "m_iHealth");

        pawn.ArmorValue = Config.KnifeArmor > 0 ? 100 : 0;
        Utilities.SetStateChanged(pawn, "CCSPlayerPawn", "m_ArmorValue");
        if (pawn.ItemServices != null)
        {
            var items = new CCSPlayer_ItemServices(pawn.ItemServices.Handle);
            items.HasHelmet = Config.KnifeArmor >= 2;
            items.HasDefuser = false;
            Utilities.SetStateChanged(pawn, "CBasePlayerPawn", "m_pItemServices");
        }

        if (p.InGameMoneyServices is { } money)
        {
            money.Account = 0;
            Utilities.SetStateChanged(p, "CCSPlayerController", "m_pInGameMoneyServices");
        }
        return true;
    }

    [GameEventHandler]
    public HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        if (State != Phase.KnifeRound || _knifePending || IsWarmupNow()) return HookResult.Continue;
        var winner = (CsTeam)@event.Winner;
        if (winner is not (CsTeam.Terrorist or CsTeam.CounterTerrorist))
        {
            // Draw: the knife round is played again.
            _knifePending = true;
            ChatAll("chat.knife_draw");
            return HookResult.Continue;
        }

        _knifeWinner = winner;
        ChatAll(winner == CsTeam.Terrorist ? "chat.knife_won_t" : "chat.knife_won_ct");
        if (Config.KnifeRoundEndDelay <= 0f) Server.NextFrame(StartSideVote);
        else AddTimer(Config.KnifeRoundEndDelay, StartSideVote);
        return HookResult.Continue;
    }

    // ---------------------------------------------------------------- side vote

    private void StartSideVote()
    {
        if (State != Phase.KnifeRound) return;
        foreach (var name in new[] { "mp_give_player_c4", "mp_buytime", "mp_free_armor", "mp_t_default_secondary", "mp_ct_default_secondary" })
            RestoreCvar(name);
        bool bhop = KnifeHasBhop();
        ApplyAfterKnifeCvars();
        if (bhop) ChatAll("chat.knife_bhop_off");
        SetCvar("mp_warmup_pausetimer", "1");
        Server.ExecuteCommand("mp_warmup_start");
        SetPhase(Phase.SideVote);
        _sideVoteEnd = Server.CurrentTime + Config.SideVoteTime;
        AnnounceVote();
    }

    private bool IsWinner(CCSPlayerController p) =>
        State == Phase.SideVote && p.Team == _knifeWinner;

    private bool AllWinnersVoted()
    {
        var winners = Utilities.GetPlayers().Where(p => p is { IsValid: true, IsBot: false, IsHLTV: false } && IsWinner(p)).ToList();
        return winners.Count > 0 && winners.All(p => _votes.ContainsKey(p.Slot));
    }

    private void FinishSideVote()
    {
        int t = _votes.Values.Count(v => v == 1), ct = _votes.Values.Count(v => v == 2);
        CsTeam pick = t > ct ? CsTeam.Terrorist : ct > t ? CsTeam.CounterTerrorist : _knifeWinner;
        Dbg("side vote: T={T} CT={CT} winner={Winner} -> {Pick}", t, ct, _knifeWinner, pick);

        if (pick != _knifeWinner)
        {
            foreach (var p in Utilities.GetPlayers())
            {
                if (p == null || !p.IsValid) continue;
                if (p.Team == CsTeam.Terrorist) p.SwitchTeam(CsTeam.CounterTerrorist);
                else if (p.Team == CsTeam.CounterTerrorist) p.SwitchTeam(CsTeam.Terrorist);
            }
        }
        ChatAll(pick == CsTeam.Terrorist ? "chat.side_t" : "chat.side_ct");

        SetPhase(Phase.Done);
        RestoreCvars();
        Server.ExecuteCommand("mp_warmup_end");
    }

    private void ChatAll(string key, params object[] args)
    {
        foreach (var p in Utilities.GetPlayers())
            if (p is { IsValid: true, IsBot: false }) Chat(p, key, args);
        Logger.LogInformation("{Text}", T(null, key, args));
    }
}
