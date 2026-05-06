# Action Point System

행동력(하트)과 날짜 전환 시스템입니다.  
공통 베이스 클래스와 방(Room)별 확장 클래스로 나누어 설계했습니다.

---

## 주요 기능

- **ActionPointManager (Base)**
  - 하트 배열 생성 및 관리
  - 날짜 전환 애니메이션 (기어 회전, 페이지 넘김, 페이드 효과)
  - 공통 추상 메서드 정의 (`CreateHearts`, `DecrementActionPoint`, `RefillHeartsOrEndDay`, `nextMorningDay`)

- **Room1ActionPointManager**
  - 기본 방 로직: 행동력 소진 시 귀가 이벤트 → 엔딩 or 다음날 전환
  - Room2 진입 전 행동력 관련 변수 초기화 (`InitActionPointVariables`)

- **Room2ActionPointManager**
  - 특수 아이템(회복제) 사용 시 하트 2개 보너스 (`EatEnergySupplement`)
  - 특정 곰인형 고치기 이벤트로 하루 최대 행동력 5 → 7로 확장
  - Room2 전용 날짜 오프셋(`ROOM2_DAY_OFFSET`) 적용

---

## 구조 다이어그램

```
[ActionPointManager]  (Abstract)
├─ CreateHearts()
├─ DecrementActionPoint()
├─ RefillHeartsOrEndDay()
└─ nextMorningDay()

[Room1ActionPointManager]
└─ 기본 로직: 행동력 소진 → 귀가 이벤트 → 엔딩 or 다음날

[Room2ActionPointManager]
└─ 확장 로직: 회복제 아이템(하트 +2), 곰인형 이벤트(최대 하트 확장)
```

---

## 🐛 버그 경험 및 QA 프로세스

### BUG-001 · 퍼즐 연속 클릭 시 행동력 중복 감소

| 항목 | 내용 |
|------|------|
| **발견 방법** | 플레이 테스트 중 퍼즐 오브젝트를 빠르게 반복 클릭하다가 직접 발견 |
| **재현 조건** | 퍼즐 오브젝트를 짧은 간격(연속)으로 3회 이상 클릭 |
| **재현율** | 100% |
| **증상** | 행동력(하트)이 1개가 아닌 2~3개 중복 감소 |
| **원인** | `DecrementActionPoint()` 호출이 중복되는 상황에서, 하트가 이미 없는 상태임에도 감소 로직이 재진입 |
| **수정** | `heartParent.transform.childCount < 1` 조건 추가 → 하트가 없으면 감소 로직 즉시 차단 |
| **검증** | 동일한 연속 클릭 조건 반복 테스트 → 하트가 1개씩만 감소하는 것 확인 |

```csharp
// Room1ActionPointManager.cs
public override void DecrementActionPoint()
{
    // Avoid ActionPoint Decrease errors when you click a puzzle in a row
    if (heartParent.transform.childCount < 1)
        return;
    // ...
}
```

> **알게 된 점:** 단순한 감소 로직처럼 보여도, 비정상 입력(빠른 연속 클릭)에 대한 방어 처리가 없으면 상태값이 꼬인다는 걸 경험했습니다. 이후 다른 시스템에서도 비슷한 가드 패턴을 먼저 고려하게 됐습니다.

---

### BUG-002 · 대화·조사 중 날짜 전환 로직 중복 실행

| 항목 | 내용 |
|------|------|
| **발견 방법** | 대사 출력 중 행동력이 0이 되는 타이밍에서 날짜 전환 연출이 비정상 실행됨을 발견 |
| **재현 조건** | 다이얼로그 활성 상태 또는 오브젝트 조사 중 마지막 행동력 소모 |
| **증상** | `RefillHeartsOrEndDay()` 가 대사 도중 바로 호출되어 날짜 전환 연출과 대사가 겹침 |
| **원인** | 대화/조사 상태 확인 없이 `RefillHeartsOrEndDay()` 를 즉시 호출 |
| **수정** | `isDialogueActive`, `isInvestigating` 플래그 확인 후 즉시 실행 또는 `refillHeartsOrEndDayState = true` 로 지연 처리 |
| **검증** | 대사 도중 행동력 소모 → 대사 종료 후 정상적으로 날짜 전환 확인 |

```csharp
if (actionPoint % actionPointsPerDay == 0)
{
    bool isDialogueActive = DialogueManager.Instance.isDialogueActive;
    bool isInvestigating = RoomManager.Instance.GetIsInvestigating();
    if (!isDialogueActive && !isInvestigating)
        RefillHeartsOrEndDay();
    else
        refillHeartsOrEndDayState = true;  // 대사/조사 종료 후 실행 예약
}
```

> **알게 된 점:** 여러 시스템이 동시에 동작하는 환경에서는, 특정 액션이 실행되는 타이밍을 명확하게 제어하지 않으면 예상치 못한 순서로 실행될 수 있다는 것을 경험했습니다.
