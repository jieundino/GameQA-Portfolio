# Sound System

배경음(BGM)과 효과음(SFX) 재생을 통합 관리하는 사운드 플레이어입니다.  
효과음 채널이 제한된 환경에서 중요 사운드가 유실되지 않도록 우선순위 기반으로 설계했습니다.

---

## 주요 기능

- **BGM 관리** — `ChangeBGM`, `SetMuteBGM`, Fade in/out 처리 (`FadeTo`)
- **우선순위 기반 효과음 채널 관리** (`UISoundPlay`)
  - `SfxPriority.High` / `SfxPriority.Low` 로 채널 영역 분리
  - 예약 채널(`reservedHighPriorityChannels`)로 중요 효과음 보호
- **보이스 스틸링 (Voice Stealing)** — High 우선순위 효과음이 Low 채널을 강제로 선점
- **클릭 디바운스** — `Time.unscaledTime` 기반으로 빠른 연속 클릭 효과음 재생 억제
- **라운드 로빈 채널 분산** — `uiSoundPlayerCursor` 로 효과음 채널 공평하게 분배

---

## 구조 다이어그램

```
UISoundPlay(num, priority)
│
├─ [클릭 디바운스 검사] unscaledTime 간격 확인
│
├─ [우선순위 구간 결정]
│     High: [0 ~ reserved)
│     Low:  [reserved ~ end)
│
├─ 1단계: 빈 채널 탐색 (라운드 로빈)
│     빈 채널 있음 → PlayOnUISound() → 종료
│
└─ 2단계: (High만) 보이스 스틸링
      Low 채널 중 재생 중인 것 → Stop() → High 강제 할당
```

---

## 🐛 버그 경험 및 QA 프로세스

### BUG-001 · 클릭음 연속 발생 시 중요 연출음 재생 불가

| 항목 | 내용 |
|------|------|
| **발견 방법** | 오브젝트를 빠르게 연속 클릭했을 때 날짜 전환 BGM이 재생되지 않는 것을 발견 |
| **재현 조건** | 클릭 가능한 오브젝트를 0.05초 이내 간격으로 10회 이상 연속 클릭 (스트레스 테스트) |
| **재현율** | 연속 클릭 횟수에 비례하여 높은 확률로 재현 |
| **증상** | `UISoundPlayer` 배열의 모든 채널이 클릭음으로 점유 → 날짜 전환 효과음 등 중요 연출음이 재생되지 않음 |
| **원인** | 채널 우선순위 구분 없이 선착순으로 채널을 할당하고, 클릭음에 대한 재생 빈도 제한이 없었음 |
| **수정 1** | 클릭 디바운스 도입: `Time.unscaledTime - lastClickTime < clickMinInterval` 조건으로 빠른 연속 클릭 효과음 억제 |
| **수정 2** | `SfxPriority` Enum 도입: High/Low 채널 영역 분리, 예약 채널로 중요 효과음 보호 |
| **수정 3** | 보이스 스틸링: High 우선순위 효과음은 Low 채널 강제 선점 가능 |
| **검증** | 동일한 연속 클릭 조건에서 날짜 전환 효과음 정상 재생 확인 |

```csharp
// SoundPlayer.cs
public void UISoundPlay(int num, SfxPriority prio = SfxPriority.Low)
{
    // 1) 클릭 디바운스
    if (num == Sound_Click && Time.unscaledTime - lastClickTime < clickMinInterval) return;
    if (num == Sound_Click) lastClickTime = Time.unscaledTime;

    // 2) 우선순위 구간 결정
    int start = (prio == SfxPriority.High) ? 0 : reservedHighPriorityChannels;

    // 3) 빈 채널 탐색 (라운드 로빈)
    // 4) High 우선순위 → Low 채널 보이스 스틸링
}
```

> **알게 된 점:** 제한된 리소스(오디오 채널) 안에서 중요도에 따라 자원을 동적으로 할당하는 설계의 중요성을 경험했습니다. 단순한 "빈 채널 선착순" 방식은 스트레스 상황에서 반드시 무너진다는 것도 직접 확인했습니다.
