using UnityEngine;

public class ReportEndingOnStart : MonoBehaviour
{
    [SerializeField] private string endingId = "";

    private void Start()
    {
        var qm = EndingLogQueueManager.Instance;
        if (qm == null)
        {
            Debug.LogError("[EndingLog] QueueManager not found");
            return;
        }

        EndingLogReporter reporter = qm.GetComponent<EndingLogReporter>();
        if (reporter != null)
        {
            Debug.Log("ReportEnding");
            reporter.ReportEnding(endingId);
        }
    }
}
