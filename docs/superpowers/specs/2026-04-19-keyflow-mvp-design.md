# KeyFlow MVP: Magic Piano 클론 Android APK 설계 문서

> 상태: 승인됨 (2026-04-19)
> 개발자 프로필: 풀스택 웹 개발자 경력자, Unity/게임 개발 첫 프로젝트
> 브레인스토밍 합의 사항: PRD를 설계 문서로 그대로 채택. 아래 "합의된 리스크" 섹션의 3가지는 인지된 전제로 간주.

## 0. 합의된 리스크 (브레인스토밍 결과)

본 설계를 착수하기 전에 합의된 전제다. 구현 중 이 가정이 깨지면 스펙 재검토가 필요하다.

1. **Android 오디오 레이턴시는 기기/OS 의존**이며, 저가 OEM에서는 Oboe 경유 시에도 120ms 이상 하드웨어 플로어가 존재할 수 있다. W1 PoC의 go/no-go 판정은 **고·중·저 사양 3기기 평균**으로 내린다.
2. **Native Audio 플러그인 ($35) vs Unity AudioSource 16채널 풀** 결정은 W1 내부에서 한다. 기본 경로는 AudioSource 풀로 시작 → 레이턴시 초과 시 Native Audio로 교체 (전환 비용 ~1일).
3. **Unity 관용구 학습 곡선**으로 PRD의 8주 예상치에 **+1주 버퍼**(총 9~10주)를 현실 예산으로 간주. 다만 PRD 일정표 자체는 수정하지 않는다.

---

## 1. MVP 정의와 목표

### 1.1 MVP 범위 선언
MVP는 상업용 제품이 아니라 **"핵심 플레이 루프가 재미있는가"**를 검증하는 프로토타입 수준의 플레이 가능 빌드다. 따라서 수익화, 소셜, 진행 시스템은 전부 후순위(Out of Scope)다.

### 1.2 MVP 목적
첫째, Android에서 **50~80ms 이내의 체감 오디오 레이턴시로 리듬게임이 동작**하는지 기술 검증. 둘째, 자유 배치 빔 노트 UX가 실제로 "피아노를 치는 느낌"을 주는지 사용성 검증. 셋째, APK를 설치한 지인 5~10명에게 30초 이상 플레이시켜 "한 번 더 하고 싶다"는 반응을 얻는 것.

### 1.3 비목표 (Out of Scope for MVP)
Song Map 월드형 UI, Composer(작곡), 구독/결제, 광고 SDK, 업적, 일일 챌린지, 2~4 finger 동시 탭, 사운드팩 전환, 리더보드, Cloud Save, Duet 모드, Firebase Analytics, 다국어(한국어만), 튜토리얼 영상, 애니메이션 컷신. 이들은 MVP 검증 이후 v1.0에서 추가한다.

---

## 2. MVP 기능 목록

표에 정리한 **10개 기능이 전부**다. 이보다 늘어나면 MVP가 아니다.

| ID | 기능 | 설명 |
|---|---|---|
| M-01 | 빔 노트 게임플레이 | 화면 상단에서 떨어지는 빛 점이 판정선에 도달할 때 탭 |
| M-02 | 단일 손가락 탭 | 1 finger 탭 노트 + Hold 노트 2종만 지원 |
| M-03 | 4단계 판정 | Perfect / Great / Good / Miss |
| M-04 | 점수·콤보 | 100만점 정규화, Miss 시 콤보 리셋 |
| M-05 | 별 획득 (0~3) | 50%/75%/90% 구간 |
| M-06 | 2단계 난이도 | Easy / Normal 두 개만 (Hard/Expert는 v1.0) |
| M-07 | 곡 선택 리스트 | 단순 세로 스크롤 리스트 (지도형 아님) |
| M-08 | 오디오 캘리브레이션 | 최초 실행 시 1회 간단 탭 테스트 |
| M-09 | 결과 화면 | 점수·콤보·별·판정별 카운트 + 재도전/홈 |
| M-10 | 기본 설정 | BGM/SFX 볼륨, NoteSpeed, 캘리브레이션 재실행 |

