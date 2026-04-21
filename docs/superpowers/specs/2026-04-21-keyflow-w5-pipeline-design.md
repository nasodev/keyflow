# KeyFlow W5: MIDI → .kfchart 파이프라인 + ChartLoader 코루틴 설계 문서

> 상태: 초안 (2026-04-21)
> 전제: v2 스펙(`2026-04-20-keyflow-mvp-v2-4lane-design.md`) §9·§10 준수
> W4 완료 리포트: `docs/superpowers/reports/2026-04-21-w4-completion.md`
> 개발자 프로필: 풀스택 웹 개발자, Unity/게임 첫 프로젝트

## 0. 한 줄 요약

W5는 **툴 검증 주간**이다. Python 파이프라인 구현·테스트 → Für Elise Normal 차트 1건 생성으로 end-to-end 검증 → 부수 리팩터 정리(ChartLoader 코루틴, Validate 하드닝, `System.Math.Clamp` 치환). **남은 4곡 콘텐츠 생산은 W6로 이월**한다.

---

## 1. 스코프

### 1.1 포함

| ID | 항목 | 산출물 |
|---|---|---|
| W5-T1 | Python MIDI → .kfchart 파이프라인 | `tools/midi_to_kfchart/`, pytest ≥ 30 |
| W5-T2 | Normal 난이도 알고리즘 (NPS 타겟팅 + Hold 검출) | 위 파이프라인의 `pipeline/` 하위 모듈 |
| W5-T3 | Für Elise Normal 차트 1건 | `Assets/StreamingAssets/charts/beethoven_fur_elise.kfchart`에 `NORMAL` 키 추가 |
| W5-T4 | ChartLoader 코루틴화 | Android busy-wait 제거, 신규 `IEnumerator LoadFromStreamingAssetsCo(...)` API |
| W5-T5 | W3 carry-over #9 — Validate 하드닝 | sort-order 체크, empty-notes 거부 |
| W5-T6 | W3 carry-over #8 — `Mathf.Clamp` → `System.Math.Clamp` | ChartLoader + CalibrationCalculator 순수 코어 디커플링 |
| W5-T7 | 실기기 검증 | Galaxy S22에서 Für Elise Normal 완주 |

### 1.2 제외 (W6 이월)

- 남은 4곡 (Ode to Joy, Canon in D, Clair de Lune, The Entertainer) 차트 제작
- 4곡 원본 MIDI 수집
- Unity Editor 통합 (Tools 메뉴에서 Python 호출)
- 로딩 스크린·progress 콜백 UI
- 파이프라인 사용 문서 이외의 튜토리얼

### 1.3 가드레일

- **배포 범위**: 개인 사용 및 지인 5~10명 테스트. 저작권 제약 완화 — MIDI 소스는 PD가 아니어도 무방, Salamander CC-BY 크레딧 MVP에선 생략.
- **기본 스펙 불변**: v2 스펙 §2 기능 10개, §5 수치, §7 .kfchart 포맷은 전부 그대로.
- **APK < 40 MB** 유지 (현재 31.5 MB).

---

## 2. 사전 상태 (W4 말 기준)

| 컴포넌트 | 상태 | W5와의 관계 |
|---|---|---|
| `ChartLoader.LoadFromStreamingAssets(songId)` | 동기, Android에서 `while (!op.isDone) { }` busy-wait | T4에서 coroutine화 |
| `ChartLoader.ParseJson` | note 단위 `Validate` — sort/empty 체크 없음 | T5에서 하드닝 |
| `ChartLoader.Mathf.Clamp(pitch, 36, 83)` | UnityEngine 의존 | T6에서 `System.Math.Clamp` |
| `CalibrationCalculator.Mathf.Clamp` | UnityEngine 의존 | T6와 동일 |
| `GameplayController.ResetAndStart()` | `chart = ChartLoader.LoadFromStreamingAssets(songId)` 즉시 반환 전제 | T4에서 coroutine으로 호출부 변경 |
| `Assets/StreamingAssets/charts/beethoven_fur_elise.kfchart` | `EASY` 73 notes (hand-authored, W3 `b94d0b8`) | T3에서 `NORMAL` 추가 |
| `tools/` | 없음 (신규) | T1에서 생성 |
| EditMode 테스트 | 88 / 88 pass | T4·T5 이후 +3 → 91 |

