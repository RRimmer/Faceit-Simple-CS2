# История изменений

## 0.1.1

### Русский

- Проект переименован: **Faceit-Simple-cs2** (dll, папки, конфиг `Faceit-Simple-cs2.jsonc`, разметка `faceit_simple_cs2.xml`, консольная команда `css_faceitsimple`).
- `KnifeResetDelay` (5 с): один раз после начала ножевого раунда игроки полностью сбрасываются — всё кроме ножа (оружие VIP, медшприц, гранаты, щипцы), HP, броня, шлем.
- `KnifeArmor` (0 — без брони, 1 — бронежилет, 2 — + шлем) и `KnifeHealth` (100). `mp_free_armor 0` на ножевой раунд.
- `VoteJoinUntil` (5 с): кто зашёл за команду во время голосования, тоже его получает (чат + плашка), пока до конца разминки не меньше N секунд.
- Бхоп на ножевом раунде: `KnifeCvars` (по умолчанию `sv_enablebunnyhopping 1`, `sv_autobunnyhopping 1`, `sv_airaccelerate 2000`, `sv_maxvelocity 7000`, стамина 0…) и `AfterKnifeCvars` (обычное движение). Старые значения запоминаются; пустой `AfterKnifeCvars` — вернуть их. Сообщения в чат «Бхоп включён / выключен».
- Тег чата настраивается в `lang/*.json` (`chat.tag`, цвета `{orange}`, `{green}`…; пустая строка — без тега).
- `MenuType 3` — только чат, но плашка остаётся (без курсора и крестика).

### English

- Renamed to **Faceit-Simple-cs2**: dll, folders, `Faceit-Simple-cs2.jsonc`, `faceit_simple_cs2.xml` and the `css_faceitsimple` console command.
- `KnifeResetDelay` (5 s): one full reset after the knife round starts — everything but the knife (VIP weapons, healthshot, grenades, defuse kit), HP, armor and helmet.
- `KnifeArmor` (0 none, 1 vest, 2 vest and helmet) and `KnifeHealth` (100); `mp_free_armor 0` for the knife round.
- `VoteJoinUntil` (5 s): players who join a team during the vote get it too (chat and card) while at least N seconds of warmup remain.
- Bunny hop on the knife round: `KnifeCvars` (bhop enabled, `sv_airaccelerate 2000`, no stamina by default) and `AfterKnifeCvars` for normal movement. Previous values are remembered; an empty `AfterKnifeCvars` restores them. Chat notices when bhop goes on and off.
- The chat tag is configurable in `lang/*.json` (`chat.tag`, colours `{orange}`, `{green}`…; an empty string means no tag).
- `MenuType 3` is chat only, but the card stays visible without the cursor and the cross.

## 0.1.0

### Русский

- Первый выпуск: порт CS:GO «Faceit Warmup» 2.0 на CounterStrikeSharp.
- Деньги на разминке (после покупки или после спавна), удаление брошенного оружия, `mp_weapons_allow_typecount` -1 / 2.
- Конфиг `Faceit-Simple-cs2.jsonc` с комментариями, команда `css_faceitsimple [reload]`.
- Голосование «Ножевой раунд?» на панорамной плашке справа (мышь и/или чат), ножевой раунд, выбор стороны победителями в паузной разминке, матч с 0:0.
- `MenuType` (1 — мышка + чат, 2 — только мышка, 3 — только чат) и `ChatType` — слова для голоса в чате (`1`, `!1`, `да`, `t`…), `!` необязателен.
- Переводы `lang/ru.json`, `lang/en.json`. CounterStrikeSharp 1.0.380.
- Убран общий голосовой чат на разминке (`sm_fw_warmup_alltalk`).

### English

- First release: a port of the CS:GO *Faceit Warmup 2.0* to CounterStrikeSharp.
- Warmup money (after a purchase or a spawn), dropped weapon cleanup and `mp_weapons_allow_typecount` -1 / 2.
- A commented `Faceit-Simple-cs2.jsonc` and the `css_faceitsimple [reload]` command.
- A "Knife round?" vote on a card on the right (mouse and/or chat), the knife round, the winners picking a side in a paused warmup and a 0:0 match start.
- `MenuType` (1 mouse and chat, 2 mouse only, 3 chat only) and `ChatType` — the chat words for votes (`1`, `!1`, `да`, `t`…), `!` is optional.
- `lang/ru.json` and `lang/en.json`. CounterStrikeSharp 1.0.380.
- The original warmup all-talk was removed.
