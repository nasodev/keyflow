# KeyFlow MVP v2: 4-Lane Piano Tiles Android APK 설계 문서

> 상태: 승인됨 (2026-04-20)
> 버전: **v2** — v1 (2026-04-19, Magic Piano 클론, 가로/자유 x좌표)에서 피벗
> 개발자 프로필: 풀스택 웹 개발자, Unity/게임 첫 프로젝트
> W1 PoC 결과: Galaxy S22 실측 ~110ms 레이턴시 (MARGINAL), 무료 경로 선택, 캘리브레이션으로 흡수 전략
> 이전 버전: [2026-04-19-keyflow-mvp-design.md](./2026-04-19-keyflow-mvp-design.md) (참고용 보존)

## 0. v1 → v2 피벗 요약

MVP 범위 축소·단순화를 위한 인터랙션 모델 변경:

1. **화면 방향**: Landscape → **Portrait**
2. **노트 x 좌표**: 0~1 실수(MIDI pitch 매핑) → **레인 0~3 정수 (4 고정)**
3. **탭 영역**: 하단 55% 별도 존 → **레인 전체 (화면 전체가 탭 가능)**
4. **BGM 트랙**: 별도 OGG 재생 → **제거 (Tap SFX만)**
5. **Hold 노트·모노포닉·판정·점수·별·2난이도·5곡 수록**: v1 그대로 유지
6. **W1 PoC 측정치 ~110ms**를 캘리브레이션(M-08)으로 흡수. Native Audio($35) 구매 유보.

장르가 "Magic Piano 클론" → **"Piano Tiles 스타일 4레인 리듬게임"** 으로 바뀝니다.

---

## 1. MVP 정의와 목표

### 1.1 MVP 범위 선언
MVP는 상업용 제품이 아니라 **"핵심 플레이 루프가 재미있는가"** 를 검증하는 프로토타입 수준의 플레이 가능 빌드다.

### 1.2 MVP 목적
첫째, Android에서 **~110ms 레이턴시를 캘리브레이션으로 체감 상쇄하여 리듬게임이 플레이 가능한지** 검증. 둘째, 4레인 Piano Tiles UX가 "피아노를 치는 느낌"을 주는지 검증. 셋째, 지인 5~10명에게 30초 이상 플레이시켜 "한 번 더 하고 싶다"는 반응을 얻는 것.

### 1.3 비목표 (Out of Scope for MVP)
Song Map 월드형 UI, Composer, 구독/결제, 광고 SDK, 업적, 일일 챌린지, 2~4 finger 동시 탭 및 화음, 사운드팩 전환, 리더보드, Cloud Save, Duet 모드, Firebase Analytics, 다국어(한국어만), 튜토리얼 영상, 애니메이션 컷신, BGM 트랙. 이들은 MVP 검증 이후 v1.0에서 추가한다.

---

## 2. MVP 기능 목록 (10개)

| ID | 기능 | v2 설명 |
|---|---|---|
| M-01 | 4레인 노트 게임플레이 | 화면 상단에서 떨어지는 빛 블록이 판정선에 도달할 때 **해당 레인** 탭 |
| M-02 | 모노포닉 탭 | 타임스탬프당 **1 노트만** (동시 2노트 없음). Tap + Hold 2종 |
| M-03 | 4단계 판정 | Perfect / Great / Good / Miss |
| M-04 | 점수·콤보 | 100만점 정규화, Miss 시 콤보 리셋 |
| M-05 | 별 획득 (0~3) | 50%/75%/90% 구간 |
| M-06 | 2단계 난이도 | Easy / Normal |
| M-07 | 곡 선택 리스트 | **세로 스크롤 카드 리스트** (세로모드) |
| M-08 | 오디오 캘리브레이션 | 최초 실행 시 1회 탭 테스트 + 설정에서 재실행. **W3~W4 우선 구현** (110ms 레이턴시 흡수 핵심) |
| M-09 | 결과 화면 | 점수·콤보·별·판정별 카운트 + 재도전/홈 |
| M-10 | 기본 설정 | SFX 볼륨, NoteSpeed, 캘리브레이션 재실행 |

수록 곡 5개 (v1과 동일).

---

## 3. MVP 수록 곡 (5곡)

