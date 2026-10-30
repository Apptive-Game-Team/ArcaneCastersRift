# 2026-10-30 — GyeMong 구조 기반 Arcane Casters 첫 구간 추가

- Date: 2026-10-30
- GitHub Issue: Apptive-Game-Team/ArcaneCastersRift#1
- Status: 구현 전 계획 리뷰 완료 — 구현 대기

## Goal

Unity 2022.3.34f1 프로젝트에서 워드 세계관의 첫 숲 구간을 플레이한다.
WASD 이동·마우스 조준·마력탄·소환·낙뢰·빙결·장막·덩굴·회피, 조사 보상과
3단계 마법 성장 및 작은 theevilent HUD를 연결한다.

## Non-goals

전체 캠페인·온라인 계정·자동 배포·기존 브라우저 세이브 자동 이전은 포함하지 않는다.
원본 GyeMong 로컬 변경·기존 포크 main·원본 아트 서브모듈은 변경하지 않는다.

## Context / Constraints

- 포크 기준 main d2dac1dd, 아트 핀 fa545ebf. 조직 포크 Apptive-Game-Team/ArcaneCastersRift를 사용한다.
- 새 작업 경로 C:/Users/jys09/Projects/ArcaneCastersRiftUnity, 브랜치 feat/1.
- Assets/Scripts/ArcaneCasters/{GameSystem,InputSystem,DataSystem,UISystem} 구조를 사용한다.
- 플레이어·골렘은 GyeMong Creature를 상속하고, 적은 StateMachineMob을 상속한다.
- 기존 InputManager는 이동/공격/회피/조사/회복에 사용한다. Q/E/1/K 같은 새 게임 명령은 얇은 입력 어댑터에서 추가한다.
- HUD는 기존 ChangeListenerCaller/IChangeListener로 상태를 받으며 게임 로직은 UI를 참조하지 않는다.
- 기존 PlayerCharacter는 근접 콤보·하트·SceneContext에 결합되어 있어 수정하지 않고 마법사를 별도 Creature로 구현한다.
- 기존 상태머신은 동기 전이 시 코루틴 핸들 문제가 있으므로 새 상태 코루틴은 먼저 한 프레임 양보한다. 기존 공유 구현은 변경하지 않는다.
- ChangeListenerCaller에는 RemoveListener가 없으므로 HUD 구독은 Start에서 한 번만 하며 플레이어와 HUD가 같은 씬 수명을 가진다.
- 새 스프라이트·글꼴은 브라우저 시안에서 Assets/ArcaneCasters로 복사하고 원본 출처·해시를 기록한다.

## Approach (Checklist)
- [x] **Step 0: Recon** 조직 원격·원본 main·Creature/InputManager/StateMachineMob/ChangeListener API 확인.
- [ ] **Step 1: Implementation** 새 게임의 데이터·입력 어댑터·Creature·상태머신 적·투사체·소환수·씬 루트·UGUI HUD를 추가한다. Editor 생성기로 스프라이트를 임포트하고 RootBoundary 씬과 전용 빌드 진입점을 만든다. 기존 씬은 남기고 포크의 빌드 대상은 새 씬으로 설정한다.
- [ ] **Step 2: Tests** Unity 배치 컴파일과 Editor 검증 명령으로 씬/상속/자산 누락을 확인한다. 새 Play Mode 검사에서 이동·공격·마력·재사용·강화·획득·리스너·씬 재진입을 확인한다. 에디터 플레이 또는 실행 가능한 개발 빌드에서 실제 조작과 화면을 확인한다.
- [ ] **Step 3: Rollout / Rollback** feat/1에 커밋·푸시하고 조직 포크 main 대상 draft PR을 만든다. 원격 main 병합과 배포는 하지 않는다.

## Validation
- **Commands to run:** 설치된 Unity 2022.3.34f1 -batchmode -projectPath <fork> -executeMethod ArcaneCasters.Editor.ArcaneProjectBuilder.Build -quit -logFile <local log>. 실제 정의한 검사 명령과 Play Mode 검사를 사용한다.
- **Expected output:** C# 컴파일 오류 0, 새 씬과 자산 검사 통과, 입력/전투/성장 검사 통과. 실시간 조작과 가속 검사는 별도로 기록한다.

