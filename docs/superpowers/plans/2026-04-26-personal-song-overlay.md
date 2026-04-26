# Personal Song Overlay Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the per-song .gitignore + manual catalog editing pattern with a directory-convention overlay system: copyrighted/personal songs live under `personal/` subdirs and are merged into the catalog at runtime via an optional `catalog.personal.kfmanifest`.

**Architecture:** Public/private boundary is encoded in directory location. Five directory-scope `.gitignore` rules cover all current and future personal assets. `SongCatalog.LoadAsync` loads `catalog.kfmanifest` (required) and merges an optional `catalog.personal.kfmanifest`, tagging overlay entries with `isPersonal=true`. `ChartLoader` resolves chart paths to `charts/personal/<id>.kfchart` when `isPersonal`. Thumbnails reuse the existing `entry.thumbnail` path field with `thumbs/personal/...` strings (no code change).

**Tech Stack:** C# (Unity 6000.3.13f1, Newtonsoft JSON 3.2.1, NUnit), Python 3 (pyyaml, mido, pytest), git.

**Spec:** `docs/superpowers/specs/2026-04-26-personal-song-overlay-design.md`

---

## File Structure

**New (5):**
- `Assets/StreamingAssets/charts/personal/.gitkeep` — directory placeholder
- `Assets/StreamingAssets/thumbs/personal/.gitkeep` — directory placeholder
- `Assets/Tests/EditMode/SongCatalogOverlayTests.cs` — overlay merge tests
- `Assets/Tests/EditMode/ChartLoaderPersonalPathTests.cs` — path resolution tests
- `tools/midi_to_kfchart/tests/test_personal_routing.py` — pipeline routing tests

**Modified (7):**
- `.gitignore`
- `Assets/Scripts/Catalog/SongEntry.cs` — add `isPersonal` field
- `Assets/Scripts/Catalog/SongCatalog.cs` — split read step, add `MergeOverlay`
- `Assets/Scripts/Charts/ChartLoader.cs` — add `isPersonal` param + `ResolveChartPath` helper
- `Assets/Scripts/Gameplay/GameplayController.cs` — pass `isPersonal` to ChartLoader
- `tools/midi_to_kfchart/midi_to_kfchart.py` — path-based routing in `_batch`
- `CLAUDE.md` — document the convention

**Moved (in working tree, all currently untracked):**
- `midi/KPop Demon Hunters - Golden.mid` → `midi/personal/...`
- `Assets/StreamingAssets/charts/huntrx_golden.kfchart` (+ `.meta`) → `charts/personal/...`
- `Assets/StreamingAssets/thumbs/huntrx_golden.png` (+ `.meta`) → `thumbs/personal/...`
- `tools/midi_to_kfchart/batch_personal.yaml` → `tools/midi_to_kfchart/personal/batch_personal.yaml`

---

## Task 1: Restructure assets and replace `.gitignore` block

Move all currently untracked Huntrx assets into `personal/` subdirs and replace the morning's 6-line `huntrx_*` block with 5 directory-scope rules. Single commit so the ignore rules and file locations transition atomically.

