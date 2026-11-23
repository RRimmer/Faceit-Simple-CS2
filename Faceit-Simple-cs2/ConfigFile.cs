using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace FaceitSimpleCs2;

// configs/plugins/Faceit-Simple-cs2/Faceit-Simple-cs2.jsonc: JSON with comments and trailing commas.
// CounterStrikeSharp loads only .json / .toml, so the plugin reads this file itself.
// No file — a .jsonc with the defaults and comments is written. An existing file is never rewritten.
internal static class ConfigFile
{
    public const string FileName = "Faceit-Simple-cs2.jsonc";

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
    };

    public static FaceitSimpleConfig Load(string dir, ILogger log)
    {
        string path = Path.Combine(dir, FileName);
        FaceitSimpleConfig config;
        try
        {
            if (File.Exists(path))
            {
                config = JsonSerializer.Deserialize<FaceitSimpleConfig>(File.ReadAllText(path), ReadOptions) ?? new FaceitSimpleConfig();
            }
            else
            {
                config = new FaceitSimpleConfig();
                Directory.CreateDirectory(dir);
                File.WriteAllText(path, Render(config), new UTF8Encoding(false));
                log.LogInformation("{File} created with the defaults", path);
            }
        }
        catch (Exception e)
        {
            log.LogError("{File} was not read, using the defaults: {Error}", path, e.Message);
            config = new FaceitSimpleConfig();
        }
        return Normalize(config, log);
    }

    private static FaceitSimpleConfig Normalize(FaceitSimpleConfig c, ILogger log)
    {
        if (c.StartMoney < 0) c.StartMoney = 0;
        if (c.MoneyMode is not (0 or 1))
        {
            log.LogWarning("MoneyMode {Mode} is unknown (0 or 1), using 0", c.MoneyMode);
            c.MoneyMode = 0;
        }
        if (c.DeleteDroppedInterval < 0f) c.DeleteDroppedInterval = 0f;
        if (c.DeleteDroppedInterval > 0f && c.DeleteDroppedInterval < 0.5f) c.DeleteDroppedInterval = 0.5f;
        c.KnifeRoundMode = (c.KnifeRoundMode ?? "").Trim().ToLowerInvariant();
        if (c.KnifeRoundMode is not ("vote" or "always" or "off"))
        {
            log.LogWarning("KnifeRoundMode \"{Mode}\" is unknown (vote, always, off), using vote", c.KnifeRoundMode);
            c.KnifeRoundMode = "vote";
        }
        c.KnifeVoteBeforeEnd = Math.Clamp(c.KnifeVoteBeforeEnd, 5, 600);
        c.KnifeRoundEndDelay = Math.Clamp(c.KnifeRoundEndDelay, 0f, 10f);
        c.SideVoteTime = Math.Clamp(c.SideVoteTime, 5, 120);
        c.VoteJoinUntil = Math.Clamp(c.VoteJoinUntil, 0, 600);
        c.KnifeArmor = Math.Clamp(c.KnifeArmor, 0, 2);
        c.KnifeHealth = Math.Clamp(c.KnifeHealth, 1, 1000);
        c.KnifeResetDelay = Math.Clamp(c.KnifeResetDelay, 0f, 60f);
        c.KnifeCvars = CvarLines(c.KnifeCvars, "KnifeCvars", log);
        c.AfterKnifeCvars = CvarLines(c.AfterKnifeCvars, "AfterKnifeCvars", log);
        if (string.IsNullOrWhiteSpace(c.LayoutPath)) c.LayoutPath = new FaceitSimpleConfig().LayoutPath;
        if (c.MenuType is not (1 or 2 or 3))
        {
            log.LogWarning("MenuType {Type} is unknown (1, 2, 3), using 1", c.MenuType);
            c.MenuType = 1;
        }
        var defWords = new ChatWords();
        c.ChatType ??= defWords;
        c.ChatType.Yes = Words(c.ChatType.Yes, defWords.Yes);
        c.ChatType.No = Words(c.ChatType.No, defWords.No);
        c.ChatType.T = Words(c.ChatType.T, defWords.T);
        c.ChatType.CT = Words(c.ChatType.CT, defWords.CT);
        return c;
    }

    // Stored as typed in chat would be compared: lower case, without a leading "!" or "/".
    internal static string NormWord(string? s) =>
        (s ?? "").Trim().TrimStart('!', '/').Trim().ToLowerInvariant();

    // "name value" lines: extra spaces are squeezed, a line without a value is dropped.
    private static List<string> CvarLines(List<string>? list, string field, ILogger log)
    {
        var result = new List<string>();
        foreach (var raw in list ?? new())
        {
            if (TryCvar(raw, out var name, out var value)) result.Add($"{name} {value}");
            else if (!string.IsNullOrWhiteSpace(raw)) log.LogWarning("{Field}: \"{Line}\" is not \"name value\", skipped", field, raw);
        }
        return result;
    }

    internal static bool TryCvar(string? line, out string name, out string value)
    {
        var parts = (line ?? "").Trim().Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries);
        name = parts.Length > 0 ? parts[0] : "";
        value = parts.Length > 1 ? parts[1].Trim().Trim('"') : "";
        return parts.Length == 2 && name.All(ch => char.IsLetterOrDigit(ch) || ch == '_');
    }

    private static List<string> Words(List<string>? list, List<string> fallback)
    {
        var words = (list ?? new()).Select(NormWord).Where(w => w.Length > 0).Distinct().ToList();
        return words.Count > 0 ? words : fallback;
    }

    private static string L(List<string> words) =>
        "[" + string.Join(", ", words.Select(w => JsonSerializer.Serialize(w, WriteOptions))) + "]";

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static string B(bool v) => v ? "true" : "false";
    private static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    // The file with comments. Keep in step with ../Faceit-Simple-cs2.example.jsonc.
    public static string Render(FaceitSimpleConfig c)
    {
        var s = new StringBuilder();
        s.Append("// Faceit-Simple-cs2 — разминка как на Faceit.\n");
        s.Append("// Комментарии // и /* */ и запятая после последнего элемента разрешены.\n");
        s.Append("// После правки: css_faceitsimple reload в консоли сервера.\n");
        s.Append("{\n");
        s.Append("  // false — плагин ничего не делает.\n");
        s.Append($"  \"Enabled\": {B(c.Enabled)},\n\n");
        s.Append("  // Warmup money amount\n");
        s.Append("  // Кол-во денег на разминке\n");
        s.Append($"  \"StartMoney\": {c.StartMoney},\n\n");
        s.Append("  // Money apply mode: 0 - after buy, 1 - after spawn\n");
        s.Append("  // Способ выдачи денег: 0 - после покупки, 1 - после спавна\n");
        s.Append($"  \"MoneyMode\": {c.MoneyMode},\n\n");
        s.Append("  // Delete dropped weapons every N seconds (0 - off)\n");
        s.Append("  // Удалять выпавшее оружие каждые N секунд (0 - выкл)\n");
        s.Append($"  \"DeleteDroppedInterval\": {F(c.DeleteDroppedInterval)},\n\n");
        s.Append("  // mp_weapons_allow_typecount: сколько одинаковых оружий можно купить за раунд.\n");
        s.Append("  // На разминке WarmupTypeCount (-1 — без ограничений), после разминки MatchTypeCount.\n");
        s.Append("  // ControlTypeCount: false — плагин не трогает эту настройку.\n");
        s.Append($"  \"ControlTypeCount\": {B(c.ControlTypeCount)},\n");
        s.Append($"  \"WarmupTypeCount\": {c.WarmupTypeCount},\n");
        s.Append($"  \"MatchTypeCount\": {c.MatchTypeCount},\n\n");
        s.Append("  // Ножевой раунд после разминки: \"vote\" — голосование, \"always\" — всегда, \"off\" — выкл.\n");
        s.Append($"  \"KnifeRoundMode\": \"{c.KnifeRoundMode}\",\n\n");
        s.Append("  // За сколько секунд до конца разминки показать голосование «Ножевой раунд?».\n");
        s.Append($"  \"KnifeVoteBeforeEnd\": {c.KnifeVoteBeforeEnd},\n\n");
        s.Append("  // Ничья или никто не проголосовал: true — ножевой раунд будет, false — не будет.\n");
        s.Append($"  \"KnifeVoteTieResult\": {B(c.KnifeVoteTieResult)},\n\n");
        s.Append("  // Кто зашёл за T/CT во время голосования — тоже получает его, пока до конца разминки не меньше N секунд.\n");
        s.Append($"  \"VoteJoinUntil\": {c.VoteJoinUntil},\n\n");
        s.Append("  // Броня на ножевом раунде: 0 — без брони, 1 — бронежилет, 2 — бронежилет + шлем.\n");
        s.Append($"  \"KnifeArmor\": {c.KnifeArmor},\n\n");
        s.Append("  // Здоровье на ножевом раунде.\n");
        s.Append($"  \"KnifeHealth\": {c.KnifeHealth},\n\n");
        s.Append("  // Через сколько секунд после начала ножевого раунда ещё раз полностью сбросить игроков (0 — выкл).\n");
        s.Append("  // Один раз за раунд: забрать всё, кроме ножа (оружие, гранаты, медшприц, щипцы), выставить HP и броню.\n");
        s.Append("  // Зачем: VIP и другие плагины выдают оружие, медшприц, HP, броню и шлем через пару секунд после спавна,\n");
        s.Append("  // а кто-то успевает поднять или купить оружие в самом начале раунда. Этот сброс всё это убирает.\n");
        s.Append($"  \"KnifeResetDelay\": {F(c.KnifeResetDelay)},\n\n");
        s.Append("  // Квары сервера на время ножевого раунда (\"имя значение\"). По умолчанию — бхоп со скоростью для всех.\n");
        s.Append("  // Перед ними плагин запоминает текущие значения этих кваров.\n");
        s.Append($"  \"KnifeCvars\": {L(c.KnifeCvars)},\n\n");
        s.Append("  // Квары после ножевого раунда (\"имя значение\"). Пустой список [] — вернуть значения,\n");
        s.Append("  // которые были до ножевого раунда (запомненные).\n");
        s.Append($"  \"AfterKnifeCvars\": {L(c.AfterKnifeCvars)},\n\n");
        s.Append("  // Через сколько секунд после ножевого раунда начинается выбор стороны (0 — сразу).\n");
        s.Append($"  \"KnifeRoundEndDelay\": {F(c.KnifeRoundEndDelay)},\n\n");
        s.Append("  // Сколько секунд победители выбирают сторону (разминка на паузе).\n");
        s.Append($"  \"SideVoteTime\": {c.SideVoteTime},\n\n");
        s.Append("  // Как голосовать: 1 — мышка (+ запасной чат), 2 — только мышка, 3 — только чат (плашка видна, курсора нет).\n");
        s.Append($"  \"MenuType\": {c.MenuType},\n\n");
        s.Append("  // Слова в чате, которые считаются голосом. Регистр и \"!\" / \"/\" в начале не важны:\n");
        s.Append("  // \"1\", \"!1\", \"/1\" — одно и то же. Сообщение должно состоять только из этого слова.\n");
        s.Append("  // Yes / No — голосование за ножевой раунд, T / CT — выбор стороны.\n");
        s.Append("  \"ChatType\": {\n");
        s.Append($"    \"Yes\": {L(c.ChatType.Yes)},\n");
        s.Append($"    \"No\": {L(c.ChatType.No)},\n");
        s.Append($"    \"T\": {L(c.ChatType.T)},\n");
        s.Append($"    \"CT\": {L(c.ChatType.CT)}\n");
        s.Append("  },\n\n");
        s.Append("  // Скомпилированная разметка в клиентском аддоне (обратите внимание на _c).\n");
        s.Append($"  \"LayoutPath\": \"{c.LayoutPath}\",\n\n");
        s.Append("  // Подробный лог.\n");
        s.Append($"  \"Debug\": {B(c.Debug)},\n\n");
        s.Append($"  \"ConfigVersion\": {c.ConfigVersion}\n");
        s.Append("}\n");
        return s.ToString();
    }
}
