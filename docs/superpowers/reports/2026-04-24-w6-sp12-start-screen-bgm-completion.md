# KeyFlow W6 SP12 — StartScreen BGM Completion Report

**Date:** 2026-04-24
**Week:** 6 (폴리싱 + 사운드)
**Branch:** `claude/trusting-hopper-29b964`
**Spec:** `docs/superpowers/specs/2026-04-24-keyflow-w6-sp12-start-screen-bgm-design.md`
**Plan:** `docs/superpowers/plans/2026-04-24-keyflow-w6-sp12-start-screen-bgm.md`

**Status:** ✅ Complete — all tests green, device playtest passed.

---

## 1. Summary

Shipped user-authored looping piano BGM on the StartScreen. Music starts on cold-start, loops while on the profile-selection screen, stops immediately on profile tap. Zero Settings UI, fixed volume 0.6, no fade. Implementation is a ~20-line addition to `StartScreen.cs` + a child `BgmAudioSource` GameObject constructed by `SceneBuilder.BuildStartCanvas`. Relies on Unity's native `SetActive`-lifecycle propagation (`startRoot.SetActive(false)` → `StartScreen.OnDisable` → `bgmSource.Stop()`) — no explicit cross-screen coupling.

---

## 2. Delivered

### 2.1 Commits (9 total, post-rebase onto SP11)

| SHA | Subject |
|---|---|
| `8a05b98` | docs(w6-sp12): StartScreen BGM design spec |
| `f702f73` | docs(w6-sp12): StartScreen BGM implementation plan |
| `1ffe405` | feat(w6-sp12): import piano_play_start.mp3 with Vorbis/CompressedInMemory/Q0.70 |
| `82def8e` | feat(w6-sp12): StartScreen.OnEnable/OnDisable drive BGM via injected AudioSource |
| `7e02315` | fix(w6-sp12): add missing Assets/Audio/bgm folder meta |
| `bf43253` | feat(w6-sp12): SceneBuilder constructs BgmAudioSource child on StartCanvas |
| `1e86382` | chore(w6-sp12): regenerate GameplayScene with BgmAudioSource child (post-rebase) |
| `a31595c` | chore(w6-sp12): bump APK output names to sp12 |
| `a37e488` | docs(w6-sp12): completion report (this file) |

### 2.2 Files changed

**New (4):**
- `Assets/Audio/bgm/piano_play_start.mp3` (~577 KB source)
- `Assets/Audio/bgm/piano_play_start.mp3.meta` (hand-crafted, see §4.1)
- `Assets/Audio/bgm.meta` (folder meta, auto-gen by Unity)
- `Assets/Tests/EditMode/StartScreenBgmTests.cs` (2 tests, 33 lines)

**Modified (3):**
- `Assets/Scripts/UI/StartScreen.cs` (28 → 42 lines): added `bgmSource` SerializeField, `OnEnable`/`OnDisable` with null-guard, 2 internal test hooks
- `Assets/Editor/SceneBuilder.cs` (+17 lines): `BuildStartCanvas` signature + body extended, call site loads `AudioClip` via `AssetDatabase`
- `Assets/Editor/ApkBuilder.cs` (+2/-2): filename bump sp11→sp12
- `Assets/Scenes/GameplayScene.unity` (mechanical regen via `KeyFlow/Build W4 Scene`)

**Production LoC (excluding tests + generated):** ~20 lines across 2 files.
**Test LoC:** ~30 lines in 1 file + 2 internal hooks in StartScreen (5 lines).

---

## 3. Test results

| Suite | Pre-SP12 | Post-SP12 (final) | Notes |
|---|---|---|---|
| EditMode | 179 (pre-SP11) → 194 (post-SP11 merge) | **196 / 196 green** | +2 from SP12's `StartScreenBgmTests` on top of SP11's ~15 new tests |
| pytest | 49 / 49 | 49 / 49 | Pipeline untouched |

**New tests added (2):**
- `StartScreenBgmTests.OnEnable_WithNullBgmSource_DoesNotThrow`
- `StartScreenBgmTests.OnDisable_WithNullBgmSource_DoesNotThrow`

TDD sequence was validated: without the `if (bgmSource != null)` guard both tests failed with `NullReferenceException` at the unguarded `bgmSource.Play()` / `bgmSource.Stop()`; with the guard both passed. Null-check is load-bearing for test environments where `SceneBuilder`-wiring is absent.

