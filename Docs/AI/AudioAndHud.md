# Sound and HUD redesign

Update 2026-10-15.

## HUD
The wallet card, upgrade shop, staff roster and shift report now use the toy style of the Day and Jobs cards (cream bubbles, brown outlines, blue/orange/green buttons).

- **Wallet card:** gold coin, an animated count-up balance, floating `+$` / `-$` popups, a reputation bar (red under 35, orange under 65, green above; the tick marks the 65 bonus line), three daily-goal rows with progress bars and a green tick when complete, and a `U  HOTEL UPGRADES` hint. It is hidden on the title screen.
- **Shop:** one card per upgrade with level pips, description and a BUY / MAX button (orange when you cannot afford it).
- **Roster and report:** striped rows, big toggle buttons, stat chips, a ledger with green/red amounts and a SHIFT NET banner.
- Shapes come from `HotelToyKit.cs`, which uses a few shared 9-sliced textures, so animated bars never allocate textures.

## Sound
All sound effects and both music loops are WAV files in `Assets/HotelTogether/Resources/HotelTogetherAudio`, made by `Tools/Audio/generate_hotel_audio.py` (numpy + scipy; run it to regenerate). They are synthesized, not studio recordings. **To replace any sound, drop a WAV/OGG with the same name in that folder.** A missing file is just silent (or falls back to the old built-in tone).

| Group | Files |
|---|---|
| UI | ui_click, ui_hover, ui_back, ui_open, ui_close, toast, toast_urgent, deny |
| Money | coin, coin_big, coin_loss, buy, goal, checkin, cha_ching |
| World | pickup, drop, throw, step_0..3, land, impact_soft, impact_hard, door_open, door_close, bell, elevator, knock, hammer, gunshot, reload |
| Zombies | zombie_groan_0..2, zombie_attack, zombie_hit, zombie_die, player_hurt |
| Stings | sting_day, sting_night, sting_dawn |
| Music | music_day (122 bpm, marimba and ukulele), music_night (104 bpm, neon bass and eerie lead) |

`HotelAudioDirector` crossfades the day and night music with the day/night grade, muffles and ducks it while paused, and plays the night, dawn and new-day stings. **Settings → MUSIC VOLUME** is saved. `HotelAudio` is the one-line API (`HotelAudio.Play("coin")`, `HotelAudio.PlayAt("door_open", position)`).

Hooked up: menu buttons, notices, wallet changes, goals, shop, pick up / drop / footsteps / landing, prop impacts, doors (not the automatic entrance), the reception bell, elevator chime, room knock, gun shot and reload, zombie groans / attacks / hits / deaths, player hurt, and the title swoop.

Not changed: the looping water, generator, pump and maintenance hiss sounds are still the original procedural ones.
