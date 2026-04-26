# Personal Song Overlay — Sustainable Public/Personal Song Separation

**Date:** 2026-04-26
**Week:** 7 (pre-W7 housekeeping; pulled forward from planned W7 SP1)
**Parent context:** Triggered by introduction of copyrighted song "Example Artist — Example Song" into the working tree. The ad-hoc fix (per-song .gitignore lines + manual catalog editing) is judged unsustainable as the song library grows.
**Status:** Proposed

---

## 1. Motivation

KeyFlow ships with public-domain classical music (Beethoven, Debussy, Joplin) that is safe to commit to git. The user has begun authoring/converting copyrighted material (e.g., K-pop charts) for personal testing — that material must never leave the local machine.

The current code path treats all songs uniformly: one `catalog.kfmanifest`, one `charts/` directory, one `thumbs/` directory. To exclude a copyrighted song today, a developer must:

1. Add per-song lines to `.gitignore` (4 patterns × 2 paths each = ~8 lines per song).
2. Manually edit `catalog.kfmanifest` to remove the song's entry before each commit.
3. Re-add the entry locally to keep playing the song during dev.
4. Repeat steps 2-3 every commit cycle, forever.

This scales to N songs as O(N) gitignore lines and O(N × commits) manual edits. Inevitably, a developer will forget step 2, and a song title (and possibly chart contents) will leak into the public repository.

**Goal:** Move the public/private boundary from "per-song manual decision" to "directory location convention." Adding a new song — public or personal — should require zero changes to `.gitignore`, no manifest re-editing, and no risk of accidental commit.

**Qualitative success criterion:** A developer can add a copyrighted MIDI by dropping it into `midi/personal/`, running the existing pipeline, and adding one line to `catalog.personal.kfmanifest`. After that, `git status` shows zero new tracked files; the song is fully playable in local builds; the public catalog and chart directories are untouched.

**Quantitative guardrails:**
- `.gitignore` rules grow O(1), not O(N songs).
- Zero per-song manifest editing for committed manifest.
- 196/196 existing EditMode tests remain green; +4-6 new tests for overlay merge + personal chart resolution.
- pytest 49+/49+ green (pipeline gets a `personal: true` flag passthrough; existing fixtures unaffected).
- No change to public song loading flow (cold-start gameplay of the four classical songs is byte-identical to today).

---

## 2. Scope

### 2.1 In scope

| ID | Item | Deliverable |
|---|---|---|
| PSO-T1 | `.gitignore` overhaul | Replace the 6-line `<song_id>_*` block (added 2026-04-26 morning) with 5 directory-scope rules. |
| PSO-T2 | Directory restructure | Create `personal/` subdirs under `midi/`, `Assets/StreamingAssets/charts/`, `Assets/StreamingAssets/thumbs/`, `tools/midi_to_kfchart/`. Move existing the personal song assets and `batch_personal.yaml` into them. |
| PSO-T3 | `SongCatalog.LoadAsync` overlay | Load `catalog.kfmanifest` (required) then attempt `catalog.personal.kfmanifest` (optional, no error if missing). Tag personal-overlay entries with `isPersonal=true`. Merge into single `loaded` array with personal entries appended. |
| PSO-T4 | `SongEntry.isPersonal` flag | Add `[JsonIgnore] public bool isPersonal;` (or non-serialized field). Set by loader, not parsed from JSON. |
| PSO-T5 | `ChartLoader` path resolution | Accept `bool isPersonal` parameter on `LoadFromStreamingAssetsCo`; prepend `personal/` to the chart path when true. Caller (GameplayController) reads the flag from the resolved `SongEntry`. |
| PSO-T6 | `MainScreen` thumbnail loading | No code change required: thumbnails are loaded from a stored path string (`entry.thumbnail`), so storing `"thumbs/personal/example_personal_song.png"` in `catalog.personal.kfmanifest` works as-is. |
| PSO-T7 | Pipeline path-based routing | `midi_to_kfchart.py` detects when invoked with a batch file under `tools/midi_to_kfchart/personal/` (resolved via `Path(batch_file).parts`) and routes outputs to `charts/personal/` and `thumbs/personal/`. No new YAML field. |
| PSO-T8 | EditMode tests | 4-6 new tests covering overlay merge (with/without personal file), `isPersonal` propagation, and chart path resolution. |
| PSO-T9 | Pipeline tests | 1-2 pytest cases covering personal output routing. |
| PSO-T10 | Documentation | Update `CLAUDE.md` with a "Personal songs" section explaining the convention. |

