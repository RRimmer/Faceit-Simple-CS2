namespace FaceitSimpleCs2;

// configs/plugins/Faceit-Simple-cs2/Faceit-Simple-cs2.jsonc (read by ConfigFile, not by CounterStrikeSharp).
public class FaceitSimpleConfig
{
    public bool Enabled { get; set; } = true;

    // sm_fw_start_money: money on warmup.
    public int StartMoney { get; set; } = 16000;

    // sm_fw_got_money: 0 - after every purchase, 1 - after spawn.
    public int MoneyMode { get; set; } = 0;

    // sm_fw_time_delete: remove dropped weapons every N seconds on warmup (0 - off).
    public float DeleteDroppedInterval { get; set; } = 3f;

    // mp_weapons_allow_typecount on warmup and after it (the original used -1 and 2).
    public bool ControlTypeCount { get; set; } = true;
    public int WarmupTypeCount { get; set; } = -1;
    public int MatchTypeCount { get; set; } = 2;

    // Knife round: "vote" - ask the players, "always" - always, "off" - never.
    public string KnifeRoundMode { get; set; } = "vote";

    // The knife vote is shown this many seconds before the warmup ends.
    public int KnifeVoteBeforeEnd { get; set; } = 30;

    // Tie or no votes: true - knife round, false - no knife round.
    public bool KnifeVoteTieResult { get; set; } = false;

    // A player who joins T/CT during the knife vote still gets it while at least this many seconds are left.
    public int VoteJoinUntil { get; set; } = 5;

    // Knife round armor: 0 - none, 1 - kevlar, 2 - kevlar + helmet.
    public int KnifeArmor { get; set; } = 0;

    // Knife round health.
    public int KnifeHealth { get; set; } = 100;

    // Seconds after the knife round start when every player is reset once more (0 - off):
    // inventory, health and armor. Catches what VIP or other plugins give a moment after the spawn.
    public float KnifeResetDelay { get; set; } = 5f;

    // Server cvars for the knife round ("name value"). Set when the knife round is chosen.
    public List<string> KnifeCvars { get; set; } = new()
    {
        "sv_enablebunnyhopping 1",
        "sv_autobunnyhopping 1",
        "sv_airaccelerate 2000",
        "sv_maxvelocity 7000",
        "sv_staminamax 0",
        "sv_staminarecoveryrate 0",
        "sv_accelerate_use_weapon_speed 0",
    };

    // Server cvars after the knife round ("name value"). Empty - the values from before the knife round come back.
    public List<string> AfterKnifeCvars { get; set; } = new()
    {
        "sv_enablebunnyhopping 0",
        "sv_autobunnyhopping 0",
        "sv_airaccelerate 12",
        "sv_maxvelocity 3500",
        "sv_staminamax 80",
        "sv_staminarecoveryrate 60",
        "sv_accelerate_use_weapon_speed 1",
    };

    // Seconds between the end of the knife round and the side vote warmup (0 - at once).
    public float KnifeRoundEndDelay { get; set; } = 1f;

    // Seconds the winners have to pick a side.
    public int SideVoteTime { get; set; } = 15;

    // How players vote: 1 - mouse + chat, 2 - mouse only, 3 - chat only (the card is shown, no cursor).
    public int MenuType { get; set; } = 1;

    // Chat words that count as a vote. Case and a leading "!" or "/" are ignored.
    public ChatWords ChatType { get; set; } = new();

    // Compiled layout inside the client addon (note the _c).
    public string LayoutPath { get; set; } = "panorama/layout/custom_game/faceit_simple_cs2.vxml_c";

    public bool Debug { get; set; } = false;

    public int ConfigVersion { get; set; } = 5;
}

public class ChatWords
{
    public List<string> Yes { get; set; } = new() { "1", "yes", "да", "+" };
    public List<string> No { get; set; } = new() { "2", "no", "нет", "-" };
    public List<string> T { get; set; } = new() { "1", "t", "т" };
    public List<string> CT { get; set; } = new() { "2", "ct", "кт" };
}