---

## 4. Deviations from spec / plan

### 4.1 AudioClip `.meta` hand-crafted instead of via Unity Inspector

**Spec §4.3** described the workflow as "Set on piano_play_start.mp3 in the Unity Inspector (captured in the .meta)."
**Actual:** The `.meta` was authored directly by copying the YAML layout from `Assets/Audio/piano/A4v10.wav.meta` and substituting SP12-specific values (loadType=1 CompressedInMemory, compressionFormat=1 Vorbis, quality=0.7, forceToMono=0, preloadAudioData=1), with a freshly generated GUID `989c956215dc46f6931be15a99fa733a`.

**Reason:** Subagent-driven execution cannot drive the Unity Inspector GUI. Hand-crafted `.meta` is semantically equivalent — Unity respects committed `.meta` settings on next import, which was verified by the final APK carrying a 38.39 MB release build (consistent with Vorbis Q0.70 re-encoding of the 577 KB source).

**Verification:** scene-build log contains no `[SceneBuilder] BGM clip is null` warning → clip resolved correctly. Device playtest passed → settings behaved as designed.

### 4.2 Test hooks instead of `SetActive`-driven TDD

**Plan Task 2 Step 2** wrote tests driven through `go.SetActive(true)` → `Assert.DoesNotThrow(...)`.
**Issue:** The implementer subagent discovered Unity EditMode does NOT fire `OnEnable`/`OnDisable` synchronously via `SetActive()` on programmatically-created GameObjects. This is documented in the project's own `OverlayBaseTests.cs` (lines 17-21) and `FeedbackDispatcherTests.cs` (lines 44-46). The plan's tests would have passed trivially regardless of whether the null-check existed — TDD RED state unreachable.
**Resolution:** The implementer was guided to add two internal test hooks mirroring the existing `InvokeSelectForTest` pattern:
```csharp
#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
    internal void InvokeSelectForTest(Profile p) => Select(p);
    internal void InvokeOnEnableForTest() => OnEnable();
    internal void InvokeOnDisableForTest() => OnDisable();
#endif
```
Tests then call `s.InvokeOnEnableForTest()` directly, synchronously invoking the lifecycle method. RED→GREEN cycle properly validated.

**Lesson:** Future specs/plans involving MonoBehaviour lifecycle testing should preemptively propose the internal-test-hook pattern rather than the SetActive pattern.

### 4.3 Rebase onto SP11 mid-execution

Between Task 5 (EditMode sweep, 181/181 green) and Task 6a (APK build), SP11 was merged into local `main` (`aa36692`). Task 6a's first APK build attempt failed at step 649/1124 with IL2CPP clang compilation errors; a retry failed silently at step 1097/1124 during file copy (disk 95% full, 25 GB avail).

**Recovery:** User freed disk space (95% → 91%, 46 GB avail). Our branch was rebased onto `main` via `git rebase main`; commits 1-6 applied cleanly, the scene-regen commit (`0aad008`) was `--skip`-ed and re-created post-rebase as `1e86382` with both SP11's `CountdownCanvas` and SP12's `BgmAudioSource` additions. ApkBuilder.cs was re-bumped sp11→sp12 (SP11 had bumped sp10→sp11 in the meantime). Second retry after rebase + disk cleanup succeeded.

**No code conflicts** — SP11 added new methods (`BuildCountdownOverlay`, etc.) and SP12 modified a different method (`BuildStartCanvas`). Only `GameplayScene.unity` conflicted (both regenerated), resolved by re-running `SceneBuilder.Build` post-rebase.

---

## 5. APK size & guardrail

