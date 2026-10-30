# Arcane Casters

워드 대륙의 두 연구생 미라와 핀이 각자 현장 연구를 떠나는 Unity 2D 액션 RPG입니다. 두 장을 플레이할 수 있습니다.

- **1장 · 세계수 뿌리 (미라):** 야영지에서 오린 선생님의 설명을 듣고, 세계수 뿌리를 조사해 깨어난 불씨를 정리합니다.
  개울의 결정과 돌무리를 조사해 유적의 봉인을 풀고, 유적의 문지기를 제압한 뒤 선생님께 보고합니다.
- **2장 · 돌골렘 마을 (핀):** 큰 돌골렘에게 인사해 마을에 들어가고, 미니록들과 놀며 모크라는 별명을 붙입니다.
  골렘을 관찰하던 중 모크의 눈이 붉어져 동굴로 달려가고, 핀은 걱정돼 따라갑니다. 동굴 안의 고대 문이 열린 뒤를 버텨 냅니다.

일시 정지 메뉴(Esc)의 "기록 선택"에서 장을 고르거나 지금 장을 처음부터 다시 할 수 있습니다. 1장을 마치면 2장 안내가 나옵니다.

## 실행

1. Unity Hub에서 **Unity 2022.3.34f1**로 이 프로젝트를 엽니다.
2. `Assets/Scenes/ArcaneCasters/RootBoundary.unity`(1장) 또는 `StoneVillage.unity`(2장)를 열고 Play를 누릅니다.

Windows 개발 빌드는 `Arcane Casters > Build Windows` 메뉴로 만듭니다.
출력은 `Build/ArcaneCastersRift/ArcaneCastersRift.exe`이고, 같은 폴더의 Data, DLL, MonoBleedingEdge 파일을 함께 보관해야 합니다.
게임명은 Arcane Casters이며 저장소와 출력 경로의 Rift는 초기 작업명입니다.

## 조작

| 키 | 동작 |
| --- | --- |
| WASD | 이동 |
| 마우스 | 조준 (적의 몸 위에 두면 그 적을 겨눕니다) |
| 왼쪽 클릭 유지 | 마력탄 |
| Q / E(오른쪽 클릭) / 1 | 장착한 마법 |
| Space | 회피 (짧은 무적) |
| F | 조사 · 대화 · 휴식 |
| R | 회복약 |
| K | 마법 연구 (장착 · 강화) |
| Esc | 일시 정지 · 창 닫기 |
| F / Enter / 클릭 | 대화 넘기기 |

적은 공격 전에 바닥에 피해 범위를 그립니다. 안쪽 채움이 테두리까지 차는 순간 피해가 들어가므로, 그 전에 범위 밖으로 나가거나 회피하면 됩니다.

| 적 | 패턴 |
| --- | --- |
| 잿불 발톱 | 짧은 직선 예고 후 덮치기 |
| 갈래불 악마 | 세 갈래 불꽃, 거리를 유지 |
| 폭주하는 돌 | 긴 직선 예고 후 돌진, 벽에 부딪히면 오래 멈춤 |
| 불고리 감시자 | 떠다니며 여덟 방향으로 불꽃 |
| 용암 분쇄자 | 앞쪽 원형 내려치기 |
| 유적의 문지기 | 원형 내려치기와 열 방향 돌조각, 체력이 절반 아래면 연속 내려치기 |

진행과 처치 기록은 PlayerPrefs의 별도 키에 저장됩니다. 쓰러지면 연구 기록을 유지한 채 야영지에서 다시 시작하고, 주변 적은 체력을 회복해 제자리로 돌아갑니다.

## 구조

GyeMong의 `Creature`, `StateMachineMob`, `InputManager`, `ChangeListener`를 사용합니다.
새 게임 코드는 `Assets/Scripts/ArcaneCasters`, 편집기 도구는 `Assets/Editor/ArcaneCasters`, 새 아트와 데이터는 `Assets/ArcaneCasters`에 있습니다.