**Files:**
- Modify: `.gitignore` (lines 89-99 — the morning's huntrx block at the file end)
- Create: `Assets/StreamingAssets/charts/personal/.gitkeep`
- Create: `Assets/StreamingAssets/thumbs/personal/.gitkeep`
- Move: 6 huntrx-related files (see File Structure)

- [ ] **Step 1: Create personal subdirectories with .gitkeep placeholders**

```bash
mkdir -p midi/personal
mkdir -p Assets/StreamingAssets/charts/personal
mkdir -p Assets/StreamingAssets/thumbs/personal
mkdir -p tools/midi_to_kfchart/personal
touch Assets/StreamingAssets/charts/personal/.gitkeep
touch Assets/StreamingAssets/thumbs/personal/.gitkeep
```

- [ ] **Step 2: Move huntrx assets and personal batch yaml into personal subdirs**

```bash
mv "midi/KPop Demon Hunters - Golden.mid" midi/personal/
mv Assets/StreamingAssets/charts/huntrx_golden.kfchart Assets/StreamingAssets/charts/personal/
mv Assets/StreamingAssets/charts/huntrx_golden.kfchart.meta Assets/StreamingAssets/charts/personal/
mv Assets/StreamingAssets/thumbs/huntrx_golden.png Assets/StreamingAssets/thumbs/personal/
mv Assets/StreamingAssets/thumbs/huntrx_golden.png.meta Assets/StreamingAssets/thumbs/personal/
mv tools/midi_to_kfchart/batch_personal.yaml tools/midi_to_kfchart/personal/batch_personal.yaml
```

- [ ] **Step 3: Replace the 6-line `huntrx_*` block in `.gitignore` with 5 directory rules**

Use the Edit tool on `.gitignore` to replace this block:

```
# Personal / copyrighted song sources (do not publish)
midi/
tools/midi_to_kfchart/batch_personal.yaml

# Personal song assets (Huntrx Golden — copyrighted).
# Must come AFTER `!/[Aa]ssets/**/*.meta` to re-exclude these specific .meta files.
Assets/StreamingAssets/charts/huntrx_*.kfchart
Assets/StreamingAssets/charts/huntrx_*.kfchart.meta
Assets/StreamingAssets/thumbs/huntrx_*.png
Assets/StreamingAssets/thumbs/huntrx_*.png.meta
```

with:

```
# Personal / copyrighted song assets.
# Drop any new copyrighted song's files into the matching `personal/` subdir
# below — they will be ignored automatically. No per-song ignore rule needed.
midi/personal/
Assets/StreamingAssets/charts/personal/
Assets/StreamingAssets/thumbs/personal/
Assets/StreamingAssets/catalog.personal.kfmanifest
tools/midi_to_kfchart/personal/
```

Note: keep the surrounding sections (`# Test runner + profiler outputs` block above) intact.

- [ ] **Step 4: Verify ignore rules cover all relocated files and the optional manifest**

Run:
```bash
git check-ignore -v \
  "midi/personal/KPop Demon Hunters - Golden.mid" \
  Assets/StreamingAssets/charts/personal/huntrx_golden.kfchart \
  Assets/StreamingAssets/charts/personal/huntrx_golden.kfchart.meta \
  Assets/StreamingAssets/thumbs/personal/huntrx_golden.png \
  Assets/StreamingAssets/thumbs/personal/huntrx_golden.png.meta \
  tools/midi_to_kfchart/personal/batch_personal.yaml
```

Expected: every line outputs a match against one of the 5 new directory rules in `.gitignore`. No "::" lines (which would indicate "no match").

Run:
```bash
git status --short | grep -i huntrx || echo "OK: no huntrx in status"
git status --short | grep -E "personal/$|personal/[^.]" | grep -v ".gitkeep" || echo "OK: no personal contents in status"
```

Expected: both lines print `OK:`. The only `personal/` entries that should appear in `git status` are the two `.gitkeep` files (and `tools/midi_to_kfchart/personal/` is not shown because the entire dir is ignored — its `batch_personal.yaml` was moved into it, not committed).

- [ ] **Step 5: Stage the changes and confirm what will commit**

Run:
```bash
git add .gitignore \
        Assets/StreamingAssets/charts/personal/.gitkeep \
        Assets/StreamingAssets/thumbs/personal/.gitkeep
git status --short
```

Expected `git status --short` to show (in addition to other unrelated in-flight work):
- `M  .gitignore`
- `A  Assets/StreamingAssets/charts/personal/.gitkeep`
- `A  Assets/StreamingAssets/thumbs/personal/.gitkeep`

No huntrx file should be staged. No `personal/` content other than the two `.gitkeep`s should be staged.

- [ ] **Step 6: Commit**

```bash
git commit -m "$(cat <<'EOF'
chore(personal-overlay): restructure to personal/ subdirs + directory-scope gitignore

Replace per-song huntrx_* gitignore patterns with 5 directory-scope rules
covering midi/personal/, charts/personal/, thumbs/personal/, the optional
catalog.personal.kfmanifest, and tools/midi_to_kfchart/personal/. Adding new
copyrighted songs no longer requires editing .gitignore.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 2: Add `isPersonal` field to `SongEntry`

**Files:**
- Modify: `Assets/Scripts/Catalog/SongEntry.cs`

- [ ] **Step 1: Add the field with `[JsonIgnore]` attribute**

Edit `Assets/Scripts/Catalog/SongEntry.cs`. Add the using directive at the top:

```csharp
using System;
using Newtonsoft.Json;
```

Inside the `SongEntry` class, add after the `durationMs` field:

```csharp
        // Set transiently by SongCatalog.LoadAsync from which manifest the entry came from.
        [JsonIgnore]
        public bool isPersonal;
```

- [ ] **Step 2: Verify it compiles and existing tests pass**

Run from a separate shell (or skip if Unity batch-mode CLI is heavy — defer to Task 8 final verification):

```bash
"<UnityEditor>" -batchmode -nographics -projectPath . \
  -runTests -testPlatform EditMode -testResults Builds/test-results.xml
grep -E '<test-suite|<test-case.*result="Failed"' Builds/test-results.xml | head
```

Expected: 196/196 pass. The `[JsonIgnore]` field defaults to `false` for any deserialized JSON entry; no existing test sets it.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Catalog/SongEntry.cs
git commit -m "$(cat <<'EOF'
feat(catalog): add transient isPersonal flag to SongEntry

[JsonIgnore] so it never round-trips through the JSON manifest. Set by
SongCatalog.LoadAsync based on which manifest file an entry came from.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 3: Add `MergeOverlay` helper to `SongCatalog` (TDD)

Pure function on arrays. Writing the helper before refactoring `LoadAsync` lets us test the merge logic without dragging Unity coroutines into the test.

**Files:**
- Create: `Assets/Tests/EditMode/SongCatalogOverlayTests.cs`
- Modify: `Assets/Scripts/Catalog/SongCatalog.cs`

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/EditMode/SongCatalogOverlayTests.cs`:

```csharp
using NUnit.Framework;
using KeyFlow;

namespace KeyFlow.Tests.EditMode
{
    public class SongCatalogOverlayTests
    {
        [Test]
        public void MergeOverlay_NullOverlay_ReturnsBaseUnchanged()
        {
            var basePart = new[]
            {
                new SongEntry { id = "a" },
                new SongEntry { id = "b" }
            };
            var result = SongCatalog.MergeOverlay(basePart, null);
            Assert.AreEqual(2, result.Length);
            Assert.AreEqual("a", result[0].id);
            Assert.AreEqual("b", result[1].id);
            Assert.IsFalse(result[0].isPersonal);
            Assert.IsFalse(result[1].isPersonal);
        }

        [Test]
        public void MergeOverlay_BaseAndPersonal_AppendsPersonalEntries()
        {
            var basePart = new[] { new SongEntry { id = "a" } };
            var overlayPart = new[] { new SongEntry { id = "b", isPersonal = true } };
            var result = SongCatalog.MergeOverlay(basePart, overlayPart);
            Assert.AreEqual(2, result.Length);
            Assert.AreEqual("a", result[0].id);
            Assert.IsFalse(result[0].isPersonal);
            Assert.AreEqual("b", result[1].id);
            Assert.IsTrue(result[1].isPersonal);
        }

        [Test]
        public void MergeOverlay_DuplicateId_PersonalOverridesBase()
        {
            var basePart = new[] { new SongEntry { id = "a", title = "Public" } };
            var overlayPart = new[] { new SongEntry { id = "a", title = "Personal", isPersonal = true } };
            var result = SongCatalog.MergeOverlay(basePart, overlayPart);
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual("Personal", result[0].title);
            Assert.IsTrue(result[0].isPersonal);
        }

        [Test]
        public void MergeOverlay_NullBase_TreatsAsEmpty()
        {
            var overlayPart = new[] { new SongEntry { id = "a", isPersonal = true } };
            var result = SongCatalog.MergeOverlay(null, overlayPart);
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual("a", result[0].id);
            Assert.IsTrue(result[0].isPersonal);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
"<UnityEditor>" -batchmode -nographics -projectPath . \
  -runTests -testPlatform EditMode -testResults Builds/test-results.xml \
  -testFilter SongCatalogOverlayTests
grep -E 'result="(Passed|Failed)"' Builds/test-results.xml | head
```

Expected: all 4 tests Failed with "MergeOverlay does not exist on SongCatalog" or compile error.

- [ ] **Step 3: Implement `MergeOverlay` in `SongCatalog`**

Edit `Assets/Scripts/Catalog/SongCatalog.cs`. Add after `TryGet`:

```csharp
        // Pure merge: base entries first (preserve order), then overlay-only
        // entries (preserve order). Same id in both → overlay wins (last write).
        // Caller is responsible for setting isPersonal=true on overlay entries
        // before calling. Either argument may be null.
        public static SongEntry[] MergeOverlay(SongEntry[] basePart, SongEntry[] overlayPart)
        {
            basePart ??= System.Array.Empty<SongEntry>();
            if (overlayPart == null || overlayPart.Length == 0) return basePart;

            var overlayById = new System.Collections.Generic.Dictionary<string, SongEntry>(overlayPart.Length);
            foreach (var e in overlayPart) overlayById[e.id] = e;

            var result = new System.Collections.Generic.List<SongEntry>(basePart.Length + overlayPart.Length);
            var emittedIds = new System.Collections.Generic.HashSet<string>();
            foreach (var e in basePart)
            {
                if (overlayById.TryGetValue(e.id, out var overlayEntry))
                {
                    UnityEngine.Debug.LogWarning($"[KeyFlow] catalog overlay: id '{e.id}' overrides base entry");
                    result.Add(overlayEntry);
                }
                else
                {
                    result.Add(e);
                }
                emittedIds.Add(e.id);
            }
            foreach (var e in overlayPart)
            {
                if (!emittedIds.Contains(e.id)) { result.Add(e); emittedIds.Add(e.id); }
            }
            return result.ToArray();
        }
```

- [ ] **Step 4: Run the tests to verify they pass**

```bash
"<UnityEditor>" -batchmode -nographics -projectPath . \
  -runTests -testPlatform EditMode -testResults Builds/test-results.xml \
  -testFilter SongCatalogOverlayTests
grep -E 'result="Failed"' Builds/test-results.xml || echo "OK: no failures"
```

Expected: `OK: no failures`.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Catalog/SongCatalog.cs Assets/Tests/EditMode/SongCatalogOverlayTests.cs
git commit -m "$(cat <<'EOF'
feat(catalog): add MergeOverlay helper for personal-song manifest overlay

Pure merge with base-first ordering and last-write-wins for id collisions.
Overlay entries must be pre-flagged with isPersonal=true by the caller.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 4: Refactor `SongCatalog.LoadAsync` to read base + optional overlay

Now wire `MergeOverlay` into the actual loader. Splits the file-read into a small reusable helper that can produce optional results without throwing on missing files.

**Files:**
- Modify: `Assets/Scripts/Catalog/SongCatalog.cs`

- [ ] **Step 1: Replace `LoadAsync` with the overlay-aware version**

Edit `Assets/Scripts/Catalog/SongCatalog.cs`. Replace the existing `LoadAsync` method (lines 39-56) with:

```csharp
        public static IEnumerator LoadAsync()
        {
            string baseJson = null;
            string baseError = null;
            yield return ReadStreamingAssetCo("catalog.kfmanifest",
                t => baseJson = t,
                e => baseError = e);
            if (baseJson == null)
                throw new System.IO.FileNotFoundException($"catalog load failed: {baseError}");

            string overlayJson = null;
            yield return ReadStreamingAssetCo("catalog.personal.kfmanifest",
                t => overlayJson = t,
                _ => { /* optional: missing file is the no-overlay case */ });

            var basePart = ParseJson(baseJson);
            SongEntry[] overlayPart = null;
            if (!string.IsNullOrEmpty(overlayJson))
            {
                overlayPart = ParseJson(overlayJson);
                foreach (var e in overlayPart) e.isPersonal = true;
            }

            loaded = MergeOverlay(basePart, overlayPart);
        }

        private static IEnumerator ReadStreamingAssetCo(
            string file,
            System.Action<string> onText,
            System.Action<string> onError)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            string url = Path.Combine(Application.streamingAssetsPath, file);
            using (var req = UnityWebRequest.Get(url))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                    onError?.Invoke(req.error);
                else
                    onText?.Invoke(req.downloadHandler.text);
            }
#else
            string path = Path.Combine(Application.streamingAssetsPath, file);
            if (!File.Exists(path)) { onError?.Invoke("file not found"); yield break; }
            onText?.Invoke(File.ReadAllText(path));
            yield break;
#endif
        }
```

- [ ] **Step 2: Run all EditMode tests to verify nothing broke**

```bash
"<UnityEditor>" -batchmode -nographics -projectPath . \
  -runTests -testPlatform EditMode -testResults Builds/test-results.xml
grep -E 'result="Failed"' Builds/test-results.xml || echo "OK: no failures"
```

Expected: `OK: no failures`. The overlay-related changes do not affect any test that uses `SetForTesting` (those bypass `LoadAsync`).

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Catalog/SongCatalog.cs
git commit -m "$(cat <<'EOF'
feat(catalog): load optional catalog.personal.kfmanifest overlay

LoadAsync now reads catalog.kfmanifest (required) and catalog.personal.kfmanifest
(optional, no error if absent), tags overlay entries with isPersonal=true, and
merges via MergeOverlay. Cold-start path for the public catalog is unchanged.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 5: Add `isPersonal` to `ChartLoader` path resolution (TDD)

Extract a pure `ResolveChartPath` for unit testing, then thread `bool isPersonal` through `LoadFromStreamingAssetsCo`. Updates the single caller (`GameplayController`) in the same task to keep the build compiling.

**Files:**
- Create: `Assets/Tests/EditMode/ChartLoaderPersonalPathTests.cs`
- Modify: `Assets/Scripts/Charts/ChartLoader.cs`
- Modify: `Assets/Scripts/Gameplay/GameplayController.cs:66`

- [ ] **Step 1: Write the failing path-resolution tests**

Create `Assets/Tests/EditMode/ChartLoaderPersonalPathTests.cs`:

```csharp
using NUnit.Framework;
using KeyFlow.Charts;

namespace KeyFlow.Tests.EditMode
{
    public class ChartLoaderPersonalPathTests
    {
        [Test]
        public void ResolveChartPath_PublicSong_UsesChartsRoot()
        {
            var path = ChartLoader.ResolveChartPath("foo", isPersonal: false);
            string expected = "charts" + System.IO.Path.DirectorySeparatorChar + "foo.kfchart";
            Assert.That(path, Does.EndWith(expected));
            Assert.That(path, Does.Not.Contain("personal"));
        }

        [Test]
        public void ResolveChartPath_PersonalSong_UsesPersonalSubdir()
        {
            var path = ChartLoader.ResolveChartPath("foo", isPersonal: true);
            string expected = "charts" + System.IO.Path.DirectorySeparatorChar
                            + "personal" + System.IO.Path.DirectorySeparatorChar
                            + "foo.kfchart";
            Assert.That(path, Does.EndWith(expected));
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
"<UnityEditor>" -batchmode -nographics -projectPath . \
  -runTests -testPlatform EditMode -testResults Builds/test-results.xml \
  -testFilter ChartLoaderPersonalPathTests
```

Expected: 2 Failed (`ResolveChartPath` does not exist).

- [ ] **Step 3: Add `ResolveChartPath` and the `isPersonal` parameter to `ChartLoader`**

Edit `Assets/Scripts/Charts/ChartLoader.cs`. Replace the body of `LoadFromStreamingAssetsCo` (lines 24-55) with:

```csharp
        public static IEnumerator LoadFromStreamingAssetsCo(
            string songId,
            bool isPersonal,
            System.Action<ChartData> onLoaded,
            System.Action<string> onError)
        {
            string path = ResolveChartPath(songId, isPersonal);

#if UNITY_ANDROID && !UNITY_EDITOR
            var req = UnityEngine.Networking.UnityWebRequest.Get(path);
            yield return req.SendWebRequest();
            var result = req.result;
            var text = req.downloadHandler.text;
            var error = req.error;
            req.Dispose();
            if (result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                onError?.Invoke($"{path}: {error}");
                yield break;
            }
            ChartData loaded;
            try { loaded = ParseJson(text); }
            catch (System.Exception e) { onError?.Invoke(e.Message); yield break; }
            onLoaded?.Invoke(loaded);
#else
            ChartData chart;
            try { chart = LoadFromPath(path); }
            catch (System.Exception e) { onError?.Invoke(e.Message); yield break; }
            yield return null;  // yield once for symmetry with Android path
            onLoaded?.Invoke(chart);
#endif
        }

        public static string ResolveChartPath(string songId, bool isPersonal)
        {
            string subdir = isPersonal
                ? System.IO.Path.Combine("charts", "personal")
                : "charts";
            return System.IO.Path.Combine(
                UnityEngine.Application.streamingAssetsPath, subdir, songId + ".kfchart");
        }
```

- [ ] **Step 4: Update the single call site in `GameplayController`**

Edit `Assets/Scripts/Gameplay/GameplayController.cs`. Replace lines 66-69:

```csharp
            StartCoroutine(ChartLoader.LoadFromStreamingAssetsCo(
                songId,
                loaded => { chart = loaded; ContinueAfterChartLoaded(); },
                err => Debug.LogError($"[KeyFlow] chart load failed: {err}")));
```

with:

```csharp
            bool isPersonal = SongCatalog.TryGet(songId, out var entry) && entry.isPersonal;
            StartCoroutine(ChartLoader.LoadFromStreamingAssetsCo(
                songId,
                isPersonal,
                loaded => { chart = loaded; ContinueAfterChartLoaded(); },
                err => Debug.LogError($"[KeyFlow] chart load failed: {err}")));
```

- [ ] **Step 5: Verify no other call sites of `LoadFromStreamingAssetsCo` exist**

```bash
grep -rn "LoadFromStreamingAssetsCo" Assets/Scripts/ Assets/Tests/ Assets/Editor/
```

Expected: only the two lines in `ChartLoader.cs` (definition) and `GameplayController.cs:66` (just-updated call). If any other caller exists, update it the same way (default to `false` if no `SongEntry` context is available).

- [ ] **Step 6: Run all EditMode tests**

```bash
"<UnityEditor>" -batchmode -nographics -projectPath . \
  -runTests -testPlatform EditMode -testResults Builds/test-results.xml
grep -E 'result="Failed"' Builds/test-results.xml || echo "OK: no failures"
```

Expected: `OK: no failures`. New `ResolveChartPath` tests pass; existing ChartLoader tests use `LoadFromPath` (absolute path) and are unaffected.

- [ ] **Step 7: Commit**

```bash
git add Assets/Scripts/Charts/ChartLoader.cs \
        Assets/Scripts/Gameplay/GameplayController.cs \
        Assets/Tests/EditMode/ChartLoaderPersonalPathTests.cs
git commit -m "$(cat <<'EOF'
feat(charts): resolve personal-song chart paths to charts/personal/<id>.kfchart

LoadFromStreamingAssetsCo gains an explicit isPersonal parameter; path
construction is extracted to ResolveChartPath for unit testing. The single
call site in GameplayController reads isPersonal from SongCatalog.TryGet.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 6: Add path-based personal routing to `midi_to_kfchart.py` (TDD)

Pipeline detects when invoked with a batch yaml whose path contains a `personal` segment and prepends `personal/` to the output directory.

**Files:**
- Create: `tools/midi_to_kfchart/tests/test_personal_routing.py`
- Modify: `tools/midi_to_kfchart/midi_to_kfchart.py` (`_batch` function)

- [ ] **Step 1: Write the failing pytest cases**

Create `tools/midi_to_kfchart/tests/test_personal_routing.py`:

```python
"""Personal routing: batch yaml under tools/midi_to_kfchart/personal/ -> outputs go to charts/personal/."""
import json
import subprocess
import sys
from pathlib import Path

import mido

TOOL = Path(__file__).resolve().parent.parent / "midi_to_kfchart.py"


def _make_tiny_midi(path):
    mid = mido.MidiFile(type=0, ticks_per_beat=480)
    track = mido.MidiTrack()
    mid.tracks.append(track)
    track.append(mido.MetaMessage("set_tempo", tempo=500000, time=0))
    for i in range(5):
        track.append(mido.Message("note_on", note=60 + i, velocity=80, time=0 if i == 0 else 240))
        track.append(mido.Message("note_off", note=60 + i, velocity=0, time=240))
    mid.save(path)


def _write_batch_yaml(path, midi_path, out_dir):
    path.write_text(
        "defaults:\n"
        f"  out_dir: {out_dir}\n"
        "songs:\n"
        "  - song_id: testsong\n"
        f"    midi: {midi_path}\n"
        "    title: Test\n"
        "    composer: Tester\n"
        "    bpm: 120\n"
        "    duration_ms: 5000\n"
        "    difficulties:\n"
        "      EASY: { target_nps: 2.0 }\n",
        encoding="utf-8",
    )


def test_batch_under_personal_dir_routes_chart_to_personal_subdir(tmp_path):
    personal_dir = tmp_path / "personal"
    personal_dir.mkdir()
    out_dir = tmp_path / "out"
    out_dir.mkdir()
    midi = tmp_path / "tiny.mid"
    _make_tiny_midi(midi)

    batch = personal_dir / "batch_test.yaml"
    _write_batch_yaml(batch, midi, out_dir)

    r = subprocess.run(
        [sys.executable, str(TOOL), "--batch", str(batch)],
        capture_output=True, text=True,
    )
    assert r.returncode == 0, r.stderr

    expected = out_dir / "personal" / "testsong.kfchart"
    assert expected.exists(), (
        f"chart not at expected path {expected}; "
        f"actual files: {sorted(out_dir.rglob('*.kfchart'))}"
    )
    data = json.loads(expected.read_text(encoding="utf-8"))
    assert data["songId"] == "testsong"


def test_batch_outside_personal_dir_routes_to_public_dir(tmp_path):
    out_dir = tmp_path / "out"
    out_dir.mkdir()
    midi = tmp_path / "tiny.mid"
    _make_tiny_midi(midi)

    batch = tmp_path / "batch_public.yaml"   # no `personal` segment in path
    _write_batch_yaml(batch, midi, out_dir)

    r = subprocess.run(
        [sys.executable, str(TOOL), "--batch", str(batch)],
        capture_output=True, text=True,
    )
    assert r.returncode == 0, r.stderr

    expected = out_dir / "testsong.kfchart"
    assert expected.exists()
    assert not (out_dir / "personal").exists(), \
        "public batch must not create personal/ subdir"
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
cd tools/midi_to_kfchart && source .venv/bin/activate && \
  pytest tests/test_personal_routing.py -v
```

Expected: `test_batch_under_personal_dir_routes_chart_to_personal_subdir` FAILS — chart appears at `out/testsong.kfchart` instead of `out/personal/testsong.kfchart`. The `outside_personal` test passes (current behavior is "no personal routing").

- [ ] **Step 3: Add path-based routing to `_batch`**

Edit `tools/midi_to_kfchart/midi_to_kfchart.py`. Replace the start of `_batch` (lines 41-46 — the `def _batch`, yaml load, and `out_dir = Path(...)` lines) with:

```python
def _batch(args) -> int:
    import yaml  # lazy; requirements add pyyaml in Task 10
    cfg = yaml.safe_load(Path(args.batch).read_text(encoding="utf-8"))
    defaults = cfg.get("defaults", {}) or {}
    out_dir = Path(defaults.get("out_dir", "."))

    # Path-based personal routing: any batch file located under a `personal`
    # directory segment routes outputs into a `personal/` subdir of out_dir.
    # Convention encodes the public/private boundary in the filesystem so
    # nobody has to remember a per-song flag.
    if "personal" in Path(args.batch).resolve().parts:
        out_dir = out_dir / "personal"
```

Leave the rest of `_batch` (the `for song in cfg.get("songs", []):` loop and below) unchanged.

- [ ] **Step 4: Re-run the tests to verify they pass**

```bash
cd tools/midi_to_kfchart && source .venv/bin/activate && \
  pytest tests/test_personal_routing.py -v
```

Expected: both tests PASS.

- [ ] **Step 5: Run the full pytest suite to verify nothing else broke**

```bash
cd tools/midi_to_kfchart && source .venv/bin/activate && pytest -q
```

Expected: previous 49 tests + 2 new = 51 PASS, 0 FAIL.

- [ ] **Step 6: Commit**

```bash
git add tools/midi_to_kfchart/midi_to_kfchart.py \
        tools/midi_to_kfchart/tests/test_personal_routing.py
git commit -m "$(cat <<'EOF'
feat(pipeline): route personal-batch outputs to charts/personal/ via path inference

When the batch yaml's resolved path contains a 'personal' segment, prepend
'personal/' to out_dir. Convention encodes public/private boundary in the
filesystem so no per-song YAML flag is needed.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 7: Recreate local `catalog.personal.kfmanifest` and smoke-test

This file is gitignored. The developer (you) re-creates it locally so Huntrx remains playable.

**Files:**
- Create: `Assets/StreamingAssets/catalog.personal.kfmanifest` (LOCAL ONLY — verified gitignored)

- [ ] **Step 1: Write the local personal manifest**

Create `Assets/StreamingAssets/catalog.personal.kfmanifest`:

```json
{
  "version": 1,
  "songs": [
    { "id": "huntrx_golden", "title": "Golden", "composer": "Huntr/x",
      "thumbnail": "thumbs/personal/huntrx_golden.png",
      "difficulties": ["Easy", "Normal"], "chartAvailable": true, "durationMs": 150000 }
  ]
}
```

- [ ] **Step 2: Verify the file is gitignored**

```bash
git check-ignore -v Assets/StreamingAssets/catalog.personal.kfmanifest
git status --short | grep catalog.personal && echo "LEAK!" || echo "OK: ignored"
```

Expected: `check-ignore` matches the `.gitignore` rule; `git status` shows `OK: ignored`.

- [ ] **Step 3: Run all EditMode tests one more time**

```bash
"<UnityEditor>" -batchmode -nographics -projectPath . \
  -runTests -testPlatform EditMode -testResults Builds/test-results.xml
grep -E '<test-run.*total=' Builds/test-results.xml | head -1
grep -E 'result="Failed"' Builds/test-results.xml || echo "OK: no failures"
```

Expected: total ≥ 200 (196 baseline + 4 overlay + 2 path = ≥ 202), 0 failures.

- [ ] **Step 4: Build the W4 scene and load the gameplay scene in Editor (manual)**

Run from Unity Editor: `KeyFlow > Build W4 Scene`.

Then in Unity Editor Play mode (or manual device test if more convenient):
- Verify the song select screen shows 5 songs (4 classical + Huntrx Golden).
- Tap Huntrx Golden EASY; verify chart loads and gameplay starts.
- Tap one of the classical songs; verify it still loads (regression check).

If Editor play-mode is unavailable, defer to a device APK build at the end of Task 9.

---

## Task 8: Document the convention in `CLAUDE.md`

**Files:**
- Modify: `CLAUDE.md`

- [ ] **Step 1: Add a "Personal songs (copyrighted material)" section**

Edit `CLAUDE.md`. After the existing `## Python charting pipeline` section, add a new section:

```markdown
## Personal songs (copyrighted material)

The repo distinguishes **public** songs (public-domain or licensed for distribution; safe to commit) from **personal** songs (copyrighted material kept on local machines only). The split is enforced by **directory convention**:

| Public | Personal (gitignored) |
|---|---|
| `midi/` (any non-`personal/` subdir) | `midi/personal/` |
| `Assets/StreamingAssets/charts/*.kfchart` | `Assets/StreamingAssets/charts/personal/*.kfchart` |
| `Assets/StreamingAssets/thumbs/*.png` | `Assets/StreamingAssets/thumbs/personal/*.png` |
| `Assets/StreamingAssets/catalog.kfmanifest` | `Assets/StreamingAssets/catalog.personal.kfmanifest` |
| `tools/midi_to_kfchart/batch_*.yaml` | `tools/midi_to_kfchart/personal/batch_*.yaml` |

**Adding a personal song:**

1. Drop the source MIDI in `midi/personal/`.
2. Add an entry to a batch yaml under `tools/midi_to_kfchart/personal/` (the location triggers personal routing — no YAML flag needed).
3. `python midi_to_kfchart.py --batch tools/midi_to_kfchart/personal/<file>.yaml` — output goes to `charts/personal/`.
4. Drop a thumbnail PNG in `thumbs/personal/`.
5. Add a song entry to `Assets/StreamingAssets/catalog.personal.kfmanifest` (create the file if absent — `version: 1` plus `songs: []` template). Set `"thumbnail": "thumbs/personal/<file>.png"`.

That's it. `git status` will show no new tracked files. The five `.gitignore` directory rules cover everything.

**At runtime:** `SongCatalog.LoadAsync` loads `catalog.kfmanifest` (required) and merges `catalog.personal.kfmanifest` (optional, missing → no overlay). Personal entries are tagged `isPersonal=true` and `ChartLoader` resolves their charts to `charts/personal/<id>.kfchart`.
```

- [ ] **Step 2: Commit**

```bash
git add CLAUDE.md
git commit -m "$(cat <<'EOF'
docs(personal-overlay): document the personal/ subdir convention in CLAUDE.md

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 9: Final verification

- [ ] **Step 1: Run all EditMode tests**

```bash
"<UnityEditor>" -batchmode -nographics -projectPath . \
  -runTests -testPlatform EditMode -testResults Builds/test-results.xml
grep -E '<test-run' Builds/test-results.xml | head -1
grep -E 'result="Failed"' Builds/test-results.xml || echo "OK: no failures"
```

Expected: total ≥ 202, 0 failures.

- [ ] **Step 2: Run all pytest tests**

```bash
cd tools/midi_to_kfchart && source .venv/bin/activate && pytest -q
```

Expected: 51 PASS, 0 FAIL.

- [ ] **Step 3: Verify git status is clean of personal artifacts**

```bash
git status --short | grep -i huntrx && echo "LEAK!" || echo "OK"
git status --short | grep "catalog.personal" && echo "LEAK!" || echo "OK"
```

Expected: both `OK`. The only tracked changes from this branch should be the 8 commits from Tasks 1, 2, 3, 4, 5, 6, 8 (Task 7 produces no commit — it's a local-file step).

- [ ] **Step 4: Verify directory ignore rules with check-ignore**

```bash
git check-ignore -v \
  "midi/personal/KPop Demon Hunters - Golden.mid" \
  Assets/StreamingAssets/charts/personal/huntrx_golden.kfchart \
  Assets/StreamingAssets/charts/personal/huntrx_golden.kfchart.meta \
  Assets/StreamingAssets/thumbs/personal/huntrx_golden.png \
  Assets/StreamingAssets/thumbs/personal/huntrx_golden.png.meta \
  tools/midi_to_kfchart/personal/batch_personal.yaml \
  Assets/StreamingAssets/catalog.personal.kfmanifest
```

Expected: every input line produces a match against one of the 5 `.gitignore` rules.

- [ ] **Step 5: Build APK and confirm Huntrx is bundled (local) but its assets are not in git**

```bash
"<UnityEditor>" -batchmode -nographics -projectPath . \
  -executeMethod KeyFlow.Editor.SceneBuilder.Build -quit
"<UnityEditor>" -batchmode -nographics -projectPath . \
  -executeMethod KeyFlow.Editor.ApkBuilder.Build -quit
unzip -l Builds/keyflow-w*.apk | grep -i 'huntrx_golden' | head
```

Expected (on developer machine with the local `catalog.personal.kfmanifest`): APK contains `assets/charts/personal/huntrx_golden.kfchart` and `assets/thumbs/personal/huntrx_golden.png`. (Adjust the apk filename glob if SP version has bumped.)

```bash
git ls-files | grep -i huntrx && echo "LEAK!" || echo "OK: no huntrx in git"
git ls-files Assets/StreamingAssets/charts/personal/ | grep -v ".gitkeep" && echo "LEAK!" || echo "OK"
```

Expected: both `OK`.

- [ ] **Step 6: Confirm public-only build path is intact**

Temporarily move the personal manifest aside:

```bash
mv Assets/StreamingAssets/catalog.personal.kfmanifest /tmp/
```

Run the Unity scene/play test (or batch test runner). Expected: 4-song catalog only, all 196+ EditMode tests still green, no errors. Then restore:

```bash
mv /tmp/catalog.personal.kfmanifest Assets/StreamingAssets/
```

(This also serves as a CI-equivalent dry run, since CI machines won't have `catalog.personal.kfmanifest`.)

---

## Self-Review Checklist (run after writing, fix inline)

- [x] Spec coverage: all 10 PSO-T items in §2.1 of the spec are mapped to tasks. PSO-T6 (no thumbnail code change) is addressed by Task 7 step 1 (manifest stores `thumbs/personal/...` path). PSO-T10 (CLAUDE.md) is Task 8.
- [x] No placeholders. All steps include exact commands or full code blocks.
- [x] Type/signature consistency: `MergeOverlay(SongEntry[], SongEntry[])` used the same way in tests (Task 3) and `LoadAsync` (Task 4). `ResolveChartPath(string, bool)` used the same way in tests (Task 5 step 1) and the implementation (Task 5 step 3) and call (Task 5 step 4 indirectly via `LoadFromStreamingAssetsCo`).
- [x] All 7 modified files from File Structure appear in tasks (`.gitignore` Task 1; `SongEntry` Task 2; `SongCatalog` Tasks 3+4; `ChartLoader` Task 5; `GameplayController` Task 5; `midi_to_kfchart.py` Task 6; `CLAUDE.md` Task 8).
