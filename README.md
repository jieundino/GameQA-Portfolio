# Unity Game Systems Portfolio

> **이 레포지토리는 QA 인턴 지원을 위해 재구성한 포트폴리오입니다.**  
> 게임을 직접 만든 개발자로서, **버그를 발견하고 재현 조건을 정의하고 수정하고 검증한 경험**을 함께 담았습니다.

Unity C# 기반 게임 클라이언트 프로그래머 포트폴리오로,  
제가 참여한 팀 프로젝트 『필연과 우연』과 『네 발자국』에서 직접 구현한 주요 시스템 코드들을 정리한 것입니다.

---

## 🔍 QA 관점에서 보는 이 포트폴리오

이 레포지토리의 각 시스템 README에는 **구현 내용뿐 아니라, 개발 과정에서 직접 발견한 버그와 재현 조건, 수정 방법**을 함께 정리했습니다.

게임을 만들면서 자연스럽게 겪은 일들이지만, 돌아보니 QA에서 말하는 프로세스와 다르지 않았습니다.

- 어떤 조건에서 버그가 생기는지 **재현 조건**을 찾고
- 코드 어디가 문제인지 **원인을 파악**하고
- 수정 후 **동일 조건으로 다시 테스트**해서 확인했습니다

특히 **Log Tracking System**은 학술 연구를 위해 직접 설계한 시스템으로,  
플레이어 행동 데이터를 Google Spreadsheet로 수집하고 중복·소실 없이 안정적으로 전송하는 구조를 구현했습니다.  
이 경험이 **로그 기반 버그 추적**에 대한 관심으로 이어졌습니다.

---

## 📌 프로젝트 개요

### 『필연과 우연』
멀티엔딩 방탈출 어드벤처 게임  
→ 퍼즐, 상호작용 오브젝트, 행동력/날짜 시스템, 룸 매니저 등 핵심 게임플레이 시스템 구현  
→ **Stove, App Store 출시** / 2025 BIC 전시 참여

### 『네 발자국』
반려동물 유기를 주제로 한 2D 내러티브 어드벤처  
→ 대화(Dialogue), 이벤트(Event), 결과(Result) 처리 시스템을 데이터 주도 설계 기반으로 구현  
→ 학술 연구용 **로그 수집 시스템** 직접 설계 및 구현  
→ **Stove 출시** / 학술저널 제1저자 게재

---

## 🛠️ 기여한 주요 시스템

| 시스템 | 프로젝트 | 핵심 키워드 |
|--------|---------|------------|
| Puzzle Systems | 필연과 우연 | 드래그 앤 드롭, 정답 검증, 드롭존 제약 |
| Interaction Systems | 필연과 우연 | EventObject 공통 인터랙션, Chair·Drawers 개별 동작 |
| Action Point System | 필연과 우연 | 추상 클래스 기반 행동력/날짜 전환, 방별 확장 |
| Room Manager | 필연과 우연 | 시점 전환, UI 제어, 무한 입력 방지 |
| Sound System | 필연과 우연 | 우선순위 채널 관리, 보이스 스틸링, 디바운스 |
| Dialogue System | 네 발자국 | Queue 기반 비동기 충돌 방지, 텍스트 연출 |
| Event & Result System | 네 발자국 | CSV 파싱, 조건/결과 파이프라인, 데이터 주도 설계 |
| Save System | 네 발자국 | Atomic Write, Fallback, 데이터 무결성 |
| **Log Tracking System** | **네 발자국** | **플레이 로그 수집, 큐 기반 재전송, 중복 방지** |

---

## 📂 Quick Links

### 『필연과 우연』
- [Puzzle Systems](./FateAndAccidy/PuzzleSystems/README.md) — 드래그 앤 드롭 퍼즐, 드롭존 제약 버그 경험
- [Interaction Systems](./FateAndAccidy/InteractableObjects/README.md) — EventObject 공통 구조, Chair·Drawers 개별 동작
- [Action Point System](./FateAndAccidy/ActionPointSystem/README.md) — 행동력/날짜 시스템, 연속 클릭 버그 경험
- [Room Manager](./FateAndAccidy/RoomManager/README.md) — 시점 전환, UI 상태 관리
- [Sound System](./FateAndAccidy/SoundSystem/README.md) — 오디오 풀 점유 버그, 우선순위 채널 최적화

### 『네 발자국』
- [Dialogue System](./FourFootsteps/DialogueSystem/README.md) — 비동기 대화 충돌 버그 경험
- [Event System](./FourFootsteps/EventSystem/README.md) — AND/OR 조건 누락 버그 경험
- [Result System](./FourFootsteps/ResultSystem/README.md) — 데이터 주도 결과 실행 파이프라인
- [Save System](./FourFootsteps/SaveSystem/README.md) — Atomic Write, 역직렬화 버그 경험
- [**Log Tracking System**](./FourFootsteps/LogSystem/README.md) — **플레이 로그 수집 및 QA 활용**

---

## 🎬 유튜브 시연 영상

- [『필연과 우연』](https://youtu.be/2FwwzZZWBQk) — 멀티엔딩 방탈출 어드벤처 게임
- [『네 발자국』](https://youtu.be/NoYuWTQv6eI) — 반려동물 유기를 주제로 한 2D 내러티브 어드벤처