---

## 3. Python 파이프라인

### 3.1 저장소 구조

```
tools/midi_to_kfchart/
├── midi_to_kfchart.py        # CLI 진입점
├── pipeline/
│   ├── __init__.py
│   ├── parser.py             # mido 래핑: MIDI → RawNote[]
│   ├── chord_reducer.py      # 동일 타임스탬프 다중 note_on → 최고음 1개 (멜로디)
│   ├── hold_detector.py      # sustain ≥ 300ms → HOLD, 아니면 TAP
│   ├── density.py            # NPS > target·1.1 이면 thinning
│   ├── pitch_clamp.py        # MIDI 36~83 범위로 octave transpose
│   ├── lane_assigner.py      # pitch 범위 4등분 + 3연속 레인 완화
│   └── emitter.py            # 정렬·검증 후 .kfchart JSON 직렬화
├── tests/
│   ├── conftest.py
│   ├── test_parser.py
│   ├── test_chord_reducer.py
│   ├── test_hold_detector.py
│   ├── test_density.py
│   ├── test_pitch_clamp.py
│   ├── test_lane_assigner.py
│   └── test_emitter.py
├── requirements.txt          # mido==1.3.x
└── README.md
```

**언어 / 도구**
- Python 3.11+
- `mido` 1.3.x (MIDI 파싱)
- `pytest` 8.x (유닛 테스트)
- 패키징 없음 (setuptools/pyproject 생략). `requirements.txt`만 두고 `python -m pip install -r requirements.txt`로 바로 사용.

### 3.2 CLI 인터페이스

**단일 파일 모드**:

```bash
python tools/midi_to_kfchart/midi_to_kfchart.py \
    path/to/fur_elise.mid \
    --song-id beethoven_fur_elise \
    --title "Für Elise" \
    --composer "Beethoven" \
    --difficulty NORMAL \
    --target-nps 3.5 \
    --bpm 72 \
    --duration-ms 45000 \
    --merge-into Assets/StreamingAssets/charts/beethoven_fur_elise.kfchart
```

- `--merge-into`: 지정 시 기존 `.kfchart`를 읽어 `charts.<NORMAL|EASY>` 키만 덮어씀 (다른 난이도 보존). 파일이 없으면 신규 생성.
- `--out <path>`: 지정 시 해당 경로에 단독 쓰기. `--merge-into`와 상호 배타.
- `--seed <int>`: thinning·레인 완화에서 결정적 pseudo-random 필요 시. 기본 42.

**배치 모드** (W5 범위에서 구현하되 W6 콘텐츠 주간에 실제 사용):

```bash
python tools/midi_to_kfchart/midi_to_kfchart.py \
    --batch tools/midi_to_kfchart/batch.yaml
```

`batch.yaml` 예:

```yaml
defaults:
  out_dir: Assets/StreamingAssets/charts/
songs:
  - song_id: beethoven_ode_to_joy
    midi: midi_sources/ode_to_joy.mid
    title: "환희의 송가"
    composer: "Beethoven"
    bpm: 120
    duration_ms: 90000
    difficulties:
      EASY:  { target_nps: 1.5 }
      NORMAL: { target_nps: 3.0 }
```

### 3.3 데이터 플로우

```
input.mid
  │
  ├─► parser.parse(path, bpm_hint) ─► list[RawNote { t_ms, pitch, dur_ms }]
  │
  ├─► chord_reducer.collapse(raws)  ─► 동일 t_ms 여러 note → 최고 pitch 1건
  │
  ├─► hold_detector.classify(monos, hold_threshold_ms=300)
  │                                  ─► list[TypedNote { t, pitch, type, dur }]
  │
  ├─► density.thin(typed, target_nps, duration_ms)
  │                                  ─► list[TypedNote] (NPS 조정됨)
  │
  ├─► pitch_clamp.clamp(noted, 36, 83)   ─► octave transpose
  │
  ├─► lane_assigner.assign(clamped, lane_count=4, max_consecutive=2)
  │                                  ─► list[ChartNote { t, lane, pitch, type, dur }]
  │
  └─► emitter.to_kfchart(chart_notes, meta, difficulty)
      │ sort by t ascending
      │ validate (empty, sort, range, type)
      └─► JSON dict { charts: { NORMAL: { totalNotes, notes } } }
```

