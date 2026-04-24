# KeyFlow W6 Sub-Project 12 — StartScreen BGM

**Date:** 2026-04-24
**Week:** 6 (폴리싱 + 사운드)
**Parent MVP spec:** `docs/superpowers/specs/2026-04-20-keyflow-mvp-v2-4lane-design.md`
**Depends on:**
- W6 SP9 profile start screen (merged `ca8c648`) — SP12 adds an `AudioSource` child to the `StartCanvas` that SP9 built. No change to the SP9 profile-selection flow itself.
- W6 SP3 profiler pass (merged `30b846d`) — GC=0 baseline this SP must not regress. `AudioSource.Play()`/`Stop()` are Unity built-ins and do not allocate.

**Status:** Proposed

---

## 1. Motivation

Today, cold-starting the KeyFlow APK shows the StartScreen (나윤 / 소윤 profile selection) in total silence. A children-oriented rhythm game benefits from an audible "hello" on launch — it sets expectation ("this is a music app"), gives the ear something to latch onto while eyes scan the two profile buttons, and mirrors the genre convention of title-screen music in every Magic-Piano-class game.

The user has authored a short looping piano composition (`C:\dev\unity-music\sound\Piano-Play-Start.mp3`, ~577 KB, ~30 s) expressly for this slot. SP12 integrates it.

**Qualitative success criterion:** On a fresh cold-start of the Galaxy S22 (R5CT21A31QB) APK build, the StartScreen appears and the piano BGM begins within the first audio frame (≤50 ms perceptual target). The track loops seamlessly for players who linger. Tapping 나윤 or 소윤 stops the music immediately (no fade), and the subsequent Main/Gameplay/Results screens remain silent except for existing piano-sample and SFX audio. Pressing back to return to StartScreen replays the track from the beginning.

**Quantitative guardrails:**
- GC.Collect count = 0 during a 1-minute StartScreen idle session (SP3 parity).
- EditMode tests: 179 (current, pre-SP11) + 2 new = 181, all green.
- pytest: 49/49 green (pipeline untouched).
- APK size: measured post-build. Source MP3 is 577 KB; Vorbis 70% re-encoding is expected to yield ~450–550 KB. Note that SP11 (if it merges first) set a guardrail of ≤38.10 MB; SP12 will likely push the APK to ~38.50 MB. This overshoot is accepted because SP11's 38.10 MB was derived from an "audio-assets-only add" posture that explicitly did not contemplate BGM. SP12 supersedes that guardrail to 38.60 MB (observed post-Vorbis-re-encode size + 50 KB headroom).
- No change to chart timing, calibration, or gameplay-scene audio.

---

## 2. Scope

### 2.1 In scope

| ID | Item | Deliverable |
|---|---|---|
| SP12-T1 | Import the user-authored MP3 | `Assets/Audio/bgm/piano_play_start.mp3` plus its `.meta` with deterministic import settings. |
| SP12-T2 | `StartScreen.cs` BGM wiring | Add `[SerializeField] private AudioSource bgmSource;`. Add `OnEnable() { bgmSource?.Play(); }` and `OnDisable() { bgmSource?.Stop(); }`. No other behavioral change. |
| SP12-T3 | `SceneBuilder.BuildStartCanvas` update | Accept `AudioClip bgmClip` parameter. Create an `AudioSource` child GameObject on the `StartCanvas` with `loop=true`, `playOnAwake=false`, `volume=0.6`, `clip=bgmClip`. Wire the `bgmSource` SerializeField on `StartScreen` via the existing `SetField` helper. Call site at line ~125 of `SceneBuilder.cs` loads the clip via `AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/bgm/piano_play_start.mp3")`. |
| SP12-T4 | EditMode tests | `StartScreenBgmTests.cs` — 2 null-tolerance tests. |
| SP12-T5 | Device playtest | Galaxy S22 checklist: cold-start playback, loop integrity, profile-tap immediate stop, back-navigation replay, no bleed into Gameplay, GC=0 on 1-minute StartScreen idle. |
| SP12-T6 | APK measurement + guardrail adjustment | After build, record APK size and update this spec's guardrail + the next SP's carry-over if needed. |

### 2.2 Out of scope

