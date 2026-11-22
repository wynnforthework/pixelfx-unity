# Upstream migration checklist

Pinned upstream revision [patch-gg/pixelfx@5294518](https://github.com/patch-gg/pixelfx/tree/5294518ce7c72e48b6100a744596e0acf57f2826). This file is the source of truth for the 244 effect IDs and the migration queue.

## Resume here

| Effect or dependency | Status | Next action |
| --- | --- | --- |
| `holy/blessing` | Next | Inspect the cone's back pass, the top-facing sprite mask highlights and how falling particles land on the actor. Record Middle layering and verify which mask operations the port needs before coding. |

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
| `explosions/small-explosion` | Use a foot anchor. For Middle, smoke, dust, scorch, ground blazes and the far shock front are behind the bound renderer. The near shock front, rocks, hot particles and flash are in front. Placement follows the binding's follow or snapshot setting. Fireball Throw will use the combined art for each Upper impact. |
| `arcane/magic-circle-cast` | Anchor at the feet. Middle draws the far floor geometry, far runes and light column behind the actor. Near floor geometry, near runes, pen glints, particles and the flash draw in front. Use the bound renderer or sorting group and honor follow or snapshot placement. |
| `fire/fire-ring` | Anchor at the feet. Middle draws scorch, the far arc, far flames and smoke behind the bound renderer or sorting group. The near arc, near flames and burst flash draw in front. Flame puffs and embers stay on their source side. The binding controls follow or snapshot placement. Gameplay owns poses, hit stop, shake, impact dust and target charring. |
| `holy/divine-shield` | Anchor at the feet and honor the binding's follow or snapshot placement. Middle puts the far film, far lattice, far seal and glow behind the bound renderer or sorting group. The near film, near lattice, rim, glints, motes, near seal and optional sprite-mask outline draw in front. The game owns the defensive pose. |
| `moves/fireball-throw` | For the requested A-to-B acceptance case, the Middle cast ring follows A and the projectile follows its travel path. Each Upper impact follows its own target at its hit anchor for the full impact phase. Check with A and B moving and using different sorting orders. This is a Unity acceptance case for stage-specific binding and sorting. |
| `impacts/hit-flash` | Draw the recolor, outline and hit accents in front of the hit actor. Use the sprite pivot for the mask and the chest for the hit accents. Follow the renderer and its current sprite throughout the effect. The game owns the hit pose, hit stop and camera shake. |
| `melee/heavy-cleave` | Standalone frames are verified. Upstream places the far and near shock-ring halves around the fighter. Revisit actor-overlap playback once shared layering is available. |

## Latest checkpoint

**14 / 244 verified** in Unity as of 2026-11-22. Divine Shield passed standalone frame comparison and actor-relative sorting checks. Interactive Play Mode checks for Divine Shield, Fire Ring, Magic Circle Cast, Small Explosion, Ground Slam and Hit Flash remain open.

Shared actor masks passed `PixelFx.Editor.PixelFxValidation.Run` in Unity 2022.3.62f3. The check covers source RGB and nonzero alpha, a four-neighbor outline, sprite-frame changes on the same sheet, both flips around an off-center pivot, conversion from sprite PPU to painter PPU, and clearing an unavailable mask. The sprites in the check use a readable, unsplit sheet.

Unity 2022.3.62f3 batch validation passed for Lower anchor snapshots, Middle passes on opposite sides of a caster's sorting group, launch from a moving caster, Upper impacts following separate targets, and stop cleanup. Full, Simplified and Off keep the same duration, and Off creates no sprite surfaces. The imported **Layered Fireball** Demo compiled. Rendered sample frames show both sides of the cast ring around the moving caster and the blast in front of the moving target. [Sample keyframes](media/fireball-throw-layered.png)

### Fireball Throw comparison

Captured 205 frames per build at seed 1 and 30 FPS through the 6.81-second reference timeline. The [comparison sheet](media/fireball-throw-comparison.png) shows upstream JavaScript, Unity Full and Unity Simplified at frames 15, 34, 46 and 90. The captures isolate the effect art on a 356 × 244 canvas with origin (64, 198). The Unity capture supplies anchors to match the source's charge (15, -22), launch (28, -22), flight endpoint (184.4, -22) and impact (190, 0), at 32 pixels per world unit. Both builds use child seeds 1, 2 and 3 and the upstream default stage palettes. Twelve Full frames match exactly in RGBA. At an alpha threshold of 32, mean silhouette overlap (intersection over union) is 73% for cast, 93% for flight and 90% for impact, excluding frames where both silhouettes are empty. Stage-local sampling, movement at the reference subpixel positions and the different canvas edge falloff affect the byte comparison.

| Stage | Unity playback |
| --- | --- |
| Cast | Spell Charge is centered on the supplied cast anchor. The upstream 0.08-second cue appears on the first available 30 FPS sample. Short casts can reduce the lead-in. Middle splits the tilted rings at the orb's horizontal plane. The orb and charge accents draw in front. |
| Flight | Fireball follows the launch-to-landing path and rotates with travel direction. The caller's distance and speed determine flight duration. |
| Impact | Each hit plays Small Explosion at its own anchor with radius, firelight and particle counts driven by impact strength and blast size. Upper draws the combined art in front. Gameplay owns the original actor poses, hit stop, shake, throw dust and actor charring. |

Full and Simplified reproduced all 205 captured frames after a forward and backward seek. Duration and cleanup, near/far and zero-distance timing, caster and target movement, and primary versus secondary impacts passed batch validation. All 246 standalone Full PNGs for the three reused effects matched their pre-integration captures.

| Effect | Verification | Evidence |
| --- | --- | --- |
| `holy/divine-shield` | Unity 2022.3.62f3 package validation, library thumbnail, asset playback and updated Demo compilation passed. At seed 8 and 30 FPS, all 72 Full frames matched upstream alpha on the 88 × 84 canvas with foot pivot (44, 68). RGB differed by at most one 8-bit level except for 22 channel samples with a difference of two. The reference was drawn directly with the default options and no actor mask. Full and Simplified reproduced every frame on backward seek and matched their first frame at the cycle boundary. Simplified keeps the shield film, lattice, rim and primary highlights and skips seeded glints and motes. Runtime tests covered Middle sorting, a moving foot anchor, same-frame mask scaling and missing-mask fallback. All qualities finish after one 2.4-second cycle at default speed. Off creates no surfaces. The caller owns the defensive pose. A batch composite shows the two passes around a test sprite. Interactive Play Mode review remains open in the `Divine Shield` Demo. | [Comparison](media/divine-shield-comparison.png) · [Unity animation](media/divine-shield.gif) · [Layer composite](media/divine-shield-layers.png) |
| `fire/fire-ring` | Unity 2022.3.62f3 package validation, library thumbnail and built-in asset playback passed. Seed 6 at 30 FPS, 224 × 128 canvas with foot pivot (112, 92). All 117 Full frames matched upstream alpha, and RGB differed by at most one 8-bit level per channel. Full and Simplified reproduced every frame after a backward seek. Simplified retains the foot flash, expanding ring, flame tongues and scorch while skipping the particle simulation. Batch playback verified both Middle passes and the shared anchor following a moving actor. All three qualities completed at 3.9 seconds at default speed, and Off created no sprite surfaces. Interactive Play Mode review of actor overlap remains open. | [Comparison](media/fire-ring-comparison.png) · [Unity animation](media/fire-ring.gif) |
| `arcane/magic-circle-cast` | Unity 2022.3.62f3 package validation, library thumbnail and built-in asset playback passed. Seed 6 at 30 FPS, 200 × 220 canvas with foot pivot (100, 192). All 117 Full frames matched upstream alpha, and each RGB channel differed by at most one 8-bit level. Full and Simplified reproduced all frames after a backward seek. Simplified retains the pen, runes, floor rings, flash, light column and its breakup while omitting the particle simulation. Batch playback checked that both Middle passes render on their respective sides of a moving actor. All qualities complete after 3.9 seconds at default speed. Off creates no sprite surfaces. Visual overlap with a character in interactive Play Mode remains open. | [Comparison](media/magic-circle-cast-comparison.png) · [Unity animation](media/magic-circle-cast.gif) |
| `explosions/small-explosion` | Unity 2022.3.62f3 package validation and built-in asset playback passed. At seed 7 and 30 FPS, all 162 Full frames matched upstream alpha on the 300 × 240 canvas with foot pivot (150, 196). RGB differed by at most one 8-bit level. Full and Simplified reproduced every frame after backward seeks. Simplified keeps the opening flash, shock fronts, glow and scorch. Batch playback verified both Middle sorting passes, movement with the bound actor, equal 5.4-second completion across all qualities and cleanup. Off creates no surfaces. Interactive Play Mode review of actor overlap remains open. | [Comparison](media/small-explosion-comparison.png) · [Unity animation](media/small-explosion.gif) |
| `fire/fireball` | Unity 2022.3.62f3 package validation and built-in asset playback passed. At seed 5 and 30 FPS, all 36 frames matched upstream alpha on the 136 × 56 canvas with the head pivot (112, 28). RGB differed by at most one 8-bit level except for two pixels on frame 18 with a difference of two. Full and Simplified matched after seeking backward and at the cycle boundary. Simplified keeps the head, flame tongues and glow. Off uses no surfaces. All qualities finish after one 1.2-second cycle at default speed. Runtime checks covered the world anchor and a rotated direction. The standalone effect uses one directional pass. Fireball Throw integration is pending. | [Comparison](media/fireball-comparison.png) · [Unity animation](media/fireball.gif) |
| `arcane/spell-charge` | Unity 2022.3.62f3 package validation and built-in asset playback passed. At seed 5 and 30 FPS, all 48 frames matched upstream alpha on the 120 × 86 canvas with foot pivot (44, 80). RGB differed by at most one 8-bit level. Full and Simplified reproduced every frame after seeking backward. Simplified keeps the orb, contracting rings, pulse flash and glow. Off uses no surfaces. Each runtime call plays one 1.6-second cycle at default speed. The standalone art draws in front with a foot anchor. The upstream actor poses and Fireball Throw's Middle cast integration remain separate tasks. | [Comparison](media/spell-charge-comparison.png) · [Unity animation](media/spell-charge.gif) |
| `impacts/hit-flash` | Unity 2022.3.62f3 package validation and imported Demo compilation passed. Seed 17, 80 × 64, foot pivot (40, 54), 30 FPS, with the same upstream stand-in in both previews. All 15 actor-composited frames matched alpha and RGB differed by at most one 8-bit level. Full and Simplified reproduce their frames after seeking backward. Runtime checks covered foreground sorting, actor movement, scale and rotation, same-frame sprite changes and flips, unreadable-texture fallback, and cleanup. Simplified retains all flash beats and uses three comic lines instead of seven. Off creates no surfaces. All three qualities complete at 0.5 seconds. The visual Play Mode check remains open in the `Hit Flash` Demo. Gameplay hit pose, hit stop and camera shake remain caller-owned. | [Comparison](media/hit-flash-comparison.png) · [Unity animation](media/hit-flash.gif) |
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
- [x] `arcane/magic-circle-cast`
- [ ] `arcane/mana-burst`
- [ ] `arcane/portal`
- [ ] `arcane/rune-circle`
- [x] `arcane/spell-charge`
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
- [x] `explosions/small-explosion`
- [ ] `explosions/smoke-bomb`

## fire

- [ ] `fire/brazier`
- [ ] `fire/campfire`
- [ ] `fire/candle`
- [ ] `fire/fire-breath`
- [x] `fire/fire-ring`
- [ ] `fire/fire-tornado`
- [x] `fire/fireball`
- [ ] `fire/flame-pillar`
- [ ] `fire/flamethrower`
- [ ] `fire/meteor`
- [ ] `fire/spirit-flame`
- [ ] `fire/torch`

## holy

- [ ] `holy/blessing`
- [x] `holy/divine-shield`
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
- [x] `impacts/hit-flash`
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
- [x] `moves/fireball-throw` — verified. See [comparison and layering notes](#fireball-throw-comparison).
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
