using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public class EndingLogReporter : MonoBehaviour
{
    [SerializeField] private EndingLogQueueManager queueManager;

    [Header("Save file name")]
    [SerializeField] private string saveFileName = "GameData.json";

    // run 단위 가드 prefix
    private const string SENT_ENDING_PREFIX = "SENT_ENDING_";

    private string SavePath => Path.Combine(Application.persistentDataPath, saveFileName);

    /// <summary>
    /// 엔딩 진입 시 호출: endingSceneName은 현재 씬 또는 엔딩 타입 구분자
    /// endingTypeOrSceneName: "Ending_Bad" 같은 엔딩 타입을 넘기는 것을 권장.
    /// 비워두면 현재 씬명을 사용.
    /// </summary>
    public void ReportEnding(string endingSceneName = null)
    {
        if (queueManager == null)
        {
            Debug.LogWarning("[EndingLogReporter] queueManager is null");
            return;
        }

        string scene = string.IsNullOrEmpty(endingSceneName)
            ? SceneManager.GetActiveScene().name
            : endingSceneName;

        // 아래 방식은 영구 1회 전송 방법
        //// 중복 전송 방지(같은 엔딩 씬에서 여러 번 호출될 가능성 대비)
        //string sentKey = SENT_ENDING_EVENT_PREFIX + scene;
        //if (PlayerPrefs.GetInt(sentKey, 0) == 1)
        //{
        //    Debug.Log("[EndingLogReporter] already sent in this device for this ending scene.");
        //    return;
        //}

        // 1) 현재 runId 확보 (새 게임 시작 시 StartNewRun()이 호출되어 있어야 함)
        string runId = queueManager.GetCurrentRunId();

        // 2) run 단위 중복 전송 가드
        // 같은 run에서 같은 endingType은 1회만 보내고,
        // 다음 run에서는 다시 기록되도록 함.
        string sentKey = $"{SENT_ENDING_PREFIX}{runId}_{scene}";
        if (PlayerPrefs.GetInt(sentKey, 0) == 1)
        {
            Debug.Log($"[EndingLogReporter] already sent for this run. runId={runId}, endingType={scene}");
            return;
        }

        // 3) 저장 데이터에서 이름/고양이 이름을 가져오는 방법
        // 현재 제공된 JSON 구조상 Variables.PlayerName / Variables.YourCatName이 존재합니다.
        // 여기서는 “간단 문자열 탐색”으로 가져오고, 실패하면 빈값 처리.
        string raw = File.Exists(SavePath) ? File.ReadAllText(SavePath) : "";

        string playerName = JsonFieldExtractor.TryGetString(raw, "\"PlayerName\"");
        string catName = JsonFieldExtractor.TryGetString(raw, "\"YourCatName\"");
        int responsibilityScore = JsonFieldExtractor.TryGetInt(raw, "\"ResponsibilityScore\"", -1);

        // 4) MemoryPuzzleStates 정규화 JSON 추출
        string memoryJson = MemoryPuzzleStateExtractor.ExtractNormalizedJsonFromSave(SavePath);

        // 5) playerKey 확보 (기기 UUID 기반)
        string playerKey = queueManager.GetOrCreatePlayerKey(playerName, catName);

        // 6) payload 구성
        // eventType은 “이 로그가 어떤 종류인지” (Ending / NameChange / etc.)
        var payload = new EndingLogPayload
        {
            apiKey = "", // 큐매니저가 주입
            playerKey = playerKey,
            runId = runId,
            eventType = "Ending",
            playerName = playerName,
            catName = catName,
            endingType = scene,
            memoryPuzzleStatesJson = memoryJson,
            responsibilityScore = responsibilityScore
        };

        // 7) eventId 생성 (정석: playerKey + runId + eventType + timestamp)
        payload.eventId = $"{playerKey}_{runId}_{payload.eventType}_{DateTime.UtcNow.Ticks}";

        // 8) 큐에 넣고 전송
        queueManager.EnqueueAndSend(payload);

        // 9) run 단위 가드 저장
        PlayerPrefs.SetInt(sentKey, 1);
        PlayerPrefs.Save();
    }
}

/// <summary>
/// JsonUtility 없이도 간단히 "키":"값" 문자열을 뽑는 최소 도구(정교하진 않지만 Save 형식이 안정적이면 충분)
/// </summary>
public static class JsonFieldExtractor
{
    public static string TryGetString(string json, string keyToken)
    {
        if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(keyToken)) return "";

        int idx = json.IndexOf(keyToken, StringComparison.Ordinal);
        if (idx < 0) return "";

        int colon = json.IndexOf(':', idx);
        if (colon < 0) return "";

        int i = colon + 1;
        while (i < json.Length && char.IsWhiteSpace(json[i])) i++;

        if (i >= json.Length || json[i] != '"') return "";

        // 문자열 슬라이스
        bool escape = false;
        int start = i + 1;
        for (int j = start; j < json.Length; j++)
        {
            char c = json[j];
            if (escape)
            {
                escape = false;
                continue;
            }
            if (c == '\\')
            {
                escape = true;
                continue;
            }
            if (c == '"')
            {
                return json.Substring(start, j - start);
            }
        }
        return "";
    }

    /// <summary>
    /// 숫자 또는 "숫자" 형태의 int 값을 추출
    /// </summary>
    public static int TryGetInt(string json, string keyToken, int defaultValue = -1)
    {
        if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(keyToken))
            return defaultValue;

        int idx = json.IndexOf(keyToken, StringComparison.Ordinal);
        if (idx < 0) return defaultValue;

        int colon = json.IndexOf(':', idx);
        if (colon < 0) return defaultValue;

        int i = colon + 1;
        while (i < json.Length && char.IsWhiteSpace(json[i])) i++;

        // "123" 형태
        if (i < json.Length && json[i] == '"')
        {
            int start = i + 1;
            for (int j = start; j < json.Length; j++)
            {
                if (json[j] == '"')
                {
                    string s = json.Substring(start, j - start);
                    if (int.TryParse(s, out int v)) return v;
                    return defaultValue;
                }
            }
            return defaultValue;
        }
        // 123 형태
        else
        {
            int start = i;
            int j = start;
            while (j < json.Length && (char.IsDigit(json[j]) || json[j] == '-'))
                j++;

            if (j <= start) return defaultValue;

            string s = json.Substring(start, j - start);
            if (int.TryParse(s, out int v)) return v;
            return defaultValue;
        }
    }
}