| # | 곡 | 작곡가 | 길이 | Easy NPS | Normal NPS |
|---|---|---|---|---|---|
| 1 | Ode to Joy | Beethoven | 1:30 | 1.5 | 3.0 |
| 2 | Für Elise | Beethoven | 2:00 | 2.0 | 3.5 |
| 3 | Canon in D | Pachelbel | 2:30 | 1.8 | 3.2 |
| 4 | Clair de Lune | Debussy | 2:00 | 1.5 | 2.8 |
| 5 | The Entertainer | Joplin | 2:00 | 2.2 | 4.0 |

**v2 변경**: BGM OGG 파일 불필요 — 탭이 곧 음악. APK 크기 예상 -10MB (~40MB 최종).

---

## 4. 화면 구성 (총 5개)

### 4.1 스플래시 (2초)
세로 모드. KeyFlow 로고 페이드 인/아웃. 최초 실행 시 한국어 약관 1회 동의.

### 4.2 메인 (곡 리스트, 세로모드)
상단: 앱 로고 + 설정 톱니 아이콘. 중단: 곡 카드 **세로 스크롤**. 각 카드는 썸네일(56dp), 곡명, 작곡가, 최고 별수, 난이도 선택 2버튼(Easy/Normal).

### 4.3 게임플레이 (세로 모드)

```
┌───┬───┬───┬───┐ ↑ 0% (상단)
│   │ █ │   │   │ │
│   │   │   │ █ │ │ 노트 낙하 영역
│ █ │   │   │   │ │ (레인 0~3, 세로 스트림)
│   │   │ █ │   │ │
│   │   │   │   │ ↓ 80%
├───┴───┴───┴───┤ ← 판정선 (80% 지점, 수평 빛줄기)
│ TAP 가능 영역  │ ↓ 20% (하단)
└───┴───┴───┴───┘
```

- **상단 0~80%**: 노트 낙하 영역. 노트는 상단에서 등장해 아래로 **1.5~2초** 이동
- **80% 지점**: 판정선 (수평 빛줄기)
- **하단 20%**: 시각적으로 탭 존 강조 (하지만 탭은 레인 전체 어디서나 유효)
- **탭 판정 규칙**: 터치 이벤트의 **x 좌표 → 레인 인덱스** 변환 후, 해당 레인의 가장 가까운 노트와 시간 비교
- HUD: 좌상단 일시정지, 중상단 점수·콤보, 우상단 진행 바
- 레인 4개는 동일 색상 (시안 톤 통일, 현재 노트가 있는 레인은 살짝 하이라이트)

### 4.4 결과 (세로 모드)
상단: 별 애니메이션 0~3개. 중단: 점수 카운트업(1.5초), 최대 콤보, 판정 카운트(P/G/G/M), 정확도 %. 하단: 재도전 / 홈 복귀.

### 4.5 설정 (세로 모드)
SFX 볼륨 슬라이더, NoteSpeed 슬라이더(1.0~3.0, 기본 2.0), 오디오 캘리브레이션 재실행 버튼, 앱 버전 표기.

> v1의 BGM 슬라이더는 제거됨 (BGM 없음).

---

## 5. 게임 디자인 핵심 수치

### 5.1 판정 윈도우 (v1과 동일)

| 난이도 | Perfect | Great | Good | Miss |
|---|---|---|---|---|
| Easy | ±75ms | ±150ms | ±225ms | >225ms |
| Normal | ±60ms | ±120ms | ±180ms | >180ms |

### 5.2 점수 계산 (v1과 동일)
```
perNote = 900_000 / totalNotes
judgmentScore = perNote × {Perfect:1.0, Great:0.7, Good:0.3, Miss:0}
comboBonus = (100_000 / totalNotes) × (hit ? 1 : 0)
totalScore = Σ judgmentScore + Σ comboBonus
```

### 5.3 별 획득 (v1과 동일)
1성 500,000 / 2성 750,000 / 3성 900,000.

