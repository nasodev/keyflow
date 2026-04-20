# KeyFlow W4 — 메인·설정·결과 화면 + Pause + carry-over 번들 설계

> 작성일: 2026-04-21
> 상위 스펙: [2026-04-20-keyflow-mvp-v2-4lane-design.md](./2026-04-20-keyflow-mvp-v2-4lane-design.md)
> 이전 주차: [W3 차트·Hold·캘리브레이션](./2026-04-20-keyflow-w3-chart-hold-calibration-design.md) (완료 2026-04-21, HEAD `3ba2661`)
> 개발자 프로필: 풀스택 웹 개발자, Unity/게임 첫 프로젝트

---

## 0. 이 문서의 역할

v2 MVP 스펙 §4(화면 구성)·§12(일정) 중 **W4 범위를 실행 가능 수준으로 구체화**한다. v2 스펙이 "무엇"(WHAT)을 말하면 이 문서는 "어떻게"(HOW)를 말한다. 구현 태스크 분해는 이 문서를 받아 `writing-plans` 스킬이 생성할 Plan 4에서 이루어진다.

W3 검수 회고에서 정리된 **carry-over 12건** 중 W4에 자연스럽게 묶이는 4건을 포함한다 (§8 참조).

---

## 1. 스코프

### 1.1 W4에 포함

| 항목 | 출처 | 비고 |
|---|---|---|
| Main 화면 (곡 리스트) | v2 §4.2, M-07 | 세로 스크롤 카드 리스트 |
| Settings 화면 (오버레이) | v2 §4.5, M-10 | SFX·NoteSpeed·Calib 재실행·버전 |
| Results 화면 | v2 §4.4, M-09 | W3의 `CompletionPanel` placeholder 대체 |
| Pause 오버레이 | v2 §4.3 HUD "좌상단 일시정지" | W3에서 미구현, W4에서 처음 구현 |
| carry-over #4 — 별 ASCII → 스프라이트 | W3 회고 | Results에서 어차피 새로 렌더 |
| carry-over #5 — 로케일 한국어 통일 | W3 회고 | 새 UI 문자열 대량 유입 타이밍 |
| carry-over #6 — 네임스페이스 통일 | W3 회고 | 새 파일 잔뜩 생기는 타이밍 |
| carry-over #7 — `OverlayBase` 추상화 | W3 회고 | Settings/Pause/Results = 세 번째 오버레이 |

### 1.2 W4에 포함하지 않음

- 스플래시 / 한국어 약관 동의 화면 (v2 §4.1, M-splash) — W6 폴리싱
- 파티클·햅틱·사운드 폴리싱 — W6
- Composer·Leaderboard·Ads 등 post-MVP 전부
- 추가 곡·Python 차트 파이프라인 — W5
- 풀 애니메이션(파티클·"NEW RECORD" 배너·배경 전환) — W6
- 다국어(영/일 등) — post-MVP

### 1.3 상위 스펙 연결

- 상위 M-07 / M-08 (재실행 부분) / M-09 / M-10 을 본 주차에서 소화한다.
- 상위 §12 일정표의 "W4 UI 화면 (메인·설정·결과)" 항목에 대응한다.

---

## 2. 전체 아키텍처

### 2.1 화면 전환 방식 — Single Scene + Canvas 전환

브레인스토밍 Q2에서 다중 Scene(A) / 단일 Scene + Canvas(B) / Hybrid(C) 비교 후 **B** 채택.

결정 이유:
1. 현 W3 코드 구조가 이미 단일 Scene + 오버레이 Canvas(Calibration·Completion)로 되어 있어 연장선.
2. 상태 전달이 단순 — `DontDestroyOnLoad` / ScriptableObject 싱글톤 설계 불필요.
3. 곡 선택 → 게임플레이 진입 시 Scene 전환 로딩 깜빡임 없음.
4. carry-over #7 (`OverlayBase`)과 자연스럽게 맞물려 모든 화면이 동일한 Show/Finish 패턴.
5. 5~6개 화면 규모에선 메모리 상주 비용 무시 가능.

### 2.2 씬 구조

`Assets/Scenes/GameplayScene.unity` 하나를 유지하며, 루트 계층을 다음과 같이 조직:

