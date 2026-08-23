# SPT Pause Phase 1 — raid validation matrix

Run on the user's exact SPT 4.1.2 mod stack. Use the standalone `runtime-pause` package only.

## Baseline

1. Open hideout and press `P`: nothing pauses; one offline-only warning is acceptable.
2. Start an offline PMC raid and wait until the player and AI are active.
3. Record the raid timer, time of day, hydration and energy before the first pause.

## Core pause

1. Press `P` while an AI is moving or animating.
2. Confirm AI, player simulation, animations, physics and any projectile in flight stop.
3. Keep the pause active for at least 20 seconds.
4. Confirm the raid timer does not decrease and `PAUSED` is displayed when that option is enabled.
5. Open inventory and move an item; UI/inventory interaction must remain available.
6. With default settings, game audio remains unchanged. Repeat once with `Pause audio` enabled and confirm audio pauses/restores.

## Resume and clocks

1. Press `P` again.
2. Confirm the world resumes once, without a simulation burst or fast-forward.
3. Confirm the raid deadline, displayed timer and time of day exclude the paused duration.
4. Confirm hydration/energy did not jump by the paused duration.
5. Run three consecutive Pause → Resume cycles, including one pause longer than 60 seconds.

## Recovery paths

1. Pause, open F12, switch `Enabled` off, then press `P`: Resume must still work; a new pause must not start afterward.
2. Re-enable the mod, pause, then leave/end the raid: menu time scale and audio must be normal.
3. Change the keybind in F12 and verify both pause and resume use the new binding.
4. Check `BepInEx/LogOutput.log`: no Harmony, reflection, timer or coroutine exceptions.

## Pass criterion

All checks above pass without freezes, menu lag, time jumps or persistent global state. Any failure is recorded with the exact step, map, active mods and the relevant BepInEx log excerpt.
