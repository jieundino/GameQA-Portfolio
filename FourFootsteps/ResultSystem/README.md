# Result System

이벤트 결과(Result)를 처리하는 허브 시스템입니다.  
CSV 기반으로 정의된 Result ID에 따라 대사 시작, 변수 조정, 페이드 효과, 씬 이동 등 다양한 동작을 수행합니다.

---

## 주요 기능

- **CSV 데이터 주도 설계** — `results.csv` 파싱으로 Result 정의 로드
- **다양한 Result 처리** (`ExecuteResultCoroutine`)
  - `Result_StartDialogue` — 대사 시작 및 비동기 대기
  - `Result_Increment / Decrement / Inverse` — 게임 변수 증감/반전
  - `Result_FadeIn / FadeOut` — 화면 전환 효과
  - `Result_GetMemoryPuzzle` — 퍼즐 획득 애니메이션 + 비동기 대기
  - `Result_MoveToRoom` — 같은 씬 내 방 간 이동 (페이드 포함)
- **IResultExecutable 인터페이스** — 오브젝트별 커스텀 동작 표준화 (`Chair`, `Drawers`, `MemoryPuzzle`)
- **싱글턴 + DontDestroyOnLoad** — 씬 전환에도 데이터 및 등록 오브젝트 유지

---

## 구조 다이어그램

```
[ResultManager]
├─ results (Dictionary<string, Result>): CSV 파싱 데이터
├─ executableObjects (Dictionary<string, IResultExecutable>): 오브젝트 등록
│
├─ RegisterExecutable(name, obj) → 씬 로드 시 오브젝트 등록
├─ InitializeExecutableObjects() → 씬 전환 시 전체 해제
│
└─ ExecuteResultCoroutine(resultID)
      ├─ Result_StartDialogue* → StartDialogue + while(isActive) 대기
      ├─ Result_Increment*     → IncrementVariable
      ├─ Result_FadeIn/Out     → UIManager.OnFade yield
      ├─ Result_GetMemoryPuzzle → ExecuteAction + while(isPuzzleMoving) 대기
      └─ Result_MoveToRoom*    → MoveToRoomCoroutine (페이드+위치이동)
```

---

## 🐛 버그 경험 및 QA 프로세스

### BUG-001 · 씬 전환 후 이전 씬의 오브젝트 참조 잔류

| 항목 | 내용 |
|------|------|
| **발견 방법** | 게임오버로 현재 씬 재로드 후 특정 Result 실행 시 이전 씬의 오브젝트가 호출되어 NullReference 에러 발생 |
| **재현 조건** | 게임오버 이후 현재 씬 재로드 → 이전 상태의 씬에 등록된 IResultExecutable 참조 Result 호출 |
| **증상** | `executableObjects` 딕셔너리에 이전 상태의 씬 오브젝트가 남아 있어 NullReference 발생 |
| **원인** | 씬 전환 시 `executableObjects`를 초기화하지 않음 |
| **수정** | `InitializeExecutableObjects()`를 씬 전환 시점(`RoomManager.Awake`)에 호출하여 전체 초기화 |
| **검증** | 씬 전환 → 이전 씬 Result 호출 → NullReference 없이 정상 처리 확인 |

```csharp
// ResultManager.cs
public void InitializeExecutableObjects()
{
    executableObjects = new Dictionary<string, IResultExecutable>();
}

// RoomManager.cs Awake()
ResultManager.Instance.InitializeExecutableObjects();
```

> **알게 된 점:** `DontDestroyOnLoad`로 유지되는 매니저는 씬 전환 시 이전 씬의 오브젝트 참조를 명시적으로 해제해야 한다는 것을 경험했습니다. 씬 전환 시나리오를 테스트 케이스로 별도로 관리해야 하는 이유이기도 합니다.

---

### BUG-002 · 같은 씬 내 방 간 이동 중복 호출 시 위치 오작동

| 항목 | 내용 |
|------|------|
| **발견 방법** | 방 이동 애니메이션 도중 이동 이벤트가 다시 호출됐을 때 플레이어 위치가 틀어지는 것을 발견 |
| **재현 조건** | `Result_MoveToRoom` 이벤트가 짧은 간격으로 중복 호출 |
| **증상** | 이동 중인 상태에서 다시 이동 시작 → 플레이어가 의도하지 않은 위치에 배치 |
| **원인** | `_isMovingRoom` 플래그 없이 코루틴이 중복 실행 가능 |
| **수정** | `_isMovingRoom` 플래그로 중복 실행 방지 → 이동 중이면 `yield break` |
| **검증** | 이동 중 재호출 → 경고 로그 출력 + 기존 이동 정상 완료 확인 |

```csharp
// ResultManager.cs
private IEnumerator MoveToRoomCoroutine(string roomName, bool useFade = true)
{
    if (_isMovingRoom)
    {
        Debug.LogWarning($"Already moving to a room! (Requested: {roomName})");
        yield break;
    }
    _isMovingRoom = true;
    // ...
    _isMovingRoom = false;
}
```
