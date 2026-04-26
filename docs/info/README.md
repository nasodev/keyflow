# KeyFlow 프로젝트 가이드 (한국어)

KeyFlow 저장소를 처음 보는 사람을 위한 한국어 입문 문서 모음이다. **30분 안에 전체 그림을 파악**하고, 그 뒤로는 필요한 시스템 문서로 깊이 들어갈 수 있도록 구성했다.

## 어떤 순서로 읽을까

### 처음 오는 사람 (15분 코스)
1. **[01-프로젝트-개요.md](./01-프로젝트-개요.md)** — 어떤 게임인지, 왜 만드는지, 지금 어디까지 왔는지
2. **[02-기술-스택.md](./02-기술-스택.md)** — Unity 6 + C# + 외부 자산이 어떻게 쓰이는지
3. **[03-디렉터리-구조.md](./03-디렉터리-구조.md)** — 폴더와 파일이 어디에 있는지

### 코드를 만지러 온 사람 (추가 30분)
4. **[04-아키텍처.md](./04-아키텍처.md)** — 어셈블리 분리, 데이터 플로우, 화면 전환 다이어그램
5. **[05-게임플레이-시스템.md](./05-게임플레이-시스템.md)** — 판정·노트·홀드·오디오·캘리브레이션 상세
6. **[06-UI-시스템.md](./06-UI-시스템.md)** — 화면 4개 + 오버레이 3개 + 피드백 디스패처
7. **[07-에디터와-빌드.md](./07-에디터와-빌드.md)** — `KeyFlow/` 메뉴, Unity 배치 모드, APK 배포

### 도구·테스트 (필요할 때만)
8. **[08-채보-파이프라인.md](./08-채보-파이프라인.md)** — `tools/midi_to_kfchart/` Python 도구
9. **[09-테스트-가이드.md](./09-테스트-가이드.md)** — EditMode 테스트 31개 + 테스트 훅 컨벤션

## 한 줄 요약

| 파일 | 한 줄 요약 |
|------|----------|
| [01-프로젝트-개요.md](./01-프로젝트-개요.md) | KeyFlow는 Magic Piano 스타일 4레인 Android 리듬게임 — 8주 솔로 MVP의 W6까지 진행한 상태 |
| [02-기술-스택.md](./02-기술-스택.md) | Unity 6000.3.13f1 / IL2CPP / arm64-v8a + Newtonsoft JSON + Salamander Piano(CC-BY) + Mutopia MIDI + Python(mido) |
| [03-디렉터리-구조.md](./03-디렉터리-구조.md) | 모든 폴더와 주요 파일을 트리로 정리 — "이건 어디 있나" 찾기용 |
| [04-아키텍처.md](./04-아키텍처.md) | 단일 `GameplayScene.unity` + 어셈블리 3개(Runtime / Editor / Tests.EditMode) + `SceneBuilder` 단일 진실 소스 |
| [05-게임플레이-시스템.md](./05-게임플레이-시스템.md) | `AudioSyncManager.dspTime` 기준 판정 + `HoldStateMachine` + 다중 피치 샘플 풀 + `CalibrationCalculator` median/MAD |
| [06-UI-시스템.md](./06-UI-시스템.md) | `ScreenManager` 화면 전환 + `OverlayBase` 모달 + `FeedbackDispatcher`로 햅틱·파티클·텍스트 일괄 디스패치 |
| [07-에디터와-빌드.md](./07-에디터와-빌드.md) | `KeyFlow/Build W4 Scene`이 씬을 재생성하고 `Build APK`로 출시 빌드 — 모두 batch-mode CLI도 지원 |
| [08-채보-파이프라인.md](./08-채보-파이프라인.md) | MIDI → 8단계 파이프라인 → `.kfchart` JSON. 튜닝은 YAML의 `target_nps`로만 |
| [09-테스트-가이드.md](./09-테스트-가이드.md) | `KeyFlow.Tests.EditMode`(NUnit) + `[InternalsVisibleTo]` + `ITimeSource` seam — PlayMode는 미존재 |

## 이 문서들의 한계

- **시점**: 2026-04-24(W6 SP12) 기준이다. 그 이후의 구현 변경은 `docs/superpowers/reports/`의 최신 완료 보고서를 함께 봐야 한다.
- **AI 에이전트용 요약본**: 빌드/테스트 명령 빠른 참조는 [`CLAUDE-KO.md`](../../CLAUDE-KO.md)(한국어) 또는 [`CLAUDE.md`](../../CLAUDE.md)(영문)에 더 압축적으로 정리되어 있다.
- **시간순 결정 컨텍스트**: 각 기능이 왜 그렇게 됐는지 더 깊이 보려면 `docs/superpowers/specs/`(설계) → `plans/`(실행 계획) → `reports/`(완료 보고)를 SP 단위로 추적하면 된다.

## 빠른 점프

- 게임 소개 + 빌드 명령 (영문): [`README.md`](../../README.md)
- 채보 도구 사용법 (영문): [`tools/midi_to_kfchart/README.md`](../../tools/midi_to_kfchart/README.md)
- 라이선스: [`LICENSE`](../../LICENSE), Salamander 피아노 CC-BY: [`Assets/StreamingAssets/licenses/salamander-piano-v3.txt`](../../Assets/StreamingAssets/licenses/salamander-piano-v3.txt)