수록 곡은 **5곡**으로 시작한다. 전부 퍼블릭 도메인 클래식이며 자체 MIDI 시퀀싱 + Salamander Grand Piano(CC0) 샘플러로 렌더링한다.

---

## 3. MVP 수록 곡 (5곡)

곡 선정 기준은 ①작곡 저작권 PD ②대중 인지도 ③난이도 스펙트럼(쉬움~보통) ④MIDI 데이터 확보 용이성이다.

| # | 곡 | 작곡가 | 길이 | Easy NPS | Normal NPS |
|---|---|---|---|---|---|
| 1 | Ode to Joy (환희의 송가) | Beethoven | 1:30 | 1.5 | 3.0 |
| 2 | Für Elise | Beethoven | 2:00 | 2.0 | 3.5 |
| 3 | Canon in D | Pachelbel | 2:30 | 1.8 | 3.2 |
| 4 | Clair de Lune | Debussy | 2:00 | 1.5 | 2.8 |
| 5 | The Entertainer | Joplin | 2:00 | 2.2 | 4.0 |

모든 곡은 OGG Vorbis 128kbps로 인코딩되며 곡당 ~2MB, 총 ~10MB를 차지한다.

---

## 4. 화면 구성 (총 5개)

MVP의 화면은 5개로 제한한다. 네비게이션은 단순 Stack 구조.

### 4.1 스플래시 (2초)
KeyFlow 로고 페이드 인/아웃. 최초 실행 시 한국어 약관 1회 동의(스크롤), 이후 **메인** 자동 진입.

### 4.2 메인 (곡 리스트)
상단: 앱 로고 + 설정 톱니 아이콘. 중단: 곡 카드 세로 스크롤 리스트. 각 카드는 썸네일(64dp), 곡명, 작곡가, 최고 별수(0~3성), 난이도 선택 버튼 2개(Easy/Normal). 탭 시 로딩 → 게임플레이로 이동.

### 4.3 게임플레이 (가로 모드)
- 상단 40%: 노트 낙하 영역. 노트는 상단에서 등장해 아래로 1.5~2초에 걸쳐 이동.
- 중단 5%: 판정 라인 (수평 빛줄기).
- 하단 55%: 탭 감지 영역 (투명). 탭 시 파티클 이펙트.
- HUD: 좌상단 일시정지, 중상단 점수·콤보, 우상단 진행 바.
- 노트 x좌표는 해당 음의 MIDI 피치에 매핑(좌=저음, 우=고음).

### 4.4 결과
상단: 별 애니메이션 0~3개. 중단: 점수 카운트업(1.5초), 최대 콤보, 판정 카운트(P/G/G/M), 정확도 %. 하단: 재도전 / 홈 복귀 2버튼.

### 4.5 설정
BGM 슬라이더, SFX 슬라이더, NoteSpeed 슬라이더(1.0~3.0, 기본 2.0), 오디오 캘리브레이션 재실행 버튼, 앱 버전 표기.

---

## 5. 게임 디자인 핵심 수치

### 5.1 판정 윈도우
| 난이도 | Perfect | Great | Good | Miss |
|---|---|---|---|---|
| Easy | ±75ms | ±150ms | ±225ms | >225ms |
| Normal | ±60ms | ±120ms | ±180ms | >180ms |

### 5.2 점수 계산 (100만점 정규화)
```
perNote = 900_000 / totalNotes
judgmentScore = perNote × {Perfect:1.0, Great:0.7, Good:0.3, Miss:0}
comboBonus = (100_000 / totalNotes) × (hit ? 1 : 0)
totalScore = Σ judgmentScore + Σ comboBonus
```
Full Combo 시 +10% 보너스(v1.0에서 추가, MVP는 생략 가능).

### 5.3 별 획득
1성 500,000 이상, 2성 750,000 이상, 3성 900,000 이상.