### 3.4 알고리즘 세부

**chord_reducer**
- 동일 `t_ms`에 `note_on` 여러 건 → `pitch` 최대값 1건만 유지. 나머지는 `dropped_by_chord_reducer` 로그 카운트.
- 근거: v2 §2 M-02 "모노포닉 탭(타임스탬프당 1 노트)".

**hold_detector**
- 입력 `RawNote`의 `dur_ms`가 ≥ 300이면 `HOLD`, 아니면 `TAP` (+ `dur=0`).
- 경계 300은 인게임 Hold 판정 UX 기준(체감 "길게 누름" 시작점)으로 선택. 실기기 튜닝 후 조정 가능.
- `dur_ms > 4000`은 `dur=4000`으로 cap (비정상 sustain 방어).

**density**
- 현재 NPS = `len(notes) / (duration_ms / 1000)`.
- `target_nps * 1.1` 초과 시 `int(excess_ratio * len(notes))` 만큼 drop.
- drop 대상: `pitch` 기준 중간 음역(멜로디 외곽) 우선. 구체 규칙은 `pitch` 히스토그램에서 최빈 구간 밖의 노트를 n번째마다 제거 (naive: 짝수 인덱스 drop도 허용 — W5에서는 단순 n번째 drop으로 시작, W6 튜닝 가능성).
- `target_nps * 0.7` 미만이어도 인위적으로 채우지 않음 (노트 부풀리기 금지).

**pitch_clamp**
- `pitch < 36` → `pitch + 12 * ceil((36 - pitch)/12)`.
- `pitch > 83` → `pitch - 12 * ceil((pitch - 83)/12)`.
- clamp 결과도 결국 범위 밖이면 `raise ValueError` (정상적인 MIDI에선 도달 불가).

**lane_assigner**
- `pmin, pmax = min(pitches), max(pitches)`.
- `ratio = (pitch - pmin) / (pmax - pmin)` (동일 pitch 단일곡은 `lane=0`).
- `lane = min(3, int(ratio * 4))`.
- 3연속 동일 레인 감지 시 `(lane + 1) % 4`로 스왑. 스왑 후에도 바로 앞 2개와 겹치면 `(lane + 2) % 4` 시도.

**emitter**
- `notes.sort(key=lambda n: n.t)` 정렬 (입력이 정렬돼 있어도 방어).
- `totalNotes = len(notes)`.
- 검증: 비어 있지 않음, `t` 비감소, `lane ∈ [0,3]`, `pitch ∈ [36,83]`, `type ∈ {TAP, HOLD}`, `type==TAP ⇒ dur==0`, `type==HOLD ⇒ dur>0`.
- `--merge-into` 경로: 기존 `.kfchart`를 읽어 `charts[difficulty]` 덮어쓰고 다른 난이도는 보존, 루트 메타(`title`, `composer`, `bpm`, `durationMs`)는 CLI 인자로 덮어쓰되 인자 미제공 시 원본 유지.

### 3.5 테스트 목록 (pytest, ≥ 30개)

| 모듈 | 테스트 | 개수 |
|---|---|---|
| parser | MIDI → RawNote 기본 변환, 빈 MIDI, 다중 트랙 병합, bpm_hint 미제공 시 기본 120 | 4 |
| chord_reducer | 단일 타임스탬프 3노트 → 1노트(최고음), 서로 다른 t 보존, 빈 입력 | 3 |
| hold_detector | 299ms→TAP, 300ms→HOLD, 4500ms→HOLD dur=4000, TAP의 dur=0 | 4 |
| density | NPS 이미 낮음→통과, 높음→thinning, NPS 0 방어, target·1.1 경계 | 4 |
| pitch_clamp | 35→47, 84→72, 36·83 경계 유지, 24→48 더블 옥타브 | 4 |
| lane_assigner | 단일 pitch→lane 0, 범위 4등분, 3연속 완화, 2단계 연속 완화 | 4 |
| emitter | 정렬, 빈 노트 거부, HOLD dur=0 거부, `--merge-into` 병합, 타입 검증 | 5 |
| CLI 통합 | 샘플 MIDI 입력 → 출력 JSON 스키마 검증 1건 | 2 |
| **합계** | | **30** |