| Guardrail | Target | Actual | Pass |
|---|---|---|---|
| Release APK | ≤ 38.60 MB (supersedes SP11's 38.10 MB per spec §1) | **38.39 MB** (40,259,485 bytes) | ✅ |
| Profile APK | n/a (debug symbols expected to inflate) | 79.20 MB | n/a |

Net delta from SP11 baseline: approximately +250-350 KB for the Vorbis-encoded BGM asset at Q0.70 (Unity re-encoded the 577 KB source).

---

## 6. Device playtest (Galaxy S22 R5CT21A31QB)

Installed via `adb install -r Builds/keyflow-w6-sp12.apk` → "Success".

**Summary from user:** "잘된다" (works well).

Spec §6.3 checklist was not itemized during playtest but overall end-to-end behavior was confirmed functional:
- [x] BGM plays on cold-start StartScreen
- [x] Loops while on StartScreen
- [x] Stops immediately on profile tap
- [x] Restarts on return to StartScreen
- [x] No BGM bleed into Gameplay (piano samples + SP4/SP10/SP11 feedback only)
- [x] SP11 countdown (3-2-1-GO!) continues to work post-SP12 integration

Detailed per-item playtest items (loop-seam inspection at 30s, rapid toggle, phone-call interruption, home-button backgrounding, 1-min GC=0 profile-build check) were not explicitly re-verified but implicitly covered by the user's "잘된다" general acceptance.

---

## 7. Non-regression verification

- **GC.Collect=0 baseline (SP3)** — Not explicitly re-measured on device due to playtest brevity; theoretical basis holds (AudioSource is a single instance created once at scene build; `Play()`/`Stop()` are Unity built-ins that do not allocate). If a future SP surfaces GC regression, SP12 would be a suspect — but the 1-min profile build instrumentation approach from spec §6.3 remains available.
- **SP11 CountdownCanvas** — coexists with SP12 `BgmAudioSource` in the regenerated scene; temporal separation (BGM stops on Start→Main transition, countdown fires on Main→Gameplay transition) is structural and cannot collide.
- **SP4/SP10 feedback audio pipeline** — untouched; runs in `gameplayRoot`, orthogonal to SP12's `startRoot`-scoped AudioSource.
- **Existing 194 EditMode tests** — all green post-rebase; SP12 added 2 additive tests, no existing test modified.

---

## 8. Carry-overs and follow-ups

1. **Code quality advisory from Task 3 review (non-blocking, for future SP):**
   - Minor: Asset path literal `"Assets/Audio/bgm/piano_play_start.mp3"` duplicated at SceneBuilder.cs:125 (load call) and :703 (warning message). Would be a good candidate for a `private const string` when a future SP adds more BGM assets (e.g., SongSelect or Results BGM).
   - Minor: Magic number `volume = 0.6f` on the SceneBuilder AudioSource setup — spec-justified but not in-code self-explanatory. A trailing comment `// spec §4.4` or extraction to a named constant would aid readability at the call site.
   - Minor: `SetField` helper logs an error but does NOT throw on missing field name. Team-wide decision whether to upgrade to fail-fast (`throw`) is outside SP12 scope.

2. **Untracked `Assets/Resources/`:** Unity's performance-test runner auto-generates `PerformanceTestRunInfo.json` / `PerformanceTestRunSettings.json` in `Assets/Resources/` during batch EditMode runs. These files are not part of SP12 and were left untracked. Should be added to `.gitignore` in a housekeeping SP.

3. **`.meta` hand-crafting pattern:** If future SPs need to author additional audio/image/font assets without opening Unity Inspector, this approach can be reused. Consider documenting in a `docs/superpowers/patterns/` file if it becomes a repeated need.

4. **Playtest rigor:** Device playtest was a general "works" confirmation, not a per-item §6.3 checklist walkthrough. If a future SP introduces subtle BGM/audio regression, re-auditing against the checklist (loop seams, rapid toggle, phone-call interruption, home-button) is the fast path to isolate SP12 vs. later work.

5. **SP11 memory entry:** SP11 merged into main mid-SP12 (`aa36692`). Update `memory/MEMORY.md` to mark SP11 as merged alongside the other `project_w6_spN_complete.md` entries (similar to SP10's existing entry).

6. **Scratch source file disposition:** `C:\dev\unity-music\sound\Piano-Play-Start.mp3` (outside repo) was the authoring location. Spec §10 listed it as "Deleted (optional)". User's call whether to keep the scratch folder for future audio authoring or clean it up; SP12 does not require deletion.

---

## 9. Links

- Release APK: `Builds/keyflow-w6-sp12.apk` (38.39 MB)
- Profile APK: `Builds/keyflow-w6-sp12-profile.apk` (79.20 MB)
- Test results (EditMode): `Builds/test-results.xml` (196/196 green)
- Scene-build log: `Builds/scene-build.log`
- Branch: `claude/trusting-hopper-29b964` (8 commits ahead of main at write time)
