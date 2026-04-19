# W1 PoC Measurement Report — 2026-04-20

## Test Setup
- **Unity**: 6000.3.13f1 (Unity 6.3 LTS), IL2CPP, arm64-v8a
- **Audio config**: DSP Buffer 256 (Best Latency), Sample Rate 48000 Hz, activeInputHandler = Input System Package (new)
- **Audio path**: Unity AudioSource pool (16 channels), PlayOneShot
- **Sample**: `piano_c4.wav` = Salamander Grand Piano V3 C4 v10, 44.1 kHz 16-bit WAV, Decompress On Load + Preload

## Devices Tested

| # | Device | Android | CPU | Notes |
|---|---|---|---|---|
| 1 | Galaxy S22 (SM-S901N) | 16 | arm64-v8a | Flagship, Samsung Exynos 2200 |

⚠️ **W1 PoC measured on 1 flagship device only.** Per spec §12 criterion #1, full go/no-go requires 3 devices (high/mid/low). Low-end devices will likely show worse latency.

## Tap-to-Audio Latency (external Audacity recording)

Method: PC microphone recording of both fingernail tap transient and speaker output. Sample-level offset measured in Audacity.

| Pair | Tap time (s) | Piano start (s) | Latency (ms) |
|---|---|---|---|
| 1 | 4.53 | 4.65 | ~120 |
| 2 | 6.53 | 6.64 | ~110 |
| **Estimated median** | — | — | **~110 ms** |

Piano onset identification by waveform inspection (not spectrogram — sub-threshold imprecision likely ±10ms).

## FPS Stability (HUD readout during ~30s tap session)

| Metric | Value | Notes |
|---|---|---|
| Average FPS | 59.8 | Target 60, target achieved |
| Min FPS | not separately logged | No visible drops |
| dspTime drift (30s) | reported as "100 이하", exact value not captured | Should stay <10ms; revisit if growing |
| Frame latency (HUD) | not logged precisely | Expected ~16ms (one frame), not a real latency |

## Subjective Feel

User reported **"탭과 소리가 맞음"** (tap and sound matched) during 30s playtest. This contradicts the objective ~110ms measurement, which is at the threshold where humans typically start noticing mismatch.

Interpretation:
- User may have adapted within 30s of play
- Casual play tolerates higher latency than rhythm-game veteran play
- MVP's acceptance criterion is "한번 더 하고 싶다" from 5-10 friends — at 110ms, likely acceptable for casual users but may fail for experienced rhythm gamers

## Go/No-Go Decision

Per spec §0 risk #1 and plan Task 14.6:

- **< 80ms**: ✅ GO with AudioSource pool
- **80~120ms**: ⚠️ **MARGINAL. Swap to Native Audio plugin, re-measure.** ← current
- **> 120ms**: ❌ Pivot to Native Kotlin + Oboe (+4 weeks)

**Decision: MARGINAL.** Recommended path forward:

1. **Short-term (this week)**: Evaluate Native Audio plugin ($35, Oboe-backed). Expected improvement: -30 to -50 ms on same hardware → target 60~80 ms range.
2. **Before W2**: Re-measure with Native Audio on same Galaxy S22. If ≤80ms, proceed to W2 with Native Audio as the default tap SFX engine.
3. **Before public MVP**: Measure on mid-tier and low-tier Android devices per spec §12 criterion #1.

Fallback: if Native Audio fails to improve the number or user finds $35 not justifiable, proceed to W2 with current AudioSource pool and accept the MVP audience may skew "casual."

## Rationale

Two independent measurements on flagship hardware both showed ~110ms. This is consistent and unlikely to be measurement noise. A flagship device showing MARGINAL is a red flag for lower-tier devices — the Unity AudioSource pipeline on Android is the likely bottleneck (input polling → OpenSL fallback → DAC chain). Native Audio's Oboe bridge typically shaves 30~50 ms.

Unity AudioSource at 110ms on flagship is within Android's typical OpenSL latency; Oboe-based Native Audio typically achieves 50~70 ms on the same class of hardware.

## Next Step

Pause W1 completion claim. Proceed to:
- Purchase/install Native Audio plugin ($35, exceed7.com)
- Swap `AudioSamplePool.PlayOneShot` internals to call Native Audio's API
- Re-record and re-measure (should take ~1 day including swap + measurement)
- If improvement confirmed (<80ms), sign off W1 and begin W2 gameplay core plan.
