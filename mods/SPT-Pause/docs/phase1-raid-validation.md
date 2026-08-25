# Pause Admiral v1 — raid validation matrix

This matrix defines the physical offline-raid acceptance gate for Pause Admiral on SPT 4.1.x.

## Baseline

1. Open hideout and press `P`: nothing pauses; one offline-only warning is acceptable.
2. Start an offline PMC raid and wait until the player and AI are active.
3. Record the raid timer, time of day, hydration and energy before the first pause.

## Core pause

1. Press `P` while an AI is moving or animating.
2. Confirm AI, player simulation, animations, physics and any projectile in flight stop.
3. Keep the pause active for at least 20 seconds.
4. Confirm the raid timer does not decrease and `PAUSED` is displayed when that option is enabled.
5. Try mouse/look input and confirm the camera remains fixed with no free-camera/drift behavior.
6. Try movement, fire, reload, inventory/action inputs while paused; none may be applied to gameplay or queued for resume.
7. With default settings, game audio remains unchanged. Repeat once with `Pause audio` enabled and confirm audio pauses/restores.

## Resume and clocks

1. Press `P` again.
2. Confirm the world resumes once, without a simulation burst or fast-forward.
3. Confirm no action entered during the paused interval executes after resume.
4. Confirm normal player and camera controls work immediately after resume.
5. Confirm the raid deadline, displayed timer and time of day exclude the paused duration.
6. Confirm hydration/energy did not jump by the paused duration.
7. Run three consecutive Pause → Resume cycles, including one pause longer than 60 seconds.

## Recovery paths

1. Pause, open F12, switch `Enabled` off, then press `P`: Resume must still work; a new pause must not start afterward.
2. Re-enable the mod, pause, then leave/end the raid: menu time scale, player input ownership and audio must be normal.
3. Change the keybind in F12 and verify both pause and resume use the new binding.
4. Check `BepInEx/LogOutput.log`: no Pause Admiral Harmony, reflection, timer, input or coroutine exceptions.

## v1 acceptance result

**PASS.** The final v1 runtime check confirmed correct world/AI/timer pause, fixed camera behavior, suppression of paused gameplay inputs without post-resume action bursts, normal controls after resume, and no observed Pause exceptions.

Any future regression reopens this gate and must be recorded with the exact failing step, SPT version, map, active mods and relevant BepInEx log excerpt.