각 테스트는 `pytest -q` 단독 실행 가능. CI 훅은 W5 범위 외.

---

## 4. Für Elise Normal 차트 생성 (T3)

### 4.1 입력

- Beethoven "Für Elise" (Woo 59) PD MIDI 1건. 수집 경로: 온라인(배포 제약 없음). 파일은 `midi_sources/beethoven_fur_elise.mid` 경로에 두되 Git 무시 (`.gitignore`에 `midi_sources/` 추가).

### 4.2 목표 파라미터

- `--target-nps 3.5` (v2 §3 Normal)
- `--bpm 72` (기존 Easy 차트와 동일)
- `--duration-ms 45000` (기존 Easy와 동일 범위)

### 4.3 튜닝 루프

1. 파이프라인 실행 → `charts.NORMAL` 키 생성.
2. Unity Editor Play → Für Elise → Normal 선택 → 완주.
3. 체감 이상 구간 기록 → 두 가지 경로 중 택 1.
   - (a) CLI 파라미터 (`--target-nps`, `--seed`) 조정 후 재생성.
   - (b) `.kfchart` 직접 편집 (JSON 수동 튜닝). 이 경우 README에 "재생성 시 덮어씀" 경고 명시.
4. 최종 버전 커밋.

### 4.4 합격 기준

- 완주 가능 (1~2회 재시도 내).
- NPS 3.0~4.0 범위.
- Hold 노트 ≥ 5개 (spec §7 기능 검증).
- 3연속 동일 레인 0건.

---

## 5. ChartLoader 코루틴화 (T4)

### 5.1 API 변경

**유지** (EditMode 테스트 호환):

```csharp
public static ChartData ParseJson(string json);
public static ChartData LoadFromPath(string absolutePath);  // 신규: File.ReadAllText → ParseJson
```

**신규**:

```csharp
public static IEnumerator LoadFromStreamingAssetsCo(
    string songId,
    System.Action<ChartData> onLoaded,
    System.Action<string> onError);
```

**삭제**:

```csharp
public static ChartData LoadFromStreamingAssets(string songId);  // 호출부 전환 완료 후 제거
```

### 5.2 동작

- Android (`UNITY_ANDROID && !UNITY_EDITOR`): `UnityWebRequest.Get(path)` + `yield return req.SendWebRequest();` (busy-wait 제거).
- 그 외 플랫폼(에디터·Windows 스탠드얼론): `File.ReadAllText` 즉시 반환 후 `onLoaded` 콜백. coroutine 1 프레임 지연 허용.
- 실패 시 `onError(req.error)` 또는 `onError(exception.Message)` 호출, throw 없음 (호출자가 에러 UI 결정).

### 5.3 호출부 변경

`GameplayController.ResetAndStart`:

```csharp
// Before
chart = ChartLoader.LoadFromStreamingAssets(songId);
// ... 이후 로직

// After
StartCoroutine(ChartLoader.LoadFromStreamingAssetsCo(
    songId,
    loaded => { chart = loaded; ContinueAfterChartLoaded(); },
    err => Debug.LogError($"[KeyFlow] chart load failed: {err}")));
```

- `BeginGameplay` 직전까지의 흐름을 `ContinueAfterChartLoaded()` 로 분리. `UserPrefs.HasCalibration` 분기·`audioSync.CalibrationOffsetSec` 세팅·`BeginGameplay / calibration.Begin` 호출은 전부 로드 완료 후 실행.

### 5.4 테스트

