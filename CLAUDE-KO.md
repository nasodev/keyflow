# CLAUDE-KO.md

이 파일은 이 저장소에서 작업하는 Claude Code(claude.ai/code)에 대한 안내서다. (영문 원본: `CLAUDE.md`)

## 프로젝트 한눈에 보기

KeyFlow는 Magic Piano 스타일의 4레인 Android 리듬 게임이며, **Unity 6000.3.13f1**(IL2CPP, arm64-v8a, 타깃: Galaxy S22 / Android 16) 기반이다. 세로 방향이고, 단일 `GameplayScene.unity`가 SetActive 토글로 모든 화면을 구동한다. 체감 입력→오디오 레이턴시 목표는 50–80 ms.

## Unity Editor 메뉴 항목 (모두 `KeyFlow/` 하위)

| 메뉴 | 메서드 | 용도 |
|---|---|---|
| `KeyFlow/Build W4 Scene` | `SceneBuilder.Build` | `Assets/Scenes/GameplayScene.unity`를 처음부터 **재생성**한다. 씬 와이어링의 단일 진실 소스(single source of truth) — `.unity` 파일을 직접 수정하지 말고, `SceneBuilder.cs`를 고친 뒤 재실행할 것. |
| `KeyFlow/Build Calibration Click` | `CalibrationClickBuilder.Build` | `Assets/Audio/calibration_click.wav`를 결정적(seeded RNG → 바이트 단위 동일 산출물)으로 생성. 최초 1회 세팅. |
| `KeyFlow/Build Feedback Assets` | `FeedbackPrefabBuilder.Build` | hit/miss 파티클 prefab + `FeedbackPresets.asset` 생성. 최초 1회 세팅. |
| `KeyFlow/Build APK` | `ApkBuilder.Build` | Release APK → `Builds/keyflow-w<X>-sp<Y>.apk`. **파일명이 하드코딩**되어 있으므로 SP 마일스톤마다 번호를 올려야 한다(`Assets/Editor/ApkBuilder.cs` 참고). |
| `KeyFlow/Build APK (Profile)` | `ApkBuilder.BuildProfile` | Development + profiler + debug APK. |
| `KeyFlow/Apply Android Icon (SP9)` | `SP9IconSetter.Apply` | `Assets/Textures/icon.png`를 모든 Android Legacy 아이콘 슬롯에 채워 넣는다. 재실행 안전. |

최초 설정 순서: Calibration Click → Feedback Assets → Build W4 Scene → Build APK.

## 배치 모드 CLI (CI / 헤드리스)

```bash
# 씬 재생성
"<UnityEditor>" -batchmode -nographics -projectPath . \
  -executeMethod KeyFlow.Editor.SceneBuilder.Build -quit

# Release APK (씬이 미리 빌드되어 있어야 함)
"<UnityEditor>" -batchmode -nographics -projectPath . \
  -executeMethod KeyFlow.Editor.ApkBuilder.Build -quit

# AudioImporter postprocessor 설정이 직렬화되도록 피아노 WAV 강제 재임포트
"<UnityEditor>" -batchmode -nographics -projectPath . \
  -executeMethod KeyFlow.Editor.PianoSampleImportPostprocessor.ForceReimportPianoSamples -quit

# EditMode 테스트 → Builds/test-results.xml
"<UnityEditor>" -batchmode -nographics -projectPath . \
  -runTests -testPlatform EditMode -testResults Builds/test-results.xml
```

**치명적 주의**: `-runTests`와 `-quit`를 **함께 전달하지 말 것** — 둘 다 지정하면 Unity가 테스트 러너를 건너뛴다. 다른 모든 `-executeMethod` 호출은 반드시 `-quit`를 붙여야 한다.

연결된 기기에 설치:

```bash
adb install -r Builds/keyflow-w6-sp12.apk
```

## Python 채보 파이프라인 (tools/midi_to_kfchart/)