```
GameplayScene
├── Main Camera
├── EventSystem
├── GameplayRoot             ← 활성/비활성 토글 대상
│   ├── LaneDividers
│   ├── JudgmentLine
│   ├── Managers (AudioSync, SamplePool, TapInput, JudgmentSystem, HoldTracker, Spawner)
│   └── GameplayController
├── HUDCanvas                ← 게임플레이 전용 HUD (일시정지·점수·콤보·LatencyMeter·진행바)
├── MainCanvas               ← 곡 리스트 스크롤 + 설정 톱니
├── ResultsCanvas            ← 결과 화면
├── SettingsCanvas           ← 설정 오버레이
├── PauseCanvas              ← 일시정지 오버레이
├── CalibrationCanvas        ← 기존. OverlayBase 상속으로 재작성
└── ScreenManager (empty GO + MonoBehaviour)
```

### 2.3 `ScreenManager`

```csharp
public enum Screen { Main, Gameplay, Results }

public class ScreenManager : MonoBehaviour {
    public static ScreenManager Instance { get; private set; }

    [SerializeField] GameObject mainRoot;       // MainCanvas + (옵션) 다른 Main 오브젝트
    [SerializeField] GameObject gameplayRoot;   // GameplayRoot + HUDCanvas
    [SerializeField] GameObject resultsCanvas;

    // 오버레이는 OverlayBase 참조로 바인딩
    [SerializeField] OverlayBase settingsOverlay;
    [SerializeField] OverlayBase pauseOverlay;
    [SerializeField] OverlayBase calibrationOverlay;

    public Screen Current { get; private set; }

    public void Replace(Screen target) { ... }   // 풀스크린 스크린 전환
    public void ShowOverlay(OverlayBase o) { ... }
    public void HideOverlay(OverlayBase o) { ... }

    void Awake() { Instance = this; Replace(Screen.Main); }

    // Android Back 처리 (Unity에서 Android Back 은 InputSystem Keyboard.escapeKey 로 매핑)
    void Update() {
        var kb = Keyboard.current;
        if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;
        HandleBack();
    }
}
```

- 풀스크린 스크린 3종(Main/Gameplay/Results)은 **단순 교체** — 스택 불필요(Results→Main 가면 Gameplay로 돌아갈 일 없음, Retry는 직접 `Replace(Gameplay)`).
- 오버레이 3종(Settings/Pause/Calibration)은 독립 토글.
- 풀스크린 전환 시 오버레이는 자동 숨김.

### 2.4 `SongSession` — 전역 상태 최소집

```csharp
public static class SongSession {
    public static string CurrentSongId;
    public static Difficulty CurrentDifficulty;
    public static ScoreManager LastScore;   // Results → Retry/Home 시 사용
}
```

Static으로 충분. 테스트에서 초기화 헬퍼 필요.

---

## 3. 신규 런타임 파일

| 경로 | 책임 | 의존성 |
|---|---|---|
| `Assets/Scripts/UI/ScreenManager.cs` | 스크린 전환 + 오버레이 토글 + Back 처리 | OverlayBase |
| `Assets/Scripts/UI/OverlayBase.cs` (abstract) | `Awake→SetActive(false)`, `Show()`, `Finish()`, `IsVisible` | — |
| `Assets/Scripts/Common/SongSession.cs` | static 전역 상태 | — |
| `Assets/Scripts/Catalog/SongEntry.cs` | `[Serializable]` DTO | — |
| `Assets/Scripts/Catalog/SongCatalog.cs` | `catalog.kfmanifest` 파싱 + `All` / `TryGet(id)` | UnityWebRequest (Android) |
| `Assets/Scripts/Common/UserPrefs.cs` | PlayerPrefs 파사드 + prefix + 마이그레이션 | — |
| `Assets/Scripts/UI/UIStrings.cs` | static 한국어 문자열 | — |
| `Assets/Scripts/UI/MainScreen.cs` | 카드 리스트 바인딩, 카드 탭 핸들링 | SongCatalog, SongSession, ScreenManager |
| `Assets/Scripts/UI/SongCardView.cs` | 카드 prefab 컴포넌트 | UserPrefs |
| `Assets/Scripts/UI/SettingsScreen.cs` : OverlayBase | 슬라이더 바인딩, Calib 재실행 트리거 | UserPrefs, CalibrationController |
| `Assets/Scripts/UI/PauseScreen.cs` : OverlayBase | Resume/Quit 버튼 | AudioSyncManager, ScreenManager |
| `Assets/Scripts/UI/ResultsScreen.cs` : OverlayBase | 별 애니, 점수 카운트업, Retry/Home | UserPrefs, ScreenManager |

### 3.1 `OverlayBase` 계약

```csharp
public abstract class OverlayBase : MonoBehaviour {
    public bool IsVisible { get; private set; }

    protected virtual void Awake() { gameObject.SetActive(false); }

    public virtual void Show() {
        gameObject.SetActive(true);
        IsVisible = true;
        OnShown();
    }

    public virtual void Finish() {
        OnFinishing();
        gameObject.SetActive(false);
        IsVisible = false;
    }

    protected virtual void OnShown() {}
    protected virtual void OnFinishing() {}
}
```