### 5.4 노트 스크롤 공식 (dspTime 기반)
```csharp
double songStartDspTime;
void StartSong() {
    songStartDspTime = AudioSettings.dspTime + 0.5;
    // v2: BGM 없음. PlayScheduled 호출 안 함. 단순 dspTime 앵커만.
}
void Update() {
    double songTime = AudioSettings.dspTime - songStartDspTime - userCalibOffset;
    float previewTime = 2.5f / userNoteSpeed;
    foreach (var note in activeNotes) {
        float progress = 1f - (float)((note.hitTime - songTime) / previewTime);
        float laneX = LaneLayout.LaneToX(note.lane);  // v2 신규
        note.transform.position = new Vector3(laneX, Mathf.Lerp(spawnY, judgmentY, progress), 0);
    }
}
```

### 5.5 레인 → x 좌표 변환 (v2 신규)

```csharp
public static class LaneLayout {
    public const int LaneCount = 4;

    public static float LaneToX(int lane, float screenWorldWidth) {
        float laneWidth = screenWorldWidth / LaneCount;
        float leftEdge = -screenWorldWidth / 2f;
        return leftEdge + laneWidth * (lane + 0.5f);
    }

    public static int XToLane(float x, float screenWorldWidth) {
        float laneWidth = screenWorldWidth / LaneCount;
        float leftEdge = -screenWorldWidth / 2f;
        return Mathf.Clamp((int)((x - leftEdge) / laneWidth), 0, LaneCount - 1);
    }
}
```

순수 함수라 EditMode 테스트 가능.

---

## 6. 기술 스택

### 6.1 엔진
**Unity 6.3 LTS (6000.3.13f1)** + C#. W1 PoC 완료 기준.

### 6.2 오디오 (v2 단순화)
- **BGM: 없음**
- 탭 SFX (피아노 음): Unity AudioSource 16채널 풀 (무료 경로 확정)
- DSP Buffer Size: Best Latency (256 samples)
- 샘플레이트: 48000 Hz
- 피아노 음색: Salamander Grand Piano V3 (CC-BY 3.0), MVP에서 48건반(MIDI 36~83)만 WAV/OGG 번들. W1 PoC는 C4 1개만.
- **개선 경로 (피드백 따라)**: UALLA 오픈소스 플러그인 → Native Audio($35) → Kotlin+Oboe 피벗 순서

### 6.3 Android 설정
- minSdk 26 / targetSdk 35 / arm64-v8a
- **Orientation**: Portrait (v1 Landscape에서 변경)
- 권한: `VIBRATE` (햅틱, P1)

### 6.4 데이터 저장 (v1과 동일)
- 사용자 설정·캘리브레이션: PlayerPrefs
- 곡별 최고 점수: PlayerPrefs
- 곡 차트 데이터: JSON (StreamingAssets)

### 6.5 APK 사이즈 목표 (v2 재산정)
- 코드 + UI: ~15MB
- 피아노 샘플 (48 키): ~15MB
- ~~곡 5개 OGG BGM: ~10MB~~ (v2 제거)
- 여유: ~10MB
- **총 APK: 40MB 이하** (v1보다 10MB 여유)

---

## 7. 곡 차트 포맷 (.kfchart, v2)

```json
{
  "songId": "beethoven_fur_elise",
  "title": "Für Elise",
  "composer": "Beethoven",
  "bpm": 120,
  "durationMs": 120000,
  "charts": {
    "EASY": {
      "totalNotes": 180,
      "notes": [
        {"t": 500,  "lane": 2, "pitch": 64, "type": "TAP",  "dur": 0},
        {"t": 750,  "lane": 2, "pitch": 63, "type": "TAP",  "dur": 0},
        {"t": 1000, "lane": 3, "pitch": 64, "type": "HOLD", "dur": 500}
      ]
    },
    "NORMAL": { }
  }
}
```

- `t`: 곡 시작 후 ms
- `lane`: **0~3 정수** (v1의 `x` 대체)
- `pitch`: MIDI 피치 (탭 시 재생)
- `type`: `TAP` | `HOLD`
- `dur`: Hold 지속 ms (Tap은 0)
- `audioFile`: **v1에서 제거** (BGM 없음)

---

## 8. 시스템 구조

