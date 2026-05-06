using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class EndingLogQueueManager : MonoBehaviour
{
    public static EndingLogQueueManager Instance;

    [Header("Google Apps Script Web App")]
    [SerializeField] private string webAppUrl = "https://script.google.com/macros/s/AKfycbzQVEKPEqz4LasQJ1i6g4JDlZupZAYHVycJh162GRhHOgeiqP9I5usooWxcO8p_stq5YA/exec";
    [SerializeField] private string apiKey = "1xwbc8Pgr39OpQudPEozyjl5PFthM0u_FourfootstepsLog";

    [Header("Queue / Retry")]
    [SerializeField] private int maxQueueSize = 20;
    [SerializeField] private int maxRetryPerSession = 3;

    private const string UUID_KEY = "ANON_UUID";
    private const string QUEUE_KEY = "ENDING_LOG_QUEUE_JSON";
    private const string RUN_ID_KEY = "RUN_ID";

    private bool isSending;
    private int retryCountThisSession;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // 게임 시작 시, 대기 큐가 있으면 자동 재전송 시도
        StartCoroutine(FlushQueue());
    }

    public string GetOrCreatePlayerKey(string playerName, string catName)
    {
        // 익명 UUID 기반
        string uuid = GetOrCreateUuid();
        return uuid; // 또는 $"{uuid}_{playerName}_{catName}";
    }

    public string GetCurrentRunId()
    {
        if (PlayerPrefs.HasKey(RUN_ID_KEY))
            return PlayerPrefs.GetString(RUN_ID_KEY);

        // 없으면 “현재 세션 최초”로 하나 만들어 둠(안전장치)
        string runId = System.Guid.NewGuid().ToString("N");
        PlayerPrefs.SetString(RUN_ID_KEY, runId);
        PlayerPrefs.Save();
        return runId;
    }

    public string StartNewRun()
    {
        string runId = System.Guid.NewGuid().ToString("N");
        PlayerPrefs.SetString(RUN_ID_KEY, runId);
        PlayerPrefs.Save();
        return runId;
    }

    public void EnqueueAndSend(EndingLogPayload payload)
    {
        // 큐에 넣고 즉시 전송 시도
        Enqueue(payload);
        if (!isSending) StartCoroutine(FlushQueue());
    }

    private void Enqueue(EndingLogPayload payload)
    {
        var list = LoadQueue();
        if (list.Count >= maxQueueSize)
        {
            // 오래된 것부터 제거
            list.RemoveAt(0);
        }
        list.Add(payload);
        SaveQueue(list);
    }

    private List<EndingLogPayload> LoadQueue()
    {
        string json = PlayerPrefs.GetString(QUEUE_KEY, "");
        if (string.IsNullOrEmpty(json)) return new List<EndingLogPayload>();

        try
        {
            // JsonUtility는 List 직렬화가 불편하므로 배열 래퍼 사용
            var wrapper = JsonUtility.FromJson<PendingLogWrapper>(json);
            if (wrapper?.items == null) return new List<EndingLogPayload>();
            return new List<EndingLogPayload>(wrapper.items);
        }
        catch
        {
            return new List<EndingLogPayload>();
        }
    }

    private void SaveQueue(List<EndingLogPayload> list)
    {
        var wrapper = new PendingLogWrapper { items = list.ToArray() };
        string json = JsonUtility.ToJson(wrapper);
        PlayerPrefs.SetString(QUEUE_KEY, json);
        PlayerPrefs.Save();
    }

    private IEnumerator FlushQueue()
    {
        if (isSending) yield break;
        isSending = true;

        var queue = LoadQueue();
        if (queue.Count == 0)
        {
            isSending = false;
            yield break;
        }

        retryCountThisSession = 0;

        while (queue.Count > 0)
        {
            var payload = queue[0];

            bool ok = false;
            yield return StartCoroutine(PostPayload(payload, success => ok = success));

            if (ok)
            {
                // 성공 → 큐에서 제거
                queue.RemoveAt(0);
                SaveQueue(queue);
                retryCountThisSession = 0;
            }
            else
            {
                retryCountThisSession++;
                if (retryCountThisSession >= maxRetryPerSession)
                {
                    // 이번 세션에서는 더 무리하지 않고 중단
                    break;
                }

                // 잠깐 대기 후 재시도
                yield return new WaitForSeconds(2f);
            }
        }

        isSending = false;
    }

    private IEnumerator PostPayload(EndingLogPayload payload, Action<bool> onDone)
    {
        // apiKey 주입
        payload.apiKey = (apiKey ?? "").Trim();        
        //payload.gameVersion = Application.version;
        //payload.platform = Application.platform.ToString();

        string json = JsonUtility.ToJson(payload);
        //Debug.Log("[EndingLog] Sending JSON: " + json);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        using (UnityWebRequest req = new UnityWebRequest(webAppUrl, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[EndingLog] Upload failed: {req.error}");
                onDone(false);
                yield break;
            }

            // 응답 확인(선택)
            string resp = req.downloadHandler.text ?? "";
            if (!resp.Contains("\"ok\":true"))
            {
                Debug.LogWarning($"[EndingLog] Upload response not ok: {resp}");
                onDone(false);
                yield break;
            }

            Debug.Log($"[EndingLog] Upload success: {resp}");
            onDone(true);
        }
    }

    private string GetOrCreateUuid()
    {
        if (PlayerPrefs.HasKey(UUID_KEY)) return PlayerPrefs.GetString(UUID_KEY);

        string uuid = Guid.NewGuid().ToString("N");
        PlayerPrefs.SetString(UUID_KEY, uuid);
        PlayerPrefs.Save();
        return uuid;
    }

    [Serializable]
    private class PendingLogWrapper
    {
        public EndingLogPayload[] items;
    }
}
