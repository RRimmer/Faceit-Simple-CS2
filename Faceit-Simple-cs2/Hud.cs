using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Extensions;
using Microsoft.Extensions.Logging;

namespace FaceitSimpleCs2;

// The custom_hud_layout entity (faceit_simple_cs2.xml): a small vote card on the right side.
// Everything is per player and goes through a cache, so a tick sends only what changed.
// A new entity (round restart, map change) starts with empty caches.
public partial class Plugin
{
    internal const string RootId = "sf-root";

    private CCSCustomHudLayout? _hud;
    private bool _hudReady;

    private readonly Dictionary<string, bool>?[] _pClass = new Dictionary<string, bool>?[65];
    private readonly Dictionary<string, string>?[] _pVar = new Dictionary<string, string>?[65];
    private readonly bool?[] _pCapture = new bool?[65];

    private bool HudAlive => _hud != null && _hud.IsValid;

    private bool OwnsHud(CCSCustomHudLayout layout) => HudAlive && layout.Handle == _hud!.Handle;

    private void HudReset()
    {
        _hud = null;
        _hudReady = false;
        ForgetAllSent();
    }

    private void HudRemove()
    {
        try
        {
            var hud = _hud;
            if (hud != null && hud.IsValid)
            {
                foreach (var p in Utilities.GetPlayers())
                    if (p is { IsValid: true, IsBot: false }) hud.SetInputCaptureEnabled(p, false);
                hud.Remove();
            }
        }
        catch { }
        HudReset();
    }

    private void HudForgetSlot(int slot)
    {
        if (slot < 0 || slot >= _pClass.Length) return;
        _pClass[slot] = null;
        _pVar[slot] = null;
        _pCapture[slot] = null;
    }

    private void ForgetAllSent()
    {
        Array.Clear(_pClass);
        Array.Clear(_pVar);
        Array.Clear(_pCapture);
    }

    // True when the entity exists and the client side has had a frame to load the layout.
    private bool EnsureHud()
    {
        if (HudAlive) return _hudReady;
        HudReset();
        CCSCustomHudLayout? created;
        try
        {
            created = Utilities.CreateEntityByName<CCSCustomHudLayout>("custom_hud_layout");
        }
        catch (Exception e)
        {
            Logger.LogError("custom_hud_layout was not created: {Error}", e.Message);
            return false;
        }
        if (created == null || created.Handle == IntPtr.Zero)
        {
            Logger.LogError("custom_hud_layout was not created");
            return false;
        }
        created.StrLayout = Config.LayoutPath;
        created.DispatchSpawn();
        if (!created.IsValid) return false;
        _hud = created;
        Server.NextFrame(() =>
        {
            if (ReferenceEquals(_hud, created) && created.IsValid) _hudReady = true;
        });
        Dbg("hud spawned: {Layout}", Config.LayoutPath);
        return false;
    }

    private void PClass(CCSPlayerController player, string cls, bool on)
    {
        var l = _hud;
        int slot = player.Slot;
        if (l == null || !l.IsValid || slot < 0 || slot >= _pClass.Length) return;
        var sent = _pClass[slot] ??= new Dictionary<string, bool>(StringComparer.Ordinal);
        if (sent.TryGetValue(cls, out bool cur) && cur == on) return;
        sent[cls] = on;
        l.SetHasClassForPlayer(player, RootId, cls, on);
    }

    private void PVar(CCSPlayerController player, string name, string value)
    {
        var l = _hud;
        int slot = player.Slot;
        if (l == null || !l.IsValid || slot < 0 || slot >= _pVar.Length) return;
        var sent = _pVar[slot] ??= new Dictionary<string, string>(StringComparer.Ordinal);
        if (sent.TryGetValue(name, out var cur) && cur == value) return;
        sent[name] = value;
        l.SetDialogVariableStringForPlayer(player, RootId, name, value);
    }

    private void PCapture(CCSPlayerController player, bool on)
    {
        var l = _hud;
        int slot = player.Slot;
        if (l == null || !l.IsValid || slot < 0 || slot >= _pCapture.Length) return;
        if (_pCapture[slot] == on) return;
        _pCapture[slot] = on;
        l.SetInputCaptureEnabled(player, on);
    }
}
