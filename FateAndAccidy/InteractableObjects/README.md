# Interaction Systems

게임 내 모든 상호작용 오브젝트의 공통 구조와 개별 동작 구현 코드입니다.  
`EventObject`를 공통 베이스로, `Chair`·`Drawers` 등 개별 오브젝트가 확장하는 구조입니다.

---

## 주요 기능

- **EventObject (Base)**
  - 마우스 클릭 시 이벤트 호출 (`OnMouseDown`)
  - 조사(isInquiry) 여부에 따라 조사창 or 직접 이벤트 분기
  - `IResultExecutable` 인터페이스를 통해 ResultManager와 연동

- **Chair**
  - 사이드별 의자 위치 이동 (`MoveChair`, `Vector2.Lerp`)
  - 이동 중 상태 플래그(`isMoving`)로 중복 입력 방지
  - `OnEnable` 시 게임 변수 기반 위치 복원 (세이브/로드 대응)

- **Drawers**
  - 윗칸/아랫칸 서랍 독립 이동 처리
  - 열림/닫힘 상태에 따라 사이드별 오브젝트 전환 (`showDrawersInSide`)
  - `UpDrawerMoved` / `DownDrawerMoved` 변수로 상태 관리

---

## 구조 다이어그램

```
[EventObject]  (Base)
├─ OnMouseDown() → isInquiry 분기
│     ├─ true  → Event_Inquiry 호출 (조사창 표시)
│     └─ false → eventId 이벤트 직접 호출
└─ IResultExecutable 인터페이스 연동

[Chair]  extends EventObject, IResultExecutable
├─ ExecuteAction() → ChairMoved 변수 기반 이동 방향 결정
├─ MoveChair(targetPosition) → Lerp 보간 이동
└─ OnEnable() → 세이브 상태 복원

[Drawers]  extends EventObject, IResultExecutable
├─ ExecuteAction() → ToggleDoors()
├─ ExecuteActionMoveDrawer() → Lerp 보간 이동
└─ showDrawersInSide() → 사이드별 열림/닫힘 오브젝트 전환
```

---

## 🐛 버그 경험 및 QA 프로세스

### BUG-001 · 의자 이동 중 중복 클릭 시 위치 오작동

| 항목 | 내용 |
|------|------|
| **발견 방법** | 의자 클릭 후 이동 애니메이션 도중 다시 클릭했을 때 위치가 꼬이는 현상 발견 |
| **재현 조건** | 의자가 이동하는 0.3초 동안 연속 클릭 |
| **증상** | 이동 중인 의자의 목표 위치가 갱신되어 중간 위치에서 멈추거나 반대 방향으로 이동 |
| **원인** | `isMoving` 플래그 확인 없이 `ExecuteAction()`이 재진입 가능 |
| **수정** | `OnMouseDown()`에서 `isMoving` 또는 `isBusy` 상태 확인 후 early return |
| **검증** | 이동 중 연속 클릭 → 현재 이동 완료 후에만 다음 입력 처리 확인 |

```csharp
// Chair.cs
public new void OnMouseDown()
{
    bool isBusy = GameManager.Instance.GetIsBusy();
    if (isMoving || isBusy) return;  // 이동 중이거나 바쁜 상태면 입력 무시
    base.OnMouseDown();
}
```

> **알게 된 점:** 애니메이션이 재생되는 구간에는 반드시 입력을 차단하는 가드가 필요하다는 걸 경험했습니다. `isMoving` 플래그 패턴은 이후 Drawers 시스템에도 동일하게 적용했습니다.

---

### BUG-002 · 세이브 후 재로드 시 의자 위치 초기화

| 항목 | 내용 |
|------|------|
| **발견 방법** | 의자를 이동한 상태로 저장 후 재시작했을 때 의자가 원래 위치로 돌아가는 것을 발견 |
| **재현 조건** | 의자 이동 → 세이브 → 게임 재시작 → 해당 씬 로드 |
| **증상** | 의자 위치가 이동 전 초기 위치로 복원 |
| **원인** | `OnEnable` 에서 게임 변수(`ChairMoved`)를 기반으로 위치를 복원하는 로직이 없었음 |
| **수정** | `OnEnable()`에서 `ChairMoved` 변수 확인 후 이동된 위치 또는 원래 위치로 즉시 설정 |
| **검증** | 이동 → 저장 → 재시작 → 의자 위치 정상 복원 확인 |

```csharp
// Chair.cs
private void OnEnable()
{
    chairMoved = (bool)GameManager.Instance.GetVariable("ChairMoved");
    rectTransform.anchoredPosition = chairMoved ? movedPositions[sideNum] : originalPosition;
}
```