```
Scenes (Unity)
  ├─ SplashScene
  ├─ MainScene
  └─ GameplayScene
        ├─ GameManager.cs         (상태 머신)
        ├─ ChartLoader.cs         (v2 신규: .kfchart 로드)
        ├─ NoteSpawner.cs         (레인 기반)
        ├─ NoteController.cs      (레인 x + dspTime 낙하)
        ├─ JudgmentEvaluator.cs   (탭 x → 레인 → 노트 매칭)
        ├─ ScoreManager.cs        (점수·콤보·별)
        ├─ AudioSyncManager.cs    (dspTime 앵커만, BGM PlayScheduled 제거)
        ├─ AudioSamplePool.cs     (16채널, 피치별 재생)
        ├─ TapInputHandler.cs     (터치 x → 레인)
        ├─ LaneLayout.cs          (v2 신규: 좌표 변환)
        └─ UIOverlay.cs           (HUD)

Services (DontDestroyOnLoad)
  ├─ SongCatalogService
  ├─ ScoreStorageService
  └─ CalibrationService

Assets (StreamingAssets)
  ├─ songs.json
  ├─ charts/*.kfchart
  └─ audio/piano/*.ogg  (48 키)
```

Object Pool은 노트·파티클에 적용(초기 64). BGM 서브시스템 제거로 `AudioSyncManager` 코드 ~30% 감소.

---

## 9. 개발 일정 (W2~W8, W1 완료됨)

| 주차 | 작업 | Deliverable |
|---|---|---|
| ~~W1 (완료)~~ | ~~PoC + 환경~~ | ~~노트 낙하 + 탭 피아노 음 + 60fps + APK + 레이턴시 측정 (~110ms MARGINAL)~~ |
| **W2** | v2 레이아웃 전환 + 판정·점수 | 세로모드, 4레인, Tap 판정 P/G/G/M, 점수 누적, 콤보. Hold는 W3 초 |
| **W3** | 차트 로더 + Hold + 첫 곡 완주 + **캘리브레이션 조기 구현** | .kfchart JSON 로드, Hold 노트, Für Elise Easy 완주 가능, M-08 캘리브레이션 MVP 버전 |
| **W4** | UI 화면 | 메인(곡 리스트), 설정, 결과 화면 |
| **W5** | 곡 4개 추가 + Python 차트 도구 | 나머지 4곡 차트 제작, midi_to_kfchart.py 최종 |
| **W6** | 폴리싱 + 사운드 | 파티클, 햅틱, 버그 수정 |
| W7 (버퍼) | 실기기 테스트 | 3~5종 기기, 마지막 버그 수정 |
| W8 (버퍼) | APK 배포 | Internal Testing 또는 Direct APK |

**v2 일정 영향**: BGM 트랙 제거로 W5 차트 제작 시간 -5~10시간. W3에 캘리브레이션 추가로 +1~2일. 총 일정은 v1 대비 거의 동일.

**캘리브레이션 조기 구현 근거**: W1 PoC에서 ~110ms 측정. 이를 흡수하는 M-08이 W5까지 미뤄지면 W4 UI 테스트에서 "박자 밀림" 착시 발생. W3에 최소 구현, W5에서 완성.

---

## 10. 차트 제작 파이프라인 (v2 갱신)

### 10.1 워크플로우
1. Mutopia / IMSLP에서 PD MIDI 확보
2. Python 스크립트로 MIDI → .kfchart **(레인 자동 배정)**
3. Easy는 주요 멜로디 노트만 (NPS 1.5~2.5), Normal은 멜로디 + 보조 화성
4. **후처리**: 3연속 같은 레인 이슈 해결 (스왑)
5. 인게임 시험 플레이 → 수동 미세 조정

### 10.2 자동 변환 스크립트 (v2)

```python
import mido, json

def midi_to_chart(midi_path, difficulty):
    mid = mido.MidiFile(midi_path)
    notes = []
    abs_time = 0
    pitches_seen = []

    # 1st pass: 노트 수집 + 피치 범위 구하기
    for msg in mid:
        abs_time += msg.time
        if msg.type == 'note_on' and msg.velocity > 0:
            if difficulty == 'EASY' and len(notes) % 2 == 1:
                continue
            notes.append({
                't': int(abs_time * 1000),
                'pitch': msg.note,
                'type': 'TAP',
                'dur': 0,
                'lane': 0  # placeholder
            })
            pitches_seen.append(msg.note)

    if not pitches_seen:
        return []
    pmin, pmax = min(pitches_seen), max(pitches_seen)

    # 2nd pass: 레인 배정 (음역 4등분)
    for n in notes:
        if pmax == pmin:
            n['lane'] = 0
        else:
            ratio = (n['pitch'] - pmin) / (pmax - pmin)
            n['lane'] = min(3, int(ratio * 4))

    # 3rd pass: 3연속 같은 레인 완화
    for i in range(2, len(notes)):
        if notes[i]['lane'] == notes[i-1]['lane'] == notes[i-2]['lane']:
            notes[i]['lane'] = (notes[i]['lane'] + 1) % 4

    return notes
```

