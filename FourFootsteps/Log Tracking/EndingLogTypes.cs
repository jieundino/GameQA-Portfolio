using System;
using UnityEngine;

[Serializable]
public class EndingLogPayload
{
    public string apiKey;
    public string eventId;

    public string playerKey;

    public string runId;
    public string eventType;   // 예: "Ending", "NameChange", "Checkpoint"

    public string playerName;
    public string catName;

    public string endingType;  // 예: "Ending_Bad", "Ending_Happy"
    public string memoryPuzzleStatesJson;
    public int responsibilityScore;
}

[Serializable]
public class PendingLogQueue
{
    public EndingLogPayload[] items = Array.Empty<EndingLogPayload>();
}
