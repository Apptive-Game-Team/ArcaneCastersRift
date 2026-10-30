# 아트 출처

## 첫 구간 아트 (2026-10-30 교체)

첫 구간이 쓰는 그림은 모두 2026-10-30에 `codex exec`의 내장 `image_gen`으로 새로 생성했습니다.
이전 프로토타입의 생성 그림 네 장(`field-atlas.png`, `mage-directions.png`, `root-forest.png`, `orin.png`)은
균일 격자 가정으로 잘린 캐릭터, 상반신 초상을 쓴 월드 NPC, 충돌과 맞지 않는 한 장짜리 배경 문제 때문에 삭제했습니다.

생성 프롬프트는 `ArtSource/prompts/<이름>.txt`에 있습니다. 인물과 생물의 외형은 원본 브라우저 프로토타입
(원격이 없는 로컬 저장소 `C:\Users\jys09\Projects\ArcaneCastersRift`, 커밋 `cbb5514`)의 정본 자료를
참조 이미지로만 붙여 맞췄습니다. 참조 그림 자체는 이 저장소에 복사하지 않았습니다.

| 참조 | 쓰인 곳 |
| --- | --- |
| `docs/story/CHARACTERS.md`, `DIALOGUE.md`, `WORLD.md` | 미라·오린 외형과 말투, 첫 구간 대사 |
| `docs/story/references/subjects/apprentice-mage.png`, `assets/dialogue/mira.png` | 미라 (긴 갈색 머리, 파란 망토, 회색 로브, 푸른 결정 지팡이, 카드 가방) |
| `assets/dialogue/orin.png` | 오린 (흰 머리·수염, 자주색 로브, 굽은 나무 지팡이) |
| `assets/FireChildSpirit.png`, `FireSpirit.png`, `RockRolling.png`, `MagmaSpirit*.png`, `FireLordSpirit.png`, `RockGolem*.png` | 적 다섯 종, 문지기, 소환 골렘 (`src/combat.js`의 역할 배정을 따름) |
| WordOnlineClient `.art/anchors/master-v2/MasterStyleKey.png` | 각진 면 종이 공예풍 렌더링 기준 |

인물·생물·아이콘·효과는 정본의 각진 면 종이 공예풍이고, 숲 소품과 바닥은 브라우저 프로토타입 숲과 같은
손그림 셀 채색입니다. 모든 그림은 위에서 약 40도로 내려다보는 3/4 시점, 좌상단 광원입니다.

### 가공 방법

생성기는 투명 배경 대신 평평한 key 색(magenta 또는 green) 배경으로 그리게 했고, `ArtSource/tools/sprite_sheet.py`가
처리합니다. 손으로 칠한 픽셀은 없습니다.

1. `key`: key 색 기여분을 나눠 빼서 투명 배경으로 만듭니다(WordOnlineClient `key-out-background.py`와 같은 계산).
2. `frames`: 시트를 균일 격자로 자르지 않습니다. 실제 불투명 영역을 연결 요소로 찾고, 각 영역이 정확히 한 칸 안에
   있는지 검사한 뒤, 공통 배율과 공통 기준점으로 다시 배치합니다. 걷기·시전 프레임은 상반신이 가장 많이 겹치는
   위치로 맞춰 머리가 튀지 않게 했고, 발 기준점(pivot)은 `.frames.json`에 기록합니다.
3. `props`: 물체마다 실제 경계로 잘라 냅니다. 떨어져 나온 반짝이 조각은 가장 가까운 물체에 붙입니다.
4. `ground`: 2×2 바닥 시트를 반 칸 밀린 사본과 섞어 이음새 없는 반복 텍스처로 만듭니다.

캐릭터·생물은 1 unit = 128px로 가져오고, 소품은 `ArcaneProjectBuilder.PropSizes`의 월드 높이에 맞춰 같은 밀도로 가져옵니다.

| 폴더 | 내용 | 프롬프트 |
| --- | --- | --- |
| `Characters/` | 미라 정면·옆·뒤(기본, 걷기 2장, 시전), 오린 전신(기본, 말하기) | `mira-down`, `mira-side`, `mira-up`, `orin-world` |
| `Creatures/` | 잿불 발톱, 갈래불 악마, 폭주하는 돌, 불고리 감시자, 용암 분쇄자, 유적의 문지기, 소환 돌 골렘 (각 기본·예고 자세) | `enemy-*`, `ally-golem` |
| `Props/` | 나무 2종, 덤불, 바위, 천막, 모닥불, 벤치, 세계수 뿌리, 결정, 표지판, 유적 아치·기둥, 룬 돌 | `props-a`, `props-b` |
| `Ground/` | 풀, 흙길, 개울, 유적 포석 반복 텍스처 | `ground` |
| `Icons/` | 마법 7종, 회복약, 연구 수첩 | `icons` |
| `Effects/` | 카드·불씨·얼음 투사체, 낙뢰, 가시덩굴, 충격, 먼지, 소환 돌 | `fx` |
| `Portraits/` | 미라·오린 대화 초상 | `portraits` |

