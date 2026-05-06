# Dialogue System

게임 내 모든 대사, 선택지, 컷씬 전환을 담당하는 대화 관리 핵심 모듈입니다.  
비동기 이벤트 환경에서 대사 충돌이 발생하지 않도록 Queue 기반으로 설계했습니다.

---

## 주요 기능

- **대화 흐름 관리** — `StartDialogue`, `DisplayDialogueLine`, `TypeSentence`
- **Queue 기반 충돌 방지** — 대사 진행 중 새 대사 요청이 오면 큐에 적재, 순차 실행
- **다양한 대화 출력 모드** — 플레이어 말풍선 / NPC 말풍선 / 내적 독백 / 일반 대화창
- **텍스트 연출 기능** — 타자 효과, 자동 진행, 페이드 아웃, 흔들림 효과
- **선택지(Choice) 시스템** — 플레이어 선택에 따른 대사/튜토리얼 분기
- **한국어 조사 자동 치환** — 유니코드 종성 판별 + 정규식 실시간 처리 (KoreanJosa)

---

## 구조 다이어그램

```
[DialogueManager]
├─ dialogues (Dictionary): 대사 데이터
├─ choices   (Dictionary): 선택지 데이터
├─ dialogueQueue (Queue): 대사 충돌 방지용 큐
│
├─ StartDialogue(id)
│     ├─ isDialogueActive == true → 큐에 적재
│     └─ false → 즉시 실행
│
├─ DisplayDialogueLine() → 텍스트/이미지/모드 세팅
├─ TypeSentence()        → 타자기 효과 코루틴
└─ DisplayChoices()      → 선택지 활성화
      └─ OnChoiceSelected() → 선택된 이벤트 처리
```

---

## 🐛 버그 경험 및 QA 프로세스

### BUG-001 · 대사 도중 새 대사 호출 시 기존 대화 끊김

| 항목 | 내용 |
|------|------|
| **발견 방법** | 대사가 출력되는 도중 이벤트가 연속으로 발생했을 때 대사가 갑자기 끊기거나 순서가 뒤섞이는 것을 발견 |
| **재현 조건** | 대사 진행 중 동일한 사물을 연속으로 조사 버튼을 눌러 동일하거나 다른 이벤트가 `StartDialogue()`를 호출하는 타이밍 |
| **재현율** | 특정 이벤트 조합에서 높은 확률로 재현 |
| **증상** | 진행 중이던 대사가 즉시 종료되거나, 두 대사가 겹쳐 출력 |
| **원인** | `StartDialogue()` 호출 시 `isDialogueActive` 상태를 확인하지 않고 즉시 실행 |
| **수정** | `dialogueQueue`를 도입 — `isDialogueActive == true`이면 큐에 적재하고, 현재 대사 종료 후 자동으로 다음 대사 실행 |
| **검증** | 대사 도중 이벤트 중첩 시나리오 반복 테스트 → 순서대로 정상 출력 확인 |

```csharp
// DialogueManager.cs
public void StartDialogue(string dialogueID)
{
    if (isDialogueActive)
    {
        // 이미 대사 중이면 큐에 적재
        dialogueQueue.Enqueue(dialogueID);
        return;
    }
    isDialogueActive = true;
    // 대사 즉시 시작...
}
```

> **알게 된 점:** 비동기 이벤트 환경에서는 "지금 실행 가능한지"를 반드시 먼저 확인해야 한다는 것, 그리고 실행 불가 상황에서는 즉시 포기하거나 큐에 쌓아 순서를 보장하는 것이 안정적이라는 걸 경험했습니다.

---

### BUG-002 · 선택지가 2개 미만일 때 Skip 버튼 비정상 표시

| 항목 | 내용 |
|------|------|
| **발견 방법** | 대사가 1줄인 경우에도 Skip 버튼이 표시되는 것을 발견 |
| **재현 조건** | 대사 라인 수가 1개인 대화 진입 |
| **증상** | 의미없는 Skip 버튼 노출 |
| **원인** | `lineCount > 1` 조건 확인 없이 Skip 버튼을 무조건 활성화 |
| **수정** | `lineCount`가 2 이상일 때만 Skip 버튼 활성화 |
| **검증** | 1줄·2줄·다줄 대사 각각 진입 → Skip 버튼 표시 여부 정상 확인 |

```csharp
// DialogueManager.cs
int lineCount = dialogues[dialogueID].Lines.Count;
if (lineCount > 1)
    foreach (GameObject skip in skipText) skip.SetActive(true);
else
    foreach (GameObject skip in skipText) skip.SetActive(false);
```
