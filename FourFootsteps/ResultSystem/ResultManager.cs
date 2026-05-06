using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Constants;
using Random = Unity.Mathematics.Random;

public class ResultManager : MonoBehaviour
{
    public static ResultManager Instance { get; private set; }

    private TextAsset resultsCSV;

    // results: dictionary of "Results"s indexed by string "Result ID"
    public Dictionary<string, Result> results = new Dictionary<string, Result>();

    // 이벤트 오브젝트 참조
    private Dictionary<string, IResultExecutable> executableObjects = new Dictionary<string, IResultExecutable>();

    void Awake()
    {
        if (Instance == null)
        {
            resultsCSV = Resources.Load<TextAsset>("Datas/results");
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void RegisterExecutable(string objectName, IResultExecutable executable)
    {
        Debug.Log($"registered {objectName}");

        if (!executableObjects.ContainsKey(objectName))
            executableObjects[objectName] = executable;
    }

    public void InitializeExecutableObjects()
    {
        Debug.Log("############### unregistered all executable objects ###############");

        executableObjects = new Dictionary<string, IResultExecutable>();
    }

    public void ParseResults()
    {
        string[] lines = resultsCSV.text.Split('\n');
        for (int i = 1; i < lines.Length; i++)
        {
            string[] fields = lines[i].Split(',');

            if ((string.IsNullOrWhiteSpace(lines[i])) || (fields[0] == "" && fields[1] == "")) continue;

            Result result = new Result(
                fields[0].Trim(),   // Result ID
                fields[1].Trim(),   // Result Description
                fields[2].Trim()    // Dialogue ID
                );

            results[result.ResultID] = result;
        }
    }

    public IEnumerator ExecuteResultCoroutine(string resultID)
    {
        string variableName;

        // ------------------------ 이곳에 모든 동작을 수동으로 추가 ------------------------
        switch (resultID)
        {
            case string when resultID.StartsWith("Result_StartDialogue"):  // 대사 시작
                variableName = resultID["Result_StartDialogue".Length..];
                DialogueManager.Instance.StartDialogue(variableName);
                //Debug.Log($"다이얼로그 {variableName} 시작");
                // 비동기 대기 (대사 끝날 때까지)
                while (DialogueManager.Instance.isDialogueActive)
                    yield return null;
                break;

            // GameManager의 해당 변수를 조정 가능(+1 / -1)
            case string when resultID.StartsWith("Result_Increment"):  // 값++
                variableName = resultID["Result_Increment".Length..];
                GameManager.Instance.IncrementVariable(variableName);

                // 증가시킨게 책임감 점수면 ChangeResponsibilityGauge 호출
                if (variableName == "ResponsibilityScore")
                {
                    if (ResponsibilityManager.Instance)
                        ResponsibilityManager.Instance.ChangeResponsibilityGauge();
                }
                yield return null; // 바로 실행이지만 코루틴 일관성 유지
                break;

            case string when resultID.StartsWith("Result_Decrement"):  // 값--
                variableName = resultID["Result_Decrement".Length..];
                GameManager.Instance.DecrementVariable(variableName);

                yield return null; // 바로 실행이지만 코루틴 일관성 유지
                break;

            case string when resultID.StartsWith("Result_Inverse"):  // !값
                variableName = resultID["Result_Inverse".Length..];
                GameManager.Instance.InverseVariable(variableName);

                yield return null; // 바로 실행이지만 코루틴 일관성 유지
                break;

            case "ResultCloseEyes": // 눈 깜빡이는 효과
                yield return UIManager.Instance.OnFade(null, 0, 1, 1, true, 1, 0);
                break;

            case "Result_FadeOut":  // fade out
                float fadeOutTime = 2f;
                yield return UIManager.Instance.OnFade(null, 0, 1, fadeOutTime);
                //StartCoroutine(UIManager.Instance.OnFade(null, 0, 1, fadeOutTime));
                break;

            case "Result_FadeIn":  // fade int
                float fadeInTime = 2f;
                yield return UIManager.Instance.OnFade(null, 1, 0, fadeInTime);
                //StartCoroutine(UIManager.Instance.OnFade(null, 1, 0, fadeInTime));
                break;

            case "Result_FastFadeOut":  // Fast fade out
                fadeOutTime = 1.5f;
                yield return UIManager.Instance.OnFade(null, 0, 1, fadeOutTime);
                //StartCoroutine(UIManager.Instance.OnFade(null, 0, 1, fadeOutTime));
                break;

            case "Result_FastFadeIn":  // Fast fade int
                fadeInTime = 1.5f;
                yield return UIManager.Instance.OnFade(null, 1, 0, fadeInTime);
                //StartCoroutine(UIManager.Instance.OnFade(null, 1, 0, fadeInTime));
                break;

            // 다이얼로그 캔버스까지 안 보이게 하는 Fade Out/In
            case "Result_DialogueFadeOut":  // fade out
                Debug.Log("Result_DialogueFadeOut");
                fadeOutTime = 2f;
                yield return UIManager.Instance.OnFade(UIManager.Instance.dialogueCoverPanel, 0, 1, fadeOutTime);
                //StartCoroutine(UIManager.Instance.OnFade(null, 0, 1, fadeOutTime));
                break;

            case "Result_DialogueFadeIn":  // fade int
                Debug.Log("Result_DialogueFadeIn");
                fadeInTime = 2f;
                yield return UIManager.Instance.OnFade(UIManager.Instance.dialogueCoverPanel, 1, 0, fadeInTime);
                //StartCoroutine(UIManager.Instance.OnFade(null, 1, 0, fadeInTime));
                break;

            // 프롤로그 다음 스텝으로 넘김
            case "Result_NextPrologueStep":
                Debug.Log("Result_NextPrologueStep");
                StartCoroutine(PrologueManager.Instance.ProceedToNextStep());
                yield return null;
                break;

            // 퍼즐 획득 애니메이션 재생 후 회상씬으로 이동
            case "Result_GetMemoryPuzzle":
                Debug.Log("Execute [Result_GetMemoryPuzzle]");
                executableObjects["MemoryPuzzle"].ExecuteAction();
                // 비동기 대기(애니메이션 끝날 때까지)
                while ((bool)GameManager.Instance.GetVariable("isPuzzleMoving"))
                    yield return null;
                break;

            // 낡은 소파 조사 시, 회상1 씬으로 이동.
            case "Result_GoToRecall1":
                InitializeExecutableObjects();
                GameManager.Instance.SetVariable("CanInvesigatingRecallObject", false);
                SceneLoader.Instance.LoadScene(GameManager.Instance.GetNextSceneData().sceneName);
                yield return new WaitForSeconds(1f);
                break;

            // 동물 병원 앞 퍼즐 조사 시, 회상2 씬으로 이동.
            case "Result_GoToRecall2":
                InitializeExecutableObjects();
                GameManager.Instance.SetVariable("CanInvesigatingRecallObject", false);
                SceneLoader.Instance.LoadScene(GameManager.Instance.GetNextSceneData().sceneName);
                yield return new WaitForSeconds(1f);
                break;

            // 정자 밑 퍼즐 조사 시, 회상3 씬으로 이동.
            case "Result_GoToRecall3":
                InitializeExecutableObjects();
                GameManager.Instance.SetVariable("CanInvesigatingRecallObject", false);
                SceneLoader.Instance.LoadScene(GameManager.Instance.GetNextSceneData().sceneName);
                yield return new WaitForSeconds(1f);
                break;

            // 웜홀 최초 등장
            case "Result_FirstWormholeActivation":
                executableObjects["WormholeActivation"].ExecuteAction();
                // 아래 코드는 임시!!! 나중에 RecallManager 제대로 만들면 수정될 것
                if (RecallManager.Instance != null)
                {
                    Debug.Log("Recall Manger 호출");
                    RecallManager.Instance.SetInteractKeyGroup(true);
                }
                yield return null;
                break;

            // 웜홀 사용 시, 다음 씬으로 이동
            case "Result_WormholeNextScene":
                //Debug.Log("웜홀 사용 ");
                SceneLoader.Instance.LoadScene(GameManager.Instance.GetNextSceneData().sceneName);
                yield return new WaitForSeconds(1f);
                break;

            case "Result_SetNextTutorial":
                // 다이얼로그 출력 완료 시 다음 트리거로 이동
                TutorialController.Instance.SetNextTutorial();
                yield return null;
                break;

            case string when resultID.StartsWith("Result_JumpToTutorial"):
                string tutorialIndexStr = resultID["Result_JumpToTutorial".Length..];
                if (int.TryParse(tutorialIndexStr, out int tutorialIndex))
                {
                    Debug.Log($"Result_JumpToTutorial{tutorialIndex}");
                    TutorialController.Instance.JumpToTutorial(tutorialIndex);

                    // 여기에 튜토리얼 점프 시 호출할 RecallManager 로직 추가
                    if (RecallManager.Instance != null)
                    {
                        Debug.Log("JumpToTutorial에서 RecallManager 호출");
                        GameManager.Instance.SetVariable("CanInvesigatingRecallObject", true);
                        RecallManager.Instance.SetInteractKeyGroup(true);
                    }
                }
                yield return null;
                break;

            // 같은 씬 내에서 방 간 이동 (페이드 효과와 함께)
            case string when resultID.StartsWith("Result_MoveToRoom"):
                string roomName = resultID["Result_MoveToRoom".Length..];
                yield return StartCoroutine(MoveToRoomCoroutine(roomName));
                break;

            // 은신 게임: 들켰을 때 연출
            case "Result_StartChaseGameIntro":
                fadeOutTime = 1.5f;
                // 다이얼로그 진행 후
                GameObject player = GameObject.FindWithTag("Player");
                player.GetComponent<PlayerCatMovement>().ForceCrouch = false;
                player.GetComponent<PlayerCatMovement>().IsJumpingBlocked = false;
                // 플레이어 오른쪽으로 강제 대쉬 이동시켜서 시야에서 벗어난 후
                player.GetComponent<CatAutoMover>().enabled = true;
                player.GetComponent<CatAutoMover>().StartMoving(player.GetComponent<CatAutoMover>().targetPoint);
                // 화면 어두워짐+ 다음 추격 미니게임 시작
                yield return UIManager.Instance.OnFade(null, 0, 1, fadeOutTime);
                FollowCamera followCamera = Camera.main.GetComponent<FollowCamera>();
                followCamera.enabled = true;
                yield return new WaitWhile(() => player.GetComponent<CatAutoMover>().IsMoving);
                fadeOutTime = 1f;
                yield return UIManager.Instance.OnFade(null, 1, 0, fadeOutTime);
                TutorialController.Instance.SetNextTutorial();
                break;

            case "Result_MiniGameFailed":
                // 페이드 인 효과와 함께 게임오버 씬 로드
                SceneLoader.Instance.LoadScene("GameOver");
                yield return null;
                break;

            default:
                Debug.Log($"Result ID: {resultID} not found!");
                yield return null;
                break;
        }
    }

    private bool _isMovingRoom = false;

    /// <summary>
    /// 같은 씬 내에서 지정된 방으로 플레이어와 카메라를 이동시키는 코루틴
    /// </summary>
    /// <param name="roomName">이동할 방의 이름 (Transform 오브젝트 이름과 일치해야 함)</param>
    /// <param name="useFade">페이드 효과 사용 여부</param>
    private IEnumerator MoveToRoomCoroutine(string roomName, bool useFade = true)
    {
        if (_isMovingRoom)
        {
            Debug.LogWarning($"Already moving to a room! (Requested: {roomName})");
            yield break;
        }

        _isMovingRoom = true;

        Debug.Log($"Moving to room: {roomName}");

        GameObject playerSpawnPoint = GameObject.Find($"{roomName}_PlayerSpawn");
        GameObject cameraTargetPoint = GameObject.Find($"{roomName}_CameraTarget");

        if (playerSpawnPoint == null)
        {
            Debug.LogError($"Player spawn point not found: {roomName}_PlayerSpawn");
            _isMovingRoom = false;
            yield break;
        }

        if (cameraTargetPoint == null)
        {
            Debug.LogError($"Camera target point not found: {roomName}_CameraTarget");
            _isMovingRoom = false;
            yield break;
        }

        // 페이드 아웃
        if (useFade)
        {
            yield return UIManager.Instance.OnFade(null, 0, 1, 1f);
        }

        // 플레이어 위치, 회전 이동 (컨트롤러 enable/disable 제거)
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            player.transform.position = playerSpawnPoint.transform.position;
            player.transform.rotation = playerSpawnPoint.transform.rotation;
        }
        else
        {
            Debug.LogError("Player object not found with tag 'Player'");
        }

        // 카메라 이동
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            mainCamera.transform.position = cameraTargetPoint.transform.position;
            mainCamera.transform.rotation = cameraTargetPoint.transform.rotation;
        }
        else
        {
            Debug.LogError("Main camera not found");
        }

        // 짧은 대기
        yield return new WaitForSeconds(0.1f);

        // 페이드 인
        if (useFade)
        {
            yield return UIManager.Instance.OnFade(null, 1, 0, 1f);
        }

        Debug.Log($"Successfully moved to room: {roomName}");

        _isMovingRoom = false;
    }
}