# 새 아트와 기존 아트 편입

## 새 플레이 구간

2026-10-30 내장 imagegen으로 생성했습니다. 별도의 이미지 API CLI를 사용하지 않았습니다.
생성 원본을 PNG 그대로 보관하고 Unity TextureImporter의 Sprite rect로 분할합니다. 이미지 픽셀을 후처리하지 않았습니다.

| 파일 | 생성 지시와 용도 |
| --- | --- |
| `Assets/ArcaneCasters/Art/field-atlas.png` | 코믹 잉크와 페인터리 셀 채색. 투명 배경 4×4 셀. 파란 망토·흰 머리·주황 마법 카드의 청년 마법사, 불씨 임프·보라색 비행 위습·이끼 골렘·붉은 유적 문지기와 숲 소품. 적과 소환수에 사용. |
| `Assets/ArcaneCasters/Art/mage-directions.png` | 위 마법사를 참조해 투명 배경 8열×2행으로 생성. 동·북동·북·북서·서·남서·남·남동, 대기와 걷기 포즈. 방향별 이동 애니메이션에 사용. 생성 이미지의 포즈 간격과 대각선 구분은 추후 아티스트 보정 대상. |
| `Assets/ArcaneCasters/Art/root-forest.png` | 정투영 탑뷰 숲. 좌하단 야영지→중앙 뿌리 광장→오른쪽 돌다리→우상단 유적, 좌상단 개울. 에메랄드·청록·황금빛, 넓고 읽기 쉬운 전투 공간. 문자·캐릭터·UI 없이 생성. 충돌은 별도로 제작. |
| `Assets/ArcaneCasters/Art/orin.png` | 회색 수염·둥근 안경·연구 서류와 카드, 남색/크림색 학자 로브의 노년 지도 마법사. 투명 배경 상반신 초상. 대화와 야영지 NPC에 사용. |

UI는 남색 `#0d2238`, 흰색, 주황 `#ff9a1c`와 Jua 글꼴을 사용합니다.
Jua-Regular.ttf는 기존 브라우저 프로젝트의 동일 파일에서 가져왔으며 글꼴 자체를 새로 만들었다고 주장하지 않습니다.
마법 수치는 기존 브라우저 `src/spells.js`의 7종·3단계에서 옮겼습니다. 거리/탄속은 픽셀 기준 값을 100으로 나눠 Unity 단위로 변환했습니다.

## 기존 아트

원본 `Apptive-Game-Team/GyeMong_Art`의 `fa545ebf277f0f8e9afc57826badb7a03ed17636`에 있던 파일을 기존 경로에 편입했습니다.
원본 `.meta` GUID를 변경하지 않았습니다. `.gitmodules`와 gitlink를 제거해 일반 파일로 추적하며, ignore된 원래 메타데이터 10개도 포함했습니다.
원본 에셋이 없는 `Cartoon FX FREE (old legacy effects).unitypackage.meta`는 Unity가 정리한 고아 메타데이터이므로 포함하지 않습니다.
기존 자산은 각 동봉 라이선스를 유지합니다. 새 게임용 생성 아트와 기존 자산의 출처는 구분합니다.
편입한 CartoonFX 환영 창은 배치 실행 때 자동으로 열지 않고, 레이아웃이 없을 때 예외를 내지 않도록 했습니다.


## 새 파일 SHA-256

| 파일 | SHA-256 |
| --- | --- |
| field-atlas.png | 0e2234973b89c76154279b6f7ff64faecd878b478f2c019e9f4822b9d68de58d |
| Jua-OFL.txt | 44a7c6e4c5572392ae122d3b1d8c6ba6fd640a7797e675384585d947f2773e3c |
| Jua-Regular.ttf | 769677aef240bfc3b9965f2b50748075bff885e6c6992fc591a3fb268279f898 |
| mage-directions.png | c5370cdcd8e462d3d9c1386dcb611cf459e0261a43cab26a0b6e6dd68d20df46 |
| orin.png | 122b6ad8b8d1733059adf4b4b13ace0b36930cfd5bf4916ecdf3ebafee02d795 |
| root-forest.png | f2cc00ec3b8eeb00812f332d548c5539f54fe82360ac4fe4f04420340de7e7ba |

글꼴 라이선스 원문: https://github.com/google/fonts/blob/main/ofl/jua/OFL.txt
