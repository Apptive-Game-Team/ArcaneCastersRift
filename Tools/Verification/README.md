# Windows build 실제 입력 검증

Windows PowerShell 5.1 또는 PowerShell 7에서 실행합니다. Unity Editor는 필요하지 않습니다. Desktop이 잠기지 않은 상태여야 하며, 게임은 1280×720 window로 열려 실행 중 다른 창 위에 표시됩니다.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tools\Verification\Run-Scenario.ps1 -Exe 'C:\Users\jys09\Projects\ArcaneCastersRiftUnity\Build\ArcaneCastersRift\ArcaneCastersRift.exe' -Scenario .\Tools\Verification\scenarios\playthrough.ps1 -OutDir "$env:LOCALAPPDATA\Temp\arcane-play-1" -SaveKey play-1
```

| 시나리오 | 내용 |
| --- | --- |
| `smoke.ps1` | 브리핑 넘기기, 4방향 이동, 조준, 마력탄, 회피, 일시 정지, 마법 연구 창 |
| `input-checks.ps1` | 대화창·마법 연구 버튼·닫기·계속하기 클릭이 공격으로 새지 않는지, 창을 닫은 뒤 WASD·조준·공격·회피·F 대화가 돌아오는지, "저장하고 종료" 뒤 같은 저장으로 다시 실행했을 때 브리핑이 반복되지 않고 진행이 유지되는지 |
| `playthrough.ps1` | 새 게임부터 브리핑, 뿌리 조사와 전투, 덩굴·개울·돌무리 조사, 문지기 전투, 보고까지 OS 입력만으로 진행하고, 저장하고 종료한 뒤 다시 실행해 완료 상태가 남는지 |

실행마다 새 출력 directory와 새 `-SaveKey`를 쓰세요. `-SaveKey`는 개발 빌드의 `--arcane-save-key`로 전달되어 플레이어의 실제 저장을 건드리지 않습니다. `telemetry.jsonl`이 이미 있는 directory는 거부합니다. 실패하면 exit code가 1입니다.

## 입력과 관찰

- 키보드와 마우스 버튼은 `user32!SendInput`, 커서 위치는 `SetCursorPos` 뒤 상대 이동 0 이벤트로 보냅니다. 절대 좌표 `SendInput`은 이 기계의 다중 모니터 가상 데스크톱에서 수십 px 어긋나서 쓰지 않습니다. 클릭 전에는 게임이 읽은 커서 위치가 3px 안에 있는지 확인합니다.
- 게임 window가 foreground인지 입력마다 검사하고, 다시 초점을 맞춘 뒤에도 아니면 중단합니다. 다른 프로그램에 키가 들어가지 않습니다.
- 게임을 띄우기 전에 데스크톱 사용자의 입력이 20초 이상 없을 때까지 기다립니다. 사람이 쓰는 중에는 창을 빼앗지 않습니다. 실행 중 다른 창을 누르면 검사가 중단됩니다.
- 누른 키와 버튼은 `finally`에서 놓고 게임을 종료합니다. `input.log`에 보낸 입력과 검사 결과가 남습니다.
- `ArcaneTelemetry`는 0.1초마다 위치, 체력, 진행, UI 상태, 시전 횟수, 카메라, 적 목록을 JSONL로 남기는 관찰용 component입니다. 게임 함수를 호출하지 않습니다.
- `playthrough.ps1`의 이동은 `scenarios/route.json`(레이아웃 격자에서 만든 목표별 걷기 거리 지도)을 따라 WASD를 누릅니다. 격자를 바꾸면 `python3 Tools/Verification/make_route.py`로 다시 만듭니다.

PNG는 게임 client 영역의 실제 desktop pixels를 `Graphics.CopyFromScreen`으로 저장합니다. Unity 내부 입력 함수 호출과 render target 저장은 실제 OS 입력 검증에 해당하지 않습니다.