- **SongSelect / Results / Gameplay BGM.** SP12 is StartScreen-only by product decision (Q2 in brainstorming). Later SPs may add menu-wide BGM on top of this foundation without retroactive changes.
- **Fade in/out on screen transition.** Rejected (Q2, option B). Profile-tap produces an immediate cut. Justified by: (i) clean, decisive feedback for a tap action; (ii) Unity's `SetActive(false)` lifecycle gives us stop-for-free without new fade infrastructure.
- **Volume slider / mute toggle in Settings.** Rejected (Q4, option A). Fixed `volume=0.6`. The Settings overlay (haptic, particle) is not touched.
- **`MusicManager` singleton or persistent audio service.** Rejected in approach selection. For a single track on a single screen, the GameObject-lifecycle approach is strictly simpler and matches the project's single-scene SetActive convention.
- **Audio focus management code.** Unity's default Android `AudioSource` already pauses/resumes on incoming calls and notification foregrounding. No bespoke focus handling needed.
- **New interface abstraction (`IBgmPlayer` / adapter).** Rejected in §3.3. The project has this pattern (`IHapticService`, `IClickPlayer` from SP11), but for two lines of code calling Unity built-ins with null-tolerance, the interface overhead is not justified.
- **Volume ducking when UI SFX plays.** Not applicable — StartScreen has no UI SFX, and profile-tap stops the music instead of ducking.
- **Preloading optimization.** `Preload Audio Data = true` (Unity default) is sufficient; Assets at <1 MB do not benefit from custom streaming strategies.
- **Replacing or re-authoring the MP3.** SP12 uses the user's file as-authored. Future re-masters are content changes, not SP-level work.

### 2.3 Guardrails (non-regression contracts)