곡당 차트 제작 시간 **약 2~4시간** (v1과 동일).

---

## 11. 법적 고려사항

### 11.1 상표
"Smule", "Magic Piano" 등 일체 사용 금지. 앱명 KeyFlow, 패키지명 `com.funqdev.keyflow`. 브랜드 컬러 주황+딥퍼플 계열.

### 11.2 저작권
5곡 전원 작곡 PD. 녹음은 자체 MIDI + **Salamander Grand Piano V3 (CC-BY 3.0)** — W1 PoC 선택 기준. 메타데이터 `"licenseInfo": "PD-composition; self-sequenced; CC-BY-3.0 Salamander V3"`. 배포 시 CC-BY 크레딧 표기 의무.

**주의**: v1 스펙은 Salamander를 "CC0"라고 썼으나 실제 V3는 **CC-BY 3.0**. About 화면에 Alexander Holm 크레딧 추가 필요.

### 11.3 Play Console
Internal Testing 또는 APK 직접 배포. 공개 배포는 v1.0부터.

---

## 12. 성공 기준 (MVP 완료 정의)

1. 실기기 3종 이상에서 60fps 유지, 크래시 없음
2. **캘리브레이션 후 Normal 난이도에서 Perfect 판정 체감 가능** (W1의 110ms 레이턴시는 캘리브레이션으로 상쇄)
3. 5곡 × 2난이도 모두 완주 가능
4. APK **40MB 이하**, 콜드 스타트 3초 이내
5. 테스터 5명 중 **3명 이상이 "한 번 더 하고 싶다"** 고 평가

---

## 13. v1.0 로드맵 예고

- **Phase 1 (MVP 이후 4주)**: 2~4 finger 동시 탭 (화음 차트), Hard/Expert 난이도, 곡 30곡, 간단한 Song Map UI
- **Phase 2 (4주)**: 업적, 일일 챌린지, AdMob 리워드 광고
- **Phase 3 (6주)**: 구독, 프리미엄 곡팩 IAP, Composer
- **Phase 4**: 리더보드, Cloud Save, 다국어, iOS 포팅
- **오디오 개선 (피드백에 따라)**: UALLA → Native Audio → Kotlin+Oboe 순

---

## 14. W1에서 무엇이 살아있는가

v1 → v2 피벗 후 W1 PoC 코드의 활용도:

- **생존 (수정 없음)**: AudioSyncManager, AudioSamplePool, TapInputHandler(일부), LatencyMeter, GameTime의 SongTime/NoteProgress, EditMode 테스트 대부분, Assets/Audio/piano_c4.wav, 모든 ProjectSettings (Orientation만 변경)
- **수정**: NoteController(레인 기반), NoteSpawner(차트 로드), TapInputHandler(x→레인), SceneBuilder(세로 레이아웃)
- **폐기**: GameTime.PitchToX, 관련 테스트 3개, 기존 GameplayScene, Note 프리팹
- **Orientation 설정**: Player Settings Portrait으로 변경 필요

W2 작업 착수 시 위 목록 기준으로 재사용 최대화.

---

## 결론

KeyFlow MVP v2의 핵심 원칙은 v1과 동일: **"탭해서 곡을 친다"는 한 문장에 모든 기능이 기여**. v2는 그 표현 수단을 **"4레인 Piano Tiles"** 로 단순화하여:

- 구현 복잡도 감소 (BGM 제거, 레인 이산화, 세로모드 자연스러움)
- 110ms 레이턴시에 관대한 UX (넓은 레인 = 탭 오차 수용)
- 플레이어 학습 시간 거의 0
- 차트 제작 자동화 용이 (레인은 정수 + 알고리즘 생성)

1인 풀스택 개발자 기준 **W2~W8 추정 작업 시간 약 160~220시간** (주 40시간 × 5~6주, 버퍼 포함). W1은 완료됨.
