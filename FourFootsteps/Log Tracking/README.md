# Log Tracking System

학술 연구 실험을 위해 플레이어의 행동 이벤트와 엔딩 결과를  
Google Spreadsheet로 자동 수집하는 시스템입니다.

> 이 시스템은 학술저널 논문(제1저자, 2026.05 게재) 실험 데이터 수집을 위해  
> 직접 설계하고 구현했습니다. 출시 빌드에는 포함되지 않습니다.

---

## QA 관점에서의 의미

이 시스템을 만들면서 **"로그 기반으로 버그를 추적한다"** 는 것이 어떤 의미인지 직접 체감했습니다.

- 어떤 플레이어가 **어떤 경로로** 어떤 엔딩에 도달했는지 로그로 파악 가능
- 특정 엔딩에서 `responsibilityScore`가 예상 범위를 벗어나면 **분기 로직 버그를 의심**할 수 있음
- 중복 로그나 소실 로그가 발생하면 **네트워크 예외 처리 문제**로 추적 가능

실제로 이 시스템을 운영하면서 중복 전송과 로그 소실 문제를 경험하고 직접 해결했습니다.  
그 과정이 **로그 기반 QA**에 대한 관심으로 이어졌습니다.

---

## 주요 기능

- **엔딩 로그 수집** — 플레이어 키, Run ID, 엔딩 타입, 책임감 점수, 퍼즐 상태 수집
- **Queue 기반 재전송** — 전송 실패 시 `PlayerPrefs`에 큐 직렬화 → 재시작 후 자동 재전송
- **RunID 기반 중복 전송 방지** — 같은 Run에서 같은 엔딩은 1회만 전송
- **Google Apps Script 연동** — Unity → GAS → Google Spreadsheet API 파이프라인
- **이벤트 ID 중복 검사** — Spreadsheet 서버 측에서 `eventId` 기반 중복 행 방지

---

## 구조 다이어그램

```
[엔딩 씬 진입]
│
└─ ReportEndingOnStart.Start()
      └─ EndingLogReporter.ReportEnding(endingId)
            ├─ RunID 기반 중복 전송 가드 (PlayerPrefs)
            ├─ SaveData 파싱 → playerName, catName, responsibilityScore 추출
            ├─ MemoryPuzzleStateExtractor → 퍼즐 상태 JSON 추출
            ├─ EndingLogPayload 구성
            └─ EndingLogQueueManager.EnqueueAndSend(payload)
                  ├─ Enqueue → PlayerPrefs 큐에 직렬화 저장
                  └─ FlushQueue()
                        └─ PostPayload() (UnityWebRequest POST)
                              └─ Google Apps Script (API Key 인증 + 중복 eventId 검사)
                                    └─ Google Spreadsheet 기록
```

---

## 🐛 버그 경험 및 QA 프로세스

### BUG-001 · 동일 회차 중복 로그 전송

| 항목 | 내용 |
|------|------|
| **발견 방법** | Spreadsheet에서 같은 플레이 회차의 로그가 2~3개씩 중복으로 기록된 것을 발견 |
| **재현 조건** | 엔딩 씬을 여러 번 로드하거나, 앱을 재시작하면서 이전 큐가 재전송되는 경우 |
| **증상** | 같은 `runId` + `endingType` 조합의 로그가 Spreadsheet에 중복 기록 |
| **원인 1 (클라이언트)** | `ReportEnding()` 호출 시 RunID 기반 중복 전송 가드가 없었음 |
| **원인 2 (서버)** | GAS 서버에서도 `eventId` 중복 검사 로직이 없었음 |
| **수정 1** | 클라이언트: `PlayerPrefs`에 `SENT_ENDING_{runId}_{scene}` 키 저장 → 같은 Run에서 재전송 차단 |
| **수정 2** | 서버: GAS에서 B열(`eventId`) 전체 스캔 → 중복이면 `append` 없이 `ok:true` 반환 |
| **검증** | 동일 엔딩 씬 반복 로드 → Spreadsheet에 1건만 기록 확인 |

```csharp
// EndingLogReporter.cs
string sentKey = $"{SENT_ENDING_PREFIX}{runId}_{scene}";
if (PlayerPrefs.GetInt(sentKey, 0) == 1)
{
    Debug.Log($"[EndingLogReporter] already sent for this run. runId={runId}");
    return;  // 같은 Run의 같은 엔딩은 1회만 전송
}
```

```javascript
// FourFootsteps_logTracking.js (Google Apps Script)
function isDuplicateEventId_(sheet, eventId) {
  const range = sheet.getRange(2, EVENT_ID_COLUMN, lastRow - 1, 1);
  const values = range.getValues();
  for (let i = 0; i < values.length; i++) {
    if (String(values[i][0]) === eventId) return true;  // 중복 발견
  }
  return false;
}
```

---

### BUG-002 · 네트워크 불안정 시 로그 소실

| 항목 | 내용 |
|------|------|
| **발견 방법** | 실험 참가자 중 일부의 데이터가 Spreadsheet에 없는 것을 발견 → 전송 실패로 추정 |
| **재현 조건** | 엔딩 진입 시점에 네트워크가 불안정하거나 앱을 바로 종료한 경우 |
| **증상** | 해당 플레이 회차의 로그가 Spreadsheet에 미기록 |
| **원인** | 전송 실패 시 재시도 로직 없이 그대로 폐기 |
| **수정** | `PlayerPrefs`에 큐 직렬화 → 전송 성공 시까지 세션 내 최대 3회 재시도 → 앱 재시작 후 `FlushQueue()`로 자동 재전송 |
| **검증** | 네트워크 차단 상태에서 엔딩 진입 → 앱 재시작 후 자동 전송 확인 |

```csharp
// EndingLogQueueManager.cs
private IEnumerator FlushQueue()
{
    while (queue.Count > 0)
    {
        bool ok = false;
        yield return StartCoroutine(PostPayload(payload, success => ok = success));

        if (ok)
        {
            queue.RemoveAt(0);
            SaveQueue(queue);     // 성공하면 큐에서 제거
            retryCountThisSession = 0;
        }
        else
        {
            retryCountThisSession++;
            if (retryCountThisSession >= maxRetryPerSession) break;  // 세션 내 최대 재시도
            yield return new WaitForSeconds(2f);
        }
    }
}
```

> **알게 된 점:** 외부 서버와 통신하는 시스템은 "전송이 실패했을 때 어떻게 되는가"를 반드시 설계해야 합니다. 특히 로그 수집처럼 데이터 신뢰성이 중요한 경우, 클라이언트와 서버 양쪽에 중복 방지 로직이 모두 필요하다는 것을 경험했습니다.

---

## 수집 데이터 항목

| 컬럼 | 설명 |
|------|------|
| `timestamp` | 기록 시각 |
| `eventId` | 중복 방지용 고유 ID |
| `playerKey` | 익명 UUID (기기 식별) |
| `runId` | 플레이 회차 구분자 |
| `eventType` | 로그 종류 (Ending 등) |
| `playerName` | 플레이어 입력 이름 |
| `catName` | 고양이 이름 |
| `endingType` | 도달한 엔딩 종류 |
| `memoryPuzzleStatesJson` | 퍼즐 수집 상태 (JSON) |
| `responsibilityScore` | 책임감 점수 |
