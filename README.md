# liminal

Submission for [Liminal Jam 4](https://itch.io/jam/liminal4).

You are alone in an empty office. Everything was interrupted mid-routine — coffee still warm, phones still blinking, lights still humming. A computer in the center of the room is running something. You press the button. The system starts. You're not sure what you've started, or how to stop it — or if you should.

---

## How to play

- **WASD / arrow keys** — move
- **Interact key** — press near highlighted objects to interact
- Point your flashlight at objects to see what's interactable

---

## Setup (team)

**Unity version:** 6000.4.8f1

1. Clone the repo
2. Open the project in Unity Hub — make sure you're on **6000.4.8f1** exactly
3. Open `Assets/Scenes/Main.unity`

### FMOD

The FMOD plugin is committed to the repo (`Assets/Plugins/FMOD/`). Bo authors banks separately in FMOD Studio — when banks are ready they go in `Assets/StreamingAssets/` and must be committed.

Do not ignore `Assets/StreamingAssets/`.

### What's ignored

`Library/`, `Temp/`, `Logs/`, `UserSettings/`, and `Assets/Plugins/FMOD/Cache/` are all gitignored and will be regenerated locally.

---

## Team

- **Rae** — engineering
- **Dylan** — art
- **Bo** — sound & music (FMOD)

Built in Unity 6 with FMOD, Cinemachine, and the Unity Input System.
