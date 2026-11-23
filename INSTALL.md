# Faceit-Simple-cs2 0.1.1 — установка

[README](README.md) · [История изменений](CHANGELOG.md) · [Лицензия](LICENSE)

Разминка как на Faceit для CS2 (CounterStrikeSharp). Порт плагина CS:GO «Faceit Warmup» 2.0. Автор: Rimmer.

## Что делает на разминке

- Деньги игрока = `StartMoney` после каждой покупки (`MoneyMode: 0`) или после спавна (`MoneyMode: 1`).
- Каждые `DeleteDroppedInterval` секунд удаляет брошенное оружие и предметы (`weapon_*`, `item_*` без владельца).
- `mp_weapons_allow_typecount`: на разминке `-1` (без ограничений), после разминки `2`.

Дальше — голосование за ножевой раунд, сам ножевой раунд и выбор стороны победителями. Общий голосовой чат (`sm_fw_warmup_alltalk` из оригинала) убран.

## Установка

1. Установите CounterStrikeSharp 1.0.380 или новее.
2. Возьмите `Faceit-Simple-CS2-0.1.1.zip` из релизов или соберите его командой `pack.cmd` в корне репозитория. Скопируйте `Server-plugins/counterstrikesharp/` в `game/csgo/addons/counterstrikesharp/` на сервере.
3. Перезапустите сервер или выполните `css_plugins load Faceit-Simple-cs2`.
4. При первой загрузке создаётся конфиг; после правок — `css_faceitsimple reload` в консоли сервера.
5. Для панорамной плашки соберите `Content-addonmanager/panorama/` в клиентский аддон — см. раздел «Клиентская часть». Без аддона голосование идёт через чат.

Других зависимостей нет.

Архив содержит:

| Путь в архиве | Назначение |
| --- | --- |
| `Server-plugins/counterstrikesharp/plugins/Faceit-Simple-cs2/` | DLL, `deps.json`, `pdb`, `lang` |
| `Server-plugins/counterstrikesharp/configs/plugins/Faceit-Simple-cs2/Faceit-Simple-cs2.jsonc` | Конфиг с комментариями |
| `Content-addonmanager/panorama/` | Разметка и стили плашки |
| `Source/` | Исходники и скрипты сборки |
| `README.md`, `INSTALL.md`, `CHANGELOG.md` | Документация |

## Конфиг

`addons/counterstrikesharp/configs/plugins/Faceit-Simple-cs2/Faceit-Simple-cs2.jsonc` (комментарии разрешены).
Если файла нет — плагин создаст его со значениями по умолчанию.

| Ключ | По умолчанию | Оригинал | Что делает |
|---|---|---|---|
| `Enabled` | `true` | — | выключатель плагина |
| `StartMoney` | `16000` | `sm_fw_start_money` | деньги на разминке |
| `MoneyMode` | `0` | `sm_fw_got_money` | 0 — после покупки, 1 — после спавна |
| `DeleteDroppedInterval` | `3` | `sm_fw_time_delete` | удалять брошенное оружие каждые N секунд (0 — выкл) |
| `ControlTypeCount` | `true` | — | трогать ли `mp_weapons_allow_typecount` |
| `WarmupTypeCount` | `-1` | зашито `-1` | значение на разминке |
| `MatchTypeCount` | `2` | зашито `2` | значение после разминки |
| `KnifeRoundMode` | `vote` | — | ножевой раунд: `vote` — голосование, `always` — всегда, `off` — выкл |
| `KnifeVoteBeforeEnd` | `30` | — | за сколько секунд до конца разминки показать голосование |
| `KnifeVoteTieResult` | `false` | — | ничья / нет голосов: `true` — ножевой будет |
| `KnifeRoundEndDelay` | `1` | — | пауза после ножевого раунда до выбора стороны, с |
| `SideVoteTime` | `15` | — | сколько секунд победители выбирают сторону |
| `MenuType` | `1` | — | как голосовать: 1 — мышка + чат, 2 — только мышка, 3 — только чат (плашка видна, курсора нет) |
| `VoteJoinUntil` | `5` | — | кто зашёл за команду во время голосования, получает его, пока до конца разминки ≥ N с |
| `KnifeArmor` | `0` | — | броня на ножах: 0 — нет, 1 — бронежилет, 2 — + шлем |
| `KnifeHealth` | `100` | — | HP на ножах |
| `KnifeCvars` | бхоп (см. конфиг) | — | квары на время ножевого раунда, строки `"имя значение"`; перед ними плагин запоминает старые значения |
| `AfterKnifeCvars` | обычное движение (см. конфиг) | — | квары после ножевого раунда; `[]` — вернуть запомненные значения |
| `KnifeResetDelay` | `5` | — | через N с после начала ножевого раунда один раз полностью сбросить игроков (0 — выкл) |
| `ChatType` | см. конфиг | — | слова в чате для голосов `Yes` / `No` / `T` / `CT`. Регистр и `!` / `/` в начале не важны |
| `LayoutPath` | `panorama/layout/custom_game/faceit_simple_cs2.vxml_c` | — | разметка плашки в клиентском аддоне |
| `Debug` | `false` | — | подробный лог |

