# KeyFlow W6 Sub-Project 2 Design — Four-Song Content Pack

**Date:** 2026-04-22
**Branch:** `claude/suspicious-hodgkin-52399d` (worktree)
**Parent MVP spec:** `docs/superpowers/specs/2026-04-19-keyflow-mvp-design.md`
**Depends on:** W5 pipeline (`tools/midi_to_kfchart/`) + W6 SP1 multi-pitch samples (merged `b23c50c`)
**Scope priority:** W6 #2 (content) — #3–6 deferred to later sub-projects.

---

## 1. Goal

KeyFlow MVP는 5곡을 수록하도록 스펙됐으나 (MVP §3), 현재 `catalog.kfmanifest`에는 1곡(Für Elise)만 실제 차트가 있고 나머지 4슬롯은 placeholder다. 본 sub-project는 나머지 4곡 **Ode to Joy, Canon in D, Clair de Lune, The Entertainer**를 각 Easy/Normal 두 난이도로 제작해 MVP 콘텐츠를 완성한다.

**코드 변경 없음** — W5 파이프라인, W6-1 다중 피치 오디오, `SongCatalog`/`MainScreen`/`SongCardView`가 모두 완성되어 있어 asset + config 추가만으로 기능 확장이 완결된다.

---

## 2. Scope

### In scope
- 4곡 × 2난이도 = 8 차트를 `batch_w6_sp2.yaml` 단일 배치로 생성
- 4개 썸네일 PNG (Für Elise 기존 스타일 매칭)
- `catalog.kfmanifest` 업데이트 — 5곡 전원 unlocked
- Mutopia PD/CC0 MIDI를 1차 소스로 사용
- Device playtest: 5곡 × 2난이도 = 10 run 완주