- **장:** 이야기 규칙은 `ArcaneChapter`(1장 `RootBoundaryChapter`, 2장 `StoneVillageChapter`)에 있고, `ArcaneSession`은 플레이어·맵·전투·대화·저장을 맡습니다. 2장 설계는 [docs/story/chapter-02-stone-village.md](docs/story/chapter-02-stone-village.md)에 있습니다.
- **맵:** 장마다 `Assets/ArcaneCasters/Data/*.txt` 격자 하나가 바닥 그림, 충돌, 숲·바위 배치, 조사 대상, 적 위치를 모두 정합니다.
  `ArcaneField`가 실행 시 이 격자로 맵을 만들고, `GroundSplat` shader가 같은 격자에서 만든 혼합 지도로 바닥을 칠합니다.
  격자는 `ArtSource/tools/make_layout.py`(1장), `make_layout_stone_village.py`(2장)로 다시 만듭니다.
- **아트:** 생성 프롬프트와 가공 도구는 `ArtSource/`에 있고 출처는 [docs/art-provenance.md](docs/art-provenance.md)에 있습니다.
  `Assets/GyeMong_Art`는 기존 씬 참조를 보존하기 위해 원본 파일과 GUID를 편입한 호환 자산이며, 첫 구간은 이를 쓰지 않습니다.
- **씬:** `Arcane Casters > Import Art and Rebuild First Field` 메뉴가 아트를 측정된 프레임 정보로 가져오고 씬을 다시 씁니다.

원본 프로젝트: [Apptive-Game-Team/GyeMong](https://github.com/Apptive-Game-Team/GyeMong).

## 검증

프로젝트를 다른 Unity 프로세스에서 열고 있다면 닫은 뒤 실행합니다. `<Unity>`는 2022.3.34f1 편집기 실행 파일, `<project>`는 이 폴더입니다.

```text
<Unity> -batchmode -quit -projectPath <project> -executeMethod ArcaneCasters.Editor.ArcaneProjectBuilder.Build -logFile <scene-log>
<Unity> -batchmode -nographics -projectPath <project> -executeMethod ArcaneCasters.Editor.ArcanePlayModeVerification.Run -logFile <play-log>
<Unity> -batchmode -quit -projectPath <project> -executeMethod ArcaneCasters.Editor.ArcaneProjectBuilder.BuildWindows -logFile <build-log>
```

검증은 네 층으로 나뉘며 서로를 대신하지 않습니다.

| 층 | 방법 | 확인하는 것 |
| --- | --- | --- |
| 게임 명령 | `ArcanePlayModeVerification.Run` (58개 항목) | 두 장의 모든 칸 지형·충돌 일치, 걸어서 닿는 경로, 예고 띠 안팎의 피해, 진행·저장·씬 재진입·사망 복귀, 2장 방어전과 끝 화면 |
| 엔진 캡처 | 개발 빌드에 `--arcane-capture-dir <dir>` (2장은 `--arcane-chapter stone-village` 추가) | 1장 20장, 2장 13장. 게임 명령으로 상태를 만들며 OS 입력이 아닙니다 |
| 실제 입력 | `Tools/Verification` (SendInput, SetCursorPos) | WASD, 마우스 조준, 공격, 회피, 상호작용, UI 클릭 누수, 창 닫은 뒤 입력 복구, 저장 후 재실행, OS 입력만으로 1장·2장 완주 |
| 사람 플레이 | 직접 실행 | 손맛, 난이도, 소리의 구분 |

실제 입력 검사 실행 방법은 [Tools/Verification/README.md](Tools/Verification/README.md)에 있습니다. 개발 빌드는 `--arcane-telemetry <file>`로 상태를 기록하고 `--arcane-save-key <key>`로 별도 저장 칸을 씁니다.
WebGL·macOS 빌드는 검증하지 않았습니다.