Mutopia PD MIDI를 `Assets/StreamingAssets/charts/` 내 `.kfchart` JSON으로 변환.

```bash
cd tools/midi_to_kfchart
python -m venv .venv && source .venv/bin/activate   # Windows: .venv\Scripts\activate
pip install -r requirements.txt
pytest -q                                            # 유닛 테스트
python midi_to_kfchart.py --batch batch_w6_sp2.yaml  # 출시된 전체 채보 재생성
```

`.kfchart` 파일에 직접 한 수정은 재실행 시 사라진다. 튜닝은 JSON을 건드리지 말고 배치 YAML의 `target_nps`로 조정한다.

## 개인 곡 (저작권 보호 자료)

저장소는 **공개** 곡(공개 도메인 또는 배포 라이선스가 있어 commit 안전)과 **개인** 곡(저작권 보호 자료, 로컬 머신에만 보관)을 구분한다. 분리는 **디렉터리 컨벤션**으로 강제된다:

| 공개 | 개인 (gitignored) |
|---|---|
| `midi/` (`personal/`이 아닌 모든 하위 디렉터리) | `midi/personal/` |
| `Assets/StreamingAssets/charts/*.kfchart` | `Assets/StreamingAssets/charts/personal/*.kfchart` |
| `Assets/StreamingAssets/thumbs/*.png` | `Assets/StreamingAssets/thumbs/personal/*.png` |
| `Assets/StreamingAssets/catalog.kfmanifest` | `Assets/StreamingAssets/catalog.personal.kfmanifest` |
| `tools/midi_to_kfchart/batch_*.yaml` | `tools/midi_to_kfchart/personal/batch_*.yaml` |

**개인 곡 추가:**

1. 원본 MIDI를 `midi/personal/`에 둔다.
2. `tools/midi_to_kfchart/personal/` 아래의 배치 YAML에 항목을 추가한다 (위치 자체가 개인 라우팅을 트리거 — YAML 플래그 불필요).
3. `python midi_to_kfchart.py --batch tools/midi_to_kfchart/personal/<파일>.yaml` — 출력은 `charts/personal/`로 간다.
4. 썸네일 PNG를 `thumbs/personal/`에 둔다.
5. `Assets/StreamingAssets/catalog.personal.kfmanifest`에 곡 항목을 추가한다 (없으면 `version: 1` + `songs: []` 템플릿으로 새로 만듦). `"thumbnail": "thumbs/personal/<파일>.png"`로 지정한다.

이게 끝이다. `git status`에 새 추적 파일이 나타나지 않는다. 다섯 개의 `.gitignore` 디렉터리 규칙이 모두 커버한다.

**런타임:** `SongCatalog.LoadAsync`가 `catalog.kfmanifest`(필수)를 읽고 `catalog.personal.kfmanifest`(선택, 없으면 오버레이 없음)를 머지한다. 개인 항목은 `isPersonal=true`로 태깅되며 `ChartLoader`는 이들의 차트를 `charts/personal/<id>.kfchart`로 해석한다.

## 코드 아키텍처

### 어셈블리

C# 어셈블리 3개 (세 개의 `.asmdef` 참고):

- **`KeyFlow.Runtime`** (`Assets/KeyFlow.Runtime.asmdef`) — 모든 게임플레이 코드, `KeyFlow` 네임스페이스 + 서브모듈(`KeyFlow.Charts`, `KeyFlow.Calibration`, `KeyFlow.UI`, `KeyFlow.Feedback`).
- **`KeyFlow.Editor`** (`Assets/Editor/KeyFlow.Editor.asmdef`) — Editor 전용 툴링(메뉴 항목, 에셋 빌더, 임포트 postprocessor, Android post-gradle 훅).
- **`KeyFlow.Tests.EditMode`** (`Assets/Tests/EditMode/KeyFlow.Tests.EditMode.asmdef`) — NUnit EditMode 스위트 (135+ 테스트), `UNITY_INCLUDE_TESTS`로 게이팅.

