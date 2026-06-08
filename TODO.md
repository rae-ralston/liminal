# liminal — TODO

Cross-reference with `GDD.md` for design intent.

## Setup & Art

- [ ] set up separate security desk room for game initiation/boot screen

## Map & Navigation

- [ ] Map layout — sketch on paper first, then build all rooms in scene (bo)
- [ ] Door system: scene-loading (weird spaces — never-ending corridor, looping room, etc.)
- [ ] Camera confiner _(hold until map is final)_

## Opening Sequence

- [ ] Boot screen — black screen, computer boot sounds, "CLICK" prompt
- [ ] Desk lamp turns on after first interact
- [ ] Flashlight on desk — first interactable, activates ViR on pickup

## ViR (Vision Radius)

- [x] Review Bo's Light2D branch before starting ViR implementation
- [x] Global Light 2D — dark ambient (near-black / deep blue-black)
- [x] Update all sprite renderers to Sprite-Lit-Default shader
- [ ] ViR core — darkness beyond player's light radius
- [ ] Desk lamp — permanent wider light pool at home base (stationary Point Light 2D)
- [x] Flashlight as in-world ViR representation (Point Light 2D on player, small radius, soft falloff)
- [ ] ViR Multiplier — expands radius, decays over time after grace period
- [ ] ViR Button — resets decay timer + increments counter (located at central computer desk)
- [ ] Grace period — time before decay begins; upgradeable

## Interact System

- [ ] Flashlight highlight — pointing at interactable object highlights it
- [ ] Static overlays — for reading monitors, papers, close-up content

## Central Computer & Counter

- [ ] SummonFinalButton — trigger final button appearance in scene (stub exists in GameManager)
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

- [ ] Security room — custom painted environment for Dylan (monitors/TVs, security desk, home base feel)
- [ ] TV flicker effect in security room — shader or animation
- [ ] Found-footage / grainy camera effect in security room — scoped post-processing Volume
- [ ] Environmental details — desks, left trays, posters, vending machines, running tap, blinking phone, flickering lights
- [ ] Looping announcements — sparse, rarely repeat, abruptly abort/glitch _(Bo — FMOD)_

## Lighting

- [ ] Assess Bo's LightsPlayground.unity setup in depth — understand Light2D configuration, URP renderer setup, FlashlightAimToMouse script
- [ ] Port lighting from LightsPlayground into Main scene
- [ ] Resolve FMOD bank location with Bo (currently FMOD/Desktop/ — may need to move to Assets/StreamingAssets/)

## Visual Polish

- [x] Post-processing Volume — bloom, chromatic aberration, film grain, vignette *(color grading / cool tint pass still needed)*
- [ ] Scanline overlay — custom fullscreen shader or UI overlay _(late polish)_
- [ ] Infinite corridor shader — scrolling UV trick for never-ending tunnel scene _(required for weird spaces)_

## Audio

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
- [x] GameManager singleton — tracks counter, GameStarted, FinalButtonSummoned
- [x] SecurityStation (central computer) interactable — calls GameManager.StartGame()
- [x] InteractableTrigger base class — handles trigger boilerplate for all interactables
- [x] Place `environment-grey-room-desk` tiles in scene
- [x] Implement new character sheet
- [x] Wire new character sprites into animation
- [x] Idle direction memory (holds last facing direction on stop)
- [x] Door system: in-scene teleport trigger (normal rooms)
- [x] FMOD banks in repo (Bo authoring) — currently in FMOD/Desktop/, location TBD with Bo
- [x] Interact button (approach object + press key) — `IInteractable` interface, `SetInteractable`/`ClearInteractable` on PlayerMovement
- [x] Central computer (SecurityStation) — first press starts system, second press (at threshold) summons final button
- [x] Counter — increments automatically over time (GameManager.Update)
- [x] Pause screen — Escape toggles pause, MenuPanel (Resume/Controls/Quit) + ControlsPanel (Back), freezes time and disables player input *(needs visual polish)*
