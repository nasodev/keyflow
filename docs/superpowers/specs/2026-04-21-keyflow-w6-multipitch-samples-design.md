# KeyFlow W6 (Sub-Project 1/2): 다중 피치 피아노 샘플 설계 문서

> 상태: 초안 (2026-04-21)
> 상위 전제: v2 스펙(`2026-04-20-keyflow-mvp-v2-4lane-design.md`) §6.2, §9·§10
> W5 완료 리포트: `docs/superpowers/reports/2026-04-21-w5-completion.md`
> 이 스펙의 범위: **W6 우선순위 1만** — 우선순위 2(4곡 차트)는 별도 스펙에서 다룸
> 개발자 프로필: 풀스택 웹 개발자, Unity/게임 첫 프로젝트

## 0. 한 줄 요약

W5 실기기 플레이테스트의 단일 피드백("타격음이 한 종류라 게임하는 느낌이 안 든다")을 해소한다. Salamander Grand Piano V3(CC-BY 3.0)의 **3반음 간격 17개 샘플**을 번들링하고, 런타임 `AudioSource.pitch`로 ±1반음 보간하여 MIDI 36–83 전체를 커버한다. 탭 시 현재 레인의 가장 가까운 pending 노트 피치를 재생하고, 노트가 윈도우 내 없으면 레인별 기본 피치로 fallback한다. 기존 레이턴시 경로(`TapInputHandler` → `samplePool` 동기 재생) 유지.

---

## 1. 스코프

### 1.1 포함

| ID | 항목 | 산출물 |
|---|---|---|
| W6S1-T1 | Salamander V3 샘플 17개 임포트 | `Assets/Audio/piano/C2v10.wav` ~ `C6v10.wav`, 각 `.meta` import 설정 |
| W6S1-T2 | `ChartNote.pitch`를 `NoteController`까지 쓰레딩 | `NoteController.Pitch` 프로퍼티, `NoteSpawner.SpawnNote` 전달 |
| W6S1-T3 | `AudioSamplePool.PlayForPitch(int midi)` API | pitch map(SerializedField array) + nearest-sample 해상 + `AudioSource.pitch` ratio 계산 |
| W6S1-T4 | `JudgmentSystem.GetClosestPendingPitch(lane, tapTimeMs, windowMs)` API | pending list 재사용, 윈도우 밖이면 -1 |
| W6S1-T5 | `TapInputHandler.FirePress/FirePressRaw` → pitch-aware 재생 | judgmentSystem 쿼리 → PlayForPitch; 없으면 레인 기본 피치 |
| W6S1-T6 | 레인 기본 피치 상수 `LanePitches` | `{48, 55, 60, 67}` (C3/G3/C4/G4) |
| W6S1-T7 | 라이선스 처리 | `Assets/StreamingAssets/licenses/salamander-piano-v3.txt`(원문 번들) + `SettingsScreen` Credits 한 줄 + Spec §11.2 `licenseInfo` 정정 |
| W6S1-T8 | EditMode 테스트 | `AudioSamplePoolTests` 확장 + `JudgmentSystemTests` 신규 쿼리 케이스 |
| W6S1-T9 | 실기기 검증 | Galaxy S22: Für Elise EASY/NORMAL 완주 + 청각 확인 + 회귀 없음 |

### 1.2 제외 (다른 트랙 또는 이월)