## Ножевой раунд и выбор стороны

1. За `KnifeVoteBeforeEnd` секунд до конца разминки справа появляется плашка «Ножевой раунд?». Голосуют мышью (кнопки «Да» / «Нет»), крестик — убрать курсор, `!vote` — вернуть. В чат можно просто написать `1` / `2`, `да` / `нет` и т.п. (слова из `ChatType`, с `!` или без). Режим задаёт `MenuType`.
2. Если большинство «Да» — первый раунд только с ножами: без C4, без покупки, деньги 0, броня и HP по `KnifeArmor` / `KnifeHealth`.
   Через `KnifeResetDelay` секунд после начала раунда игроки один раз сбрасываются полностью. Зачем: VIP-плагины выдают через 1–3 секунды после спавна комплект оружия, медшприц, +HP, бронежилет и шлем, а кто-то успевает поднять оружие или купить что-то в самом начале раунда. Без этого сброса VIP с AK и 150 HP против игрока с ножом — уже не ножевой раунд. Сброс забирает всё, кроме ножа, и выставляет HP и броню по конфигу.
3. Через `KnifeRoundEndDelay` секунд после ножевого раунда включается разминка на паузе (`mp_warmup_pausetimer 1`). У победителей плашка «За T» / «За CT» (чат `!t` / `!ct`).
4. Через `SideVoteTime` секунд (или когда проголосовали все победители) команды меняются местами, если нужно, и матч начинается с 0:0. Изменённые квары возвращаются.

## Тексты и тег чата

Все сообщения — в `plugins/Faceit-Simple-cs2/lang/ru.json` и `en.json`. Тег перед сообщениями — ключ `chat.tag`:

```json
"chat.tag": "{orange}[Faceit]{default}",
```

Цвета: `{default}`, `{white}`, `{red}`, `{green}`, `{lime}`, `{olive}`, `{orange}`, `{gold}`, `{yellow}`, `{blue}`, `{lightblue}`, `{darkblue}`, `{purple}`, `{magenta}`, `{grey}` и др. Пустая строка `""` — сообщения без тега. После правки — `css_plugins reload Faceit-Simple-cs2`.

## Клиентская часть (панорама)

Плашка — свой `custom_hud_layout`. Содержимое `Content-addonmanager/panorama/` нужно скомпилировать в клиентский аддон (как у PMM_C4Timer, через MultiAddonManager):

```text
panorama/layout/custom_game/faceit_simple_cs2.xml
panorama/styles/custom_game/faceit_simple_cs2.css
```

Без аддона ножевой раунд и выбор стороны всё равно работают через чат. Позиция плашки правится в `.sf-root` файла CSS.

## Команды (консоль сервера)

- `css_faceitsimple` — статус.
- `css_faceitsimple reload` — перечитать конфиг.

## Сборка из исходников

Нужен .NET SDK 10. В корне репозитория:

```powershell
dotnet build Faceit-Simple-cs2/Faceit-Simple-cs2.csproj -c Release
.\pack.cmd
```

`pack.cmd` собирает Release и создаёт `Faceit-Simple-CS2-0.1.1.zip` в корне: серверные файлы, клиентская панорама, исходники и документация.

## EN

Faceit-style warmup for CS2: money on every purchase or spawn, dropped weapons removed every few seconds, `mp_weapons_allow_typecount` unlimited on warmup. A vote card offers a knife round, the first match round is knives only with a second full strip five seconds in, then the winners pick T or CT in a paused warmup and the match starts at 0:0. Copy `Server-plugins/counterstrikesharp/` into `game/csgo/addons/counterstrikesharp/`, keep your `Faceit-Simple-cs2.jsonc` on update and run `css_faceitsimple reload`. The card is optional: compile `Content-addonmanager/panorama/` into a client addon, otherwise everything works through chat.