런타임 코드는 `[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("KeyFlow.Tests.EditMode")]`(선언 위치: `JudgmentSystem.cs`)을 통해 internal 멤버를 테스트에 노출한다.

### 게임플레이 데이터 플로우

`GameplayScene`은 이 컴포넌트들을 파이프라인으로 연결한다. 의존성은 `SceneBuilder`가 설정하는 `[SerializeField]` 참조이며, 손으로 연결하지 않는다:

```
TapInputHandler ──onLaneTap──►  JudgmentSystem ──OnJudgmentFeedback──►  FeedbackDispatcher
                                     ▲                                  ├──► HapticService (Android VibrationEffect)
                                     │                                  ├──► ParticlePool
NoteSpawner ──RegisterPendingNote──┘                                    └──► JudgmentTextPool
     ▲
     │reads ChartDifficulty
ChartLoader (JSON → ChartData)          HoldTracker ──drives──► HoldStateMachine (Spawned→Holding→Completed/Broken)
     ▲                                       │
AudioSyncManager (owns songStart dspTime)    └─►  AudioSamplePool.PlayForPitch (16채널 라운드로빈, 반음 단위 피치 시프트된 Salamander 샘플)
     ▲
CalibrationController → UserPrefs.CalibrationOffsetMs
```

`GameplayController.ResetAndStart()`가 곡 단위 시작을 지휘한다: 채보 로드 → (옵션) 캘리브레이션 → 카운트다운 → `audioSync.StartSilentSong()`. 완료 판정은 `Update()`에서 폴링하며 `SongTimeMs`가 마지막 노트의 예정 종료 + Good 윈도를 넘었는지로 판단하고, `ScreenManager.Instance.Replace`로 `Results`로 전환한다.

### 타이밍 모델

**`AudioSyncManager`**가 `AudioSettings.dspTime`(`Time.time`이 아님)으로 권위 있는 곡 시계를 소유한다. 모든 게임플레이 시간은 `songStartDsp` 기준의 **ms**로 표현되며, `GameTime.GetSongTimeMs(nowDsp, songStartDsp, calibOffsetSec)`에서 계산된다. 시간을 읽는 컴포넌트는 전부 `audioSync.SongTimeMs`를 호출하고 `IsPlaying && !IsPaused` 가드를 통과해야 한다.

`AudioSyncManager.TimeSource`는 테스트 seam이다: EditMode 테스트가 수동 `ITimeSource` 시계를 주입해 Unity 오디오 스레드 없이도 `DspTime`을 결정적으로 진행시킬 수 있게 한다.

캘리브레이션 오프셋은 `UserPrefs.CalibrationOffsetMs`(PlayerPrefs 백엔드)에 저장되고 `audioSync.CalibrationOffsetSec`로 적용된다. `UserPrefs.MigrateLegacy()`는 최초 실행 시 W4 이전의 `CalibOffsetMs` 키를 마이그레이션한다.

### 씬 구성은 Editor 전용

SP7에서 빌드 후 씬 와이어링을 전부 `SceneBuilder.cs`로 통합해 단일 진실 소스로 만들었다. 이전에는 두 번째 단계였던 "W6 Samples Wireup"을 빠뜨리면 "모든 레인이 같은 피치로 재생"되는 회귀가 W6 동안 두 번 발생했다. **씬 와이어링이 필요한 새 컴포넌트를 추가할 때**는 `SceneBuilder.cs`에 `BuildX` 메서드를 추가하고 `Build`에서 호출되도록 해라 — 새 `[MenuItem]` 헬퍼 툴을 만들지 말 것.