### 2.2 Out of scope

- **W7 SP2 (score persistence), SP3 (results screen), SP4 (Hard difficulty).** Pulled forward from W7 SP1 only the manifest-overlay portion. The personal-song pipeline-stabilization in-flight work (BPM retrigger, multi-track parser, start_offset/truncator, difficulty-scaled spawn) remains in W7 SP1 and is not blocked by this spec.
- **Encrypting personal chart files.** Local-only assets are not encrypted; the only protection is "don't commit them." Encryption would add key management overhead with no security gain (anyone with the APK + decryption key has the contents anyway).
- **CI verification that personal files are absent from commits.** Could be added as a pre-commit hook in a future SP, but `.gitignore` correctness is verifiable by hand (`git check-ignore -v`) and the convention removes the manual step where mistakes happen.
- **A "song is missing" UI.** If `catalog.personal.kfmanifest` references a song whose chart file is missing (e.g., on a fresh clone where personal assets haven't been re-added), the existing chart-load error path applies. No new UI for this edge case.
- **Migration of legacy users.** No legacy users; this is the introduction of the convention. The four classical songs stay where they are.
- **Symlinks / git-lfs / submodule** alternatives. Considered and rejected (see §3.3).
- **Multi-tier overlay** (e.g., `catalog.beta.kfmanifest`). YAGNI — public + personal is a clean two-tier split that covers the foreseeable need.

### 2.3 Guardrails (non-regression contracts)

- **Public catalog cold-start path is byte-identical.** A device running today's APK with no `catalog.personal.kfmanifest` present sees exactly the same four-song list, same chart contents, same gameplay timing. The overlay loader's missing-file branch is the no-op fallback.
- **`isPersonal` is never serialized.** `catalog.kfmanifest` and `catalog.personal.kfmanifest` JSON files contain no `isPersonal` field; it is set transiently by `SongCatalog.LoadAsync` based on which file the entry came from. Round-tripping JSON would not preserve it (which is correct).
- **Existing `SongEntry` JSON parsing remains compatible** with already-written `catalog.kfmanifest`. The added `isPersonal` field is non-serialized and absent from input JSON.
- **No path string concatenation outside the dedicated resolver.** `ChartLoader.LoadFromStreamingAssetsCo` is the single function that knows about `personal/`. All callers pass the `bool`, never construct paths themselves.
- **Personal manifest schema = public manifest schema.** Same `CatalogDto` parser. Reduces test surface and avoids dual-schema drift.
- **Existing 196 EditMode tests remain green** with zero source modification. (Tests that mock `SongCatalog` via `SetForTesting` are unaffected since no `isPersonal` is set in those code paths → defaults to `false` → existing path resolution.)
- **Existing 49 pytest tests remain green.** The `personal: true` flag in YAML is an opt-in; default is `personal: false`, matching current behavior.

---

## 3. Approach

### 3.1 Design decisions

| # | Decision | Chosen | Rejected alternatives |
|---|---|---|---|
| 1 | Public/private boundary location | **Directory subpath (`personal/`).** Visible in file system, captured in 5 .gitignore rules that never grow. | (B) Per-song flag in single YAML + build-time split — committed `catalog.kfmanifest` becomes a generated artifact prone to drift; (C) Naming convention (`<song_id>_*`) — easy to forget; brittle. |
| 2 | Catalog merge mechanism | **Two manifest files merged at load time** (`catalog.kfmanifest` + optional `catalog.personal.kfmanifest`). | Single manifest with `personal` flag — leaks song titles in committed file; defeats purpose. |
| 3 | `isPersonal` field placement | **Transient field on `SongEntry`, set by loader.** | (B) Wrapper class — clutter, every consumer needs to unwrap; (C) Parallel `personalIds: HashSet<string>` map on `SongCatalog` — two sources of truth. |
| 4 | Chart path resolution | **`ChartLoader.LoadFromStreamingAssetsCo(songId, isPersonal, ...)` parameter.** | (B) `chartPath` string field in `SongEntry` — more flexible but unused flexibility; (C) Two-stage lookup (try public, fall back to personal) — masks bugs where a personal song accidentally has a public chart file. |
| 5 | Thumbnail path resolution | **Reuse existing `entry.thumbnail` string (no code change).** Personal manifest stores `"thumbs/personal/<name>.png"`. | Symmetric `isPersonal` flag for thumbnails — unnecessary; thumbnail field is already a path. |
| 6 | Pipeline output routing | **Inferred from batch YAML location: any batch file under `tools/midi_to_kfchart/personal/` routes outputs to `charts/personal/` and `thumbs/personal/`.** No new YAML field. | (B) Explicit `personal: true` flag in batch YAML — easy to forget, undermines the convention; (C) Separate `midi_to_kfchart_personal.py` script — code duplication for an output-dir difference. |
| 7 | Personal manifest existence | **Optional. Missing → no overlay applied. No error.** | Required + empty-array template — every fresh clone needs a placeholder; awkward. |
| 8 | Manifest merge semantics | **Append personal entries after public entries; no dedup; if same `id` collides, personal overrides (last write wins).** | Strict reject on collision — overconservative; ID overlap is a developer mistake worth flagging via warning, not crashing. |
| 9 | Tests for overlay merge | **EditMode-pure** (use `ParseJson` + an explicit merge helper, no file I/O). | Integration tests that touch StreamingAssets — slow, brittle on Android emulator. |
| 10 | `.gitignore` rule precision | **Directory rules** (`midi/personal/`, etc.). One rule per personal subdir. | File-glob rules (`<song_id>_*.kfchart`) — back to per-song pattern thinking; defeats purpose. |

### 3.2 Data flow

```
[Cold start]
  SongCatalog.LoadAsync()
    │
    ├─► Load catalog.kfmanifest (required)
    │     └─► ParseJson → SongEntry[] (all .isPersonal = false)
    │
    ├─► Try load catalog.personal.kfmanifest
    │     ├─► File exists?
    │     │     ├─ yes → ParseJson → SongEntry[]; mark each .isPersonal = true
    │     │     └─ no  → empty array, log "no personal overlay" at INFO
    │     │
    │     └─► Append personal entries to public entries
    │
    └─► loaded = merged array

[Song selection from MainScreen]
  User taps a song card → MainScreen pushes SongEntry to GameplayController
    │
    └─► GameplayController.ResetAndStart(entry)
          │
          └─► ChartLoader.LoadFromStreamingAssetsCo(entry.id, entry.isPersonal, onLoaded, onError)
                │
                ├─► path = isPersonal
                │     ? streamingAssetsPath/charts/personal/<id>.kfchart
                │     : streamingAssetsPath/charts/<id>.kfchart
                │
                └─► UnityWebRequest / File.ReadAllText → ChartData

[Thumbnail load — MainScreen.cs:74, unchanged]
  Path.Combine(streamingAssetsPath, entry.thumbnail)
    where entry.thumbnail = "thumbs/personal/example_personal_song.png" for personal
                           "thumbs/ode_to_joy.png" for public
```

Single magic point: the `isPersonal ? "personal/" : ""` prefix in `ChartLoader`. Everything else is convention captured in directory structure or in the manifest's `thumbnail` field.

### 3.3 Rejected architectural alternatives

- **Single YAML with `personal:` flag, build-time split into two manifests.** Tempting "single source of truth" but `catalog.kfmanifest` becomes a generated artifact. Generated files committed to git rot the moment someone edits the source YAML and forgets to regenerate. Bound to leak.
- **Symlinks from public dirs into a personal/ tree.** Symlinks and Git on Windows are a known foot-gun; the project already supports macOS + Linux + Windows dev. Discarded for portability.
- **git-lfs for personal assets.** LFS doesn't change committedness — a `git add example_personal_song.kfchart` still commits the file (just as an LFS pointer). No leak protection.
- **Git submodule for personal assets pointing to a private repo.** Adds clone-time complexity (developers without access to the private submodule see broken refs). Overkill for "I have a few personal MIDIs."
- **Encrypt personal chart JSONs at rest, decrypt at load time.** Key has to live somewhere — APK or environment — and once leaked, all charts decrypt. Adds runtime cost for zero security gain.
- **Pre-commit hook that scans staged files for forbidden patterns.** Belt-and-suspenders, could add later, but the directory convention already removes the failure mode (no developer is staging files inside a `personal/` dir by accident — and if they `git add -f`, that's an explicit override).
- **Strict ID dedup on overlay merge (throw on collision).** Considered but rejected (Q8). A developer testing a personal version of a public chart (e.g., a Hard remix of `joplin_the_entertainer`) might legitimately want the personal entry to shadow the public one. Last-write-wins with a warning log is the friendlier rule.

---

## 4. Components

### 4.1 New files

| Path | Type | Responsibility |
|---|---|---|
| `Assets/StreamingAssets/charts/personal/.gitkeep` | placeholder | Forces the directory to exist on a fresh clone so AssetDatabase doesn't complain about missing AssetDir at scene-build time. |
| `Assets/StreamingAssets/thumbs/personal/.gitkeep` | placeholder | Same. |
| `Assets/Tests/EditMode/SongCatalogOverlayTests.cs` | EditMode tests | 3-4 tests: merge with/without personal file, `isPersonal` flag propagation, ID-collision warning. |
| `Assets/Tests/EditMode/ChartLoaderPersonalPathTests.cs` | EditMode tests | 1-2 tests: path resolution branches on `isPersonal`. |
| `tools/midi_to_kfchart/tests/test_personal_routing.py` | pytest | 1-2 tests: `personal: true` routes to `charts/personal/`, `thumbs/personal/`. |
| `docs/superpowers/specs/2026-04-26-personal-song-overlay-design.md` | spec | This document. |

### 4.2 Modified files

| Path | Change |
|---|---|
| `.gitignore` | Replace the 6-line `<song_id>_*` block with 5 directory-scope rules (see §4.3). |
| `Assets/Scripts/Catalog/SongEntry.cs` | Add `[Newtonsoft.Json.JsonIgnore] public bool isPersonal;` to `SongEntry` class. ~2 LoC. |
| `Assets/Scripts/Catalog/SongCatalog.cs` | Refactor `LoadAsync()` into "load required base + try optional overlay + merge". Add `MergeOverlay(SongEntry[] base, SongEntry[] personal)` helper that marks personal entries and appends. ~30 net LoC. |
| `Assets/Scripts/Charts/ChartLoader.cs` | Add `bool isPersonal` parameter to `LoadFromStreamingAssetsCo`. Path computation branches on it. ~5 net LoC. |
| `Assets/Scripts/Gameplay/GameplayController.cs` | Update the single call site to `ChartLoader.LoadFromStreamingAssetsCo(songId, entry.isPersonal, ...)`. ~1 net LoC. (Verify no other call sites — see §4.5 verification.) |
| `Assets/StreamingAssets/catalog.kfmanifest` | No change. Already contains only the four public songs after the morning fix. |
| `tools/midi_to_kfchart/midi_to_kfchart.py` | Detect "personal mode" by checking if the batch file path contains a `personal/` segment. When detected, prepend `personal/` to the chart and thumbnail output dirs. ~10 net LoC. |
| `tools/midi_to_kfchart/pipeline/parser.py` or runner | Receive routing info; no logic change beyond passing through. |
| `CLAUDE.md` | Add a "Personal songs (copyrighted material)" subsection under the existing Python pipeline section explaining the `personal/` convention. ~10 lines of markdown. |

### 4.3 New `.gitignore` block (replaces current 6-line block)

```gitignore
# Personal / copyrighted song assets.
# Drop any new copyrighted song's files into the matching `personal/` subdir
# below — they will be ignored automatically. No per-song ignore rule needed.
midi/personal/
Assets/StreamingAssets/charts/personal/
Assets/StreamingAssets/thumbs/personal/
Assets/StreamingAssets/catalog.personal.kfmanifest
tools/midi_to_kfchart/personal/
```

Note: this block must remain AFTER `!/[Aa]ssets/**/*.meta` (line 14 of the current `.gitignore`) so that `.meta` files inside `Assets/StreamingAssets/charts/personal/` are also ignored. Directory-scope ignore rules suppress all contents including `.meta` files even with the negation, because git rules state: "It is not possible to re-include a file if a parent directory of that file is excluded." The directory `Assets/StreamingAssets/charts/personal/` excludes its entire contents.

### 4.4 New file movements

Existing files (currently untracked due to morning's `<song_id>_*` ignore rules) will be physically relocated:

| From | To |
|---|---|
| `midi/Example Song.mid` | `midi/personal/Example Song.mid` |
| `Assets/StreamingAssets/charts/example_personal_song.kfchart` | `Assets/StreamingAssets/charts/personal/example_personal_song.kfchart` |
| `Assets/StreamingAssets/charts/example_personal_song.kfchart.meta` | `Assets/StreamingAssets/charts/personal/example_personal_song.kfchart.meta` |
| `Assets/StreamingAssets/thumbs/example_personal_song.png` | `Assets/StreamingAssets/thumbs/personal/example_personal_song.png` |
| `Assets/StreamingAssets/thumbs/example_personal_song.png.meta` | `Assets/StreamingAssets/thumbs/personal/example_personal_song.png.meta` |
| `tools/midi_to_kfchart/batch_personal.yaml` | `tools/midi_to_kfchart/personal/batch_personal.yaml` |

The morning's existing `.gitignore` rules (`<song_id>_*` patterns) are removed in this spec — they are subsumed by the new directory rules.

A new local file is created during testing (gitignored, optional):

`Assets/StreamingAssets/catalog.personal.kfmanifest`:
```json
{
  "version": 1,
  "songs": [
    { "id": "example_personal_song", "title": "Example Song", "composer": "Example Artist",
      "thumbnail": "thumbs/personal/example_personal_song.png",
      "difficulties": ["Easy", "Normal"], "chartAvailable": true, "durationMs": 150000 }
  ]
}
```

### 4.5 Call-site verification checklist (during implementation)

Before merging, confirm `ChartLoader.LoadFromStreamingAssetsCo` has a single call site by:

```bash
grep -rn "LoadFromStreamingAssetsCo\|LoadFromPath" Assets/Scripts/ Assets/Tests/ Assets/Editor/
```

Any caller other than `GameplayController` and tests must be updated to pass `isPersonal`. EditMode tests using `LoadFromPath` (absolute path) are unaffected.

---

## 5. Testing

### 5.1 `SongCatalogOverlayTests` (3-4 tests)

Pure EditMode, scene-independent. Use `SongCatalog.ParseJson` + a new internal `MergeOverlay` helper directly (no file I/O).

| # | Name | Verifies |
|---|---|---|
| 1 | `MergeOverlay_BaseOnly_ReturnsBaseEntriesUnflagged` | Public manifest only → all entries have `isPersonal == false`; count matches input. |
| 2 | `MergeOverlay_BaseAndPersonal_AppendsPersonalEntriesFlagged` | Base + personal → personal entries appended after base; only personal entries have `isPersonal == true`. |
| 3 | `MergeOverlay_DuplicateId_PersonalOverridesBase_LogsWarning` | Same `id` in both → personal entry replaces base entry; warning is logged via UnityEngine.Debug.LogWarning. |
| 4 (opt) | `MergeOverlay_NullPersonal_ReturnsBaseUnchanged` | Null overlay (file missing case at the merge step) → base array returned as-is. |

### 5.2 `ChartLoaderPersonalPathTests` (1-2 tests)

| # | Name | Verifies |
|---|---|---|
| 1 | `ResolvePath_PublicSong_UsesChartsRoot` | `isPersonal=false` → resolved path == `<streamingAssets>/charts/<id>.kfchart`. |
| 2 | `ResolvePath_PersonalSong_UsesPersonalSubdir` | `isPersonal=true` → resolved path == `<streamingAssets>/charts/personal/<id>.kfchart`. |

Implemented via a new internal static `ResolveChartPath(string songId, bool isPersonal)` extracted from `LoadFromStreamingAssetsCo` to make the path construction unit-testable without async coroutine machinery.

### 5.3 `test_personal_routing.py` (1-2 pytest cases)

| # | Name | Verifies |
|---|---|---|
| 1 | `test_batch_under_personal_dir_routes_chart_to_personal_subdir` | Batch yaml file located at `tools/midi_to_kfchart/personal/foo.yaml` → chart output path is under `Assets/StreamingAssets/charts/personal/`. |
| 2 | `test_batch_under_personal_dir_routes_thumbnail_to_personal_subdir` | Same input → thumbnail output is under `thumbs/personal/`. |
| 3 (opt) | `test_batch_outside_personal_dir_routes_to_public_subdirs` | Control: batch yaml outside `personal/` → chart output at `charts/`, thumbnail at `thumbs/`. |

### 5.4 Regression surface

- 196 existing EditMode tests pass with zero source modification (added `isPersonal` field defaults to `false` on `SongEntry`; existing tests never set it; existing `ChartLoader` callers in tests use `LoadFromPath` with absolute paths and don't go through `LoadFromStreamingAssetsCo`).
- 49 existing pytest tests pass — `personal: true` is opt-in.
- Cold-start and gameplay of the four classical songs is byte-identical (verify by playing one through on a manual device test — see §5.5).

### 5.5 Manual smoke test

- [ ] Fresh clone (or `git clean -fdx` of `Assets/StreamingAssets/` → restore from git): `catalog.personal.kfmanifest` does not exist; cold-start shows 4-song MainScreen; play through one song to completion.
- [ ] Add `catalog.personal.kfmanifest` with one the personal song entry; cold-start shows 5-song MainScreen; play the personal song to completion.
- [ ] Run `git status`: empty (no tracked changes from local-only personal additions).
- [ ] `git check-ignore -v` against each of the 6 personal asset paths: all ignored, all matched against directory rules in `.gitignore` lines.
- [ ] Build APK: only public songs included if `catalog.personal.kfmanifest` is absent at build time. (Verify via APK-extracting `unzip -l Builds/<apk> | grep -i example_personal_song` → no matches when running CI; matches on local dev machine after personal manifest is added.)

---

## 6. Risks & mitigations

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Developer adds personal song but forgets `personal/` subdir, drops file in `Assets/StreamingAssets/charts/` directly | Medium | File staged for commit, leak risk | The catalog editing step (adding to `catalog.personal.kfmanifest`) prompts subdir-correct path. Code review on commits is the human safety net. Add a CI check post-MVP if leaks occur. |
| `git check-ignore` rules behave differently on Windows vs macOS | Low | False sense of security | Verified using same git binary semantics across platforms — directory ignore is portable per git docs. Manual smoke test on Windows recommended once. |
| `catalog.personal.kfmanifest` references a song whose chart was never built | Low | Runtime "chart not found" error during play | Existing chart-load error path (toast or back-to-MainScreen) applies. Acceptable. |
| Existing EditMode test fixtures inadvertently set `isPersonal=true` via constructor or default | Low | Test breakage | `isPersonal` defaults to `false` (C# zero-value); no test currently passes a non-default value. Field is `[JsonIgnore]` so JSON fixtures don't carry it. |
| Developer places personal batch yaml outside `tools/midi_to_kfchart/personal/` and pipeline writes to public `charts/` dir → leak | Medium | Public dir contains personal chart | Convention is "batch yaml location IS the routing decision." A misplaced batch yaml is the developer mistake; an explicit YAML flag would be no safer (still requires getting one thing right per song). The shorter feedback loop is `git status` after the pipeline run — public chart dir gets a new `??` line, prompting investigation. |
| Future addition of HARD difficulty (W7 SP4) breaks merge semantics | Low | Merge logic needs awareness of new schema | The merge happens at the `SongEntry` level, not at the `Difficulty` level. Adding a "Hard" string to `entry.difficulties` is a manifest content change, orthogonal to overlay mechanics. |
| `[JsonIgnore]` attribute requires a specific Newtonsoft import | Low | Compile error | The project already uses Newtonsoft (see `ChartLoader.cs:4`). Same `using` directive. |
| Merge order makes `MainScreen` show personal songs at bottom — unintuitive sort | Low | Cosmetic | Acceptable for personal songs (they are the developer's own additions; explicit ordering preference can be added later via a per-entry `sortOrder` field). |
| Asset `.meta` files inside `personal/` get re-included by the `!/Assets/**/*.meta` negation rule | Low (verified) | Files leak | Per git docs, an excluded parent directory cannot be re-included via negation on its contents. The directory rule `Assets/StreamingAssets/charts/personal/` overrides the file-pattern negation. Verified in §5.5 smoke test. |

---

## 7. Rollback

If this overlay system causes a post-merge regression:

1. Revert the merge commit (PSO is delivered as one PR).
2. Move the `personal/` subdir contents back to their flat-directory locations (one command per directory).
3. Restore the morning's `<song_id>_*` 6-line `.gitignore` block.
4. No data migration needed — `catalog.personal.kfmanifest` is local-only and gitignored regardless of revert.

The four classical songs are unaffected by either direction since their files never moved.

---

## 8. Acceptance criteria

- [ ] All new EditMode tests green (4-6 added).
- [ ] All existing 196 EditMode tests green with zero source modification.
- [ ] All existing 49 pytest tests green; new pytest tests (1-2) green.
- [ ] `git status` shows no Personal-song-related files after restructure (all six properly ignored via directory rules).
- [ ] `git check-ignore -v` verification passes for all six personal asset paths.
- [ ] Manual smoke test §5.5 all checked.
- [ ] CLAUDE.md updated with the convention.
- [ ] `chore:` commit on its own (no functional gameplay changes mixed in).

---

## 9. File summary

**New (6):**
- `Assets/StreamingAssets/charts/personal/.gitkeep`
- `Assets/StreamingAssets/thumbs/personal/.gitkeep`
- `Assets/Tests/EditMode/SongCatalogOverlayTests.cs`
- `Assets/Tests/EditMode/ChartLoaderPersonalPathTests.cs`
- `tools/midi_to_kfchart/tests/test_personal_routing.py`
- `docs/superpowers/specs/2026-04-26-personal-song-overlay-design.md` (this file)

**Modified (6):**
- `.gitignore`
- `Assets/Scripts/Catalog/SongEntry.cs`
- `Assets/Scripts/Catalog/SongCatalog.cs`
- `Assets/Scripts/Charts/ChartLoader.cs`
- `Assets/Scripts/Gameplay/GameplayController.cs`
- `tools/midi_to_kfchart/midi_to_kfchart.py`
- `CLAUDE.md`

**Moved (6 files into `personal/` subdirs):**
- example_personal_song assets (chart, chart.meta, png, png.meta) and the source MIDI + batch yaml.

**Total LoC (C# production):** ~40 lines across 4 files.
**Total LoC (Python production):** ~10 lines.
**Total LoC (tests):** ~80 lines across 3 files.
