using System.Globalization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;

namespace FaceitSimpleCs2;

// The vote card: what each player sees, mouse clicks, the cursor, chat fallback (!yes / !no, !t / !ct).
// Button ids in faceit_simple_cs2.xml: sf-btn-a (yes / T), sf-btn-b (no / CT), sf-btn-close (hide the cursor).
public partial class Plugin
{
    private const string BtnA = "sf-btn-a";
    private const string BtnB = "sf-btn-b";
    private const string BtnClose = "sf-btn-close";

    // The player closed the cursor with the cross: the card stays, the mouse aims again.
    private readonly bool[] _released = new bool[65];
    private readonly float[] _lastClick = new float[65];

    private bool VoteShown => State is Phase.KnifeVote or Phase.SideVote;

    private bool CanVote(CCSPlayerController p) => State switch
    {
        Phase.KnifeVote => p.Team is CsTeam.Terrorist or CsTeam.CounterTerrorist && Joined(p),
        Phase.SideVote => IsWinner(p),
        _ => false,
    };

    // Players already offered the current vote (chat line sent, card shown).
    private readonly bool[] _offered = new bool[65];
    // Server.CurrentTime when the player last changed team; the card waits a moment after the team menu.
    private readonly float[] _teamAt = new float[65];
    private const float TeamSettle = 1f;

    // Knife vote: a player who came in late gets it only while VoteJoinUntil seconds or more are left.
    private bool Joined(CCSPlayerController p)
    {
        int slot = p.Slot;
        if (slot < 0 || slot >= _offered.Length) return false;
        return _offered[slot] || WarmupLeft() >= Config.VoteJoinUntil;
    }

    [GameEventHandler]
    public HookResult OnPlayerTeam(EventPlayerTeam @event, GameEventInfo info)
    {
        var p = @event.Userid;
        if (p == null || !p.IsValid || p.IsBot) return HookResult.Continue;
        int slot = p.Slot;
        if (slot < 0 || slot >= _teamAt.Length) return HookResult.Continue;
        // Everything is sent again after the team change: what was sent during the team menu may be lost.
        _teamAt[slot] = Server.CurrentTime;
        HudForgetSlot(slot);
        return HookResult.Continue;
    }

    // Called from the tick after MatchTick.
    private void VoteTick()
    {
        if (!VoteShown)
        {
            if (!HudAlive) return;
            foreach (var p in Humans())
            {
                PClass(p, "sf-on", false);
                PCapture(p, false);
            }
            return;
        }

        // Late joiners: the chat line once they are on a team (the card follows below).
        float now = Server.CurrentTime;
        foreach (var p in Humans())
        {
            int slot = p.Slot;
            if (slot < 0 || slot >= _offered.Length || _offered[slot]) continue;
            if (now - _teamAt[slot] < TeamSettle) continue;
            if (State == Phase.KnifeVote && !CanVote(p)) continue;
            if (State == Phase.SideVote && p.Team is not (CsTeam.Terrorist or CsTeam.CounterTerrorist)) continue;
            _offered[slot] = true;
            AnnounceTo(p);
        }

        if (!EnsureHud()) return;
        foreach (var p in Humans())
        {
            int slot = p.Slot;
            bool show = slot >= 0 && slot < _offered.Length && _offered[slot] && now - _teamAt[slot] >= TeamSettle;
            if (show) RenderVote(p);
            else
            {
                PClass(p, "sf-on", false);
                PCapture(p, false);
            }
        }
    }

    private static IEnumerable<CCSPlayerController> Humans() =>
        Utilities.GetPlayers().Where(p => p is { IsValid: true, IsBot: false, IsHLTV: false });

    private void RenderVote(CCSPlayerController p)
    {
        int slot = p.Slot;
        if (slot < 0 || slot >= _released.Length) return;
        bool knife = State == Phase.KnifeVote;
        bool can = CanVote(p);
        _votes.TryGetValue(slot, out int mine);

        int secs = knife
            ? (int)Math.Ceiling(Math.Max(0f, WarmupLeft()))
            : (int)Math.Ceiling(Math.Max(0f, _sideVoteEnd - Server.CurrentTime));
        int a = _votes.Values.Count(v => v == 1), b = _votes.Values.Count(v => v == 2);

        PVar(p, "sf_title", T(p, knife ? "hud.knife_title" : "hud.side_title"));
        PVar(p, "sf_sub", T(p, knife ? "hud.knife_sub" : can ? "hud.side_sub" : "hud.side_wait"));
        PVar(p, "sf_a", T(p, knife ? "hud.yes" : "hud.t"));
        PVar(p, "sf_b", T(p, knife ? "hud.no" : "hud.ct"));
        PVar(p, "sf_ca", a.ToString(CultureInfo.InvariantCulture));
        PVar(p, "sf_cb", b.ToString(CultureInfo.InvariantCulture));
        PVar(p, "sf_time", $"{secs / 60}:{secs % 60:00}");
        var (wa, wb) = HintWords();
        PVar(p, "sf_hint", Config.MenuType switch
        {
            2 => T(p, "hud.hint_mouse"),
            3 => T(p, "hud.hint_chatonly", wa, wb),
            _ => T(p, "hud.hint_chat", wa, wb),
        });
        PClass(p, "sf-chatonly", !MouseVoting);

        PClass(p, "sf-knife", knife);
        PClass(p, "sf-side", !knife);
        PClass(p, "sf-can", can);
        PClass(p, "sf-voted-a", mine == 1);
        PClass(p, "sf-voted-b", mine == 2);
        PClass(p, "sf-urgent", secs <= 5);
        PClass(p, "sf-on", true);
        PCapture(p, MouseVoting && can && mine == 0 && !_released[slot]);
    }