생성 중 정본과 다르게 나온 결과(은발의 미라, 손그림 화풍의 적)는 사용하지 않았습니다.
`fx` 시트의 방어막 거품은 초록 key 색과 겹쳐 내부가 지워져 제외했고, 장막 효과는 장막 아이콘을 반투명하게 씁니다.

글꼴 `Jua-Regular.ttf`는 기존 브라우저 프로젝트의 같은 파일이며 OFL 라이선스(`Jua-OFL.txt`)를 그대로 둡니다.
글꼴 라이선스 원문: https://github.com/google/fonts/blob/main/ofl/jua/OFL.txt

## 기존 아트

원본 `Apptive-Game-Team/GyeMong_Art`의 `fa545ebf277f0f8e9afc57826badb7a03ed17636`에 있던 파일을 기존 경로에 편입했습니다.
원본 `.meta` GUID를 변경하지 않았습니다. `.gitmodules`와 gitlink를 제거해 일반 파일로 추적하며, ignore된 원래 메타데이터 10개도 포함했습니다.
원본 에셋이 없는 `Cartoon FX FREE (old legacy effects).unitypackage.meta`는 Unity가 정리한 고아 메타데이터이므로 포함하지 않습니다.
기존 자산은 각 동봉 라이선스를 유지합니다. 새 게임용 생성 아트와 기존 자산의 출처는 구분합니다.
편입한 CartoonFX 환영 창은 배치 실행 때 자동으로 열지 않고, 레이아웃이 없을 때 예외를 내지 않도록 했습니다.

## 새 파일 SHA-256