텍스처/오디오 임포트 설정은 AssetPostprocessor(`BackgroundImporterPostprocessor`, `PianoSampleImportPostprocessor`)가 강제한다. 덕분에 `.meta`가 재생성될 수 있는 fresh checkout에서도 설정이 살아남는다. 대상 에셋의 텍스처/오디오 임포트 설정을 Inspector에서 직접 수정하지 말 것 — 다음 임포트 때 덮어써진다.

### 채보 포맷 (`.kfchart`)

Newtonsoft JSON, `ChartLoader.ParseJson`이 로드한다. 난이도별 노트 배열이며 각 노트는 `{t, lane (0-3), pitch (36-83 clamp), type ("TAP"|"HOLD"), dur}` 구조다. `ChartLoader.Validate`가 강제하는 규칙: `TAP.dur=0`, `HOLD.dur>0`, 노트는 `t` 오름차순, `totalNotes == notes.Count`. 이 검증은 중요한 역할을 하므로 테스트 없이 완화하지 말 것.

채보는 `Application.streamingAssetsPath/charts/<songId>.kfchart`에서 로드된다. Android에서는 StreamingAssets가 APK 내부에 있어 `UnityWebRequest`가 필요하다. 로더는 `UNITY_ANDROID && !UNITY_EDITOR`로 분기한다.

### 화면 모델

`ScreenManager`(`AppScreen.Start | Main | Gameplay | Results`)가 상호 배타적인 루트 GameObject 4개 + 오버레이 3개(`Settings`, `Pause`, `Calibration`)를 관리한다. **씬은 하나뿐이다.** 전환은 `Replace(target)`이며 `SetActive`를 토글하고 `OnReplaced` 이벤트를 발화한다. 뒤로가기는 Android 하드웨어 `Escape`로 `ScreenManager.Update`에서 처리하고, `Start`에서 더블 프레스 시 앱이 종료된다.

StartScreen BGM은 Unity의 네이티브 `SetActive` 라이프사이클 전파를 이용한다: `startRoot.SetActive(false)` → `StartScreen.OnDisable` → `bgmSource.Stop()`. 명시적인 화면 간 커플링이 없고 — 이 패턴은 의도된 것이니 매니저로 대체하지 말 것.

### 테스트 훅 컨벤션

런타임 컴포넌트는 `#if UNITY_EDITOR || UNITY_INCLUDE_TESTS` 블록 안에 EditMode 전용 테스트 seam을 노출한다. 두 가지 패턴:

- `internal void SetDependenciesForTest(...)` — 평소 SceneBuilder가 `SerializeField`로 연결하는 의존성을 주입.
- `internal void TickForTest()` / `InvokeXForTest(...)` — `Update` 또는 private 메서드를 결정적으로 구동.

기능을 추가할 때 필드를 `public`으로 열거나 리플렉션을 쓰지 말고 이 패턴을 따를 것.

## 작업 메모

- `Assets/Scenes/GameplayScene.unity`는 git에 들어있지만 **생성물**이다. 씬에 영향을 주는 코드가 바뀔 때마다 `KeyFlow/Build W4 Scene`으로 재생성하고 결과 diff를 커밋할 것.
- `docs/superpowers/{specs,plans,reports}/`에는 스프린트별 설계 문서, 플랜, 완료 리포트(SP별 하나씩)가 있다. W6 SP 스타일 작업을 시작하기 전에 가장 최근의 spec + 완료 리포트 쌍을 읽고 맥락을 파악할 것.
- 프로젝트는 내장 패키지(`Packages/manifest.json` 참고)를 특정 버전으로 고정한다 — Input System 1.19.0, Test Framework 1.6.0, Newtonsoft JSON 3.2.1. 가볍게 업그레이드하지 말 것. SceneBuilder + InputSystem 와이어링이 이 버전을 전제한다.
- Android VIBRATE 권한은 `AddVibratePermission : IPostGenerateGradleAndroidProject`가 gradle 생성 시점에 주입한다. `AndroidManifest.xml` 오버라이드 방식이 아니다.