- **GC.Collect=0 during 1-min StartScreen idle session.** `AudioSource.Play()`/`Stop()` and looping playback do not allocate. The single AudioSource instance is created at scene build time, not per-tap.
- **No change to gameplay-scene audio.** `AudioSyncManager`, piano sample playback, calibration click, SP11 countdown click (if merged) are all in `gameplayRoot` and untouched.
- **No change to profile-selection behavior.** `StartScreen.Select(Profile)` body is unchanged. The BGM wiring attaches via OnEnable/OnDisable only.
- **Existing 179 (pre-SP11) or ~191 (post-SP11) EditMode tests remain green** with zero source modification to those tests.
- **SP9 background image rendering unchanged.** The new `AudioSource` child GameObject is ordered after the Background child in the StartCanvas hierarchy and has no visual footprint.
- **`playOnAwake=false` is explicit.** SceneBuilder sets it explicitly; we do not rely on component defaults (Unity's `AudioSource` default is `playOnAwake=true`).

---

## 3. Approach

### 3.1 Design decisions (from brainstorming)

| # | Decision | Chosen | Rejected alternatives |
|---|---|---|---|
| 1 | Semantic scope | **StartScreen only — music stops on profile tap.** | (B) Menu-wide BGM spanning StartScreen + SongSelect — larger scope, requires coordinating stop point with Gameplay entry; (C) Fade-out on tap — new fade infrastructure for minor UX gain. |
| 2 | Loop | **Infinite loop while StartScreen active.** | One-shot play → silence after 30 s of idle — awkward UX for a kids' title screen. |
| 3 | Volume control | **Fixed `volume=0.6`, no Settings UI.** | On/off toggle — PlayerPrefs + UI work; slider — more PlayerPrefs + UI work. Both punted to post-MVP if requested. |
| 4 | Architecture | **`AudioSource` component on StartCanvas GameObject; `StartScreen.cs` calls `Play()`/`Stop()` in `OnEnable`/`OnDisable`.** | (B) `MusicManager` singleton — over-engineered for N=1 track on N=1 screen; (C) `AudioSource.playOnAwake=true` + no code — fails to restart when returning to StartScreen from Main (Awake fires only once). |
| 5 | Stop trigger | **Unity `SetActive(false)` lifecycle calls `OnDisable` → `Stop()`.** No explicit `Stop()` in `Select()`. | Explicit `bgmSource.Stop()` before `ScreenManager.Replace` — redundant, since OnDisable runs automatically. |
| 6 | Testability | **EditMode tests verify null-tolerance only (2 tests).** Playback itself is validated on device. | (B) `IBgmPlayer` + adapter + mock — matches project convention (IHapticService, IClickPlayer), but adds ~60 LoC for 2 lines of production code; (C) PlayMode tests for actual audio — Unity AudioSource does not play predictably in test environments without an AudioListener; cost-benefit poor. |
| 7 | File naming | **`piano_play_start.mp3`** (snake_case, matches `calibration_click.wav` convention). User's original name `Piano-Play-Start.mp3` mixes PascalCase and dashes. | Keep original name — inconsistent with project convention; rename to `start_bgm.mp3` or `menu_bgm.mp3` — loses the user's deliberate semantic choice. |
| 8 | Asset placement | **`Assets/Audio/bgm/` subfolder** (parallels `Assets/Audio/piano/` samples subfolder). | Flat `Assets/Audio/piano_play_start.mp3` — harder to scale if additional BGM is added later. |
| 9 | Import settings | **Load Type: Compressed In Memory, Compression Format: Vorbis, Quality: 70, Force Mono: false, Preload Audio Data: true.** See §4.3. | Streaming (overkill for 577 KB); Decompress On Load (higher RAM, no playback benefit); Vorbis Quality 50 (risks audible artifacts given source is already lossy); Force Mono (flatter BGM feel). |
| 10 | Volume level | **0.6.** Audible over Galaxy S22 speaker without drowning out ambient room noise / UI responsiveness. | 1.0 — dominates; 0.3 — easily missed on device playback; per-profile volume — YAGNI. |

### 3.2 Control flow

```
[Scene load]
  startRoot SetActive(true)  (saved state in GameplayScene.unity)
    │
    ├─► StartScreen.Awake        (existing — button listener wiring)
    ├─► AudioSource.Awake        (playOnAwake=false → no playback yet)
    └─► StartScreen.OnEnable     [NEW] → bgmSource.Play()   ▶ music starts, looping
         │
         └─► ScreenManager.Start → Replace(AppScreen.Start)
              (startRoot already active → SetActive(true) is a no-op → no OnEnable re-fire)

[User taps Nayoon/Soyoon button]
  StartScreen.Select(profile)    (unchanged body)
    │
    └─► ScreenManager.Replace(Main)
         │
         └─► startRoot.SetActive(false)
              │
              └─► StartScreen.OnDisable  [NEW] → bgmSource.Stop()   ■ music stops immediately

[User presses back from Main → return to Start]
  ScreenManager.HandleBack → Replace(Start)
    │
    └─► startRoot.SetActive(true)
         │
         └─► StartScreen.OnEnable → bgmSource.Play()   ▶ music restarts from 0:00
```

Key insight: `Unity's GameObject lifecycle` provides exactly the start/stop semantics required — no explicit state management in `StartScreen.Select` or `ScreenManager.Replace`. The `OnEnable`/`OnDisable` pair is idempotent with respect to repeated SetActive toggles.

### 3.3 Rejected architectural alternatives

- **`MusicManager` singleton with `PlayTrack(clip, loop)`/`Stop()`/`FadeOut()` API.** Rejected: overkill for a single track on a single screen. If a future SP adds SongSelect/Results BGM, we can extract this into a manager without disturbing SP12 (trivial refactor — the `AudioSource` is already an injected reference).
- **`AudioSource.playOnAwake = true` and no `StartScreen.cs` change.** Rejected: `Awake` fires only on first scene load; returning to StartScreen via back-navigation would find the AudioSource in a "played-once-and-done" state with no replay. Forcing the clip to restart requires calling `Play()` explicitly, which belongs in `OnEnable`.
- **Introduce `IBgmPlayer` interface + `AudioSourceBgmPlayer` adapter + mock.** Rejected: matches the project's `IHapticService`/`IClickPlayer` pattern but adds ~60 LoC (interface + adapter + wiring changes) for the purpose of testing 2 lines of code. The null-tolerance EditMode tests in §6 provide sufficient guard against dev-environment mis-wiring; actual playback verification moves to device playtest.
- **Explicit `bgmSource.Stop()` call inside `Select()` before `Replace()`.** Rejected: redundant. Unity's `SetActive(false)` invokes `OnDisable` synchronously; by the time `Replace()` returns, the audio has already stopped. Explicit stop adds no observable difference but creates a second place where stop logic lives.
- **`DontDestroyOnLoad` for the BGM AudioSource.** Rejected: the project is single-scene; there is no scene-load event. The `SetActive` lifecycle handles the equivalent.

---

## 4. Components

### 4.1 New files

| Path | Type | Responsibility |
|---|---|---|
| `Assets/Audio/bgm/piano_play_start.mp3` | asset | Source MP3 (user-authored), imported by Unity per §4.3 settings. |
| `Assets/Audio/bgm/piano_play_start.mp3.meta` | Unity meta | Captures deterministic import settings (Vorbis 70, Compressed In Memory, stereo). Committed to repo so CI and developer machines produce identical APKs. |
| `Assets/Tests/EditMode/StartScreenBgmTests.cs` | EditMode tests | 2 null-tolerance tests (see §6.1). |

### 4.2 Modified files

| Path | Change |
|---|---|
| `Assets/Scripts/UI/StartScreen.cs` | Add `[SerializeField] private AudioSource bgmSource;`. Add `private void OnEnable() { if (bgmSource != null) bgmSource.Play(); }`. Add `private void OnDisable() { if (bgmSource != null) bgmSource.Stop(); }`. ~5 net LoC. |
| `Assets/Editor/SceneBuilder.cs` | Change `BuildStartCanvas(Sprite startBg, out StartScreen startScreen)` signature to `BuildStartCanvas(Sprite startBg, AudioClip bgmClip, out StartScreen startScreen)`. Inside, create a child `GameObject("BgmAudioSource")`, add `AudioSource` component, set `clip=bgmClip`, `loop=true`, `playOnAwake=false`, `volume=0.6`, `spatialBlend=0f` (2D). Call `SetField(startScreen, "bgmSource", audioSource)`. Update the call site (line ~125) to load the clip via `AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/bgm/piano_play_start.mp3")` and pass it in. ~15 net LoC. |
| `Assets/Scenes/GameplayScene.unity` | Scene snapshot regenerated by `SceneBuilder`; adds `BgmAudioSource` child under `StartCanvas` and wires `bgmSource` on `StartScreen`. Reviewer diff will be small but mechanical. |

### 4.3 AudioClip import settings

Set on `piano_play_start.mp3` in the Unity Inspector (captured in the `.meta`):

| Setting | Value | Rationale |
|---|---|---|
| Load Type | `Compressed In Memory` | 577 KB is too small to justify Streaming overhead. `Compressed In Memory` keeps the Vorbis-compressed bytes in RAM (~450 KB) and decompresses on-the-fly — negligible CPU on modern Android. |
| Compression Format | `Vorbis` | Unity-native format for Android, re-encoded from the MP3 source at import time. |
| Quality | `70` | Source is MP3 (already lossy). Vorbis 70 preserves perceived quality without compounding artifacts. |
| Force To Mono | `false` | Stereo preserves the composition's spatial intent. |
| Preload Audio Data | `true` | Ensures the clip is ready at StartScreen's first `OnEnable` with no audible delay. |
| Normalize | `false` | Leave authored dynamics intact. |
| Load In Background | `false` | Asset is small; synchronous load at scene init is fine. |

### 4.4 AudioSource component defaults (set by SceneBuilder)

| Field | Value | Rationale |
|---|---|---|
| `clip` | `piano_play_start` | Wired via `AssetDatabase.LoadAssetAtPath` in `SceneBuilder.BuildStartCanvas`. |
| `loop` | `true` | Infinite playback while StartScreen is active (Q3 decision). |
| `playOnAwake` | `false` | `StartScreen.OnEnable` is the sole playback trigger; prevents race at scene init where startRoot might or might not be active-at-Awake. |
| `volume` | `0.6` | Balanced against Galaxy S22 built-in speaker in quiet-room playtest conditions. |
| `spatialBlend` | `0f` (2D) | BGM has no spatial position. |
| `bypassEffects` | `false` | Default; no audio effects in the project today. |

### 4.5 AudioSource placement in scene hierarchy

```
StartCanvas (GameObject)
├─ Background (SP9, existing)
├─ NayoonButton (SP9, existing)
├─ SoyoonButton (SP9, existing)
└─ BgmAudioSource [NEW]          ← child of StartCanvas, not a root GO
     └─ AudioSource component
```

Child-of-StartCanvas placement is deliberate: the AudioSource follows the `startRoot.SetActive` lifecycle managed by `ScreenManager.Replace`. If we parented it elsewhere (e.g., directly under scene root), we would need explicit stop logic.

---

## 5. Flow integration with existing screens

| Entry scenario | Trigger | BGM state transition |
|---|---|---|
| Cold-start (APK launched) | Unity loads `GameplayScene` → `ScreenManager.Awake` → `ScreenManager.Start` → `Replace(Start)` | `StartScreen.OnEnable` fires on first frame → `bgmSource.Play()` → music starts. |
| User taps 나윤 or 소윤 | `StartScreen.Select(profile)` → `ScreenManager.Replace(Main)` → `startRoot.SetActive(false)` | `StartScreen.OnDisable` → `bgmSource.Stop()` → music cuts immediately. |
| User taps back from Main | `ScreenManager.HandleBack` (AppScreen.Main case) → `Replace(Start)` → `startRoot.SetActive(true)` | `StartScreen.OnEnable` → `bgmSource.Play()` → music restarts from 0:00. |
| User taps back from Results/Gameplay | `Replace(Main)` first, then later `Replace(Start)` — two hops | Each `Replace(Start)` triggers a fresh `Play()`. Music restarts from 0:00 on every return. |
| Gameplay running | `gameplayRoot` active, `startRoot` inactive | BGM is already stopped (it stopped on the initial profile tap). Piano samples and SP4/SP10/SP11 feedback audio play in gameplay unaffected. |
| Phone call / incoming notification | Android audio focus loss | Unity AudioSource auto-pauses; auto-resumes when focus returns. No code changes required. |
| App backgrounded (home button) | Unity's `OnApplicationPause(true)` | Unity suspends audio playback; resumes on foreground. No code changes required. |

### 5.1 SP11 countdown interaction (if SP11 merges first)

SP11's countdown click audio lives on the `GameplayCanvas` (via `BuildCountdownOverlay`), which is a child of `gameplayRoot`. The countdown's `AudioSource` is unrelated to SP12's `BgmAudioSource`. The two are never both active — StartScreen BGM stops on profile tap, well before the user reaches Gameplay where the countdown fires. Temporal and logical separation is complete.

### 5.2 Calibration overlay interaction

Calibration overlay appears on top of `Gameplay` (not StartScreen), triggered from `GameplayController.ContinueAfterChartLoaded()`. SP12's BGM is already stopped by that point. No interaction.

### 5.3 Settings / Pause overlay interaction

Settings and Pause overlays are shown over Main/Gameplay/Results, never over StartScreen. No interaction.

---

## 6. Testing

### 6.1 `StartScreenBgmTests` (2)

Pure EditMode, scene-independent. Constructs a `StartScreen` MonoBehaviour on a bare GameObject (no SceneBuilder) and exercises the lifecycle hooks with `bgmSource` unassigned. Guards against NRE in test or minimal-wiring environments.

| # | Name | Verifies |
|---|---|---|
| 1 | `OnEnable_WithNullBgmSource_DoesNotThrow` | `go.SetActive(true)` on a StartScreen GO whose `bgmSource` field is null → no exception. Uses reflection to instantiate without SceneBuilder. |
| 2 | `OnDisable_WithNullBgmSource_DoesNotThrow` | As above, then `go.SetActive(false)` → no exception. |

**Not tested in EditMode:** actual audio playback (requires AudioListener + PlayMode), loop correctness (inherent to Unity AudioSource), volume level (audible validation), import-settings correctness (Unity-managed). These move to device playtest.

### 6.2 Regression surface

- **179 existing EditMode tests (pre-SP11) or ~191 (post-SP11)** expected to remain green.
- `StartScreen.InvokeSelectForTest` is unchanged; any test exercising profile selection continues to work.
- **49 pytest tests**: untouched (no pipeline change).

### 6.3 Manual playtest — Galaxy S22 R5CT21A31QB

- [ ] APK cold-start → StartScreen appears; BGM starts within ~50 ms (no silent gap).
- [ ] Leave StartScreen idle 45 s → verify loop at ~30 s boundary (no glitch, no restart-from-silence).
- [ ] Tap 나윤 → Main appears; audio stops immediately (no lingering trailing tone).
- [ ] Tap 소윤 → same as above.
- [ ] Press back on Main → StartScreen reappears; BGM restarts from 0:00.
- [ ] Rapidly toggle Start↔Main 3× → no stuck audio, no overlapping play. (Expected "cut-cut-cut" audio is acceptable per Q2 decision.)
- [ ] Enter Gameplay → play a song → verify no BGM bleed (only piano samples + SP4/SP10/SP11 feedback audio).
- [ ] Enter Gameplay → finish song → Results → press back to Main → press back to Start → BGM starts fresh.
- [ ] Incoming phone call during StartScreen BGM → call pauses music; ending call resumes music.
- [ ] Home button during StartScreen BGM → backgrounding pauses music; foregrounding resumes.
- [ ] 1-minute StartScreen idle: `GC.Collect` count = 0 (Unity Profiler on-device).
- [ ] Total APK size measured and recorded in completion report. Update SP11 guardrail if needed.
- [ ] Volume 0.6 subjectively balanced — not drowning, not missed. Revisit value if playtest suggests otherwise.

---

## 7. Risks & mitigations

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| APK overshoots SP11's 38.10 MB guardrail (expected ~38.50 MB) | High | Spec-review friction; no user-facing impact | SP12 explicitly supersedes the SP11 guardrail to 38.60 MB (§1). Alternative Vorbis quality=50 reduces to ~350 KB if needed. Decision after post-build APK measurement. |
| Rapid Start↔Main toggling produces "cut-cut-cut" audio artifact | Low | Minor UX oddity in rare case | Accepted per Q2 (user chose "immediate cut" over "fade-out"). Not a crash or data issue; can revisit if playtest surfaces it. |
| `playOnAwake=true` accidentally left enabled → music plays during inactive GameObject moment (shouldn't happen, but belt-and-suspenders) | Low | BGM audible outside StartScreen | SceneBuilder explicitly sets `playOnAwake=false`. Component default is `true`, so the explicit set is load-bearing, not cosmetic. |
| Android audio focus: calls/notifications leave music in weird state | Medium | User confusion | Unity handles this by default on Android. Verify via §6.3 playtest; no code response planned. If playtest reveals issue, add `OnApplicationFocus` handler in follow-up. |
| `.meta` file import settings drift across machines / CI | Low | Inconsistent APK sizes | `.meta` is committed to repo with Vorbis=70 encoded in `AudioImporter` settings. Any reimport uses the committed meta. |
| Unity AudioClip `LoadAssetAtPath` fails in SceneBuilder if file was moved/renamed | Low (if wired once) | Scene build throws, no BGM in APK | SceneBuilder logs an explicit warning if `bgmClip == null` and still proceeds (no AudioSource created). Playtest catches this via "no audio on cold-start" symptom. |
| Returning to StartScreen after long absence — audio restart feels jarring to parent observing | Low | Minor UX judgment call | Restart-from-0 is the expected behavior of a screen-scoped BGM. Not a bug. |
| Volume 0.6 wrong for device loudspeaker | Medium | BGM too loud/quiet at default ringer setting | Tune via playtest. A second playtest after minor volume adjustment is cheap (SceneBuilder rebuild). |
| Overlap with SP11 SerializeField additions on `GameplayController` | None | — | SP12 touches `StartScreen`, not `GameplayController`. No merge conflict. |

---

## 8. Rollback

If SP12 causes a post-merge regression:

1. Revert the merge commit (SP12 is delivered as one PR).
2. Scene regenerates on next `KeyFlow/Build Scene` — SceneBuilder's `BuildStartCanvas` returns to the SP9 two-arg signature.
3. `Assets/Audio/bgm/piano_play_start.mp3` remains in repo but is no longer referenced by any scene → Unity build excludes unreferenced audio. No APK size impact post-revert.
4. No data migration (no PlayerPrefs keys, no scene layout changes beyond the BgmAudioSource child).

---

## 9. Acceptance criteria

- [ ] Both new EditMode tests green.
- [ ] Existing 179 (or ~191 post-SP11) EditMode tests green with zero source modification.
- [ ] pytest 49/49 green.
- [ ] Manual playtest checklist §6.3 all passing on Galaxy S22.
- [ ] APK size measured; if ≤38.60 MB the guardrail holds; if >38.60 MB, retune Vorbis quality or escalate.
- [ ] GC=0 verified on 1-min StartScreen idle session.
- [ ] Completion report committed to `docs/superpowers/reports/2026-04-?-w6-sp12-start-screen-bgm-completion.md`.

---

## 10. File summary

**New (3):**
- `Assets/Audio/bgm/piano_play_start.mp3`
- `Assets/Audio/bgm/piano_play_start.mp3.meta`
- `Assets/Tests/EditMode/StartScreenBgmTests.cs`

**Modified (3):**
- `Assets/Scripts/UI/StartScreen.cs`
- `Assets/Editor/SceneBuilder.cs`
- `Assets/Scenes/GameplayScene.unity` (mechanical, regenerated by SceneBuilder)

**Deleted (1):**
- `C:\dev\unity-music\sound\Piano-Play-Start.mp3` — source scratch file; superseded by `Assets/Audio/bgm/piano_play_start.mp3`. User may choose to keep the scratch folder for future audio authoring, in which case this delete is optional.

**Total LoC (C# production):** ~20 lines across 2 files.
**Total LoC (tests):** ~30 lines in 1 file.
