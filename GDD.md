# liminal — Game Design Document

**Jam:** [Liminal Jam 4](https://itch.io/jam/liminal4)  
**Due:** July 26, 2026  
**Team:** Rae (engineering), Dylan (art), Bo (sound/music)

---

## Elevator Pitch

You are alone in an empty office. Everything was interrupted mid-routine — coffee still warm, phones still blinking, lights still humming. A computer in the center of the room is running something. You press the button. The system starts. You're not sure what you've started, or how to stop it — or if you should.

---

## Aesthetic & Tone

**Keywords:** liminal, kenopsia, anemoia, weirdcore, dreamcore, oddly familiar places

The world is defined by **interrupted life** — not absence, but the aftermath of interruption. Something was happening here, and suddenly wasn't. The environment tells this story without words. If something is put into words, it remains incomplete. Things are implied. Some things contradict each other.

**Design principles:**

- Signs of former activity everywhere — abandoned tasks, left-on machines, open doors
- Repetition with subtle variation creates unease more than pure randomness
- Disproportionate sizing of scattered elements (the final button is enormous)
- Sound is spatial and environmental — hissing electronics, sparse glitchy announcements, unsettling silence
- The player never fully understands what is happening or why

**Environmental details (non-exhaustive):**

- Empty desks and chairs
- Left trays in a cafeteria
- Posters, flyers, announcements (e.g. "Bring Your Cat Day")
- Active vending machines, water dispensers
- Running tap in a kitchen or bathroom
- Blinking light on a desk telephone
- Open doors
- Loose papers
- Flickering lights/signs
- Looping announcements that rarely repeat, then abruptly abort in a distorted/glitchy way
- Weird architecture — passages that shouldn't exist, stretching corridors

---

## Core Loop

```
Press central computer button
        ↓
System begins incrementing
        ↓
Explore office → find upgrades → solve interactions
        ↓
Malfunctions occur → repair minigame → return to restart
        ↓
Counter reaches threshold
        ↓
Press central computer button again
        ↓
Final button appears (enormous, somewhere in the office)
        ↓
Press final button → ending
```

---

## Opening Sequence

1. Black screen — sounds of an old computer booting (loud fan, mechanical clicking)
2. The monitor flicks on — only light in the room. Screen shows a single word: **CLICK**
3. Player clicks / presses interact
4. The desk lamp turns on — the home base pool of light establishes
5. The flashlight on the desk is highlighted as the first interactable object
6. Player picks up the flashlight — portable ViR is now active
7. The system starts. The game begins.

*Details TBD: whether the boot screen is a static overlay (first-person close-up of monitor) or top-down with an unlit sprite — both work technically. Decide closer to implementation.*

---

## Interactions

- **Interact button** — player approaches an object and presses a key to interact (not pure proximity auto-pickup, not mouse-only)
- **Flashlight highlight** — pointing the flashlight at an interactable object highlights it, so the player knows it can be used before committing to an interaction
- **Static overlays** — for reading monitors, papers, or other close-up content: a simple screen overlay (no perspective switch required)
- Some interactables require an item from the environment to unlock (key, found object, etc.)

---

## Systems

### Vision Radius (ViR)

The player has a limited circle of visibility. Beyond it: darkness.

- The ViR is represented in-world as a **flashlight** — the player picks it up from the home desk at the start of the game
- The flashlight also serves as the **interaction highlight system**: pointing it at an interactable object highlights it, signaling to the player that it can be used
- The home desk has a **single lamp** that stays on — this creates a natural, wider pool of light at home base so the player can always orient themselves and find their way back
- Base ViR (flashlight range) is small — beyond the desk lamp, the office is mostly dark
- A **ViR Multiplier** expands the radius but decays over time after a grace period
- There is a single **ViR Button** in a fixed location in the office
  - Pressing it resets the decay timer, maintaining the expanded ViR
  - Pressing it also increments the counter, benefiting from current multipliers
  - Staying near it is safe and productive — but keeps the player away from exploration
- The **grace period** (time before decay begins after leaving the button) starts small and can be extended through upgrades — early game the player is tethered, late game they have more freedom
- Exploration is the only way to find permanent ViR expansions
- This creates the core tension: **safety vs. discovery**

The darkness is not just a mechanic — it is the liminal feeling made playable.

### Central Computer & Counter

- The central computer is the first and second-to-last interaction in the game
- First press: starts the system, begins incrementing the counter
- The counter increments automatically over time (idle mechanic)
- The player's job is to increase the counter and keep it running
- Second press: only available when the counter reaches a threshold — summons the final button

### Upgrades

Found by exploring the office. Each requires an interaction to unlock (not a logic puzzle — something simpler, like finding a key item or completing a physical task).

- **Counter Multiplier** — increases how fast the counter increments
- **Auto-Clicker** — increments the counter automatically without player presence
- **Auto-Clicker Multiplier** — increases auto-clicker speed
- **Bulk Click** (one-time use) — large instant counter boost
- **ViR expansion** — permanently increases vision radius
- **Grace Period extension** — increases the time before ViR decay begins after leaving the ViR Button

### Malfunctions

- The counter or auto-clicker randomly malfunctions and stops incrementing
- Player must find the malfunctioning device and repair it
- **Repair minigame:** a slider — player must stop it in an optimal zone
  - Successful repair: device restored, system restarts at current count
  - Failed repair: cooldown before retry. Player can restart system without that device in the meantime, but risks losing a multiplier or triggering a partial reset
- Risk of reset creates tension: is it worth attempting a bad repair, or waiting?

### The Final Button

- Appears after the second central computer press
- Physically large — disproportionately huge compared to everything else
- Located somewhere in the office, must be found
- Pressing it ends the game
- The ending is ambiguous

---

## Win / End Condition

There is one ending. It is not clearly good or bad. The player presses the final button. Something happens. The interpretation is left open.

**What the player understands:** they completed the system.  
**What the player doesn't understand:** what the system was for, whether they helped or hurt something, whether anyone is coming back.

---

## Map

Top-down 2D office space. **Scale: roughly one floor of a high-rise office building** — not a sprawling warehouse, not a single room. Small enough to feel claustrophobic, large enough to require exploration.

**Layout philosophy:** Winding rooms and corridors rather than long linear hallways — back-and-forth travel should feel like navigating a building, not walking a corridor. First draft should be sketched on paper.

**Doors:** Some lead to normal spaces, some to weird ones, some require keys or items found in the environment.

**Potential areas (subject to change):**

- Main office floor (central computer + home desk here)
- Boss's office
- Break room / cafeteria
- Bathroom or kitchen (running tap)
- Server room or utility closet (malfunctions happen here)
- At least one corridor that feels slightly wrong

**Architecture ideas:**

- Some passages that shouldn't architecturally exist
- At least one never-ending tunnel / stretching corridor
- Slight repetition between rooms — same layout, something slightly different each time
- Some doors lead to weird spaces (never-ending tunnel, room that loops back on itself)

---

## Audio Direction (Bo)

- Spatial and environmental — FMOD for positional audio
- Hissing and humming of electronics when walking past them
- Sparse looping announcements — seldom, abrupt glitchy abort, not in every area
- Unsettling silence as a tool, not a gap
- The central computer and final button have distinct audio signatures
- ViR Multiplier decay could have an audio cue

---

## Production Timeline

| Week     | Focus                                                       |
| -------- | ----------------------------------------------------------- |
| 1 (done) | Project setup, player movement, tilemap, animations         |
| 2-3      | ViR system, central computer + counter, basic interactables |
| 3-4      | Malfunctions + repair minigame, auto-clicker                |
| 5        | Map expansion, environmental storytelling details           |
| 6        | Final button sequence, ending                               |
| 7        | Polish, FMOD audio integration, WebGL build                 |

---

## Open Questions

- What exactly happens at the ending? (team to decide)
- How many malfunction types? (start with 1, expand if time allows)
- Does the player have any dialogue or text? (current direction: no — environment only)
- What is the counter counting? (intentionally unclear, but worth agreeing internally)
- Does the office change visually as the counter increases? (would reinforce the liminal feeling)
- **Counter display:** some visual indication of progress is agreed — method TBD. Options: in-world element (digital clock, ticker on the computer), UI overlay, or both. In-world preferred for immersion.
- ~~Where exactly is the ViR Button located?~~ **Resolved:** The ViR Button is on the same desk as the central computer. The desk is the player's home base — lamp provides a permanent pool of light. Flashlight is picked up here at game start.
