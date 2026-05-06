# Save System

게임 데이터의 저장·로드·초기화를 담당하는 시스템입니다.  
강제 종료 상황에서도 데이터가 파손되지 않도록 Atomic Write 방식을 도입했습니다.

---

## 주요 기능

- **Atomic Write** — `.tmp` 임시 파일 생성 후 `File.Replace()`로 원자적 교체 → 저장 중 강제 종료 시 데이터 파손 방지
- **Newtonsoft.Json 직렬화** — `Dictionary<string, object>` 복합 타입 안정적 직렬화/역직렬화
- **타입 정보 함께 저장** — `variablesToJson` + `variablesTypeToJson` 쌍으로 저장 → 타입 손실 없이 복원
- **레거시 데이터 복구 (Fallback)** — 구버전 세이브의 타입 불일치(`System.Int32` 등) 자동 정규화
- **Lock 동기화** — `_saveLock` 으로 멀티스레드 환경 안전 보장
- **부분 저장** — `SaveVariable()`은 안전을 위해 전체 저장으로 통합 처리

---

## 구조 다이어그램

```
[SaveGameData()]
│
├─ lock(_saveLock) → 멀티스레드 동기화
├─ JsonConvert.SerializeObject(saveData)
└─ AtomicWrite(path, json)
      ├─ File.WriteAllText(tmp)
      └─ File.Replace(tmp, path, bak)  ← 원자적 교체

[ApplySavedGameData()]
│
├─ File.ReadAllText(path)
├─ JsonConvert.DeserializeObject<SaveData>
├─ [레거시 복구] MemoryPuzzleStates가 string이면 dict<int,bool>로 변환
└─ GameManager.Instance.Variables = loadedVars
```

---

## 🐛 버그 경험 및 QA 프로세스

### BUG-001 · 세이브/로드 반복 시 점수 중복 합산

| 항목 | 내용 |
|------|------|
| **발견 방법** | 중요 이벤트 중 종료 후 이어하기로 여러 번 해당 이벤트를 반복했을 때, 책임감 점수가 비정상적으로 높아지는 것을 발견 |
| **재현 조건** | 엔딩 진입 → 저장 → 새 게임 시작 → 동일 이벤트 발생 시 점수가 이미 높은 값에서 시작 |
| **증상** | 점수가 누적되어 엔딩 분기가 의도와 다르게 결정됨 |
| **원인** | `ResponsibilityScore`를 이벤트 발생마다 누적하는 방식인데, 세이브에 저장된 값이 초기화되지 않고 로드됨 |
| **수정** | 점수를 저장된 상태값으로부터 매번 재계산(Derive)하는 구조로 개편하고, 새 게임 시작 시 `LoadInitGameData()`로 초기화 |
| **검증** | 저장 → 새 게임 → 점수 초기화 확인 / 동일 플레이 경로에서 동일 점수 재현 확인 |

---

### BUG-002 · `MemoryPuzzleStates` 역직렬화 에러 (레거시 세이브 호환성)

| 항목 | 내용 |
|------|------|
| **발견 방법** | 구버전으로 저장된 파일을 신버전에서 로드할 때 `ArgumentException` 발생 |
| **재현 조건** | `MemoryPuzzleStates`가 `string` 타입으로 저장된 구버전 세이브 파일 로드 |
| **증상** | 세이브 로드 실패, 게임 데이터 초기화 |
| **원인** | `Dictionary<int, bool>` 타입이 직렬화 과정에서 타입명 문자열(`"System.Collections.Generic.Dictionary\`2[...]"`)로 저장되는 버그가 구버전에 존재 |
| **수정** | `ApplySavedGameData()`에서 `MemoryPuzzleStates` 값이 `string` 타입이면 GameManager 초기값으로 자동 복구 |
| **검증** | 구버전 세이브 파일 로드 → 경고 로그 출력 + 정상 데이터로 복원 확인 |

```csharp
// SaveManager.cs
if (loadedVars.TryGetValue("MemoryPuzzleStates", out object memVal) && memVal is string)
{
    // 레거시 세이브 복구: string → dict<int,bool>
    loadedVars["MemoryPuzzleStates"] = new Dictionary<int, bool>(defaultDict);
    Debug.Log("[SaveManager] Fixed legacy MemoryPuzzleStates.");
}
```

> **알게 된 점:** 저장 시스템은 "현재 버전"만이 아니라 "이전 버전에서 저장한 데이터"도 처리할 수 있어야 한다는 것을 경험했습니다. 특히 타입 정보가 함께 저장되지 않으면 역직렬화 시 타입이 꼬일 수 있다는 점, 그리고 Fallback 로직이 없으면 기존 유저의 데이터가 날아갈 수 있다는 점을 직접 겪었습니다.

---

### BUG-003 · 강제 종료 시 세이브 파일 파손

| 항목 | 내용 |
|------|------|
| **발견 방법** | 저장 도중 프로세스를 강제 종료했을 때 세이브 파일이 빈 파일이 되는 것을 발견 |
| **재현 조건** | `SaveGameData()` 실행 중 강제 종료 (Task Manager 등) |
| **증상** | 세이브 파일이 0바이트로 초기화되어 게임 데이터 전체 손실 |
| **원인** | `File.WriteAllText(path)` 직접 쓰기는 쓰기 도중 중단 시 파일이 비워진 상태로 남음 |
| **수정** | Atomic Write 도입: `.tmp` 파일에 먼저 쓰고 `File.Replace()`로 원자적 교체 → `.bak` 백업도 함께 유지 |
| **검증** | 저장 중 강제 종료 → 재시작 후 직전 세이브 데이터 정상 복원 확인 |

```csharp
// SaveManager.cs
private static void AtomicWrite(string path, string content)
{
    string tmp = path + ".tmp";
    File.WriteAllText(tmp, content);       // 임시 파일에 먼저 쓰기
    try {
        File.Replace(tmp, path, path + ".bak", true);  // 원자적 교체 + 백업
    } catch {
        if (File.Exists(path)) File.Delete(path);
        File.Move(tmp, path);
    }
}
```
