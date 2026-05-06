# Room Manager

플레이어의 시점 전환, 확대 화면, UI 버튼 상태 등을 통합 관리하는 핵심 매니저입니다.

---

## 주요 기능

- **시점 이동** (`MoveSides`) — 좌/우 시점 전환, 튜토리얼 연동
- **확대/조사 상태 관리** — `isInvestigating`, `isZoomed` 플래그로 상태 통합 관리
- **UI 버튼 표시 규칙** (`SetButtons`) — 대화·조사·메모·랩탑 상태에 따라 버튼 동적 제어
- **행동력 시스템 연동** — `ActionPointManager`와 연결, 행동력 소진 시 날짜 전환 트리거
- **무한 입력 방지** (`ProhibitInput`) — 하트가 없는 상태에서 추가 입력 차단

---

## 구조 다이어그램

```
[RoomManager]
├─ MoveSides()       → 시점 전환, 튜토리얼 연동
├─ OnExitButtonClick() / ExitToRoot()
│     └─ 조사·확대 상태 해제 + RefillHeartsOrEndDay 지연 실행 체크
├─ SetButtons()      → UIManager와 다중 상태 동기화
│     (isInvestigating, isZoomed, isDialogueActive, isMemoOpen, isLaptopOpen)
└─ ProhibitInput()   → heartParent.childCount 기반 무한 입력 방지
```

---

## 🐛 버그 경험 및 QA 프로세스

### BUG-001 · 조사/대화 중 이동 버튼 활성화

| 항목 | 내용 |
|------|------|
| **발견 방법** | 오브젝트 조사창이 열려 있는 상태에서 좌우 이동 버튼이 보이는 것을 발견 |
| **재현 조건** | 오브젝트 클릭 → 조사창 열림 → 이동 버튼 클릭 시도 |
| **증상** | 조사창이 열린 채로 시점이 전환되거나, 이동 버튼이 불필요하게 노출 |
| **원인** | 조사/대화/확대 상태가 변할 때마다 버튼 상태를 동기화하는 로직이 통합되지 않음 |
| **수정** | `SetButtons()`를 단일 진입점으로 통합 — 모든 상태 플래그를 한 번에 읽어 버튼 표시 결정 |
| **검증** | 각 상태(조사·대화·메모·랩탑)별 조합 시나리오에서 버튼 노출 여부 확인 |

```csharp
// RoomManager.cs
public void SetButtons()
{
    bool isInvestigatingOrZoomed = isInvestigating || isZoomed;
    bool isDialogueActive = DialogueManager.Instance.isDialogueActive;
    bool isMemoOpen = MemoManager.Instance.isMemoOpen;
    bool isLaptopOpen = (bool)GameManager.Instance.GetVariable("isLaptopOpen");

    SetExitButton((isInvestigatingOrZoomed && !isDialogueActive) || ...);
    SetMoveButtons(!isInvestigatingOrZoomed && !isDialogueActive && !isMemoOpen);
}
```

> **알게 된 점:** 여러 상태가 동시에 존재하는 UI 시스템에서는, 개별 상태마다 버튼을 건드리는 것보다 하나의 함수에서 전체 상태를 읽어 한 번에 처리하는 것이 훨씬 안전하다는 걸 경험했습니다.

---

### BUG-002 · 행동력 소진 시 날짜 전환이 조사창 닫히기 전에 실행

| 항목 | 내용 |
|------|------|
| **발견 방법** | 오브젝트를 조사하는 도중 마지막 행동력이 소모됐을 때 날짜 전환 연출이 즉시 실행되는 것을 발견 |
| **재현 조건** | 오브젝트 조사창 열림 상태에서 마지막 행동력 소모 |
| **증상** | 조사창이 열린 채로 날짜 전환 애니메이션 실행 → 화면 연출 겹침 |
| **원인** | `RefillHeartsOrEndDay()` 즉시 호출 |
| **수정** | `OnExitButtonClick()` 에서 `RefillHeartsOrEndDay` 변수 확인 → 조사창 닫힌 후 실행 |
| **검증** | 조사 도중 행동력 소진 → 조사창 닫힌 후 날짜 전환 정상 실행 확인 |

```csharp
// RoomManager.cs
public void OnExitButtonClick()
{
    if (isInvestigating) imageAndLockPanelManager.OnExitButtonClick();
    // ...
    var refillHeartsOrEndDay = (bool)GameManager.Instance.GetVariable("RefillHeartsOrEndDay");
    if (refillHeartsOrEndDay)
        actionPointManager.RefillHeartsOrEndDay();  // 조사 종료 후 지연 실행
}
```