### Out of scope
- 프로파일러 패스 (W6 #3)
- 캘리브레이션 전용 클릭 샘플 (W6 #4)
- UI 폴리시, 스타 스프라이트, 에러 토스트 (W6 #5)
- 2nd 디바이스 확장 테스트 (W6 #6)
- BGM 오디오 스트림 (v2 pivot으로 post-MVP)
- `licenseInfo` JSON 필드 추가 (스펙 doc에만 존재; Credits 화면 확장 시 재논의)
- 기존 Für Elise 차트/썸네일 수정 (device에서 검증된 상태, 건드리지 않음)

---

## 3. Approach

**단일 배치 YAML (Approach B)** — `tools/midi_to_kfchart/batch_w6_sp2.yaml` 하나에 4 songs × 2 difficulties = 8 entries. `midi_to_kfchart.py --batch` 1회 실행으로 8개 차트 병합 출력 (곡당 `.kfchart` 1파일, `charts.EASY`/`charts.NORMAL` 맵).

**Rejected 대안**:
- **A. 순차** (곡 1개씩 pipeline + 검증) — 안전하지만 세션 소요 2-3배, 이득 미미.
- **C. 병렬 subagent** — I/O 바운드 작업에 오버엔지니어링, YAML 공유 자원에 Git conflict 위험.

---

## 4. Data flow

```
Mutopia CC0/PD MIDI (4)
    → tools/midi_to_kfchart/midi_sources/{song_id}.mid (gitignored 대형 input)
    → midi_to_kfchart.py --batch batch_w6_sp2.yaml
    → Assets/StreamingAssets/charts/{song_id}.kfchart (4 파일, 각 EASY+NORMAL)
    → Assets/StreamingAssets/thumbs/{song_id}.png (4 파일, PIL 자체 생성)
    → Assets/StreamingAssets/catalog.kfmanifest (placeholder 4개 → 실제 메타)
    → Unity build → 5곡 × 2난이도 device playtest
```

---

## 5. MIDI sources

**허용 라이선스**: Mutopia Project의 `Public Domain` 또는 `CC0`만. CC-BY / CC-BY-SA 제외 (재배포 조건 복잡).

### 부모 MVP §11.2와의 관계 (의도적 재해석)

MVP §11.2 문구는 *"인터넷 무료 MIDI 파일 사용 금지 (재배포 라이선스 불명확)"* + *"자체 MIDI 시퀀싱"*을 규정한다. 본 sub-project는 **자체 시퀀싱 대신 Mutopia PD/CC0 MIDI**를 1차 소스로 채택한다. 근거:

- MVP §11.2의 금지 대상은 *"라이선스 불명확"한 일반 무료 MIDI 사이트*이며, Mutopia는 piece별로 PD/CC0/CC-BY 라이선스를 명시하는 전문 아카이브다.
- 본 spec은 **PD/CC0로 명시된 piece만** 허용하도록 필터링하여 MVP §11.2의 *진의*(재배포 리스크 제거)는 유지.
- 자체 시퀀싱은 solo 개발 8주 MVP 스케줄에서 곡당 수 시간의 음악 저작 작업을 요구 — ROI 미흡.
- 사후 70년 경과 PD 작곡에 대한 PD MIDI는 공법적으로 추가 리스크 없음.

Fallback (Mutopia 부재 시) 경로에서는 자체 시퀀싱을 별도 sub-project로 재검토한다.

| # | 곡 | 작곡가 | Mutopia 검색 가이드 |
|---|---|---|---|
| 1 | Ode to Joy | Beethoven | "Symphony No. 9" 4악장 합창 주제부, 또는 단독 편곡본 |
| 2 | Canon in D | Pachelbel | 3-성부 카논, PD 여러 편곡 존재 |
| 3 | Clair de Lune | Debussy | Suite bergamasque 3번, Debussy 1918 사망 → PD |
| 4 | The Entertainer | Joplin | Joplin 1917 사망 → PD |

(번호는 본 sub-project 내 작업 순서; MVP §3 곡 번호와 무관.)

### Verification procedure (implementation 중)
1. 각 곡의 Mutopia piece 페이지 방문 (WebFetch)
2. **License 필드가 "Public Domain" 또는 "Creative Commons Zero"인 piece만 선택**
3. `.mid` 다운로드 → `tools/midi_to_kfchart/midi_sources/{song_id}.mid`
4. Commit message에 Mutopia piece ID / URL / arranger / license 기록

### Fallback (부재 시)
MVP §3 지정 곡 중 Mutopia에 PD/CC0 MIDI가 부재하는 곡 발생 시:
- Implementer는 해당 곡을 scope에서 제거하고 보고
- 본 sub-project는 부분 완료 (3곡 또는 2곡)로 종료
- 완료 리포트에 플래그 → 사용자가 자체 시퀀싱 경로를 별도 sub-project로 결정

### License 정책
- **작곡 저작권**: 4곡 전원 사후 70년 경과 PD 확정 (Beethoven 1827, Pachelbel 1706, Debussy 1918, Joplin 1917).
- **편곡/시퀀싱 저작권**: Mutopia PD/CC0 소스 사용 시 문제 없음. CC-BY 편곡은 재배포 크레딧 의무로 복잡 → 제외.
- **녹음(샘플) 저작권**: Salamander Grand Piano V3 CC-BY 3.0 (W6-1에서 Credits 반영 완료).
- **kfchart 파일 메타**: 본 sub-project에서는 `licenseInfo` 필드 추가 **안 함** (YAGNI). 출처는 commit message + 본 spec doc로만 추적. 향후 in-app Credits 확장 시 추가 결정.

---

## 6. Batch YAML schema

**`tools/midi_to_kfchart/batch_w6_sp2.yaml`** (신규 체크인):

```yaml
defaults:
  out_dir: Assets/StreamingAssets/charts/

songs:
  - song_id: beethoven_ode_to_joy
    midi: midi_sources/ode_to_joy.mid
    title: "Ode to Joy"
    composer: "Beethoven"
    bpm: 120
    duration_ms: 120000
    difficulties:
      EASY:   { target_nps: 1.5 }
      NORMAL: { target_nps: 3.0 }

  - song_id: pachelbel_canon_in_d
    midi: midi_sources/canon_in_d.mid
    title: "Canon in D"
    composer: "Pachelbel"
    bpm: 60
    duration_ms: 120000
    difficulties:
      EASY:   { target_nps: 1.8 }
      NORMAL: { target_nps: 3.2 }

  - song_id: debussy_clair_de_lune
    midi: midi_sources/clair_de_lune.mid
    title: "Clair de Lune"
    composer: "Debussy"
    bpm: 66
    duration_ms: 120000
    difficulties:
      EASY:   { target_nps: 1.5 }
      NORMAL: { target_nps: 2.8 }

  - song_id: joplin_the_entertainer
    midi: midi_sources/the_entertainer.mid
    title: "The Entertainer"
    composer: "Joplin"
    bpm: 100
    duration_ms: 120000
    difficulties:
      EASY:   { target_nps: 2.2 }
      NORMAL: { target_nps: 4.0 }
```

### Schema decisions
- **Song ID**: `{composer_slug}_{title_slug}` (기존 `beethoven_fur_elise`와 일관).
- **Duration 공통 120000ms**: MVP §3 길이(90~150s)와 차이는 있으나 MVP 수준 UX 일관성 우선. Mutopia MIDI가 120초 초과 시 pipeline의 thin()이 window 내 notes만 유지. 미만이면 조기 종료 (허용).
- **BPM**: Mutopia MIDI tempo header와 전통 템포를 절충한 guideline 값. Implementer가 MIDI 실측 후 조정 가능.
- **NPS 타겟**: MVP spec §3 표 그대로 (Ode 1.5/3.0, Canon 1.8/3.2, Clair 1.5/2.8, Entertainer 2.2/4.0).

---

## 7. Thumbnails

### Style
Für Elise 기존 썸네일(`thumbs/fur_elise.png`, 64×64, 다크 블루 `#28305F` + 크림 "F" 글리프) 과 매칭:

- **해상도**: 64×64 PNG (Für Elise와 일치, 일관성 우선)
- **배경**: 다크 블루 `#28305F`
- **전경**: 크림 대문자 단일 글리프

### Glyph mapping (Für Elise "F"와 충돌 회피)
| 곡 | Glyph | 근거 |
|---|---|---|
| Ode to Joy | **O** | 곡 첫 글자, 충돌 없음 |
| Für Elise | F (기존) | — |
| Canon in D | **C** | 곡 첫 글자, 충돌 없음 |
| Clair de Lune | **D** | "C" 충돌 → 작곡가 Debussy 이니셜 대체 |
| The Entertainer | **E** | 관사 "The" 제외, Entertainer 첫 글자 |

### Generation
**`tools/gen_thumbs.py`** (신규 체크인):
- Python PIL (Pillow) 기반, idempotent
- 4 PNG 생성 (Für Elise 재생성 **안 함** — 기존 검증된 asset 보존)
- 폰트: OS default sans-serif bold (Arial/DejaVu), 대체 매칭 불필요 (글리프 단순)
- 출력: `Assets/StreamingAssets/thumbs/{song_id}.png`
- `.meta` 파일은 Unity가 자동 생성 (기존 `fur_elise.png.meta`의 TextureImporter 세팅을 참조해 동일 설정 적용 — AlphaIsTransparency=false, Filter=Bilinear)

### 대안 검토
- Wikimedia PD 초상화 → 시각적 다양성 ↑ but 라이선스 표기 의무 + 크롭 시간 증가. Reject.
- `locked.png` 재사용 → MVP 품질 미달. Reject.

---

## 8. Manifest update

**`Assets/StreamingAssets/catalog.kfmanifest`** — 5곡 전원 unlocked 상태로 전환:

```json
{
  "version": 1,
  "songs": [
    { "id": "beethoven_ode_to_joy",     "title": "환희의 송가",     "composer": "Beethoven",
      "thumbnail": "thumbs/ode_to_joy.png",     "difficulties": ["Easy", "Normal"], "chartAvailable": true },
    { "id": "beethoven_fur_elise",      "title": "엘리제를 위하여", "composer": "Beethoven",
      "thumbnail": "thumbs/fur_elise.png",      "difficulties": ["Easy", "Normal"], "chartAvailable": true },
    { "id": "pachelbel_canon_in_d",     "title": "Canon in D",      "composer": "Pachelbel",
      "thumbnail": "thumbs/canon_in_d.png",     "difficulties": ["Easy", "Normal"], "chartAvailable": true },
    { "id": "debussy_clair_de_lune",    "title": "Clair de Lune",   "composer": "Debussy",
      "thumbnail": "thumbs/clair_de_lune.png",  "difficulties": ["Easy", "Normal"], "chartAvailable": true },
    { "id": "joplin_the_entertainer",   "title": "The Entertainer", "composer": "Joplin",
      "thumbnail": "thumbs/the_entertainer.png","difficulties": ["Easy", "Normal"], "chartAvailable": true }
  ]
}
```

### Decisions
- **순서**: MVP spec §3 순서 (Ode → Für Elise → Canon → Clair → Entertainer). 난이도 점진적 오름차순.
- **제목 현지화**: Ode to Joy만 "환희의 송가"로 한국어 번역 (Für Elise "엘리제를 위하여" 패턴 따름). Canon/Clair/Entertainer는 원제 유지 (한국어 번역이 여러 갈래).
- **No code changes**: `MainScreen.PopulateCards()`는 `foreach SongCatalog.All`로 동적 순회 — entries 수 제약 없음.

---

## 9. Testing & validation

### Automated
- **Python pytest**: 32 / 32 유지 (파이프라인 코드 변경 0).
- **Unity EditMode**: 112 / 112 유지 (C# 코드 변경 0).

### Generated chart acceptance (per `.kfchart` 파일)
- JSON parseable
- `charts.EASY.totalNotes` > 0 AND `charts.NORMAL.totalNotes` > 0
- NORMAL `totalNotes` > EASY `totalNotes` (density 오름차순)
- `durationMs == 120000`
- 첫 note `t >= 1000` (시작 버퍼)
- note `lane` 값이 0..3 범위, `pitch` 값이 36..84 범위 (W6-1 Salamander sample bank 실 범위: MIDI 36–84, 17개 샘플 × minor-third 간격; ResolveSample이 ±1 semitone 까지 pitch-shift로 보간, 범위 밖은 최외곽 샘플로 클램프)

### Device playtest
Galaxy S22 (`R5CT21A31QB`), `Builds/keyflow-w6-sp2.apk`:
1. 앱 정상 기동 (ANR 없음)
2. 메인 화면에 5곡 카드 모두 렌더링, 썸네일 표시, 난이도 버튼 2개씩
3. 5곡 × Easy/Normal = 10 run 모두 완주, crash/hang 없음
4. 피치 오디오가 각 차트 멜로디와 일치 (W6-1 regression 확인)
5. 첫·마지막 note 배치가 자연스러움

---

## 10. Acceptance criteria

- [ ] `batch_w6_sp2.yaml` 체크인
- [ ] 4 × `.kfchart` 생성됨, §9 acceptance 통과
- [ ] `tools/gen_thumbs.py` + 4 × PNG 체크인
- [ ] `catalog.kfmanifest` 5곡 전원 `chartAvailable: true`
- [ ] Unity Editor로 10 charts 모두 load 성공 (테스트 또는 수동 확인)
- [ ] Device 10 run 완주
- [ ] 각 Mutopia 출처 commit message에 기록
- [ ] pytest 32/32 · EditMode 112/112 pass

---

## 11. Risks

| # | 리스크 | 확률 | Mitigation |
|---|---|---|---|
| R1 | Mutopia에 일부 곡의 PD/CC0 MIDI 부재 | High | §5 fallback: 해당 곡 scope 제외 → 부분 완료 후 리포트 |
| R2 | Mutopia MIDI가 다성부 피아노 편곡이라 melody track 식별 어려움 | Medium | W5 `midi_to_kfchart.py`의 track selector 활용; 필요 시 `--track` 명시 |
| R3 | 특정 곡 BPM guideline 값이 MIDI와 괴리 → 차트 timing 이상 | Medium | Implementer가 MIDI tempo header 확인 후 yaml 조정 |
| R4 | 120초 공통 duration이 Ode to Joy(90s)에 너무 길어 끝에 공백 | Low | Pipeline이 자동 처리, UX 영향 미미. 필요 시 해당 곡만 90000ms로 예외 |
| R5 | Unity 6 IL2CPP build 재발 flaky (W6-1 발생) | Medium | 첫 실패 시 재시도; 3회 실패 시 `Library/PackageCache` 정리 후 재시도 |

---

## 12. Commit plan

- C1: `docs(w6-sp2): four-song content design spec` (본 문서)
- C2: `feat(w6-sp2): batch YAML + gen_thumbs tool`
- C3: `feat(w6-sp2): 4 PD/CC0 MIDI sources` (Mutopia 출처 message에 기록)
- C4: `feat(w6-sp2): 4 × .kfchart generated`
- C5: `feat(w6-sp2): 4 × thumbnail PNGs`
- C6: `feat(w6-sp2): catalog.kfmanifest — 5 songs unlocked`
- C7 (선택): `chore(w6-sp2): APK artifact keyflow-w6-sp2.apk`

---

## 13. Deferred / out-of-scope follow-ups

- Profiler pass (W6 #3) — 별도 sub-project
- Calibration-only click sample (W6 #4) — 별도 sub-project
- UI polish / star sprites / chart-load error toast (W6 #5) — 별도 sub-project
- 2nd device playtest (W6 #6) — 별도 sub-project
- BGM audio streams — post-MVP (v2 pivot)
- `licenseInfo` JSON field 추가 — in-app Credits 확장 시 재논의
- Für Elise 썸네일/차트 리마스터 — 현재 검증된 상태 보존, 해상도 통일화는 별도 polish 작업