`CalibrationController`, `CompletionPanel`(→ `ResultsScreen`로 대체), `SettingsScreen`, `PauseScreen` 4개가 상속.

### 3.2 `SongCatalog` — JSON 매니페스트 파싱

경로: `Assets/StreamingAssets/catalog.kfmanifest` (UTF-8).

```json
{
  "version": 1,
  "songs": [
    {
      "id": "beethoven_fur_elise",
      "title": "엘리제를 위하여",
      "composer": "Beethoven",
      "thumbnail": "thumbs/fur_elise.png",
      "difficulties": ["Easy", "Normal"],
      "chartAvailable": true
    },
    {
      "id": "placeholder_w5_1",
      "title": "(W5에 공개)",
      "composer": "—",
      "thumbnail": "thumbs/locked.png",
      "difficulties": [],
      "chartAvailable": false
    }
    /* placeholder 3개 더 */
  ]
}
```

로딩 방식은 W3 `ChartLoader`와 동일 경로 분기:
- **에디터/스탠드얼론**: `File.ReadAllText`
- **Android**: `UnityWebRequest` (jar:file:// 경로)

API:
```csharp
public static class SongCatalog {
    public static IReadOnlyList<SongEntry> All { get; }
    public static bool TryGet(string id, out SongEntry entry);
    public static IEnumerator LoadAsync();   // 앱 시작 시 1회
}
```

### 3.3 `UserPrefs` — PlayerPrefs 파사드

```csharp
public static class UserPrefs {
    // Settings
    public static float SfxVolume { get; set; }          // 기본 0.8
    public static float NoteSpeed { get; set; }          // 기본 2.0
    public static int CalibrationOffsetMs { get; set; }  // 기존 값 마이그레이션

    // Records
    public static int GetBestStars(string songId, Difficulty d);
    public static int GetBestScore(string songId, Difficulty d);
    public static bool TrySetBest(string songId, Difficulty d, int stars, int score);  // 갱신 시 true

    // 1회 마이그레이션
    public static void MigrateLegacy();  // legacy "CalibOffsetMs" → "KeyFlow.Settings.CalibrationOffsetMs"
}
```

키 네이밍:
- `KeyFlow.Settings.SfxVolume`
- `KeyFlow.Settings.NoteSpeed`
- `KeyFlow.Settings.CalibrationOffsetMs`
- `KeyFlow.Record.{songId}.{difficulty}.Stars`
- `KeyFlow.Record.{songId}.{difficulty}.Score`
- `KeyFlow.Migration.V1.Done` (bool, 마이그레이션 완료 플래그)

기존 `CalibrationController.cs:11`의 하드코딩 키는 실제로 `"CalibOffsetMs"`(축약형)이다. `UserPrefs.MigrateLegacy`는 이 정확한 키명을 원본으로 읽어서 prefixed 키로 복사한 뒤 레거시 키를 삭제한다. 삭제 후에는 `CalibrationController`도 `UserPrefs.CalibrationOffsetMs` 경유로 read/write 하도록 수정.

### 3.4 `UIStrings`

```csharp
public static class UIStrings {
    // Common
    public const string Confirm = "확인";
    public const string Cancel = "취소";
    public const string Retry = "재도전";
    public const string Home = "홈";

    // Main
    public const string AppTitle = "KeyFlow";
    public const string Easy = "쉬움";
    public const string Normal = "보통";
    public const string ComingSoon = "(W5에 공개)";

    // Settings
    public const string SettingsTitle = "설정";
    public const string SfxVolumeLabel = "효과음 볼륨";
    public const string NoteSpeedLabel = "노트 속도";
    public const string RecalibrateButton = "오디오 다시 맞추기";
    public const string VersionLabelFmt = "버전 {0}";

    // Calibration (W3 기존 문구 이관)
    public const string CalibrationPrompt = "화면 아무 곳이나, 클릭 소리에 맞춰 8번 탭하세요.";
    public const string CalibrationStart = "시작";

    // Pause
    public const string Paused = "일시정지";
    public const string Resume = "계속하기";
    public const string QuitToMain = "메인으로 나가기";

    // Results
    public const string SongComplete = "곡 완주!";
    public const string ScoreFmt = "점수 {0:N0}";
    public const string MaxComboFmt = "최대 콤보 {0}";
    public const string BreakdownFmt = "P:{0}  G:{1}  G:{2}  M:{3}";
    public const string NewRecord = "최고 기록!";  // 조건부 표시
}
```

Calibration 영어 문구는 전혀 없음(이미 한국어). Completion 영어 문구 5개("SONG COMPLETE", "Score:", "Max Combo:", "Perfect/Great/Good/Miss", "Restart")는 ResultsScreen 신규 작성 시 UIStrings 경유.

---

## 4. 수정되는 기존 파일

| 파일 | 수정 내용 |
|---|---|
| `CalibrationController.cs` | `OverlayBase` 상속, `Begin(Action onDone)` 시그니처로 콜백 파라미터화(재진입 지원), 하드코딩 PlayerPrefs 키 제거 |
| `CompletionPanel.cs` | **삭제**, `ResultsScreen.cs`로 대체 |
| `GameplayController.cs` | 하드코딩 `songId` 제거 → `SongSession.CurrentSongId/Difficulty` 참조. `CalibrationController.HasSavedOffset()` 대신 `UserPrefs.CalibrationOffsetMs` 직접 참조 가능 여부 검토. 완주 시 `ResultsScreen.Show(score)`로 호출 변경, `UserPrefs.TrySetBest` 호출로 기록 저장. `paused` 상태 존중. |
| `AudioSyncManager.cs` | `Pause()`, `Resume()` 메서드 추가 (§5 참조) |
| `NoteSpawner.cs`, `NoteController.cs`, `HoldTracker.cs` | 각자 `audioSync.IsPaused`에 따라 Update skip |
| `TapInputHandler.cs` | Paused 시 입력 무시 |
| `JudgmentSystem.cs` | Paused 시 이벤트 처리 무시 (HoldTracker와 결합돼 있어 한 번 더 검토) |
| `SceneBuilder.cs` | Main·Settings·Pause·Results Canvas 빌드 루틴 추가, 별 스프라이트 생성(`EnsureStarSprite`), 썸네일 placeholder 생성(`EnsureLockedThumbSprite`), `ScreenManager` GO 생성 + 필드 와이어링, GameplayRoot 그룹핑 |
| `ApkBuilder.cs` | 변경 없음 (기대) |
| 네임스페이스 (carry-over #6) | `KeyFlow` / `KeyFlow.UI` / `KeyFlow.Editor` / `KeyFlow.Calibration` / `KeyFlow.Charts` 5개로 통일. 현재 혼재분 전부 정리 |

### 4.1 네임스페이스 정리 규칙

- **`KeyFlow`** — 런타임 코어 타입: `AudioSyncManager`, `AudioSamplePool`, `GameTime`, `LaneLayout`, `Judgment`/`Difficulty` enum, `JudgmentSystem`, `JudgmentEvaluator`, `NoteController`, `NoteSpawner`, `TapInputHandler`, `HoldStateMachine`, `HoldTracker`, `ScoreManager`, `GameplayController`, `SongSession`
- **`KeyFlow.UI`** — UI MonoBehaviour/오버레이: `ScreenManager`, `OverlayBase`, `MainScreen`, `SongCardView`, `SettingsScreen`, `PauseScreen`, `ResultsScreen`, `LatencyMeter`, `UIStrings`
- **`KeyFlow.Calibration`** — `CalibrationController`, `CalibrationCalculator`
- **`KeyFlow.Charts`** — `ChartData`, `ChartNote`, `ChartDifficulty`, `ChartLoader`
- **`KeyFlow.Editor`** — `SceneBuilder`, `ApkBuilder`

현재 상태 확인 후 이동/필요 시 `using` 추가. 테스트 파일도 동일 규칙 적용.

---

## 5. Pause 구현 상세

### 5.1 `AudioSyncManager` 확장

현재 `AudioSyncManager`는 `songStartDsp` 앵커 + `dspTime - songStartDsp + calibrationOffset` 공식으로 `SongTimeMs`를 계산한다. Pause는 이 앵커를 밀어주는 방식으로 구현한다.

```csharp
public class AudioSyncManager : MonoBehaviour {
    double songStartDsp;
    double pauseStartDsp;
    public bool IsPaused { get; private set; }
    public double CalibrationOffsetSec { get; set; }

    public int SongTimeMs {
        get {
            double now = IsPaused ? pauseStartDsp : AudioSettings.dspTime;
            return (int)((now - songStartDsp + CalibrationOffsetSec) * 1000.0);
        }
    }

    public void Pause() {
        if (IsPaused) return;
        pauseStartDsp = AudioSettings.dspTime;
        AudioListener.pause = true;
        IsPaused = true;
    }

    public void Resume() {
        if (!IsPaused) return;
        double elapsed = AudioSettings.dspTime - pauseStartDsp;
        songStartDsp += elapsed;
        AudioListener.pause = false;
        IsPaused = false;
    }
}
```

핵심 보증: `SongTimeMs`는 Pause 중 동결되고, Resume 시 정지 직전 값부터 연속된다. 스폰된 노트들은 `SongTimeMs` 기반으로 위치를 계산하므로 자동으로 정지/재개된다.

### 5.2 소비측 guard

- `NoteSpawner.Update` — 맨 앞에 `if (audioSync.IsPaused) return;`
- `NoteController.Update` — 위치 갱신 루프. `if (audioSync.IsPaused) return;`
- `TapInputHandler.Update` — 입력 처리 전 `if (audioSync.IsPaused) return;`
- `HoldTracker.Update` — 홀드 틱 계산 중단. 현재 누르고 있던 Hold는 Resume 시 상태 유지 (OK — `pressedThisFrame` 로컬 상태는 프레임당 리셋이므로 `isHeld`만 유지됨).
- `JudgmentSystem` — Tap/Hold 이벤트 핸들러에서 paused 체크. 이미 `TapInputHandler`가 차단하면 이벤트 자체가 안 올 가능성 높음. 검증 후 중복 guard 불필요하면 제거.

### 5.3 HUD 일시정지 버튼

HUDCanvas 좌상단에 ⏸ 아이콘 버튼 (`Image` + `Button`). 클릭 시 `ScreenManager.ShowOverlay(pauseOverlay)` + `audioSync.Pause()`.

### 5.4 PauseScreen

```
┌──────────────────────┐
│                      │
│      일시정지         │
│                      │
│   ┌──────────────┐   │
│   │   계속하기    │   │
│   └──────────────┘   │
│   ┌──────────────┐   │
│   │ 메인으로 나가기│   │
│   └──────────────┘   │
│                      │
└──────────────────────┘
```

- **계속하기** → `audioSync.Resume()`, `HideOverlay(pause)`.
- **메인으로 나가기** → 점수 버림, `audioSync.Resume()` 호출로 내부 상태 정리 후 `ScreenManager.Replace(Main)`.

### 5.5 Android Back 매핑

| 현재 화면/오버레이 | Back 동작 |
|---|---|
| Main (오버레이 없음) | 종료 확인 다이얼로그 — **MVP 단순화: 두 번 누르면 종료**, 토스트 없이 2초 이내 재입력이면 `Application.Quit()` |
| Settings overlay | 오버레이 닫기 |
| Gameplay (오버레이 없음) | PauseScreen 표시 + `audioSync.Pause()` |
| Pause overlay | 계속하기와 동일 |
| Calibration overlay | 무시 (진행 중단 불가 — MVP 단순화) |
| Results | Home 버튼과 동일 |

토스트 UI는 W6로 연기. "두 번 누르면 종료"는 `ScreenManager`에 `lastBackTime` 필드로 단순 구현.

---

## 6. 화면별 UI 스펙

### 6.1 MainScreen

레이아웃 (720×1280 레퍼런스):

```
┌──────────────────────────┐
│  KeyFlow        ⚙        │  ← 상단 고정 헤더 (80dp 높이)
├──────────────────────────┤
│  ┌──────────────────┐    │  ← 카드 (세로 스크롤)
│  │ 🎵 엘리제를 위하여 │    │     - 썸네일 56dp
│  │   Beethoven       │    │     - 제목 18sp
│  │   ★★☆            │    │     - 작곡가 14sp
│  │ [쉬움] [보통]     │    │     - 별 3개 (현재 최고 별수, Easy 기준)
│  └──────────────────┘    │     - 난이도 버튼 2개
│  ┌──────────────────┐    │
│  │ 🔒 (W5에 공개)    │    │
│  │   —               │    │
│  │ [쉬움] [보통]     │    │  ← chartAvailable=false면 비활성 회색
│  └──────────────────┘    │
│  ... (총 5개)             │
└──────────────────────────┘
```

- 썸네일은 `Resources.Load` 아닌 `StreamingAssets/thumbs/*.png` → `UnityWebRequest.GetTexture` 로딩. Locked 썸네일은 회색 자물쇠 절차 생성 스프라이트(`SceneBuilder.EnsureLockedThumbSprite`).
- 스크롤뷰는 Unity UI `ScrollRect` + `VerticalLayoutGroup` + `ContentSizeFitter`.
- 별수 표시는 Easy 최고 별수를 표시 (spec §4.2가 별수 1개로 명시 — Easy 기준). Normal 최고 별수도 저장되지만 카드엔 Easy만.
- 난이도 버튼 탭:
  1. `SongSession.CurrentSongId = id; SongSession.CurrentDifficulty = diff;`
  2. `ScreenManager.Replace(Screen.Gameplay)`
  3. Gameplay는 초기 진입 시 캘리브 필요 여부 판단하고 게임 시작

### 6.2 SettingsScreen (오버레이)

```
┌──────────────────────────┐
│    설정              ✕    │
│                          │
│  효과음 볼륨              │
│  ├──●───────┤             │
│                          │
│  노트 속도                │
│  ├──────●───┤ 2.0        │
│                          │
│  [ 오디오 다시 맞추기 ]    │
│                          │
│           버전 0.4.0      │
└──────────────────────────┘
```

- SFX Volume: `Slider` (0~1), `onValueChanged` → `UserPrefs.SfxVolume = v; AudioListener.volume = v;`
- NoteSpeed: `Slider` (1.0~3.0, step 0.1), 값 라벨 옆 표기. `UserPrefs.NoteSpeed = v`. 게임플레이 중에는 적용 안 함(다음 곡부터 반영) — 단순화.
- 오디오 다시 맞추기:
  1. Settings 오버레이 Finish
  2. `CalibrationController.Begin(onDone: () => ScreenManager.ShowOverlay(settingsOverlay))`
  3. 캘리브 끝나면 Settings 다시 표시
- 버전: `Application.version` 표기 (`UIStrings.VersionLabelFmt`).
- ✕ 또는 Android Back으로 닫기 → Main으로.

### 6.3 ResultsScreen

```
┌──────────────────────────┐
│                          │
│       곡 완주!            │
│                          │
│      ★  ★  ★             │  ← 별 3개 순차 pop (0.2s 간격)
│                          │
│    점수 842,500           │  ← 0→N 카운트업 1.5s EaseOut
│    최대 콤보 42           │
│    P:30 G:10 G:3 M:0      │
│                          │
│    (최고 기록!)            │  ← 신기록일 때만
│                          │
│  [ 재도전 ]   [ 홈 ]      │
└──────────────────────────┘
```

애니메이션:
- **별 pop**: 각 별 `Image` scale 0 → 1.2 → 1.0, 0.2s 간격 순차. `IEnumerator` 코루틴 + `Mathf.Lerp`/`Mathf.SmoothStep`. 파티클·Particle System 없음.
- **점수 카운트업**: 1.5초 동안 `Mathf.Lerp(0, finalScore, t)` + `Mathf.Floor`로 정수 표시, EaseOut(`1 - (1-t)^2`).
- 콤보·판정 breakdown은 즉시 표시.
- "최고 기록!" 라벨: `UserPrefs.TrySetBest`가 true를 리턴하면 표시.

버튼:
- **재도전**: `ScreenManager.Replace(Screen.Gameplay)` — `SongSession`은 그대로이므로 같은 곡/난이도 재시작. **주의: W3의 `CompletionPanel.Restart`는 `SceneManager.LoadScene`으로 씬 전체 재로드를 했지만, Single-Scene 전환으로 바뀌면서 씬 재로드는 사라진다.** 대신 `GameplayController`에 `ResetAndStart()` 진입점을 추가해 다음을 수행:
  1. 스폰된 모든 노트 GameObject 파괴 (`NoteSpawner`의 활성 리스트 순회)
  2. `NoteSpawner` 내부 상태 초기화 (인덱스, `AllSpawned`, `LastSpawnedHitMs` 등)
  3. `JudgmentSystem`/`ScoreManager` 점수·콤보 0으로 리셋
  4. `HoldTracker` 진행 중 홀드 정리
  5. `ChartLoader` 재로드 (이미 메모리에 있으면 재사용 가능)
  6. `AudioSyncManager.StartSilentSong()` 재호출 (`songStartDsp` 재세팅)
  7. `completed = false` / `playing = true` 재설정
- **홈**: `ScreenManager.Replace(Screen.Main)`. `GameplayRoot`는 비활성화되지만 내부 상태는 정리하지 않음 (다음 진입 시 어차피 `ResetAndStart`가 호출됨).

### 6.4 별 스프라이트 생성 (carry-over #4)

`SceneBuilder.EnsureStarSprite(filled)` — 64×64 RGBA 텍스처에 오각별 그리기. 절차적:

```
정점 5개 (중심 기준 반지름 30, 시작각 -90°, 72° 간격)
오각별 = 정점 순서를 2칸씩 건너뛰며 연결 (0→2→4→1→3→0)
내부 삼각분할로 채우기 (filled=true) 또는 외곽선만 (filled=false)
색상: filled=금색 (1.0, 0.85, 0.2, 1), outline=회색 (0.4, 0.4, 0.4, 0.6)
```

생성된 PNG는 `Assets/Sprites/star_filled.png`, `Assets/Sprites/star_empty.png`로 저장하고 Sprite import 세팅. `WhiteSprite`와 동일 패턴.

Results·Main 카드 별 표시는 `star_filled`/`star_empty`를 Image에 스왑.

### 6.5 Locked 썸네일 스프라이트

`thumbs/locked.png` — 128×128 회색 배경 + 🔒 이모지 대신 간단한 자물쇠 실루엣(절차 생성) 또는 단색+물음표 텍스트. SceneBuilder가 빌드 시 생성해 `StreamingAssets/thumbs/locked.png`에 복사.

---

## 7. 흐름 요약 (상태 전이)

```
(앱 시작)
    ↓ ScreenManager Awake → Replace(Main)
[Main]
    ↓ 톱니 탭                  ↓ 카드 난이도 탭
[Main + Settings]              SongSession 세팅
    ↓ ✕ / Back                  ↓
[Main]                         [Gameplay] (calibration 필요 시 오버레이 먼저)
                                ↓ ⏸ 또는 Back           ↓ 완주
                               [Gameplay + Pause]      [Results]
                                ↓ 계속하기  ↓ 메인으로    ↓ 재도전   ↓ 홈
                               [Gameplay]  [Main]      [Gameplay] [Main]
```

Settings의 "오디오 다시 맞추기":
```
[Main + Settings] → Settings.Finish → Calibration.Begin(onDone: ShowSettings)
    → [Main + Calibration] → Calibration.Finish → [Main + Settings]
```

---

## 8. carry-over 통합

### 8.1 #4 별 ASCII → 스프라이트
§6.4에 명세. 기존 `CompletionPanel.starsText` (Text 컴포넌트의 `**-` 문자열)는 `ResultsScreen`의 `Image[3]`으로 대체되므로 자연 소멸.

### 8.2 #5 로케일 통일
§3.4 `UIStrings`에 한국어로 일괄. Calibration 기존 한국어 문구도 UIStrings 경유로 교체. Completion 영어 5개는 ResultsScreen 신규 작성 시 자동으로 한국어화.

### 8.3 #6 네임스페이스 통일
§4.1에 규칙. 리팩터링은 **새 파일 작성 전**에 먼저 수행 — 새 파일들이 올바른 네임스페이스로 태어나게. W3 커밋이 클린한 상태이므로 네임스페이스 전용 커밋 1개로 분리.

### 8.4 #7 OverlayBase 추상화
§3.1에 계약. CalibrationController를 먼저 상속으로 변환 (기존 동작 유지 검증), 그 뒤 SettingsScreen / PauseScreen / ResultsScreen을 같은 패턴으로 신규 작성.

---

## 9. 테스트 계획 (EditMode)

W3의 68개 테스트를 전부 유지하고, W4에서 **추가**한다:

| 테스트 스위트 | 케이스 수 | 대상 |
|---|---|---|
| `SongCatalogTests` | 4 | 정상 파싱 / 빈 `songs` / 필수 필드 누락 / `TryGet` 존재·부재 |
| `UserPrefsTests` | 6 | SFX·NoteSpeed 기본값·round-trip / Stars·Score round-trip / 마이그레이션 (legacy 키 있음 → prefixed + 레거시 제거) / 마이그레이션 멱등성 (이미 완료) |
| `ScreenManagerTests` | 4 | `Replace` 동작 / 오버레이 Show·Hide 독립성 / 풀스크린 전환 시 오버레이 자동 숨김 / `Current` 게터 |
| `AudioSyncPauseTests` | 3 | Pause 후 `SongTimeMs` 동결 / Resume 후 연속성 (허용 오차 ±2 dspFrame) / Pause/Resume 반복 멱등. **EditMode에선 AudioListener/AudioSource가 실제 재생되지 않으므로 `songStartDsp` 산술만 검증**; 테스트는 `AudioSettings.dspTime` 직접 주입 대신 `AudioSyncManager`에 테스트 전용 `TimeSource` 주입 훅을 두거나, `pauseStartDsp`/`songStartDsp` internal setter로 상태를 조립한 뒤 `SongTimeMs` 계산을 검증 |
| `OverlayBaseTests` | 2 | `Awake` 후 비활성 / `Show` `Finish` 이벤트 훅 호출 순서 |

총 W4 추가 19 테스트. 합계 87 테스트 목표.

PlayMode 테스트는 W3과 마찬가지로 스코프 외.

---

## 10. 디바이스 검증 체크리스트

W4 완료 시 Galaxy S22에서 다음을 확인:

1. **첫 실행 경로**: 앱 시작 → Main 표시 → 카드 탭 → Calibration(신규 설치 기준) → Gameplay → 완주 → Results (별 애니+카운트업) → Retry → Gameplay → Home → Main.
2. **설정 경로**: Main → ⚙ → Settings → SFX 슬라이더 움직여 효과음 크기 변화 귀로 확인 → NoteSpeed 슬라이더 변경 → 재생 시 속도 차이 체감 → "오디오 다시 맞추기" → Calibration 재실행 → Settings 복귀 → ✕ → Main.
3. **Pause 경로**: Gameplay 진행 중 ⏸ → 노트 정지 / 오디오 정지 → 계속하기 → 박자 끊김 없이 재개. 같은 테스트를 Android Back으로 반복.
4. **종료 경로**: Main에서 Back 두 번 → `Application.Quit()`.
5. **기록 저장**: Easy 2성 클리어 → Main 복귀 시 해당 카드에 별 2개 표시. Retry로 3성 달성 → Results에 "최고 기록!" 라벨 + Main 카드 3성 반영.
6. **마이그레이션**: W3 빌드로 설치해서 캘리브 값 저장 → W4 빌드로 업데이트 설치 → 캘리브 화면 스킵 (오프셋 유지) + PlayerPrefs 키 확인 (adb shell로 `com.funqdev.keyflow` 디렉터리 직접 덤프하거나 간접 검증: Settings에서 오프셋 수치 표시 추가하면 쉬움, 단 MVP엔 수치 노출 없음 — 동작으로만 확인).
7. **APK 크기**: <40MB 유지 (W3 33MB, W4는 썸네일 4장 placeholder + 별 스프라이트로 +1~2MB 예상).
8. **60 FPS**: W3의 59.8 FPS가 W4 UI 추가 후에도 유지되는지 측정. ScrollRect는 Android에서 가벼움.

---

## 11. 리스크

| 리스크 | 대응 |
|---|---|
| dspTime Pause/Resume 동기 실패 (Galaxy S22 특정 이슈) | `AudioSyncPauseTests`로 단위 검증. 디바이스 테스트에서 드리프트 발견 시 `AudioListener.pause` 대신 수동 루프 보류 방식 고려 |
| ScrollRect가 InputSystem과 충돌 (W3에서 `EventSystem` + `InputSystemUIInputModule` 이슈 기억) | SceneBuilder가 `InputSystemUIInputModule` 설정 유지 확인. 카드 버튼 `Button` + `GraphicRaycaster` 동작 검증 |
| 네임스페이스 리팩터링 후 `.meta` 파일 및 직렬화 필드 참조 파괴 | `namespace`만 바꾸고 클래스명은 유지. Unity `SerializedObject` 참조는 GUID 기반이라 네임스페이스 변경 영향 없음 (검증 필요) |
| PlayerPrefs 마이그레이션이 여러 번 실행되는 경우 | `KeyFlow.Migration.V1.Done` 플래그로 멱등성 보장. 테스트로 보증 |
| ResultsScreen 애니메이션이 끝나기 전 Retry 탭 | 버튼은 애니메이션 완료 후 활성화(`interactable = false` → 1.7초 후 `true`) |

---

## 12. 일정 추정 (개략)

W3이 16 task + 3 device-fix로 1.5일 소요. W4는 UI 작업 비중이 높아 비슷하거나 살짝 긴 1.5~2일 예상.

예상 태스크 수: **13~16** (Plan 4에서 확정).

주요 마일스톤:
- M4.1 (~30%): 네임스페이스·OverlayBase·UserPrefs·UIStrings 정리 (새 파일 기반 완성)
- M4.2 (~60%): SongCatalog + MainScreen + SongCardView (곡 선택 흐름)
- M4.3 (~80%): SettingsScreen + PauseScreen + AudioSyncManager Pause
- M4.4 (~100%): ResultsScreen + 별 스프라이트 + 디바이스 검증

---

## 13. 참고

- 상위 스펙: v2 `2026-04-20-keyflow-mvp-v2-4lane-design.md` §4 / §12 / M-07~M-10
- W3 회고 carry-over 원문: `~/.claude/projects/C--dev-unity-music/memory/project_w4_carryover.md`
- W3 완료 리포트: `docs/superpowers/reports/2026-04-20-w3-completion.md`
- 팀 에이전트 패턴(W3 검증): `~/.claude/projects/C--dev-unity-music/memory/feedback_team_agent_pattern.md` — W4에도 적용