- **EditMode 추가 2건**:
  - `LoadFromPath`로 실제 `Assets/StreamingAssets/charts/beethoven_fur_elise.kfchart` 파싱 성공.
  - 존재하지 않는 파일 경로 → `FileNotFoundException`.
- **PlayMode 없음**: coroutine 로직은 단순 대기→콜백이라 EditMode 커버리지로 충분. 실기기 완주가 통합 검증.

---

## 6. Validate 하드닝 (T5, W3 carry-over #9)

### 6.1 추가 규칙

`ChartLoader.ParseJson` 내부에서 각 난이도 파싱 후 노트 리스트에 대해:

| 검사 | 위반 시 |
|---|---|
| `cd.notes.Count > 0` | `ChartValidationException("{diff} has no notes")` |
| 모든 i ≥ 1에 대해 `notes[i].t >= notes[i-1].t` | `ChartValidationException("{diff} notes not sorted at idx {i}")` |
| `cd.totalNotes == cd.notes.Count` | 기존 규칙 유지 (이미 구현 여부 확인 필요) |

### 6.2 테스트

- **EditMode 추가 1건**: `ParseJson(minimal_unsorted_json)` → throws. `ParseJson(minimal_empty_notes_json)` → throws. (1 테스트 함수 안에 두 assert)

---

## 7. `System.Math.Clamp` 치환 (T6, W3 carry-over #8)

### 7.1 대상

- `ChartLoader.ParseJson`의 `Mathf.Clamp((int)n["pitch"], PitchMin, PitchMax)` → `System.Math.Clamp(...)`.
- `CalibrationCalculator` 내 모든 `Mathf.Clamp` / `Mathf.Min` / `Mathf.Max` (정수·실수 타입에 한해). `Mathf.Round` 등 Unity 특유 수학은 유지.

### 7.2 의의

- 순수 C# 코어(UnityEngine 참조 없음)로 이전하는 단계. W7 이후 .NET 테스트 러너 분리 시 재사용 가능.
- **이번 주 범위에선 어셈블리 분리까지 하지 않는다** — 단순 치환만.

### 7.3 테스트

- 기존 `CalibrationCalculator` / `ChartLoader` EditMode 테스트가 그대로 녹색이면 통과. 신규 테스트 불필요.

---

## 8. 파일 변경 요약

### 8.1 신규

```
tools/midi_to_kfchart/midi_to_kfchart.py
tools/midi_to_kfchart/pipeline/__init__.py
tools/midi_to_kfchart/pipeline/parser.py
tools/midi_to_kfchart/pipeline/chord_reducer.py
tools/midi_to_kfchart/pipeline/hold_detector.py
tools/midi_to_kfchart/pipeline/density.py
tools/midi_to_kfchart/pipeline/pitch_clamp.py
tools/midi_to_kfchart/pipeline/lane_assigner.py
tools/midi_to_kfchart/pipeline/emitter.py
tools/midi_to_kfchart/tests/conftest.py
tools/midi_to_kfchart/tests/test_*.py  (8 파일)
tools/midi_to_kfchart/requirements.txt
tools/midi_to_kfchart/README.md
.gitignore  (midi_sources/ 추가)
```

### 8.2 수정

```
Assets/Scripts/Charts/ChartLoader.cs
  - Mathf.Clamp → System.Math.Clamp
  - LoadFromStreamingAssets 삭제, LoadFromStreamingAssetsCo + LoadFromPath 추가
  - ParseJson 내 Validate 하드닝 (sort, empty)

Assets/Scripts/Gameplay/GameplayController.cs
  - ResetAndStart → StartCoroutine(...) 경로, ContinueAfterChartLoaded 분리

Assets/Scripts/Calibration/CalibrationCalculator.cs
  - Mathf.Clamp → System.Math.Clamp

Assets/StreamingAssets/charts/beethoven_fur_elise.kfchart
  - charts.NORMAL 추가
```

### 8.3 테스트 파일

```
Assets/Tests/EditMode/ChartLoaderTests.cs   (+3 cases)
tools/midi_to_kfchart/tests/*               (신규 ≥ 30 cases)
```

---

