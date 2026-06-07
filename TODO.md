# liminal — TODO

Cross-reference with `GDD.md` for design intent.

## Setup & Art

- [ ] Place `environment-grey-room-desk` tiles in scene
- [ ] Implement new character sheet 
- [ ] Wire new character sprites into animation
- [ ] Idle direction memory (snaps to IdleDown on stop) _(blocked on idle animation state)_

## Map & Navigation

- [ ] Map layout — sketch on paper first, then build all rooms in scene
- [x] Door system: in-scene teleport trigger (normal rooms)
- [ ] Door system: scene-loading (weird spaces — never-ending corridor, looping room, etc.)
- [ ] Camera confiner _(hold until map is final)_

## Opening Sequence

- [ ] Boot screen — black screen, computer boot sounds, "CLICK" prompt
- [ ] Desk lamp turns on after first interact
- [ ] Flashlight on desk — first interactable, activates ViR on pickup

## ViR (Vision Radius)

- [ ] Review Bo's Light2D branch before starting ViR implementation
- [ ] Global Light 2D — dark ambient (near-black / deep blue-black)
- [ ] Update all sprite renderers to Sprite-Lit-Default shader
- [ ] ViR core — darkness beyond player's light radius *(assess Dylan's solution)*
- [ ] Desk lamp — permanent wider light pool at home base (stationary Point Light 2D)
- [ ] Flashlight as in-world ViR representation (Point Light 2D on player, small radius, soft falloff)
- [ ] ViR Multiplier — expands radius, decays over time after grace period
- [ ] ViR Button — resets decay timer + increments counter (located at central computer desk)
- [ ] Grace period — time before decay begins; upgradeable

## Interact System

- [x] Interact button (approach object + press key) — `IInteractable` interface, `SetInteractable`/`ClearInteractable` on PlayerMovement
- [ ] Flashlight highlight — pointing at interactable object highlights it
- [ ] Static overlays — for reading monitors, papers, close-up content

## Central Computer & Counter

- [ ] Central computer — first press starts system, second press (at threshold) summons final button
- [ ] Counter — increments automatically over time
- [ ] Counter display — in-world indicator (TBD: digital clock, ticker; in-world preferred)

## Upgrades

- [ ] Counter Multiplier — increases counter increment rate
- [ ] Auto-Clicker — increments counter without player presence
- [ ] Auto-Clicker Multiplier — increases auto-clicker speed
- [ ] Bulk Click — one-time large instant counter boost
- [ ] ViR expansion — permanently increases vision radius
- [ ] Grace Period extension — upgrades time before ViR decay begins

## Malfunctions

- [ ] Malfunctions — counter/auto-clicker randomly stops
- [ ] Repair minigame — slider, stop in optimal zone
- [ ] Repair outcomes — success restores device; failure triggers cooldown + partial reset risk

## Endgame

- [ ] Final button — large, disproportionate, appears after second computer press, must be found
- [ ] Ending sequence — ambiguous, player presses final button, something happens

## Environment & Storytelling

- [ ] Environmental details — desks, left trays, posters, vending machines, running tap, blinking phone, flickering lights
- [ ] Looping announcements — sparse, rarely repeat, abruptly abort/glitch _(Bo — FMOD)_

## Visual Polish

- [ ] Post-processing Volume — bloom, color adjustments (desaturated cool tint), chromatic aberration, film grain, vignette
- [ ] Scanline overlay — custom fullscreen shader or UI overlay *(late polish)*
- [ ] Infinite corridor shader — scrolling UV trick for never-ending tunnel scene *(required for weird spaces)*

## Audio

- [ ] FMOD banks in repo (Bo authoring)
- [ ] Audio wired to scene — positional audio, electronics hum, ViR decay cue, computer/button signatures

## Build

- [ ] WebGL build

## Completed
- [x] Player movement (WASD/arrows, Rigidbody2D velocity)
- [x] Directional animations with sprite flipping for left
- [x] Tiled room — floor, wall, furniture, props tilemap layers
- [x] Wall + furniture collisions
- [x] Camera follow (Cinemachine)
- [x] Dylan's environment tilesets imported and sliced (32x32)
- [x] FMOD plugin installed and merged (Bo)
- [x] Tilemap Order in Layer values — floor -1, walls 0, furniture 1, props 2, player 3
- [x] Rigidbody2D Z rotation freeze fix (spinning character bug)
- [x] `environment-grey-room-desk` imported, sliced (16x16), tiles added to palette
