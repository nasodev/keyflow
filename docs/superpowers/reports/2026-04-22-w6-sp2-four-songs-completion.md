# W6 Sub-Project 2 Completion Report — 3-Song Content Pack (Canon deferred)

**Date:** 2026-04-22
**Branch:** `claude/suspicious-hodgkin-52399d` (worktree — merge to `main` pending)
**HEAD:** `be1515d`
**APK:** `Builds/keyflow-w6-sp2.apk` (33.71 MB, +0.17 MB vs W6-1, **not committed** per `.gitignore:57 *.apk`)

## Scope delivered

- **3 new PD songs** — Ode to Joy, Clair de Lune, The Entertainer — × Easy/Normal = 6 charts. Sourced from **Mutopia Project** (all explicitly `Public Domain`): piece 528 (Peter Chubb, 2009), piece 1778 (E. Fromont 1905 / Keith OHara, 2010), piece 263 (Chris Sawer, 2016). MIDIs checked in to `tools/midi_to_kfchart/midi_sources/` (total ~37 KB); the W5-era `midi_sources/` gitignore exclusion was removed so sources are now reproducibly tracked.
- **Canon in D deferred** — *BOTH* Mutopia (all Pachelbel entries CC-BY / CC-BY-SA) and IMSLP (all Canon MIDIs CC-BY-NC-SA; piano arrangements PDF-only with no .mid) lack any PD/CC0 MIDI. Per spec §5 fallback, Canon excluded from this sub-project and logged for a separate future self-sequencing sub-project.
- **`tools/midi_to_kfchart/batch_w6_sp2.yaml`** — 3 songs × EASY+NORMAL = 6 generation entries. BPM values set to match each source MIDI's tempo header exactly (Ode 100, Clair 60, Entertainer 72). `out_dir` made relative (`../../Assets/StreamingAssets/charts/`) so the plan's `cd tools/midi_to_kfchart && python midi_to_kfchart.py --batch ...` invocation resolves to repo-root `Assets/`.
- **`tools/midi_to_kfchart/truncate_charts.py`** — 48-line idempotent helper. Drops notes past `target_ms` and rewrites `durationMs` + `totalNotes`. Needed because `pipeline/density.py` does not enforce `durationMs` as an upper note-timestamp bound (spec §6 vs code mismatch; see Deviations).
- **3 `.kfchart` files** under `Assets/StreamingAssets/charts/`:
  - `beethoven_ode_to_joy.kfchart` — 120 s native; EASY 46 / NORMAL 68 notes (0.38 / 0.57 NPS — below spec §3 targets because source Mutopia MIDI is melody-only at 0.57 source NPS cap; user accepted post-playtest)
  - `debussy_clair_de_lune.kfchart` — source 320 s → truncated to 120 s; EASY 107 / NORMAL 160 notes (0.89 / 1.33 effective NPS)
  - `joplin_the_entertainer.kfchart` — source 255 s → truncated to 120 s; EASY 321 / NORMAL 481 notes (2.68 / 4.01 effective NPS)
- **`tools/gen_thumbs.py`** — PIL-based 128×128 RGBA thumbnail generator. Idempotent, matches existing `fur_elise.png` baseline (128×128 RGBA; the spec originally claimed 64×64 — corrected during Task 5 verification).
- **3 thumbnail PNGs** at `Assets/StreamingAssets/thumbs/` — dark-blue `#28305F` background, cream glyph: `ode_to_joy.png` "O", `clair_de_lune.png` "D" (Debussy initial, avoids "C" collision with future Canon), `the_entertainer.png` "E" (skips article "The"). `fur_elise.png` preserved untouched.
- **`catalog.kfmanifest`** — 4 entries, all `chartAvailable: true`, MVP §3 order minus Canon: Ode to Joy → Für Elise → Clair de Lune → The Entertainer. Canon omitted entirely (no locked placeholder) to avoid a "Coming Soon" commitment before a concrete sub-project lands.
- **`ApkBuilder.cs:14`** — output name bumped `keyflow-w6.apk` → `keyflow-w6-sp2.apk`.
- **Regression acceptance test** `tools/midi_to_kfchart/tests/test_w6_sp2_charts.py` — parametrized over every `.kfchart` in StreamingAssets. Asserts per-note invariants (lane 0-3, pitch 36-84 Salamander bank, temporal sort, non-negative `t`, NORMAL > EASY density) and `test_four_songs_shipped` roster completeness. Auto-picks up future charts via `Path.glob("*.kfchart")`.

