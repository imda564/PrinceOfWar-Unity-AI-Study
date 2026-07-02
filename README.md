# Prince of War Unity AI Study

## English

This is a Unity 2D strategy game prototype inspired by the classic *Prince of War* style of lane-based battles. The project focuses on recreating core gameplay ideas such as unit spawning, side-scrolling combat, enemy waves, simple battle UI, and resource-based decisions inside Unity.

This repository is published as an educational and non-commercial study project. It was made to practice Unity development, 2D asset setup, gameplay prototyping, animation handling, and AI-assisted development workflows.

### Features

- 2D battle prototype built in Unity
- Player and enemy unit spawning
- Basic combat and wave progression
- Extracted sprite and animation resources organized for Unity
- URP 2D project setup

## Playable Build

Download the Windows build from the latest pre-release:

- [Download `pack.zip`](https://github.com/imda564/PrinceOfWar-Unity-AI-Study/releases/download/0.1.0/pack.zip)
- [Release page](https://github.com/imda564/PrinceOfWar-Unity-AI-Study/releases/tag/0.1.0)

### How to Play

1. Download `pack.zip`.
2. Extract the zip file.
3. Run `Prince of war.exe`.

The objective is to escort at least one allied unit into the enemy gate while protecting your morale. Gold increases over time and is spent to recruit units or heal the hero.

### Controls

| Situation | Control |
| --- | --- |
| Main menu | `Enter` / `Space` to continue |
| Main menu | `S` for stage select, `O` for options, `C` for credits |
| Stage select | Click a stage card, or press `0` for Meteor Test and `1`-`9` for quick stage selection |
| Battle | `WASD` / Arrow keys to move the hero |
| Battle | `Space` / Left mouse button to attack |
| Battle | `Shift` to heal the hero, costs 25 gold |
| Battle | Click recruit buttons or press `1`-`9` to summon allied units |
| Battle | `Esc` to pause or resume |
| Result screen | `Enter` for next stage after clearing, `R` to restart, `Esc` to return to stage select |

### Educational Notice

This project is for learning, research, and portfolio study purposes only. It is not intended for commercial release or redistribution as an official product. Some visual references and extracted resources are used only to study how a similar game structure can be rebuilt in Unity.

## 한국어

이 프로젝트는 고전 게임 *Prince of War* 스타일의 라인 기반 전투를 Unity 2D로 구현해 본 전략 게임 프로토타입입니다. 유닛 소환, 횡스크롤 전투, 적 웨이브, 간단한 전투 UI, 자원 기반 선택 같은 핵심 플레이 구조를 Unity 안에서 재현하고 실험하는 데 초점을 두었습니다.

이 저장소는 공부용 및 비상업적 학습 프로젝트로 공개됩니다. Unity 개발, 2D 에셋 구성, 게임플레이 프로토타이핑, 애니메이션 처리, AI 보조 개발 흐름을 연습하기 위해 제작했습니다.

### 주요 기능

- Unity 기반 2D 전투 프로토타입
- 플레이어 및 적 유닛 소환
- 기본 전투와 웨이브 진행
- Unity에서 사용할 수 있도록 정리된 스프라이트 및 애니메이션 리소스
- URP 2D 프로젝트 설정

## 플레이 가능한 빌드

Windows 실행 빌드는 아래 pre-release에서 받을 수 있습니다.

- [`pack.zip` 다운로드](https://github.com/imda564/PrinceOfWar-Unity-AI-Study/releases/download/0.1.0/pack.zip)
- [릴리즈 페이지](https://github.com/imda564/PrinceOfWar-Unity-AI-Study/releases/tag/0.1.0)

### 실행 방법

1. `pack.zip`을 다운로드합니다.
2. 압축을 풉니다.
3. `Prince of war.exe`를 실행합니다.

목표는 아군 유닛을 적 성문까지 호위하는 것입니다. 사기가 0이 되지 않게 버티면서, 시간이 지날 때마다 들어오는 골드로 유닛을 소환하거나 영웅을 회복시키면 됩니다.

### 조작키

| 상황 | 조작 |
| --- | --- |
| 메인 메뉴 | `Enter` / `Space`로 이어하기 |
| 메인 메뉴 | `S` 스테이지 선택, `O` 옵션, `C` 크레딧 |
| 스테이지 선택 | 스테이지 카드를 클릭하거나 `0`으로 Meteor Test, `1`-`9`로 빠른 선택 |
| 전투 | `WASD` / 방향키로 영웅 이동 |
| 전투 | `Space` / 마우스 왼쪽 버튼으로 공격 |
| 전투 | `Shift`로 영웅 회복, 골드 25 소모 |
| 전투 | 하단 유닛 버튼 클릭 또는 `1`-`9`로 아군 소환 |
| 전투 | `Esc`로 일시정지 또는 재개 |
| 결과 화면 | 클리어 후 `Enter` 다음 스테이지, `R` 재시작, `Esc` 스테이지 선택 |

### 학습 목적 안내

이 프로젝트는 학습, 연구, 포트폴리오 공부 목적으로만 사용됩니다. 상업적 배포나 공식 제품으로의 재배포를 목적으로 하지 않습니다. 일부 시각 자료와 추출 리소스는 유사한 게임 구조를 Unity에서 어떻게 구현할 수 있는지 공부하기 위한 참고 용도로만 사용되었습니다.

## Unity Version

Open with Unity `6000.4.9f1` or a compatible Unity 6 editor.

## Repository Contents

The Unity project source is kept in:

- `Assets/`
- `Packages/`
- `ProjectSettings/`

Generated folders such as `Library/`, `Temp/`, `Logs/`, `obj/`, IDE project files, local builds, and reference extraction data are excluded by `.gitignore`.