### 5.4 노트 스크롤 공식 (dspTime 기반, 프레임 독립)
```csharp
double songStartDspTime;
void StartSong() {
    songStartDspTime = AudioSettings.dspTime + 0.5;
    audioSource.PlayScheduled(songStartDspTime);
}
void Update() {
    double songTime = AudioSettings.dspTime - songStartDspTime - userCalibOffset;
    float previewTime = 2.5f / userNoteSpeed;
    foreach (var note in activeNotes) {
        float progress = 1f - (float)((note.hitTime - songTime) / previewTime);
        note.transform.position = Vector3.Lerp(spawnPos, judgmentPos, progress);
    }
}
```
`Time.deltaTime` 누적 금지. 반드시 dspTime 단일 진실원.

---

## 6. 기술 스택

### 6.1 엔진
**Unity 6 LTS** (C#). 근거는 리듬게임 레퍼런스 풍부, `AudioSettings.dspTime`의 정밀 타이밍, Native Audio 플러그인 접근성, Android 빌드 성숙도.

### 6.2 오디오
- BGM: Unity AudioSource + `PlayScheduled(dspTime + 0.5)`
- 탭 SFX (피아노 음): **Native Audio 플러그인** ([exceed7.com](https://exceed7.com/native-audio/), 내부적으로 Oboe 사용, 유료 $35)
- DSP Buffer Size: Best Latency (256)
- 샘플레이트: 48000 Hz 고정
- 피아노 음색: Salamander Grand Piano V3 (CC0), 88건반 중 MVP에서 48건반(MIDI 36~83)만 OGG로 사전 인코딩 → ~15MB

저렴한 대안: Native Audio 대신 Unity 내장 AudioSource를 사용하되 AudioClip을 `LoadType.DecompressOnLoad`로 메모리 상주시키고 Polyphony(동시 재생 채널) AudioSource 16개를 풀링. 레이턴시 30~50ms 악화되나 $0.

### 6.3 Android 설정
- minSdk: 26 (Android 8.0)
- targetSdk: 35 (Android 15)
- Architecture: arm64-v8a 단일 (MVP는 armeabi-v7a 생략)
- Orientation: Landscape (게임플레이), Portrait (메인/설정)
- 필요 권한: `INTERNET`(미사용 선언 불필요), `VIBRATE`(햅틱, P1로 미뤄도 됨)

### 6.4 데이터 저장
- 사용자 설정·캘리브레이션: **PlayerPrefs**
- 곡별 최고 점수: **PlayerPrefs** (MVP 수준에선 SQLite 오버엔지니어링)
- 곡 차트 데이터: **JSON 파일** (StreamingAssets에 번들링)

### 6.5 APK 사이즈 목표
- 코드 + UI 에셋: ~15MB
- 피아노 샘플: ~15MB
- 곡 5개 OGG: ~10MB
- 여유: ~10MB
- **총 APK: 50MB 이하**

---

## 7. 곡 차트 포맷 (.kfchart)

간단한 JSON 구조. MIDI 파일을 자체 Python 스크립트로 변환해 생성한다.

```json
{
  "songId": "beethoven_fur_elise",
  "title": "Für Elise",
  "composer": "Beethoven",
  "bpm": 120,
  "audioFile": "fur_elise.ogg",
  "durationMs": 120000,
  "charts": {
    "EASY": {
      "totalNotes": 180,
      "notes": [
        {"t": 500,  "x": 0.65, "pitch": 64, "type": "TAP",  "dur": 0},
        {"t": 750,  "x": 0.63, "pitch": 63, "type": "TAP",  "dur": 0},
        {"t": 1000, "x": 0.65, "pitch": 64, "type": "HOLD", "dur": 500}
      ]
    },
    "NORMAL": { ... }
  }
}
```

`t`: 곡 시작부터 ms, `x`: 0.0~1.0 정규화 x좌표, `pitch`: MIDI 피치(탭 시 재생할 음), `type`: TAP | HOLD, `dur`: Hold 지속시간 ms.

---

## 8. 간략한 시스템 구조

복잡한 레이어 분리 없이 **3계층**으로 충분하다.

```
Scenes (Unity)
  ├─ SplashScene
  ├─ MainScene (곡 리스트 + 설정 오버레이)
  └─ GameplayScene
        ├─ GameManager.cs        (게임 상태 머신)
        ├─ NoteSpawner.cs        (차트 JSON 읽어 노트 인스턴스화)
        ├─ NoteController.cs     (개별 노트 낙하)
        ├─ JudgmentEvaluator.cs  (탭 ms vs 노트 hitTime 비교)
        ├─ ScoreManager.cs       (점수·콤보·별 계산)
        ├─ AudioSyncManager.cs   (dspTime + BGM + SFX)
        └─ UIOverlay.cs          (HUD 업데이트)

Services (DontDestroyOnLoad)
  ├─ SongCatalogService         (songs.json 파싱 + 메타데이터)
  ├─ ScoreStorageService        (PlayerPrefs CRUD)
  └─ CalibrationService         (오프셋 저장/로드)

Assets (StreamingAssets)
  ├─ songs.json                 (곡 카탈로그)
  ├─ charts/*.kfchart           (5개 차트)
  └─ audio/*.ogg                (5개 곡)
```

Object Pool은 노트·파티클에 적용(초기 사이즈 64). 기타 디자인 패턴은 MVP에선 생략.

---

## 9. 개발 일정 (6~8주, 1인 풀스택 기준)

| 주차 | 작업 | Deliverable |
|---|---|---|
| **W1** | PoC + 환경 세팅 | 노트 1개 낙하, 탭 시 피아노 음 재생, 60fps. 이 마일스톤이 **전체 프로젝트의 기술 go/no-go** |
| **W2** | 판정·점수 시스템 | Perfect/Great/Good/Miss 판정, 점수 누적, 콤보 |
| **W3** | 차트 파서 + 곡 1개 완주 | .kfchart JSON 로드, Für Elise Easy 완주 가능 |
| **W4** | UI 구현 | 메인 곡 리스트, 설정, 결과 화면 |
| **W5** | 캘리브레이션 + 곡 4개 추가 | 나머지 4곡 차트 제작, 캘리브레이션 UX |
| **W6** | 폴리싱 + 사운드 | 파티클, 햅틱, 효과음, 버그 수정 |
| **W7** (버퍼) | 실기기 테스트 | 3~5종 기기에서 레이턴시 검증, 마지막 버그 수정 |
| **W8** (버퍼) | APK 빌드 배포 | Play Console Internal Testing 또는 Direct APK 배포 |

W1 PoC가 실패하면(체감 레이턴시 150ms 이상 해소 불가) 즉시 Native Kotlin + Oboe로 피벗 결정. 이 경우 일정 +4주.

---

## 10. 차트 제작 파이프라인

MVP에서 가장 과소평가되는 작업이 **차트 제작**이다. 5곡 × 2난이도 = 10개 차트를 만들어야 한다.

### 10.1 워크플로우
1. Mutopia Project 또는 IMSLP에서 PD MIDI 확보 (또는 직접 MuseScore로 입력)
2. Python 스크립트로 MIDI → .kfchart 자동 변환 (mido 라이브러리)
3. Easy는 주요 멜로디 노트만 추출(NPS 1.5~2.5 유지), Normal은 멜로디 + 보조 화성음 일부
4. 인게임에서 시험 플레이 → 손으로 미세 조정(특히 동시 탭 제거, 템포 가속 구간 NPS 완화)

### 10.2 자동 변환 스크립트 개요
```python
import mido, json
def midi_to_chart(midi_path, difficulty):
    mid = mido.MidiFile(midi_path)
    notes = []
    abs_time = 0
    for msg in mid:
        abs_time += msg.time
        if msg.type == 'note_on' and msg.velocity > 0:
            x = (msg.note - 36) / (83 - 36)  # MIDI 36~83 → 0.0~1.0
            if difficulty == 'EASY' and len(notes) % 2 == 1:
                continue  # Easy는 노트 절반만
            notes.append({
                't': int(abs_time * 1000),
                'x': round(x, 3),
                'pitch': msg.note,
                'type': 'TAP',
                'dur': 0
            })
    return notes
```
곡당 차트 제작 시간 **약 2~4시간** 소요 예상.

---

## 11. 법적 고려사항 (MVP 축약본)

### 11.1 상표
"Smule", "Magic Piano" 등 일체 사용 금지. 앱명 KeyFlow, 패키지명 `com.yourcompany.keyflow`. Smule의 보라+시안 브랜드 컬러 회피, KeyFlow는 주황+딥퍼플 계열.

### 11.2 저작권
MVP 수록 5곡 전원 **작곡 저작권 PD 확정**(Beethoven 1827 사망, Debussy 1918, Joplin 1917, Pachelbel 1706, 모두 사후 70년 경과). 녹음은 **자체 MIDI 시퀀싱 + Salamander CC-BY 샘플러**로 렌더링하여 연주 저작권도 해결 (CC-BY 3.0, 저자: Alexander Holm — Assets/StreamingAssets/licenses/salamander-piano-v3.txt 및 Settings Credits에 귀속 표기). 인터넷 무료 MIDI 파일 사용 금지(재배포 라이선스 불명확). 각 곡 메타데이터에 `"licenseInfo": "PD-composition; self-sequenced; CC-BY-samples(Salamander V3, Alexander Holm)"` 기록.

### 11.3 Play Console
MVP는 Play Store 공개 배포 대신 **Internal Testing Track** 또는 **APK 직접 배포**(테스터에게 설치 파일 전달)를 권장. 공개 배포는 v1.0부터.

---

## 12. 성공 기준 (MVP 완료 정의)

다음 5개를 **모두 충족**하면 MVP 완료로 본다.

1. 실기기 3종(고/중/저 사양) 이상에서 **60fps 유지, 크래시 없음**
2. Normal 난이도에서 캘리브레이션 후 **Perfect 판정 체감 가능**(테스터 주관 평가)
3. 5곡 × 2난이도 모두 **완주 가능**(무한 로딩·음성 끊김 없음)
4. APK **50MB 이하**, 콜드 스타트 3초 이내
5. 테스터 5명 중 **3명 이상이 "한 번 더 하고 싶다"**고 평가

이 5개가 충족되지 않으면 v1.0 착수 전 MVP를 반복한다.

---

## 13. v1.0 로드맵 예고 (MVP 이후)

MVP 검증이 끝나면 다음 순서로 확장한다.

- **Phase 1 (MVP 이후 4주)**: 2~4 finger 동시 탭, Hard/Expert 난이도, 곡 30곡 확장, 간단한 Song Map(지도형) UI
- **Phase 2 (추가 4주)**: 업적, 일일 챌린지, 광고(AdMob 리워드)
- **Phase 3 (추가 6주)**: Google Play Billing 구독, 프리미엄 곡 팩 IAP, Composer(로컬 저장)
- **Phase 4**: 리더보드, Cloud Save, 다국어, iOS 포팅

---

## 14. Week 0 체크리스트 (착수 전)

1. Unity 6 LTS 설치 (Android Build Support 모듈 포함)
2. Native Audio 플러그인 구매 결정 — 기본은 미구매 (W1에서 AudioSource 풀로 먼저 시도)
3. Salamander Piano V3 다운로드 (CC0)
4. Beethoven Für Elise PD MIDI 확보 (Mutopia / IMSLP)
5. Android 실기기 2대 이상 준비 (고사양 1 + 중·저사양 1)
6. JDK 17 + Android SDK 설정, 테스트 기기 USB 디버깅 켜기

## 결론

KeyFlow MVP의 핵심 원칙은 **"탭해서 곡을 친다"는 한 문장에 모든 기능이 기여해야 한다**는 것이다. Song Map·Composer·구독·업적은 이 문장을 검증하는 데 불필요하므로 전부 뺐다. W1 PoC에서 오디오 레이턴시와 dspTime 정확도가 증명되면 나머지 5~7주는 콘텐츠(차트)와 폴리싱 작업이 전부다.

1인 풀스택 개발자 기준 **총 개발 시간 약 200~280시간**(주 40시간 × 6~8주)으로 추산한다. Unity 경험이 있다면 하단, 처음이라면 상단 값에 가깝다. 완성된 APK는 Play Store 공개 없이 지인 10명에게 배포 → 피드백 → v1.0 착수라는 린 스타트업식 루프가 이 MVP의 진짜 목적이다.
