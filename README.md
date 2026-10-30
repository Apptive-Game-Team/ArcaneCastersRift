# Arcane Casters

워드 대륙을 배경으로 한 Unity 2D 마법 액션 RPG의 첫 플레이 구간입니다.
야영지에서 출발해 세계수의 뿌리, 개울과 돌무리를 조사하고 유적의 문지기를 처치한 뒤 오린에게 돌아갑니다.

## 실행

1. Unity Hub에서 **Unity 2022.3.34f1**로 이 프로젝트를 엽니다.
2. `Assets/Scenes/ArcaneCasters/RootBoundary.unity`를 열고 Play를 누릅니다.
3. 새 아트와 씬이 이미 커밋되어 있으므로 별도의 아트 저장소 동기화가 필요하지 않습니다.

Windows 개발 빌드는 `Arcane Casters > Build Windows` 메뉴로 만듭니다.
출력은 `Build/ArcaneCastersRift/ArcaneCastersRift.exe`입니다. 실행 파일과 같은 폴더의 Data, DLL, MonoBleedingEdge 파일을 함께 보관해야 합니다.
게임명은 Arcane Casters이며 저장소와 출력 경로의 Rift는 초기 작업명입니다.

## 조작

| 키 | 동작 |
| --- | --- |
| WASD | 이동 |
| 마우스 | 조준 |
| 왼쪽 클릭 유지 | 마력탄 |
| Q / E / 1 | 장착한 마법 |
| 오른쪽 클릭 | E 슬롯 마법 |
| Space | 회피 |
| F | 조사 / 대화 / 휴식 |
| R | 회복약 |
| K | 마법 획득 정보 / 장착 / 3단계 강화 |
| Escape | 일시 정지 |
| F / Enter | 대화 계속 |

소환 골렘으로 전열을 유지하고, 빙결로 둔화한 적에게 낙뢰를 이어 쓸 수 있습니다.
적은 근접·원거리·돌진·비행·범위 공격·보스의 여섯 행동 유형이며, 공격 예고 후 공격하고 회복 시간을 가집니다.
고정 배치 적의 처치와 연구 진행은 별도의 PlayerPrefs 키에 저장됩니다. 사망하면 성장 기록을 보존하고 야영지로 돌아옵니다.

## 구조

GyeMong의 `Creature`, `StateMachineMob`, `InputManager`, `ChangeListener`를 사용합니다.
새 게임 코드는 `Assets/Scripts/ArcaneCasters`에 있고, 새 아트·마법 수치는 `Assets/ArcaneCasters`에서 관리합니다.
`Assets/GyeMong_Art`는 기존 씬 참조를 보존하기 위해 원본 파일과 GUID를 편입한 호환 자산입니다. 새 구간의 캐릭터·숲·초상은 새로 생성했습니다.
원본 프로젝트: [Apptive-Game-Team/GyeMong](https://github.com/Apptive-Game-Team/GyeMong).

## 검증 명령

프로젝트를 다른 Unity 프로세스에서 열고 있다면 닫은 뒤 실행합니다. `<Unity>`는 설치된 2022.3.34f1 에디터 실행 파일 경로입니다.

```text
<Unity> -batchmode -nographics -quit -projectPath <project> -executeMethod ArcaneCasters.Editor.ArcaneProjectBuilder.Build -logFile <scene-log>
<Unity> -batchmode -nographics -projectPath <project> -executeMethod ArcaneCasters.Editor.ArcanePlayModeVerification.Run -logFile <play-log>
<Unity> -batchmode -nographics -quit -projectPath <project> -executeMethod ArcaneCasters.Editor.ArcaneProjectBuilder.BuildWindows -logFile <build-log>
```

Play Mode 검사는 성공/실패에 따라 에디터를 종료하므로 `-quit`을 붙이지 않습니다. 실제 게임 명령으로 이동·피격·회복·소환·쿨다운·비행 적 조준·낙뢰·강화·조사·보스·사망 복귀·씬 재진입 2회를 검사합니다. 임시 저장 키를 사용하고 검사 종료 시 삭제합니다.
이 가속 검사는 실제 키보드·마우스 플레이와 전투 재미 평가를 대신하지 않습니다. WebGL·macOS 빌드는 이번에 검증하지 않았습니다.

씬 생성 메뉴는 기존 씬이 있으면 덮어쓰지 않습니다. 지형·충돌은 커밋된 씬에서 조정합니다.
개발 빌드에서 `--arcane-capture-dir <directory>`를 주면 Unity 렌더 타깃으로 야영지·대화 화면을 저장하고 종료합니다. 이 옵션은 OS 입력을 합성하지 않습니다.