    private void OnHudClick(CCSPlayerController player, CCSCustomHudLayout layout, string buttonId)
    {
        if (!Config.Enabled || !MouseVoting || !OwnsHud(layout) || player is not { IsValid: true, IsBot: false }) return;
        int slot = player.Slot;
        if (slot < 0 || slot >= _released.Length) return;
        float now = Server.CurrentTime;
        if (now - _lastClick[slot] < 0.2f) return;
        _lastClick[slot] = now;

        switch (buttonId)
        {
            case BtnA: Vote(player, 1); break;
            case BtnB: Vote(player, 2); break;
            case BtnClose:
                _released[slot] = true;
                PCapture(player, false);
                break;
        }
    }

    private void Vote(CCSPlayerController p, int choice)
    {
        if (!VoteShown || !CanVote(p)) return;
        _votes[p.Slot] = choice;
        PCapture(p, false);
        RenderVote(p);
        Dbg("{Player} voted {Choice} in {Phase}", p.PlayerName, choice, State);
    }

    // MenuType: 1 - mouse + chat, 2 - mouse only, 3 - chat only.
    private bool MouseVoting => Config.MenuType is 1 or 2;
    private bool ChatVoting => Config.MenuType is 1 or 3;

    private void RegisterVoteCommands()
    {
        AddCommandListener("say", OnSay);
        AddCommandListener("say_team", OnSay);
        AddCommand("css_vote", "Faceit-Simple-cs2: show the cursor on the vote card again", (p, _) =>
        {
            if (!MouseVoting || p is not { IsValid: true } || p.Slot < 0 || p.Slot >= _released.Length) return;
            // The cursor comes back and the vote can be changed.
            _released[p.Slot] = false;
            _votes.Remove(p.Slot);
        });
    }

    // Any chat message that is exactly one of the ChatType words ("1", "!1", "/1", "да", ...).
    // The message still goes to the chat.
    private HookResult OnSay(CCSPlayerController? player, CommandInfo info)
    {
        if (!Config.Enabled || !ChatVoting || !VoteShown) return HookResult.Continue;
        if (player is not { IsValid: true, IsBot: false }) return HookResult.Continue;
        string text = info.ArgCount > 1 ? info.GetArg(1) : info.ArgString;
        string word = ConfigFile.NormWord(text.Trim().Trim('"'));
        if (word.Length == 0) return HookResult.Continue;

        var words = Config.ChatType;
        int choice = State == Phase.KnifeVote
            ? words.Yes.Contains(word) ? 1 : words.No.Contains(word) ? 2 : 0
            : words.T.Contains(word) ? 1 : words.CT.Contains(word) ? 2 : 0;
        if (choice == 0) return HookResult.Continue;

        if (!CanVote(player))
        {
            Chat(player, "chat.cant_vote");
            return HookResult.Continue;
        }
        Server.NextFrame(() =>
        {
            if (!player.IsValid) return;
            Vote(player, choice);
            Chat(player, "chat.voted", OptionName(player, choice));
        });
        return HookResult.Continue;
    }

    private string OptionName(CCSPlayerController? p, int choice) =>
        T(p, State == Phase.KnifeVote ? choice == 1 ? "hud.yes" : "hud.no" : choice == 1 ? "hud.t" : "hud.ct");

    // Chat words shown in hints: the first word of each list.
    private (string A, string B) HintWords() => State == Phase.KnifeVote
        ? (Config.ChatType.Yes[0], Config.ChatType.No[0])
        : (Config.ChatType.T[0], Config.ChatType.CT[0]);

    // At the start of a vote nobody is offered yet; VoteTick offers it to every player on a team
    // (and to late joiners while VoteJoinUntil allows).
    private void AnnounceVote()
    {
        Array.Clear(_offered);
    }

    private void AnnounceTo(CCSPlayerController p)
    {
        bool knife = State == Phase.KnifeVote;
        if (!knife && !CanVote(p))
        {
            Chat(p, "hud.side_wait");
            return;
        }
        if (!ChatVoting) return;
        var (a, b) = HintWords();
        int secs = knife
            ? Math.Max(0, (int)Math.Ceiling(WarmupLeft()))
            : Math.Max(0, (int)Math.Ceiling(_sideVoteEnd - Server.CurrentTime));
        Chat(p, knife ? "chat.knife_vote" : "chat.side_vote", a, b, secs);
    }

    private void Chat(CCSPlayerController p, string key, params object[] args)
    {
        if (!p.IsValid) return;
        // The tag comes from lang ("chat.tag"); an empty tag means no prefix.
        string tag = T(p, "chat.tag");
        if (tag == "chat.tag") tag = $"{ChatColors.Orange}[Faceit]{ChatColors.Default}";
        string text = T(p, key, args);
        p.PrintToChat(tag.Length > 0 ? $" {tag} {text}" : $" {text}");
    }

    // ---------------------------------------------------------------- texts (lang/*.json)

    internal string T(CCSPlayerController? p, string key, params object[] args)
    {
        string text;
        try
        {
            text = p != null && p.IsValid ? Localizer.ForPlayer(p, key) : Localizer[key].Value;
        }
        catch
        {
            text = key;
        }
        if (args.Length == 0) return text;
        try { return string.Format(text, args); }
        catch { return text; }
    }
}
