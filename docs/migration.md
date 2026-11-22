# Upstream migration checklist

Pinned upstream revision [patch-gg/pixelfx@5294518](https://github.com/patch-gg/pixelfx/tree/5294518ce7c72e48b6100a744596e0acf57f2826). This file is the source of truth for the 244 effect IDs and the migration queue.

## Resume here

| Effect or dependency | Status | Next action |
| --- | --- | --- |
| `impacts/hit-flash` | Next | Bind the foreground flash to the hit actor's current sprite using the shared mask. Port the white, red, rim and cooldown beats, comic lines and lights. Compare seed 17 at 30 FPS using the same actor artwork in Unity and upstream. Verify preview fallback and animated runtime alignment. |
| `moves/fireball-throw` | Playable adaptation | Compare the charge and blast to upstream and record remaining differences. |

1. Read this queue and run `git status --short` at the Unity project root. Check the effect folder and existing diffs before resuming an in-progress entry.
2. Use a checkout of the pinned upstream revision. The source for each ID is `src/fx/<effect-id>.js`. Follow the [authoring guide](authoring.md) and [repository skill](../skills/pixelfx-unity/SKILL.md). Record the effect's layering requirements in the active queue entry before coding.
3. Set the queue status to **In progress** before starting. After implementation, record any unfinished step, dependency, or validation failure directly in the queue before stopping.
4. Run `PixelFx.Editor.PixelFxValidation.Run` in Unity 2022.3 and compare representative Full frames against the pinned JavaScript source at the same seed and 30 FPS. Check anchor, duration, the library preview, asset playback, and the Simplified and Off quality levels.
5. Check a catalog entry when it passes. Store the seed, comparison frames and any intentional differences in the completed entry or a linked evidence file. Update both README totals to match the checked entries. The original Shuriken belongs outside the upstream count.
6. Follow [the commit rules](../skills/pixelfx-unity/SKILL.md#commit-each-verified-step). Commit the verified port, its evidence and the updated queue together before starting the next effect. Review the staged diff and check `git status --short` after committing.

An unchecked entry without a queue note is pending. Keep upcoming batches small enough to leave a verified checkpoint between them.

## Character layering assessment

For each next effect, read its upstream drawing passes and any referenced effects or helpers. Check `drawBack`, `draw`, `p.layer`, per-particle layer arguments, and far/near geometry. A `drawBack` declaration is a search lead. Inspect what it draws and what should sit between the passes. Check actor mask or pixel reads separately. For chained actions, inspect the cast, travel and hit phases and any upstream `act` events.

Use the [Lower, Middle and Upper authoring convention](authoring.md#actor-relative-effect-layers). For each stage, record the layer choices, anchor, sorting reference, and whether it follows an actor or uses a position snapshot. Record any required actor mask or action events. When a prerequisite is missing, put it in the queue, verify and commit the shared capability first, then verify and commit the effect. Layered acceptance includes testing overlap with a character and comparing the standalone preview with upstream.

| Effect | Layering and actor requirements |
| --- | --- |
| `impacts/ground-slam` | Use a foot anchor. Crater, cracks, embers and back particles are Lower. The dust ring is Middle with far and near passes. Shock fronts, rocks, foreground particles and the flash are Upper at the foot anchor. Verify the upstream order with an actor between the passes. |
| `arcane/magic-circle-cast`, `fire/fire-ring`, `holy/divine-shield` | Upstream draws around a center or actor using both passes. Verify the geometry and particle assignment for each effect when it enters the queue. For a ring, put the far arc behind the actor and the near arc in front. |
| `moves/fireball-throw` | For the requested A-to-B acceptance case, the Middle cast ring follows A and the projectile follows its travel path. Each Upper impact follows its own target at its hit anchor for the full impact phase. Check with A and B moving and using different sorting orders. This is a Unity acceptance case for stage-specific binding and sorting. |
| `impacts/hit-flash` | Draw the recolor, outline and hit accents in front of the hit actor. Use the sprite pivot for the mask and the chest for the hit accents. Follow the renderer and its current sprite throughout the effect. The game owns the hit pose, hit stop and camera shake. |
| `melee/heavy-cleave` | Standalone frames are verified. Upstream places the far and near shock-ring halves around the fighter. Revisit actor-overlap playback once shared layering is available. |

## Latest checkpoint

**6 / 244 verified** in Unity as of 2026-11-22. Hit Flash is next.

Shared actor masks passed `PixelFx.Editor.PixelFxValidation.Run` in Unity 2022.3.62f3. The check covers source RGB and nonzero alpha, a four-neighbor outline, sprite-frame changes on the same sheet, both flips around an off-center pivot, conversion from sprite PPU to painter PPU, and clearing an unavailable mask. The sprites in the check use a readable, unsplit sheet. The Hit Flash port still needs the runtime foreground overlay, editor preview artwork and an upstream frame comparison.

Unity 2022.3.62f3 batch validation passed for Lower anchor snapshots, Middle passes on opposite sides of a caster's sorting group, launch from a moving caster, Upper impacts following separate targets, and stop cleanup. Full, Simplified and Off keep the same duration, and Off creates no sprite surfaces. The imported **Layered Fireball** Demo compiled. Visual Play Mode review of the overlap remains open. Fireball Throw still needs upstream visual comparison before it counts as a verified port.

| Effect | Verification | Evidence |
| --- | --- | --- |
| `impacts/ground-slam` | Unity 2022.3.62f3 package validation and imported Demo compilation passed. Seed 5, 288 × 208, foot pivot (144, 164), 30 FPS. All 96 playback frames matched upstream alpha and RGB differed by at most one 8-bit level. Full and Simplified reproduced the same frames after seeking backward and forward. Batch validation checked both passes around the actor's sorting order and the contact position after the actor moved. Simplified keeps the crater, cracks, arcs, shock fronts and flash. Off creates no surfaces, and all qualities complete at 3.2 seconds. The in-editor visual overlap check remains open in the `Ground Slam` Demo. | [Comparison](media/ground-slam-comparison.png) · [Unity animation](media/ground-slam.gif) |
| `impacts/crit-hit` | Unity 2022.3.62f3 package validation and imported Demo compilation passed. Seed 3, 216 × 136, pivot (84, 106), 30 FPS. Alpha matched all 27 playback frames and RGB differed by at most one 8-bit level. Backward seeks reproduced the same frames. Simplified keeps the starburst, ring and flare. Off uses no surface. All qualities last 0.9 seconds at default speed. Parry remained pixel-identical across 30 frames in both Full and Simplified after extracting shared code. | [Comparison](media/crit-hit-comparison.png) · [Unity animation](media/crit-hit.gif) |
| `impacts/blunt-hit` | Unity 2022.3.62f3 package validation and imported Demo compilation passed. Seed 9, 128 × 88, pivot (64, 72), 30 FPS. Alpha matched across all 27 playback frames. Six pixel samples differed by two 8-bit RGB levels, and all other samples differed by at most one. Re-seeking reproduced the same frames for seeds 9, 17 and 52. Simplified keeps the flash, wedges and glow and skips dust and clods. Off uses no surface. All three qualities last 0.9 seconds at default speed. | [Comparison](media/blunt-hit-comparison.png) · [Unity animation](media/blunt-hit.gif) |
| `impacts/parry` | Unity 2022.3.62f3 package validation and imported Demo compilation passed. Seed 11, 200 × 112, pivot (100, 96), 30 FPS. All 30 playback frames matched upstream alpha, and RGB differed by at most one 8-bit level. Backward seeks reproduced the captured frames. Simplified keeps the blade flash, star and ring. Off uses no surface. All three quality levels last one second at default speed. | [Comparison](media/parry-comparison.png) · [Unity animation](media/parry.gif) |
| `impacts/hit-spark` | Unity 2022.3.62f3 package validation, quality and backward-seek checks passed. Seed 4, 136 × 80, pivot (44, 64), 30 FPS. All 15 playback frames matched upstream alpha, and RGB differed by at most one 8-bit level. | [Comparison](media/hit-spark-comparison.png) · [Unity animation](media/hit-spark.gif) |
| `melee/heavy-cleave` | Verified in Unity and accepted in Play Mode. | [Comparison](media/heavy-cleave-unity-comparison.gif) |

For byte comparisons, use upstream `renderFrames(...).frames[i].clear` RGBA data. The sprite-sheet exporter multiplies color by alpha when pasting each frame. Unity one-shots finish at their duration, so compare frames sampled before that endpoint.

## Upstream catalog

## arcane

- [ ] `arcane/arcane-beam`
- [ ] `arcane/arcane-missiles`
- [ ] `arcane/arcane-orb`
- [ ] `arcane/blink-in`
- [ ] `arcane/blink-out`
- [ ] `arcane/magic-circle-cast`
- [ ] `arcane/mana-burst`
- [ ] `arcane/portal`
- [ ] `arcane/rune-circle`
- [ ] `arcane/spell-charge`
- [ ] `arcane/star-fall`
- [ ] `arcane/time-stop`

## critters

- [ ] `critters/bat`
- [ ] `critters/bird`
- [ ] `critters/butterfly`
- [ ] `critters/eyeball`
- [ ] `critters/fish-school`
- [ ] `critters/flying-skull`
- [ ] `critters/frog`
- [ ] `critters/ghost`
- [ ] `critters/jellyfish`
- [ ] `critters/slime`
- [ ] `critters/snake`
- [ ] `critters/wisp`

## explosions

- [ ] `explosions/barrel-explosion`
- [ ] `explosions/big-explosion`
- [ ] `explosions/cartoon-pow`
- [ ] `explosions/dirt-explosion`
- [ ] `explosions/firework-burst`
- [ ] `explosions/firework-rocket`
- [ ] `explosions/grenade`
- [ ] `explosions/implosion`
- [ ] `explosions/nuke`
- [ ] `explosions/plasma-explosion`
- [ ] `explosions/small-explosion`
- [ ] `explosions/smoke-bomb`

## fire

- [ ] `fire/brazier`
- [ ] `fire/campfire`
- [ ] `fire/candle`
- [ ] `fire/fire-breath`
- [ ] `fire/fire-ring`
- [ ] `fire/fire-tornado`
- [ ] `fire/fireball`
- [ ] `fire/flame-pillar`
- [ ] `fire/flamethrower`
- [ ] `fire/meteor`
- [ ] `fire/spirit-flame`
- [ ] `fire/torch`

## holy

- [ ] `holy/blessing`
- [ ] `holy/divine-shield`
- [ ] `holy/feather-burst`
- [ ] `holy/halo`
- [ ] `holy/heal-aoe`
- [ ] `holy/heal`
- [ ] `holy/holy-pillar`
- [ ] `holy/resurrection`
- [ ] `holy/smite`
- [ ] `holy/sunburst`

## ice

- [ ] `ice/blizzard-vortex`
- [ ] `ice/freeze`
- [ ] `ice/frost-breath`
- [ ] `ice/frost-nova`
- [ ] `ice/ice-crystal`
- [ ] `ice/ice-lance`
- [ ] `ice/ice-shatter`
- [ ] `ice/ice-spikes`
- [ ] `ice/icicle-fall`
- [ ] `ice/snowflake-burst`

## impacts

- [x] `impacts/blunt-hit`
- [ ] `impacts/bullet-impact`
- [x] `impacts/crit-hit`
- [x] `impacts/ground-slam`
- [ ] `impacts/heavy-landing`
- [ ] `impacts/hit-flash`
- [x] `impacts/hit-spark`
- [ ] `impacts/impact-frame`
- [x] `impacts/parry`
- [ ] `impacts/ricochet`
- [ ] `impacts/shield-block`

## lightning

- [ ] `lightning/ball-lightning`
- [ ] `lightning/chain-lightning`
- [ ] `lightning/charge-up`
- [ ] `lightning/electrified`
- [ ] `lightning/lightning-strike`
- [ ] `lightning/plasma-ball`
- [ ] `lightning/spark-arc`
- [ ] `lightning/storm-cloud`
- [ ] `lightning/tesla-coil`
- [ ] `lightning/thunderclap`
- [ ] `lightning/zap-hit`

## melee

- [ ] `melee/claw-swipe`
- [ ] `melee/dagger-flurry`
- [ ] `melee/double-slash`
- [ ] `melee/hammer-smash`
- [x] `melee/heavy-cleave`
- [ ] `melee/scythe-reap`
- [ ] `melee/spear-sweep`
- [ ] `melee/spin-slash`
- [ ] `melee/sword-slash`
- [ ] `melee/thrust`
- [ ] `melee/uppercut`
- [ ] `melee/whip-crack`

## moves

- [ ] `moves/arrow-shot`
- [ ] `moves/dash-strike`
- [ ] `moves/earth-stomp`
- [ ] `moves/fireball-throw` — playable C# adaptation. Upstream charge and blast details remain.
- [ ] `moves/ground-pound`
- [ ] `moves/holy-smite`
- [ ] `moves/ice-lance-throw`
- [ ] `moves/lightning-call`
- [ ] `moves/meteor-call`
- [ ] `moves/pistol-shots`
- [ ] `moves/rocket-launcher`
- [ ] `moves/sword-combo`

## nature

- [ ] `nature/acid-splash`
- [ ] `nature/boulder`
- [ ] `nature/earth-spike`
- [ ] `nature/healing-bloom`
- [ ] `nature/leaf-storm`
- [ ] `nature/poison-cloud`
- [ ] `nature/quake`
- [ ] `nature/root-snare`
- [ ] `nature/spore-burst`
- [ ] `nature/thorns`
- [ ] `nature/tornado`
- [ ] `nature/toxic-puddle`
- [ ] `nature/vine-whip`

## pickups

- [ ] `pickups/chest-open`
- [ ] `pickups/coin-spin`
- [ ] `pickups/collect-burst`
- [ ] `pickups/crystal-shard`
- [ ] `pickups/gem-spin`
- [ ] `pickups/heart-pickup`
- [ ] `pickups/key-glint`
- [ ] `pickups/loot-beam`
- [ ] `pickups/potion`
- [ ] `pickups/power-star`
- [ ] `pickups/xp-orb`

## projectiles

- [ ] `projectiles/arrow`
- [ ] `projectiles/bullet-tracer`
- [ ] `projectiles/cannonball`
- [ ] `projectiles/energy-disc`
- [ ] `projectiles/flare`
- [ ] `projectiles/homing-missile`
- [ ] `projectiles/laser-bolt`
- [ ] `projectiles/magic-missile`
- [ ] `projectiles/muzzle-pistol`
- [ ] `projectiles/muzzle-shotgun`
- [ ] `projectiles/muzzle-smg`
- [ ] `projectiles/plasma-shot`
- [ ] `projectiles/rocket`

## scifi

- [ ] `scifi/emp-blast`
- [ ] `scifi/energy-core`
- [ ] `scifi/force-field`
- [ ] `scifi/glitch`
- [ ] `scifi/hologram`
- [ ] `scifi/jetpack`
- [ ] `scifi/laser-grid`
- [ ] `scifi/scanner`
- [ ] `scifi/teleporter-pad`
- [ ] `scifi/tractor-beam`
- [ ] `scifi/warp-in`
- [ ] `scifi/warp-out`

## shadow

- [ ] `shadow/black-hole`
- [ ] `shadow/curse`
- [ ] `shadow/dark-explosion`
- [ ] `shadow/eclipse`
- [ ] `shadow/grasping-hands`
- [ ] `shadow/hex-sigil`
- [ ] `shadow/life-steal`
- [ ] `shadow/necro-skulls`
- [ ] `shadow/shadow-flames`
- [ ] `shadow/shadow-tendrils`
- [ ] `shadow/soul-drain`
- [ ] `shadow/void-orb`

## spawn

- [ ] `spawn/coin-drop`
- [ ] `spawn/disintegrate`
- [ ] `spawn/dissolve-burn`
- [ ] `spawn/egg-hatch`
- [ ] `spawn/poof`
- [ ] `spawn/respawn-beam`
- [ ] `spawn/shatter-death`
- [ ] `spawn/soul-rise`
- [ ] `spawn/spawn-portal`
- [ ] `spawn/summon-rise`

## status

- [ ] `status/bleeding`
- [ ] `status/burning`
- [ ] `status/charmed`
- [ ] `status/confused`
- [ ] `status/cursed`
- [ ] `status/frozen`
- [ ] `status/haste`
- [ ] `status/petrified`
- [ ] `status/poisoned`
- [ ] `status/rage`
- [ ] `status/sleep`
- [ ] `status/slowed`
- [ ] `status/stunned`

## transitions

- [ ] `transitions/blinds`
- [ ] `transitions/burn-away`
- [ ] `transitions/crt-off`
- [ ] `transitions/diamond-wipe`
- [ ] `transitions/glitch-wipe`
- [ ] `transitions/iris`
- [ ] `transitions/pixel-dissolve`
- [ ] `transitions/shatter-wipe`
- [ ] `transitions/slash-wipe`
- [ ] `transitions/star-iris`

## ui

- [ ] `ui/achievement`
- [ ] `ui/coin-collect`
- [ ] `ui/combo-counter`
- [ ] `ui/confetti`
- [ ] `ui/countdown`
- [ ] `ui/crit-number`
- [ ] `ui/damage-number`
- [ ] `ui/exclamation`
- [ ] `ui/health-bar-hit`
- [ ] `ui/heart-lost`
- [ ] `ui/level-up`
- [ ] `ui/quest-complete`

## water

- [ ] `water/bubbles`
- [ ] `water/drip`
- [ ] `water/fountain`
- [ ] `water/geyser`
- [ ] `water/rain-splash`
- [ ] `water/ripples`
- [ ] `water/splash`
- [ ] `water/water-jet`
- [ ] `water/water-orb`
- [ ] `water/waterfall`
- [ ] `water/wave-crash`
- [ ] `water/whirlpool`

## weather

- [ ] `weather/aurora`
- [ ] `weather/blizzard`
- [ ] `weather/cherry-blossoms`
- [ ] `weather/clouds`
- [ ] `weather/falling-leaves`
- [ ] `weather/fireflies`
- [ ] `weather/fog`
- [ ] `weather/god-rays`
- [ ] `weather/rain`
- [ ] `weather/shooting-stars`
- [ ] `weather/snow`
- [ ] `weather/storm`