- **4곡 차트 콘텐츠** — W6 우선순위 2, 별도 sub-project 스펙.
- **캘리브레이션 전용 클릭 샘플** — W6 우선순위 4 (carry-over #2). 본 스펙은 `Assets/Audio/piano_c4.wav`를 `CalibrationController.clickSample`로 유지.
- **16 velocity layer 중 dynamic 선택 (velocity-sensitive playback)** — v10(mezzo-forte) 고정. 탭 세기 감지는 MVP 비목표.
- **스테레오 유지, 8-band EQ, 리버브 등 사운드 디자인** — Force Mono + 원본 그대로.
- **48 semitone 전량 사전 확장 (Option β)** — 런타임 pitch-shift로 대체. 필요 시 후속 이터레이션으로 전환 용이.
- **Credits 전용 화면** — `SettingsScreen` 하단에 한 줄 통합.

### 1.3 가드레일

- **APK < 40 MB 유지** (현재 33.15 MB → 예상 +1.5 MB ≈ 34.7 MB).
- **탭→소리 레이턴시 회귀 금지**: `TapInputHandler.FirePress`의 `PlayOneShot` 호출 지점 위치 유지. 새 쿼리(`GetClosestPendingPitch`)는 동일 프레임 내 동기 호출, 비동기 없음.
- **기존 EASY/NORMAL 회귀 금지**: Für Elise EASY 정상 플레이가 유지되어야 통과.
- **v2 스펙 §2 기능 목록, §5 수치, §7 .kfchart 포맷 불변**. `ChartNote.pitch`는 이미 존재하며 여기서 "처음 사용"만 한다.

---

## 2. 사전 상태 (W5 말 기준)

| 컴포넌트 | 상태 | W6S1과의 관계 |
|---|---|---|
| `Assets/Audio/piano_c4.wav` | 단일 샘플, 44.1kHz 예상 | 유지 (캘리브레이션 전용) |
| `AudioSamplePool` | 16 AudioSource round-robin, `defaultClip` 1개 | T3에서 `PlayForPitch` 추가, 기존 `PlayOneShot` API 유지 |
| `TapInputHandler.FirePress/FirePressRaw` | `samplePool.PlayOneShot()` 호출, pitch 정보 없음 | T5에서 pitch-aware로 교체 |
| `NoteController` | `lane/hitMs/type/dur` 수용, pitch 필드 없음 | T2에서 `int pitch` 추가 |
| `NoteSpawner.SpawnNote(ChartNote n)` | `n.pitch`를 읽지 않음 | T2에서 `n.pitch` 전달 |
| `JudgmentSystem.pending` | private `List<NoteController>`, `HandleTap`에서 closest 스캔 | T4에서 pitch 쿼리 재사용 |
| `ChartNote.pitch` | 36–83 clamp 완료 (`ChartLoader` const) | 런타임에서 처음 사용됨 |
| `CalibrationController` | 자체 `AudioSource[] clickSources` + `clickSample` (AudioSamplePool 미사용) | 변경 없음 |
| `LatencyMeter` | `samplePool` 참조만 있고 호출 없음 | 변경 없음 |
| EditMode 테스트 | 93 / 93 pass | T8에서 +5 ≈ 98 |

---

## 3. 자산 레이어

### 3.1 샘플 소스

- **위치 (로컬 개발 머신)**: `C:\dev\music\piano-v3\44.1khz16bit\*.wav` (이미 사용자 머신에 있음)
- **원본 사양**: 44.1 kHz / 16-bit WAV, 스테레오, 각 키 16 velocity layer
- **라이선스**: CC-BY 3.0 (Alexander Holm, Salamander Grand Piano V3) — 원문 `C:\dev\music\piano-v3\README` 복사본을 프로젝트에 번들

### 3.2 선별 기준

- Velocity: **v10** (16단계 중 mezzo-forte에 해당, 밝고 중간 세기) 고정.
- 피치: 3반음 간격으로 MIDI 36–83 + 상향 여유 1개 = **17키**.

```
MIDI  Note   File
 36   C2     C2v10.wav
 39   D#2    Ds2v10.wav
 42   F#2    Fs2v10.wav
 45   A2     A2v10.wav
 48   C3     C3v10.wav
 51   D#3    Ds3v10.wav
 54   F#3    Fs3v10.wav
 57   A3     A3v10.wav
 60   C4     C4v10.wav
 63   D#4    Ds4v10.wav
 66   F#4    Fs4v10.wav
 69   A4     A4v10.wav
 72   C5     C5v10.wav
 75   D#5    Ds5v10.wav
 78   F#5    Fs5v10.wav
 81   A5     A5v10.wav
 84   C6     C6v10.wav   ← 경계 안전 (MIDI 82,83 = A5 + 1, C6 - 1)
```

커버리지 증명: 각 샘플 `p`는 MIDI `p-1, p, p+1`을 ±1반음으로 커버. 17개 샘플 × 3 = 51 semitone coverage, MIDI 35–85를 포함 → MIDI 36–83 전부 ±1반음 이내.

### 3.3 프로젝트 내 위치

```
Assets/
├── Audio/
│   ├── piano_c4.wav              # 기존, 캘리브레이션 전용 유지
│   └── piano/
│       ├── C2v10.wav
│       ├── Ds2v10.wav
│       ├── ... (17개)
│       └── C6v10.wav
└── StreamingAssets/
    └── licenses/
        └── salamander-piano-v3.txt   # CC-BY 3.0 원문 (README 복사)
```

Salamander 파일명은 `Ds`, `Fs` 접두 사용 (`#`는 Unity asset 이름에 부적합). 원본 머신에서 복사 시 리네임.

### 3.4 Unity AudioImporter 설정 (17개 전 파일 공통)

```
Force To Mono:          true
Normalize:              false
Load In Background:     false
Ambisonic:              false
Preload Audio Data:     true

[Default (Standalone) / Android 동일]
Load Type:              Decompress On Load
Compression Format:     Vorbis
Quality:                60  (슬라이더 기준, OGG Vorbis ~q3)
Sample Rate Setting:    Override Sample Rate
Sample Rate:            48000
```

근거:
- **Force To Mono**: 스테레오 AB 마이크는 탭 SFX에 불필요. 디스크/RAM 절반.
- **Load Type = Decompress On Load**: 재생 시 디코드 비용 0. Tap 레이턴시에 필수. 17 × ~500KB RAM ≈ 8.5 MB — Galaxy S22 여유 충분.
- **Compression Format = Vorbis, Q60**: Q60이면 17 × ~80KB ≈ 1.4 MB APK 증가 추정. WAV 직번들(17 × ~500KB = 8.5 MB)보다 유리.
- **Override Sample Rate = 48000**: Unity DSP 기본 48kHz와 일치. 44.1→48 런타임 리샘플 오버헤드 제거.

### 3.5 APK 예상 증가량

- 샘플 17개 Vorbis Q60: ~1.4 MB
- License 텍스트: < 5 KB
- 합계 +1.5 MB 내외. 현재 33.15 MB → 약 34.7 MB. 가드레일(40 MB) 내.

---

## 4. 런타임 API

### 4.1 `AudioSamplePool` 확장

```csharp
// Assets/Scripts/Gameplay/AudioSamplePool.cs
public class AudioSamplePool : MonoBehaviour
{
    [SerializeField] private int channels = 16;
    [SerializeField] private AudioClip defaultClip;         // 기존

    [Header("Pitch Sample Map")]
    [SerializeField] private AudioClip[] pitchSamples;      // 길이 17 고정, index 0 = baseMidi
    [SerializeField] private int baseMidi = 36;             // pitchSamples[0]의 MIDI 피치
    [SerializeField] private int stepSemitones = 3;         // 샘플 간 간격

    private AudioSource[] sources;
    private int nextIndex;

    public int Count => sources?.Length ?? 0;

    public void Initialize(int channelCount) { ... }        // 기존 유지
    public void InitializeForTest(int channels) => Initialize(channels);  // 기존 유지
    public AudioSource NextSource() { ... }                 // 기존 유지
    public void PlayOneShot(AudioClip clip = null) { ... }  // 기존 유지 (레거시 호출자)

    // 신규:
    public void PlayForPitch(int midiPitch)
    {
        var (clip, ratio) = ResolveSample(midiPitch);
        if (clip == null) { PlayOneShot(); return; }        // 방어: 맵 미설정 시 기존 경로
        var src = NextSource();
        src.pitch = ratio;
        src.PlayOneShot(clip);
    }

    public (AudioClip clip, float pitchRatio) ResolveSample(int midiPitch)
    {
        if (pitchSamples == null || pitchSamples.Length == 0) return (null, 1f);

        // midiPitch를 baseMidi ~ baseMidi + (len-1)*step 범위로 clamp
        int hi = baseMidi + (pitchSamples.Length - 1) * stepSemitones;
        int p = System.Math.Clamp(midiPitch, baseMidi, hi);

        int baseIdx = (p - baseMidi) / stepSemitones;
        int sampleMidi = baseMidi + baseIdx * stepSemitones;
        int offset = p - sampleMidi;  // 0, 1, 2

        if (offset == 2 && baseIdx + 1 < pitchSamples.Length)
        {
            baseIdx += 1;
            sampleMidi = baseMidi + baseIdx * stepSemitones;
            offset = -1;
        }

        float ratio = Mathf.Pow(2f, offset / 12f);
        return (pitchSamples[baseIdx], ratio);
    }
}
```

설계 포인트:
- `pitchSamples` 길이/간격이 비-Salamander 셋으로 바뀌어도 baseMidi/stepSemitones만 바꾸면 동작. 특정 값 하드코딩 없음.
- `ResolveSample`은 `public`으로 노출 — EditMode 테스트가 순수 함수로 검증 가능 (Unity AudioSource 없이).
- out-of-range: `Math.Clamp`로 가장 가까운 쪽으로 흡수. 런타임에는 도달 불가능(파이프라인이 36–83 clamp)이나 방어적 안전망.
- `PlayForPitch`가 미-초기화(`pitchSamples` 빈 배열)일 때 `PlayOneShot()`으로 fallback — 테스트 씬/개발 중 임시 실행을 깨지지 않게.

### 4.2 `NoteController` + `NoteSpawner` 업데이트

```csharp
// NoteController.cs
private int pitch;
public int Pitch => pitch;

public void Initialize(
    AudioSyncManager sync,
    int lane, float laneX,
    int hitMs,
    int pitch,                    // 신규
    NoteType type,
    int durMs,
    float spawnY, float judgmentY,
    int previewMs,
    int missGraceMs,
    System.Action<NoteController> onAutoMiss)
{
    this.pitch = pitch;
    // ... 나머지 기존 로직 동일
}
```

```csharp
// NoteSpawner.cs SpawnNote
ctrl.Initialize(
    audioSync, n.lane, laneX,
    n.t,
    n.pitch,                      // 신규
    n.type,
    n.dur,
    spawnY, judgmentY,
    previewMs,
    missMs,
    onAutoMiss: missed => judgmentSystem.HandleAutoMiss(missed));
```

변경 지점은 이 두 곳뿐. 기존 호출자(테스트 포함)는 Initialize 시그니처 변경으로 인해 컴파일 에러 발생 → 테스트에서도 일괄 수정 필요.

### 4.3 `JudgmentSystem` 쿼리 API

```csharp
// JudgmentSystem.cs
public int GetClosestPendingPitch(int lane, int tapTimeMs, int windowMs)
{
    NoteController closest = null;
    int closestAbsDelta = int.MaxValue;
    for (int i = 0; i < pending.Count; i++)
    {
        var n = pending[i];
        if (n == null || n.Judged) continue;
        if (n.Lane != lane) continue;
        int delta = tapTimeMs - n.HitTimeMs;
        int abs = delta < 0 ? -delta : delta;
        if (abs < closestAbsDelta)
        {
            closestAbsDelta = abs;
            closest = n;
        }
    }
    if (closest == null || closestAbsDelta > windowMs) return -1;
    return closest.Pitch;
}
```

- `HandleTap`의 알고리즘과 동일한 스캔을 하지만 별도 메서드. 두 메서드를 공통 헬퍼로 묶지 않는 이유: `HandleTap`은 무한 윈도우(가장 가까운 것을 무조건 평가), 쿼리는 `windowMs` 필터. 공통화 시 분기 추가가 오히려 번잡.
- **레이턴시 영향**: pending 리스트는 전형적으로 수십 개 이하(미리 spawn된 라이브 노트). 선형 스캔 O(n) 한 번 — 프레임당 수 μs 수준. 현재 아키텍처 유지.

### 4.4 `TapInputHandler` 변경

```csharp
// TapInputHandler.cs
[SerializeField] private JudgmentSystem judgmentSystem;  // 신규 참조
[SerializeField] private int pitchLookupWindowMs = 500;

private void FirePress(int touchId, int lane, int songTimeMs)
{
    touchToLane[touchId] = lane;
    pressedLanes.Add(lane);
    PlayTapSound(lane, songTimeMs);            // 신규 헬퍼
    OnTap?.Invoke(songTimeMs);
    OnLaneTap?.Invoke(songTimeMs, lane);
}

private void FirePressRaw(int lane, int songTimeMs)
{
    pressedLanes.Add(lane);
    PlayTapSound(lane, songTimeMs);
    OnTap?.Invoke(songTimeMs);
    OnLaneTap?.Invoke(songTimeMs, lane);
}

private void PlayTapSound(int lane, int songTimeMs)
{
    int pitch = judgmentSystem != null
        ? judgmentSystem.GetClosestPendingPitch(lane, songTimeMs, pitchLookupWindowMs)
        : -1;
    if (pitch < 0) pitch = LanePitches.Default(lane);
    samplePool.PlayForPitch(pitch);
}
```

- `samplePool.PlayOneShot()` → `samplePool.PlayForPitch(pitch)`로 교체.
- judgmentSystem null 가능성: 테스트/편집 씬 상황. null이면 바로 레인 기본 피치.
- 윈도우 500ms: 판정 윈도우(NORMAL GOOD 60ms, POOR 120ms)보다 현저히 넓어 "내가 치려는 노트"를 대략 잡기에 충분. 값은 `[SerializeField]`로 인스펙터 노출.

### 4.5 `LanePitches` 상수

```csharp
// Assets/Scripts/Gameplay/LanePitches.cs (신규 파일)
namespace KeyFlow
{
    public static class LanePitches
    {
        // Perfect 5th 계단: 장조/단조 어느 조성과도 충돌 적음.
        // 0=C3(48), 1=G3(55), 2=C4(60), 3=G4(67)
        private static readonly int[] defaults = { 48, 55, 60, 67 };

        public static int Default(int lane)
        {
            if (lane < 0 || lane >= defaults.Length) return 60; // C4 fallback
            return defaults[lane];
        }
    }
}
```

레인 수 4는 v2 스펙 §5에 고정. 하드코딩 허용.

---

## 5. 라이선스 처리

### 5.1 원문 번들

`Assets/StreamingAssets/licenses/salamander-piano-v3.txt`에 원본 README(`C:\dev\music\piano-v3\README`) 전체 복사. StreamingAssets이므로 APK에 그대로 포함.

### 5.2 Credits UI (SettingsScreen)

`SettingsScreen` 오버레이 하단에 한 줄 추가. 기존 `UIStrings`에 신규 키:

```
UIStrings.CreditsSamples = "Piano samples: Salamander Grand Piano V3 by Alexander Holm, CC-BY 3.0"
```

전용 버튼/화면 없음. 텍스트만 추가.

### 5.3 Spec 정정

`docs/superpowers/specs/2026-04-19-keyflow-mvp-design.md` §11.2의 `licenseInfo` 예시 문자열:

```
"licenseInfo": "PD-composition; self-sequenced; CC0-samples(Salamander)"
```

→

```
"licenseInfo": "PD-composition; self-sequenced; CC-BY-samples(Salamander V3, Alexander Holm)"
```

(이 정정은 W6S1 구현과 함께 같은 PR에서 수행.)

---

## 6. 테스트 전략

### 6.1 EditMode 신규 (5)

`Assets/Tests/EditMode/AudioSamplePoolTests.cs` 확장:

1. `ResolveSample_ExactSamplePitch_ReturnsRatioOne`
   - midi 36 → (clip[0], 1.0); midi 48 → (clip[4], 1.0); midi 84 → (clip[16], 1.0).
2. `ResolveSample_PlusOneSemitone_ReturnsRatioUp`
   - midi 37 → (clip[0], 2^(1/12) ≈ 1.0595); midi 49 → (clip[4], ≈1.0595).
3. `ResolveSample_MinusOneSemitone_PicksNextSampleDown`
   - midi 38 → (clip[1], 2^(-1/12) ≈ 0.9439) [38 = C2+2 = Ds2-1].
4. `ResolveSample_OutOfRangeLow_ClampsToFirst`
   - midi 20 → (clip[0], 1.0); midi -5 → (clip[0], 1.0).
5. `ResolveSample_OutOfRangeHigh_ClampsToLast`
   - midi 100 → (clip[16], 1.0).

구현 노트: 테스트는 실제 WAV를 로드하지 않음. 길이 17의 fake `AudioClip[]`(각 요소는 `AudioClip.Create`로 1-sample dummy)만 주입해서 인덱스/비율만 검증.

`Assets/Tests/EditMode/` (신규 또는 기존 JudgmentSystem 테스트 확장):

6. `JudgmentSystem_GetClosestPendingPitch_InWindow_ReturnsPitch`
7. `JudgmentSystem_GetClosestPendingPitch_OutOfWindow_ReturnsMinusOne`
8. `JudgmentSystem_GetClosestPendingPitch_WrongLane_ReturnsMinusOne`

(현재 `JudgmentSystemTests.cs` 존재 여부 확인 후, 있으면 확장; 없으면 신규 파일.)

### 6.2 PlayMode

없음. 실제 오디오 재생·레이턴시는 device에서만 유의미.

### 6.3 Device (Galaxy S22, R5CT21A31QB)

- [ ] 빌드 + `adb install -r Builds/keyflow-w6.apk` 성공
- [ ] Für Elise EASY 완주, 기존 청각과 차이 확인
- [ ] Für Elise NORMAL 완주, 멜로디가 탭과 함께 들림 (원곡 라인 인지 가능)
- [ ] 오타(빈 레인 탭) 시 레인 기본 피치 들림, 어색하지 않음
- [ ] 탭→소리 레이턴시 체감 W5와 동등 또는 개선 (W1 PoC 50~80ms 예산 내)
- [ ] ANR 없음, 설치/초기 로드 시간 체감 동등
- [ ] APK 크기 ≤ 36 MB

### 6.4 회귀

- EditMode 전체 ≥ 93 pass 유지 (+5 → 98).
- Python pytest 변경 없음 (파이프라인 미변경).
- 캘리브레이션 플로우 정상 (piano_c4.wav 유지).

---

## 7. 작업 순서 (구현 사이클 힌트)

구체적 태스크 쪼개기는 `writing-plans` 단계에서 다루지만, 의존성 DAG은 명시:

```
T7 (라이선스 텍스트) ─────────────── 독립
T1 (17 WAV 임포트) ──┐
                    ├── T3 (PlayForPitch) ──┐
T6 (LanePitches)  ──┘                        ├── T5 (TapInputHandler 연결) ── T9 (device)
                                             │
T2 (pitch 쓰레딩) ── T4 (JudgmentSystem 쿼리)┘
                                             │
                                             T8 (EditMode 테스트)
```

권장 순서: T7 → T1 → T2 → T6 → T3 → T4 → T5 → T8 → T9.

T2(NoteController 시그니처 변경)가 테스트/호출자를 다수 건드리므로 일찍 처리하고, 이후 T3/T4/T5는 상대적으로 국소.

---

## 8. 리스크 & 대응

| 리스크 | 가능성 | 영향 | 대응 |
|---|---|---|---|
| Galaxy S22에서 `AudioSource.pitch` 변경이 각 재생당 비가청 글리치 유발 | 낮음 | 중 | device 테스트에서 확인. 글리치 시 β안(오프라인 48개 사전 확장)으로 전환. 전환 비용 ≈ 0.5일 (기존 API 유지). |
| Vorbis Q60 음질 저하 체감 | 낮음 | 저 | 필요 시 Q80으로 상향 (APK +0.5MB 예상). 설정값만 변경. |
| Pitch-shift ±1반음의 음색 왜곡 | 중 | 저 | 피아노는 ±2까지 관용, ±1은 청각적 무해. W5 피드백의 초점은 "변화"이지 "절대 음높이 정확도"가 아님. |
| `JudgmentSystem` 참조가 `TapInputHandler`에 새로 추가 → 테스트 세팅 수정 필요 | 확정 | 저 | `judgmentSystem` null-safe 경로가 이미 설계에 포함. 기존 테스트 중 null로 두면 레인 기본 피치로 동작. |
| 17 샘플 파일 리네임 실수 (`Ds`/`Fs`) | 중 | 저 | T1에서 각 파일을 import 후 `AudioClip` 이름 확인. Unity import 실패 시 즉시 감지. |
| APK 가드레일 초과 | 매우 낮음 | 중 | 예상 +1.5 MB가 여유 있음. 초과 시 Vorbis Q를 40으로 낮춤. |
| CC-BY 크레딧 누락 위험 | 확정 (대응 시) | 법적 | T7에서 원문 번들 + Credits UI 두 곳에 표기. 둘 중 하나만으로도 CC-BY 요건 충족이나 이중으로 안전. |

---

## 9. 오픈 퀘스천 (설계 완료 후 추가 결정 필요 없음 — 구현 중 해결 가능)

- **velocity v10 청감 확인**: 16 layer 중 다른 layer(v8, v12)와 비교할지 여부는 T1 직후 device에서 몇 분 들어보고 결정. 교체 비용 = 17 파일 재임포트(10분). 설계 변경 아님.
- **Ds/Fs 이외의 "F#" 표기**: 원본 파일이 `Fs2v10.wav`인지 `F#2v10.wav`인지 T1 시작 시 실제 파일명 재확인. 이 스펙은 Unity-safe `Ds`/`Fs` 표기 전제.

---

## 10. 완료 기준 (Definition of Done)

- [ ] T1 ~ T9 모두 완료
- [ ] EditMode ≥ 98 pass
- [ ] Device (§6.3) 체크리스트 전부 체크
- [ ] APK 크기 ≤ 36 MB
- [ ] v2 스펙 §11.2 `licenseInfo` 문자열 정정 커밋 포함
- [ ] `Assets/StreamingAssets/licenses/salamander-piano-v3.txt` 존재
- [ ] SettingsScreen에 Credits 한 줄 표시 (device 화면 확인)
- [ ] W6 완료 리포트에 "피드백 해소: 타격음 단일 → 17-샘플 pitched pool" 기재

---

## 부록 A. `ResolveSample` 알고리즘 경계 검증

커버리지 패턴은 MIDI `p`, `p+1`, `p+2`(=`(p+3)-1`) 3개 반음을 한 샘플 쌍이 커버하는 규칙이 36부터 84까지 반복된다. 경계 및 분기 진입 케이스만 추적으로 검증:

| MIDI | baseIdx 계산 | sampleMidi | offset | 분기 | 최종 결과 | 비고 |
|------|--------------|------------|--------|------|-----------|------|
| 36 | (36−36)/3=0 | 36 | 0 | — | (C2v10, 1.000) | 하한 경계 |
| 37 | 0 | 36 | 1 | — | (C2v10, 1.059) | |
| 38 | 0 | 36 | 2 | 진입 (1<17) | (Ds2v10, 0.944) | baseIdx=1, offset=−1 |
| 39 | (39−36)/3=1 | 39 | 0 | — | (Ds2v10, 1.000) | |
| 81 | (81−36)/3=15 | 81 | 0 | — | (A5v10, 1.000) | |
| 82 | 15 | 81 | 1 | — | (A5v10, 1.059) | C6로 가지 않음 |
| 83 | 15 | 81 | 2 | 진입 (16<17) | (C6v10, 0.944) | baseIdx=16, offset=−1 |
| 84 | 16 | 84 | 0 | — | (C6v10, 1.000) | 상한 경계 (미 사용, 17번째 샘플 필요성 증명) |

핵심: MIDI 83에서 분기가 `baseIdx=15 → 16`으로 진입하려면 `pitchSamples.Length = 17`(즉 C6 포함)이어야 한다. C6을 빼면 MIDI 83은 `A5v10`으로 +2반음 pitch-shift되어 음색 왜곡이 커진다. 그래서 17번째 샘플이 "상향 여유"로 필요하다.

테스트 6.1의 #2 (`midi 37 → clip[0], 1.059`), #3 (`midi 38 → clip[1], 0.944`)이 정방향·분기 진입을 각각 단위 테스트로 증명한다.