## Risks & Rollback
- **Risks:** 최초 Unity 임포트 시간과 라이선스, 씬 수명·물리 레이어·동적 글꼴, 원본 상태머신 전이와 리스너 제거 API 부재. 먼저 컴파일과 작은 전투 구간을 확인한다.
- **Rollback steps:** 조직 포크의 feat/1을 사용하지 않고 기존 main을 연다. 새 게임 자산과 빌드 설정 변경은 해당 커밋으로 되돌린다.

## Open Questions
- 기존 프로젝트의 Unity 2022.3.34f1 배치 임포트와 컴파일은 성공했다. 새 게임 구현의 Play Mode 및 빌드는 아직 검증하지 않았다.
- 리뷰 피드백은 구현 전에 반영한다.

## Review refinements

- 새 적은 ArcaneSession에서 받은 Target 마법사 참조만 사용한다. Mob의 SceneContext.Character 기반 거리·추적·돌진 헬퍼는 호출하지 않는다. 원래 씬의 PlayerCharacter는 요구하지 않는다.
- 각 Creature 파생형은 OnAttacked 후 체력 0을 판정하고 dead 플래그로 사망·보상을 한 번만 처리한다. 사망 시 상태/공격 코루틴과 물리 이동을 중단한다. 소환수는 경험치를 주지 않는다.
- InputManager는 한 어댑터만 소비한다. InputManager.Start 초기화 후 한 프레임 기다려 키를 설정하고, 버퍼 입력은 현재 프레임의 실제 KeyDown으로 거른다. 어댑터 파괴 시 이전 매핑을 복원한다. HUD는 입력을 소비하지 않는다.
- 상태는 public 중첩 BaseState 파생형과 기본 생성자를 사용한다. StateCoroutine의 첫 yield로 기존 동기 전이 핸들 문제를 피하고 예고→공격→회복→추적 전이를 검사한다.
- HUD 구독은 Start에서 한 번만 하고 즉시 현재 값을 전달한다. 플레이어와 HUD의 독립 교체는 이번 범위에 없다.
- 생성기는 Assets/ArcaneCasters와 새 RootBoundary 씬만 대상으로 한다. 기존 파일이 있으면 덮어쓰지 않고 열어 검증한다. 새 씬용 빌드 진입점은 출력 경로와 씬을 명시한다.
- 구현 순서는 이동·마력탄·적 1종·HUD → 소환과 마법 → 조사와 성장이다. 각 단계에서 컴파일과 작은 플레이 구간을 확인한다.
- 첫 구간: 야영지 시작 → 세계수 뿌리 F 조사 → 첫 방어전에서 낙뢰 획득 → 개울·돌무리 조사로 빙결창·장막 획득 → 유적 보스 제압 → 야영지 보고로 완료. 샛길 전투에서 덩굴을 얻는다. 적은 고정 배치이며 무한 생성하지 않는다.
- 마법의 단계별 수치는 기존 ArcaneCastersRift/src/spells.js를 기준으로 고정한다. Q/E/1 장착, LMB 기본 공격, Space 회피, R 회복, F 조사, K 마법. 강화 연구점 2/3, 최대 3단계, 전투 중 변경 제한을 유지한다. 사망 시 연구·마법을 보존하고 야영지로 복귀한다.
- 검증은 새 Assets/Editor/ArcaneCasters/ArcanePlayModeVerification.cs의 Run 명령으로 Play Mode를 진입하고 이동·피격·회복·시전·재사용·성장·사망 중복·씬 2회 재진입을 확인한다. 검증용 PlayerPrefs 키는 고유하게 분리하고 검증 종료 시 지운다. 입력 어댑터를 끄고 실제 게임 명령을 호출하는 가속 검사이며 실제 키보드 플레이와 구분한다.
- 검증 명령은 -executeMethod ArcaneCasters.Editor.ArcanePlayModeVerification.Run을 사용하고 -quit은 넣지 않는다. 검사기가 성공/실패를 로그에 기록한 뒤 종료 코드 0/1로 Editor를 종료한다. 에디터 라이선스/초기 임포트가 막히면 실행 검증 미완료로 보고한다.

Rejected feedback: 없음. 두 1차 리뷰의 must-fix와 구체적인 should-fix를 반영했다.

## 이번 Draft PR의 범위

이 PR에는 구현 계획만 포함한다. 새 게임 코드·씬·자산·빌드 설정은 아직 포함하지 않는다.
전투·UI 구현과 플레이 검증이 끝나기 전까지 Draft로 유지한다. 이슈 #1은 열어 둔다.
