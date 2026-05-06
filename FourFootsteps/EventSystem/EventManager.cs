using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EventManager : MonoBehaviour
{
    // EventManager를 싱글턴으로 생성
    public static EventManager Instance { get; private set; }

    private TextAsset eventsCSV;

    // events: dictionary of "GameEvent"s indexed by string "Event ID"
    public Dictionary<string, GameEvent> events = new Dictionary<string, GameEvent>();


    void Awake()
    {
        if (Instance == null)
        {
            eventsCSV = Resources.Load<TextAsset>("Datas/events");
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // ParseConditions와 ParseResults가 먼저 실행되어야 함
            ConditionManager.Instance.ParseConditions();
            ResultManager.Instance.ParseResults();
            ParseEvents();
        }
        else
        {
            Destroy(gameObject);
        }
    }


    // events.csv 파일 파싱
    private void ParseEvents()
    {
        string[] lines = eventsCSV.text.Split('\n');

        for (int i = 1; i < lines.Length; i++)
        {
            string[] fields = lines[i].Split(',');

            if ((string.IsNullOrWhiteSpace(lines[i])) || (fields[0] == "" && fields[1] == "")) continue;

            string eventID = fields[0].Trim();
            string eventName = fields[1].Trim();
            string eventDescription = fields[2].Trim();
            string eventLogic = fields[3].Trim();

            // conditions와 results: '/' 기준으로 스플릿한 리스트
            List<Condition> conditions = new List<Condition>();
            List<Result> results = new List<Result>();

            if (!string.IsNullOrWhiteSpace(fields[4].Trim()))  // 조건이 존재할 때만 수행
            {
                string[] conditionIDs = fields[4].Trim().Split('/');
                foreach (string conditionID in conditionIDs)
                {
                    if (!ConditionManager.Instance.conditions.ContainsKey(conditionID.Trim()))
                    {
                        Debug.Log($"Condition ID \"{conditionID.Trim()}\" not found!");
                        continue;
                    }
                    conditions.Add(ConditionManager.Instance.conditions[conditionID.Trim()]);
                }
            }

            string[] resultIDs = fields[5].Trim().Split('/');
            foreach (string resultID in resultIDs)
            {
                string resultIDTrimmed = resultID.Trim();
                // Function-wrapped results (자동으로 임시 Result 객체 생성)
                if (IsFunctionWrappedResult(resultIDTrimmed))
                {
                    Result tempResult = new Result(resultIDTrimmed, "", "");
                    results.Add(tempResult);
                }
                else
                {
                    // 딕셔너리에 키가 존재하는지 확인 후 추가
                    if (ResultManager.Instance.results.ContainsKey(resultIDTrimmed))
                    {
                        results.Add(ResultManager.Instance.results[resultIDTrimmed]);
                    }
                    else
                    {
                        Debug.LogWarning($"Result ID '{resultIDTrimmed}' not found in ResultManager! Creating temporary result.");
                        // 임시 Result 객체 생성
                        Result tempResult = new Result(resultIDTrimmed, "", "");
                        results.Add(tempResult);
                    }
                }
            }

            // result 실행 모드
            // Instant일 경우 즉각 실행, Sequential일 경우 순차 실행
            string eventExecutionMode = fields.Length > 6 ? fields[6].Trim() : "";
            if (string.IsNullOrEmpty(eventExecutionMode) || results.Count <= 1)
            {
                eventExecutionMode = "Instant";
            }
            else
                eventExecutionMode = eventExecutionMode.Trim();


            if (events.ContainsKey(eventID)) // 이미 존재하는 event ID인 경우: EventLine을 추가
            {
                events[eventID].AddEventLine(eventLogic, conditions, results, eventExecutionMode);
            }
            else // 새로운 event ID인 경우: events에 새로 추가
            {
                // 예약어 event를 피하기 위해 event_라고 이름 지음
                GameEvent event_ = new GameEvent(
                    eventID,
                    eventName,
                    eventDescription
                );
                event_.AddEventLine(eventLogic, conditions, results, eventExecutionMode);
                events[event_.EventID] = event_;
            }
        }
    }

    /// <summary>
    /// Result ID가 함수 형태로 래핑된 결과인지 확인
    /// </summary>
    /// <param name="resultID">확인할 Result ID</param>
    /// <returns>함수 형태의 Result인지 여부</returns>
    private bool IsFunctionWrappedResult(string resultID)
    {
        return resultID.StartsWith("Result_StartDialogue") ||
               resultID.StartsWith("Result_Increment") ||
               resultID.StartsWith("Result_Decrement") ||
               resultID.StartsWith("Result_Inverse") ||
               resultID.StartsWith("Result_JumpToTutorial") ||
               resultID.StartsWith("Result_MoveToRoom");
    }

    // Event ID를 받아서 전체 조건의 true/false 판단하여 true인 경우 결과 수행
    public void CallEvent(string eventID)
    {
        if (GameManager.Instance.isDebug) Debug.Log($"-#-#-#-#-#-#-#-#- event: \"{eventID}\" -#-#-#-#-#-#-#-#-");

        List<EventLine> eventLines = events[eventID].EventLine;

        int eventCount = 0;

        foreach (EventLine eventLine in eventLines)
        {
            eventCount++;
            if (GameManager.Instance.isDebug) Debug.Log($"--------- #{eventCount} ---------");

            string logic = eventLine.Logic;
            List<Condition> conditions = eventLine.Conditions;
            List<Result> results = eventLine.Results;
            string executionMode = eventLine.ExecutionMode;

            if (conditions.Count == 0)
            { // 조건이 존재하지 않는 경우 무조건 실행
                ExecuteResults(results, executionMode);
                continue;
            }

            if (logic == "AND") // logic이 AND인 경우
            {
                if (CheckConditions_AND(conditions))
                {
                    ExecuteResults(results, executionMode);
                    return;
                }
            }
            else if (logic == "OR") // logic이 OR인 경우
            {
                if (CheckConditions_OR(conditions))
                {
                    ExecuteResults(results, executionMode);
                    return;
                }
            }
            else // logic이 빈칸인 경우
            {
                string conditionID = conditions[0].ConditionID;
                bool isCondition = ConditionManager.Instance.IsCondition(conditionID);
                //Debug.Log(conditionID+" : "+isCondition);
                if (isCondition)
                {
                    ExecuteResults(results, executionMode);
                    // 밑에 return 안 넣어주면 계속 아랫줄에 있는 Conditions에 맞는 Results까지 불러오게 됨
                    return;
                }
            }
        }
    }

    private bool CheckConditions_OR(List<Condition> conditions)
    {
        foreach (Condition condition in conditions)
        {
            string conditionID = condition.ConditionID;
            bool isCondition = ConditionManager.Instance.IsCondition(conditionID);
            if (isCondition)
            {
                return true;
            }
        }

        return false;
    }

    private bool CheckConditions_AND(List<Condition> conditions)
    {
        foreach (Condition condition in conditions)
        {
            string conditionID = condition.ConditionID;
            bool isCondition = ConditionManager.Instance.IsCondition(conditionID);
            if (!isCondition)
            {
                return false;
            }
        }

        return true;
    }


    private void ExecuteResults(List<Result> results, string mode)
    {
        if (mode == "Instant")
        {
            foreach (Result result in results)
            {
                StartCoroutine(ResultManager.Instance.ExecuteResultCoroutine(result.ResultID));
            }
        }
        else if (mode == "Sequential")
        {
            StartCoroutine(ExecuteResultsSequentially(results));
        }
        else
        {
            Debug.LogWarning($"[EventExecutor] Unknown ExecutionMode: {mode}, fallback to Instant.");
            foreach (Result result in results)
            {
                StartCoroutine(ResultManager.Instance.ExecuteResultCoroutine(result.ResultID));
            }
        }
    }

    private IEnumerator ExecuteResultsSequentially(List<Result> results)
    {
        foreach (Result result in results)
        {
            yield return StartCoroutine(ResultManager.Instance.ExecuteResultCoroutine(result.ResultID));
        }
    }


    // -------------------------------------------- Debug Method --------------------------------------------

    //Event 정보를 로그로 출력하는 메서드
    private void DebugLogEvents()
    {
        Debug.Log("##### events #####");
        foreach (var evt in events)
        {
            Debug.Log($"Event ID: {evt.Value.EventID}, Name: {evt.Value.EventName}, Description: {evt.Value.EventDescription}");
            foreach (var lcr in evt.Value.EventLine)
            {
                Debug.Log($"    Logic: {lcr.Logic}");
                Debug.Log("    Conditions:");
                foreach (var condition in lcr.Conditions)
                {
                    Debug.Log($"        {condition.ConditionID}");
                }
                Debug.Log("    Results:");
                foreach (var result in lcr.Results)
                {
                    Debug.Log($"        {result.ResultID}");
                }
            }
        }
    }
}