| 파일 | SHA-256 |
| --- | --- |
| `Characters/mira-down.png` | 0890963d0c06bad8a6e0a3f46efe4c668369abec201a3483216d7766532a8523 |
| `Characters/mira-side.png` | a1310ac7a6d50ffe24f3348df119e480baee9dcdd02d90c4deb974e53dfebe87 |
| `Characters/mira-up.png` | 73bdbc58b8fa5d67d510a35a6af568a65af6bdd3b55d3928877df7713b0785cc |
| `Characters/orin.png` | bfbd186a9d6ca7c87c944edaa037e160e21536b1534698f52f45bdb9e622f44e |
| `Creatures/ally-golem.png` | 65eec258d8a8e5db7dd87157fd86d966fce590b606a05f29bb7b5c8ce16d4018 |
| `Creatures/enemy-brute.png` | b197e9928aa0d42e05381751a1ae48ea25e0a13eff85380c8aa1c848da607383 |
| `Creatures/enemy-charger.png` | 65c72bc3bcd6a64ead753680775420f60217e78daeb573d7f48de9b4402192b8 |
| `Creatures/enemy-imp.png` | 07d2a70f72e0256ea731cf5b962a2c16d786a726e549230ed031945905a88315 |
| `Creatures/enemy-spitter.png` | 723d19aed09c76219aea337bae82c14b7fcafdd1d2d1bdfddd8fc10efa5dfd66 |
| `Creatures/enemy-warden.png` | ba38e31c4b091643d4a733f6a255aafc3e6e0db9c56f29176b86866ca47a9198 |
| `Creatures/enemy-wisp.png` | a1b6828d6ca96e364d43d8437394af1d556cd1f3ccdaedd66e51e3bfc44b8c1c |
| `Effects/card.png` | 825d748775b0a2386b5202ee9190f4a9de9be3a56a9cc0c0b92ffbf05f95c28d |
| `Effects/dust.png` | fa4544a659a8dd47e76691a8ab4846b08553aead936616b5620ad83a06d84dc1 |
| `Effects/ember.png` | 2105e52e9693d0c6f4291bc9b9ae6d7442c0b777744bbff4dddeff0ba5cdec70 |
| `Effects/ice.png` | 40aed03dc30a586659c90389688169eedf318012fc2bee742bbaf5d3e950efa1 |
| `Effects/spark.png` | 6986638f3a986d946575072f8e4e08f67c5d24338a4d6541ca7d8e6e554777ce |
| `Effects/stones.png` | 6de339418acd3c65e378e077d3b042be4628979e7770b5b90ebbac92b54075b0 |
| `Effects/strike.png` | 10e4d736a20dde4952c3b236439ab2dda123cf6e8a2b90d12602e4302850c699 |
| `Effects/thorns.png` | 84fb0bc26f7414c0b7260505f61fbc8380022f21cd9acd74f32f8eed619ec50d |
| `Ground/dirt.png` | 9e0f28068f17bb50f76faa4ba006defa08b18120e55f1bc637e68ce4ec667d56 |
| `Ground/grass.png` | b58b2ae72528e2fad2b7a31dc6f673e4697a70b65f873b857d6eb444cb8f6836 |
| `Ground/stone.png` | 289308a6e800110efa487f6c490c0cda3a30ac1fded4dc16071edfe080a55bd0 |
| `Ground/water.png` | 93a016bc62fc7c2eb7c20f6be94a2af6c071be1746b981389b95ffffb03b08ce |
| `Icons/bolt.png` | 7fd54b7e85d7cf5af96d98ac9556b511fbb885a2fd80dd05c852ac6c7d788bff |
| `Icons/dash.png` | a03e9ef9c592801b1188ecb9b8aa052fe19854712affade5bb31bc2ad5a4c398 |
| `Icons/frost.png` | b2c32481693d43ff64785f36a927278fc42baf4b382e59f45eda9140ee9c73ab |
| `Icons/journal.png` | 66f05824d98c71fde0ce8659c0e4c1fe075c4ae156f7db8fe790630ab2fc3ba0 |
| `Icons/lightning.png` | 240ca527ef90653b30420d2da7b20937d1a75ae1b13da25e1c4033aa153ac18d |
| `Icons/potion.png` | 3f1ad851c6c59de8498ff11f39575a01a787b36b0fb24a03df9b80ce89ace411 |
| `Icons/roots.png` | 25b2e313015329deb50b33e3c46c3f9c5f9f18f3c784ca7b6d051d10cbfe75e2 |
| `Icons/summon.png` | 116e33df319a940bb64010b3e26e8bd36d0dbab566bca0e2f97efc7803f00b32 |
| `Icons/ward.png` | 547d696ef8914db37fef4408e6cc366d07e53b632b6393709018037820050992 |
| `Portraits/mira.png` | 485b77ccbee271b2cbf5e3792152a69f6f92463fbe4c963e14008f6746283f05 |
| `Portraits/orin.png` | a8bcc188efc75cadd0d93dbc030479bd7fbbbfa1934e7649fa73c33f7d9b1926 |
| `Props/bench.png` | 37eb2b0b0a7b33328a23a687a7f2cf28a90f33b652c415220b414a8b39364470 |
| `Props/boulders.png` | 2c15ceea69fe4430a54dca62f7b8a3f3c5ce58fb4706c8b97d229bd57a8d9019 |
| `Props/bush.png` | c50f35f5e9ccadde5af0d51d5b2cda41a99f9b95292fa517d458a9daf1bc33cb |
| `Props/campfire.png` | 3d354417da29ddd38361f501158ad6aa77693d18821cd7267bb0652f80915f5a |
| `Props/crystals.png` | 76d5b70073bb03eb108094ae349be37be090723e63a9e299881cf16c1ff08bcb |
| `Props/ruin-arch.png` | 3012c320bf4a1cfcee5954d34e7e224a3d66ca4a3686c0951274a214f8704bf9 |
| `Props/ruin-pillar.png` | bb7dd18521105c9855f7e228f67a1253eb2bb63f4de6c30ced2d5d84918bdc2c |
| `Props/rune-stone.png` | 42d72dc574b1e1b0fe1062a31ea627409fb6bcec5baa290b37eb8d5755d296c2 |
| `Props/signpost.png` | b693db4d501fa903ff073b2ad390b4492edcfba532020ee6819ced423e1ffdc9 |
| `Props/tent.png` | fe53e18bb27543ee804802d6035cfac7e95eed3b9b9b3d13d094e158bbb419ae |
| `Props/tree-broad.png` | 3e4b7f6b640f41ba5f932af28ffe4eb097a7db3051bc612c1d5516b997672a74 |
| `Props/tree-pine.png` | 718d250f0cc08c99d51b652dedae9d8b317736fe04b645df305ca76f75a2e931 |
| `Props/world-root.png` | 0272917e882d0219e0e34cf7dded4f968ccea051d042c90724c888b02b016896 |
| `Jua-Regular.ttf` | 769677aef240bfc3b9965f2b50748075bff885e6c6992fc591a3fb268279f898 |
| `Jua-OFL.txt` | 44a7c6e4c5572392ae122d3b1d8c6ba6fd640a7797e675384585d947f2773e3c |