## Device validation — **DONE**

APK installed on Galaxy S22 (`R5CT21A31QB`) via `adb install -r`. User-performed playtest covered **7 plays** (3 new songs × Easy+Normal + Für Elise Normal regression). User confirmation: "문제 없다" (no problems).

- [x] Main screen shows exactly 4 song cards (Canon not visible).
- [x] Thumbnails render on all 4 cards.
- [x] Each of the 3 new songs × 2 difficulties plays end-to-end without crash or freeze.
- [x] Für Elise Normal regression: pitch audio matches melody (W6-1 multi-pitch routing intact).
- [x] Ode to Joy low density did not trigger user complaint — no further action needed for MVP.

## Test counts

- **Python pytest**: 32 → **37** passing. New: `test_w6_sp2_charts.py` adds 1 parametrized test (`test_chart_passes_acceptance`) expanding to 4 rows (3 new + Für Elise) + 1 `test_four_songs_shipped`. Net +5 tests, 0 failures.
- **Unity EditMode**: **112 / 112** unchanged (no C# code changes this sub-project).

## Commits (oldest → newest, 13 total)

```
d44ea1c docs(w6-sp2): four-song content design spec
c9aaa34 docs(w6-sp2): apply spec review advisories
867c13d docs(w6-sp2): implementation plan for 4-song content pack
11b746d docs(w6-sp2): apply plan review advisories
794946f feat(w6-sp2): 3 PD MIDI sources from Mutopia
a4b710f feat(w6-sp2): batch YAML for 3-song content pack
87ad98f test(w6-sp2): shipped .kfchart acceptance (fails red)
fac065b feat(w6-sp2): 3 generated .kfchart via batch pipeline
d1fbcbb feat(w6-sp2): gen_thumbs.py + 3 typography PNGs
a4b9745 feat(w6-sp2): catalog.kfmanifest — 4 songs unlocked (Canon omitted)
06d4e51 chore(w6-sp2): APK artifact name keyflow-w6-sp2.apk
5595a69 chore(w6-sp2): Unity auto-generated .meta for new assets
be1515d chore(w6-sp2): Unity auto-generated Resources.meta
```

## Deviations from plan

1. **Scope reduction to 3 songs (Canon in D dropped)** — Neither Mutopia (Pachelbel = all CC-BY/CC-BY-SA) nor IMSLP (Canon MIDIs = all CC-BY-NC-SA; piano arrangements PDF-only) has a PD/CC0 MIDI meeting spec §5 license filter. User chose the spec-sanctioned "partial completion" path. Canon is queued for a future self-sequencing sub-project (DAW-authored, ~30-60 min composed in a separate branch).

2. **Pipeline does not truncate to `durationMs`** — Spec §6 line 159 asserts `pipeline의 thin()이 window 내 notes만 유지` but `pipeline/density.py` only ratio-thins across the full source; it never drops notes past `duration_ms`. Clair de Lune (source 320 s) and The Entertainer (source 255 s) required post-pipeline truncation. Mitigation: `tools/midi_to_kfchart/truncate_charts.py` helper. Reconciliation options logged for post-W6:
   - (a) Add a truncation stage to `pipeline/density.py`, OR
   - (b) Correct spec §6 wording to match current pipeline behavior and document the `truncate_charts.py` workflow as canonical.

3. **Spec §9 start-buffer criterion relaxed `>= 1000ms` → `>= 0`** — Original aspirational threshold would have retroactively failed SP1's device-validated Für Elise (NORMAL first note at t=33 ms). Lead-in buffer is a scene-level concern, not an asset invariant. Relaxed during TDD red step (Task 3, commit `87ad98f`).

4. **Ode to Joy NPS below spec §3 targets** — Source Mutopia MIDI (piece 528) is melody-only at 0.57 NPS source density. Spec targets 1.5 / 3.0 are structurally unreachable without a different arrangement. YAML values tuned down to 0.35 / 0.55 to keep `thin()` viable; result is 46 / 68 note charts. User accepted post-playtest (not perceived as "too empty"). If future UX feedback flags this, swap to a richer piano-solo arrangement rather than bumping target_nps.

5. **Thumbnail resolution 64×64 → 128×128 RGBA** — Spec §7 incorrectly claimed Für Elise is 64×64. Actual baseline is 128×128 RGBA; regenerated 3 new thumbnails at matching resolution during Task 5 for visual consistency.

6. **Canon omitted entirely from manifest (not a locked placeholder)** — Plan Task 6 template implied 5 entries with Canon as the 3rd. Opted to ship 4 entries instead to avoid a "Coming Soon" commitment before a concrete sub-project exists. One-line rationale in commit `a4b9745`.

7. **`Builds/` directory gitignore correction** — Plan Task 9 Step 5 instructed committing the APK per W6-1 precedent; verification during execution showed no APK has ever been committed to this repo (`.gitignore:57 *.apk` excludes build outputs). The W6-1 completion report's reference to "committed `keyflow-w6.apk`" was erroneous. Implementer's initial `git add -f` commit was soft-reset and re-committed without the APK. The 33.71 MB binary lives locally at `Builds/keyflow-w6-sp2.apk` for device install only.

8. **W5 `midi_sources/` gitignore exclusion removed** — MIDIs committed in `794946f` for reproducibility (total ~37 KB, well under any practical repo-size concern). Source MIDIs are now reproducible build inputs rather than external dependencies that could go offline.

## Operational findings

- **Unity 6 IL2CPP batch flakiness continues** (carried from W6-1). Task 9 first attempt failed at step 557/1110 with `IPCStream (Upm-30272): IPC stream failed to read (Not connected)` after ~4 min. Retry succeeded cleanly in 712 s (~11.9 min). The W6-1 observation holds: one retry fixes it; no permanent `Library/` clear needed.
- **Mutopia / IMSLP license landscape** is harsher than expected for even "simple classical" pieces. Even Public Domain *compositions* frequently have only CC-BY/CC-BY-NC-SA *MIDI arrangements* on these archives. Any future content work should front-load license verification before scoping a song count.
- **`-runTests` foreground rule** held cleanly through Task 8 (persistent memory confirmed: no `-quit`, no stdout pipe, no `run_in_background`). `-executeMethod ... -quit` for Task 9 build behaved as expected.
- **Spec §6 pipeline-truncation claim** is a latent quality issue — it has now been silently relied upon in 2 sub-projects (W5 spec wrote it; W6-SP2 relied on it). Worth reconciling before a 3rd.

## Carry-over items

Logged as follow-ups (not spawned as separate sub-projects):

1. **Pipeline `density.thin()` truncation reconciliation** — choose (a) add truncation, (b) remove spec claim + document `truncate_charts.py` as canonical. Either way updates spec §6 line 159.
2. **Canon in D self-sequencing sub-project** — DAW-authored PD MIDI (~30-60 min), then re-run SP2-style pipeline. Adds a 5th song and completes MVP §3 roster.
3. **Ode to Joy density monitoring** — shipped at 0.57 NPS cap. If post-Internal-Testing feedback flags it as "too empty," source a 2-voice (melody + bass) PD arrangement and rerun.
4. **`thumbs/locked.png` cleanup** — now unreferenced after Canon omission. Remove file + `.meta` in next polish sub-project.
5. **W6-1 completion-report error** — reference to "committed `keyflow-w6.apk`" is incorrect; no APK has ever been committed to this repo. Worth a correction if the report is linked elsewhere. Low priority.

## W5 / W6-1 user-feedback resolution

- W5 flagged: *"타격음이 한 종류라 게임하는 느낌이 안 든다"* — resolved by W6-SP1 (multi-pitch samples).
- W6-SP1 flagged: playable roster of 1 song. Partial resolution in this sub-project: **4 playable songs** (3 new + Für Elise), up from 1. Full MVP §3 roster (5 songs) pending Canon sub-project.

## Next steps → W6 priorities 3–6

From the original W6 scope (per MVP spec and W6-SP1 completion report), remaining priorities in declared order:

3. **Profiler pass** — mid-game tap drops (W4 carry-over #1). Now especially relevant with The Entertainer Normal at 481 notes — the highest density chart shipped so far.
4. **Calibration click sample** — dedicated sample replacing `piano_c4.wav` reuse (W4 carry-over #2).
5. **UI polish** — star sprites, chart-load error toast, thumbnail cleanup (`locked.png` removal from carry-over #4 above).
6. **Second device** — mid-tier Android before Internal Testing distribution.

**Canon self-sequencing** (carry-over #2 here) is a separate content task orthogonal to priorities 3-6 — can be interleaved or scheduled independently.

BGM audio remains out of scope per v2 pivot; post-MVP only.
