# Puzzle Systems

반짇고리 퍼즐 시스템 구현 코드입니다.  
플레이어는 드래그 앤 드롭으로 비즈를 올바른 위치에 배치해야 퍼즐을 클리어할 수 있습니다.

---

## 주요 기능

- **SewingBoxPuzzle**
  - Dictionary 기반 정답 테이블로 퍼즐 검증 (`CheckBeadsAnswer`)
  - 정답 시 게임 변수 업데이트 및 이벤트 호출
  - 중복 정답 판정 방지 (`isBeadsCorrect` 플래그)
  - 무한 입력 방지 연동 (`RoomManager.ProhibitInput`)

- **SewingBoxBead**
  - 드래그 앤 드롭 이벤트 처리 (`IBeginDragHandler`, `IDragHandler`, `IEndDragHandler`)
  - 최근접 드롭존 탐색 (`GetClosestDropZone`) — 월드 좌표 → 스크린 좌표 변환
  - 행(Row) 제약 조건 검사 (`GetValidDropZone`) — 비즈 간 겹침 방지
  - 부드러운 이동 애니메이션 (`SmoothMoveToParent`, `Vector3.Lerp`)

---

## 구조 다이어그램

```
[SewingBoxPuzzle]
├─ BeadsAnswer (Dictionary<int, int>): 정답 테이블
├─ CompareBeads() → CheckBeadsAnswer() → 정답 판정
└─ EventManager.CallEvent("EventSewingBoxB") 호출

[SewingBoxBead]
├─ OnBeginDrag / OnDrag / OnEndDrag
├─ GetClosestDropZone() — 거리 기반 최근접 드롭존 탐색
├─ GetValidDropZone()   — 행 제약 조건 검사 (비즈 겹침 방지)
└─ SmoothMoveToParent() — Lerp 보간 이동 애니메이션
```

---

## 🐛 버그 경험 및 QA 프로세스

### BUG-001 · 비즈 드래그 시 겹침 및 잘못된 드롭존 진입

| 항목 | 내용 |
|------|------|
| **발견 방법** | 퍼즐 테스트 중 비즈를 빠르게 드래그했을 때 비즈가 겹치는 것을 직접 발견 |
| **재현 조건** | Row 1의 비즈를 빠른 속도로 드래그하거나, 다른 비즈가 이미 있는 드롭존에 드롭 |
| **재현율** | 특정 순서·속도에서 높은 확률로 재현 |
| **증상** | 두 비즈가 같은 드롭존에 겹쳐서 배치되거나, 의도하지 않은 드롭존에 진입 |
| **원인** | 최근접 드롭존(`GetClosestDropZone`)만 계산하고, 같은 행(Row)의 다른 비즈 위치를 고려하지 않음 |
| **수정** | `GetValidDropZone()`을 별도로 구현 — `beadRow`, `otherCol` 비교 로직으로 이미 다른 비즈가 있는 경우 허용 열을 강제로 조정 |
| **검증** | 동일한 드래그 패턴 반복 테스트 → 겹침 없이 정상 배치 확인 |

```csharp
// SewingBoxBead.cs
RectTransform GetValidDropZone(RectTransform target)
{
    if (beadRow != 1) return target;

    int targetCol = ParseColumn(target.name);
    int otherCol  = FindBeadColumn(otherBeadNumber);

    int allowedCol = -1;
    if (beadNameNumber == 1 && targetCol >= otherCol)
        allowedCol = otherCol - 1;   // 왼쪽 비즈는 오른쪽 비즈 왼편으로만
    else if (beadNameNumber == 2 && targetCol <= otherCol)
        allowedCol = otherCol + 1;   // 오른쪽 비즈는 왼쪽 비즈 오른편으로만
    // ...
}
```

> **알게 된 점:** UI 상호작용은 단순해 보여도, 여러 오브젝트가 같은 공간을 공유하는 상황에서는 데이터 기반 제약 로직이 없으면 불안정하다는 걸 경험했습니다. 이후 인터랙션 시스템을 설계할 때 "동시 입력 상황"을 먼저 고려하게 됐습니다.

---

### BUG-002 · 정답 판정 후 퍼즐 재클릭 시 이벤트 중복 호출

| 항목 | 내용 |
|------|------|
| **발견 방법** | 퍼즐을 맞춘 뒤 뚜껑 버튼을 다시 클릭했을 때 이벤트가 재실행되는 것을 발견 |
| **재현 조건** | 퍼즐 정답 판정 완료 후 버튼 재클릭 |
| **증상** | 이미 완료된 퍼즐인데 정답/오답 이벤트가 다시 호출됨 |
| **원인** | `CompareBeads()` 에 정답 완료 상태를 확인하는 가드가 없었음 |
| **수정** | `isBeadsCorrect` 플래그 추가 → 이미 정답인 경우 즉시 `return` |
| **검증** | 정답 완료 후 버튼 반복 클릭 → 이벤트 중복 호출 없음 확인 |

```csharp
// SewingBoxPuzzle.cs
public void CompareBeads()
{
    RoomManager.Instance.ProhibitInput();
    if (isBeadsCorrect) return;  // 이미 정답이면 중복 실행 차단
    // ...
}
```