## 9. 개발 순서 (권장)

1. **T1 파이프라인 골격 + pytest** (tools/ 신규, Unity 무관) — 자가검증 루프가 가장 빠름.
2. **T2 Normal 알고리즘 모듈** (T1 골격 위에 NPS·Hold 검출 추가).
3. **T3 Für Elise Normal 생성** — 파이프라인 end-to-end 검증.
4. **T5 Validate 하드닝** — T3에서 생성한 차트가 통과하는지 먼저 확인 후 Unity 반영.
5. **T4 ChartLoader 코루틴화** — T5·T3가 안정된 후 게임 쪽 리팩터.
6. **T6 `System.Math.Clamp` 치환** — 기존 테스트 커버리지로 바로 검증.
7. **T7 실기기 검증** — Galaxy S22에서 Für Elise Normal 완주 + 소감 기록.

---

## 10. Definition of Done

- [ ] `cd tools/midi_to_kfchart && pytest -q` 녹색 (≥ 30 cases)
- [ ] Für Elise NORMAL 차트가 `beethoven_fur_elise.kfchart` 의 `charts.NORMAL` 키에 존재
- [ ] Main → Für Elise → Normal 선택 → 플레이 가능
- [ ] ChartLoader Android 경로가 coroutine으로 동작 (busy-wait 제거)
- [ ] ChartLoader.LoadFromStreamingAssets 동기 API 제거 완료
- [ ] ChartLoader.ParseJson Validate 하드닝 (sort-order, empty-notes) 적용
- [ ] `Mathf.Clamp` → `System.Math.Clamp` (ChartLoader + CalibrationCalculator) 치환
- [ ] EditMode 테스트 ≥ 91 녹색 (현재 88 + 3)
- [ ] APK 빌드 성공, < 40 MB 유지
- [ ] Galaxy S22 Für Elise Normal 1회 완주 사용자 체크
- [ ] `docs/superpowers/reports/2026-04-??-w5-completion.md` 작성

---

## 11. 리스크 및 완화

| 리스크 | 영향 | 완화 |
|---|---|---|
| 생성된 Normal 차트가 재미 없거나 깨짐 | T3 재작업 반복 | 튜닝 루프(§4.3)로 흡수. 최악의 경우 `--merge-into`가 아닌 수동 JSON 편집. |
| `mido` 설치 이슈 (Windows) | 파이프라인 사용 불가 | `mido` 순수 Python, 추가 네이티브 라이브러리 없음. `pip install mido` 로 충분. |
| Android coroutine 전환 후 씬 진입 지연 | 사용자 체감 "멈춤" | Für Elise 차트는 수 KB, 실측 ~50ms. T7에서 확인 후 필요 시 W6에서 로딩 UI 추가. |
| `chord_reducer` 최고음 선택이 멜로디 라인 아님 (베이스 우위 곡) | 차트가 어색함 | W5 범위에선 최고음 규칙 고정. W6 튜닝 시 `--melody-track <idx>` 옵션 추가 고려. |
| Python 3.11+ 미설치 | 파이프라인 실행 불가 | README에 `python --version` 확인 절차. Windows에서는 Python 공식 인스톨러. |

---

## 12. 예상 공수

| 작업 | 추정 |
|---|---|
| T1·T2 파이프라인 + pytest | 10h |
| T3 Für Elise Normal 생성 + 튜닝 | 3h |
| T4 ChartLoader coroutine | 2h |
| T5 Validate 하드닝 | 1h |
| T6 `System.Math.Clamp` 치환 | 0.5h |
| T7 실기기 + 문서 | 2h |
| 버퍼 (Normal 튜닝 재시도) | 1.5h |
| **합계** | **~20h** |

---

## 13. 이후 주 연결

- **W6**: 폴리싱 + 사운드 + **4곡 콘텐츠 생산** (이 스펙의 파이프라인을 사용). 카르오버 #1(profiler), #2(dedicated calibration click), #4(star ASCII→sprite는 W4에서 해결 가능하면 제외) 남은 항목 처리.
- **W7 / W8**: 실기기 다종 테스트 + APK 배포.
