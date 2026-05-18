# Event System

게임 내 모든 이벤트를 CSV 데이터 기반으로 관리하는 시스템입니다.  
이벤트 ID → 조건(Condition) 확인 → 결과(Result) 실행의 파이프라인 구조입니다.

---

## 주요 기능

- **CSV 파싱 기반 이벤트 로드** — `events.csv`에서 이벤트 정의를 읽어 `Dictionary`에 매핑
- **AND / OR 조건 처리** — `CheckConditions_AND`, `CheckConditions_OR` 로 복합 조건 판별
- **실행 모드 지원** — `Instant`(병렬 즉시) / `Sequential`(순차 코루틴) 선택 가능
- **Function-wrapped Result 지원** — `Result_StartDialogue`, `Result_Increment` 등 함수형 Result 자동 처리
- **싱글턴 + DontDestroyOnLoad** — 씬 전환에도 이벤트 데이터 유지

---

## 구조 다이어그램

```
events.csv 파싱
│
└─ events (Dictionary<string, GameEvent>)
      │
      └─ CallEvent(eventID)
            ├─ 조건 없음 → ExecuteResults() 즉시
            ├─ AND → CheckConditions_AND() → true면 실행
            └─ OR  → CheckConditions_OR()  → true면 실행

ExecuteResults(results, mode)
├─ Instant    → 각 Result를 독립 코루틴으로 병렬 실행
└─ Sequential → ExecuteResultsSequentially() 순차 실행
```

---

## 🐛 버그 경험 및 QA 프로세스

### BUG-001 · 다중 조건 이벤트에서 AND/OR 처리 누락으로 잘못된 분기

| 항목 | 내용 |
|------|------|
| **발견 방법** | 두 조건을 모두 만족해야 하는 이벤트가, 조건 하나만 만족해도 실행되는 것을 발견 |
| **재현 조건** | 조건이 2개 이상이고 Logic이 `AND`로 설정된 이벤트 호출 |
| **증상** | AND 조건임에도 첫 번째 조건만 만족하면 결과가 실행됨 |
| **원인** | `CallEvent()` 내에서 AND/OR 로직 분기가 없고, 조건을 순차 확인하다 첫 번째 true 조건에서 바로 결과 실행 |
| **수정** | `CheckConditions_AND()` / `CheckConditions_OR()` 메서드를 별도로 구현하여 Logic 필드 기반으로 분기 |
| **검증** | AND 이벤트: 조건 하나만 충족 → 미실행 확인 / 모두 충족 → 정상 실행 확인 |

```csharp
// EventManager.cs
if (logic == "AND")
{
    if (CheckConditions_AND(conditions))
        ExecuteResults(results, executionMode);
}
else if (logic == "OR")
{
    if (CheckConditions_OR(conditions))
        ExecuteResults(results, executionMode);
}
```

> **알게 된 점:** 데이터 기반 시스템에서는 데이터(CSV)의 값이 코드 동작을 결정하기 때문에, 가능한 모든 값 조합에 대해 테스트 케이스를 만들어야 한다는 것을 경험했습니다. Logic 필드가 빈 값인 경우도 별도로 처리해야 했습니다.

---

### BUG-002 · 존재하지 않는 Result ID 참조 시 이벤트 전체 중단

| 항목 | 내용 |
|------|------|
| **발견 방법** | CSV에서 오타로 잘못된 Result ID를 입력했을 때, 해당 이벤트 전체가 실행되지 않는 것을 발견 |
| **재현 조건** | `results.csv`에 정의되지 않은 Result ID를 `events.csv`에서 참조 |
| **증상** | 해당 이벤트가 조용히 실패하거나 에러 없이 아무 일도 일어나지 않음 |
| **원인** | `ResultManager.results` Dictionary에 키가 없을 때 예외 처리 없음 |
| **수정** | 키 존재 여부 확인 후 없으면 임시 Result 객체 생성 + `Debug.LogWarning` 출력 |
| **검증** | 잘못된 ID 입력 → 경고 로그 출력 + 나머지 이벤트 정상 실행 확인 |

```csharp
// EventManager.cs
if (ResultManager.Instance.results.ContainsKey(resultIDTrimmed))
    results.Add(ResultManager.Instance.results[resultIDTrimmed]);
else
{
    Debug.LogWarning($"Result ID '{resultIDTrimmed}' not found! Creating temporary result.");
    Result tempResult = new Result(resultIDTrimmed, "", "");
    results.Add(tempResult);
}
```
