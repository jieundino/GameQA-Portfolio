using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Constants;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    // Dialogue UI
    [Header("Dialogue UI")]
    public DialogueType dialogueType = DialogueType.PLAYER_TALKING; // 사용할 대화창 종류
    public GameObject[] dialogueSet;
    public TextMeshProUGUI[] speakerTexts;
    public TextMeshProUGUI[] scriptText;
    public Image[] cutSceneImages;
    public Image[] characterImages;
    public Transform[] choicesContainer;
    public GameObject choicePrefab;
    public GameObject[] skipText;
    public Image[] cutsceneCoverPanels;

    // 상태 추적
    private bool lastHadCutscene = false;        // 직전 프레임(또는 현재 라인)에서 컷씬이 있었는지
    [SerializeField] private bool isCoverTransitioning = false;   // 커버 페이드 중인지(중복 방지)

    // 스킵 전용 상태
    private bool isSkippingToLast = false;

    // 커버 페이드 시간 (요구대로 2초)
    [SerializeField] private float coverFadeDuration = 2f;

    // 타자 효과 속도
    [Header("Typing Speed")]
    public float typeSpeed = 0.05f;
    private float defaultTypeSpeed = 0.05f;
    private float FastTypeSpeed = 0.01f;

    // 글자 흔들리는 효과
    [Header("Text Shake")]
    public float shakeAmount = 1.0f;
    public float shakeSpeed = 30.0f;
    //private bool isTextShaking = false;

    // 자료 구조
    public Dictionary<string, Dialogue> dialogues = new Dictionary<string, Dialogue>();
    private Dictionary<string, Choice> choices = new Dictionary<string, Choice>();

    // 상태 변수
    private string currentDialogueID = "";
    public bool isDialogueActive = false;
    private bool isTyping = false;
    private bool isAuto = false;
    private bool isFast = false;
    private string fullSentence;
    private bool isAutoDelayed = false; // 2초 지난 후 자동으로 넘겨짐
    private bool isFadeOut = false; // AutoDelayed이랑 주로 같이 쓰이며 글자가 투명하게 사라짐
    [SerializeField] private bool isCutsceneFadingToBlack = false;

    private float fadeTime = 0.5f;

    // Dialogue Queue
    private Queue<string> dialogueQueue = new Queue<string>();

    // Bubble Dialogue
    [Header("Bubble Dialogue Function")]
    public Transform playerTransform;   // 플레이어 위치
    public Transform npcTransform;      // npc 위치
    public Vector3 bubbleOffset = new Vector3(0, 2.0f, 0);
    public float catOffset_y = 0.5f;
    public float humanOffset_y = 2.0f;
    private static readonly string[] CatIdMarkers = { "CAT", "Ttoli", "Leo", "Bogsil", "Miya" };

    void Awake()
    {
        if (Instance == null)
        {
            DialoguesParser dialoguesParser = new DialoguesParser();
            dialogues = dialoguesParser.ParseDialogues();
            choices = dialoguesParser.ParseChoices();

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        if (dialogueType.ToInt() >= 0 && dialogueType.ToInt() < dialogueSet.Length)
        {
            dialogueSet[dialogueType.ToInt()].SetActive(false);
        }
        else
        {
            Debug.LogWarning("Index out of range: " + dialogueType.ToInt());
        }
        EnsureCoverPanelsInitialized();
    }

    private void Update()
    {
        // 선택지가 있는 상태면 키 입력 받지 못하게 함.
        if (!isDialogueActive || choicesContainer[dialogueType.ToInt()].childCount > 0)
            return;

        // 다이얼로그 출력될 때 space bar 누르면 스크립트 넘겨짐
        if (Input.GetKeyDown(KeyCode.Space)&&!isCoverTransitioning)
            OnDialoguePanelClick();
    }

    private void LateUpdate()
    {
        // 플레이어 말풍선
        if (dialogueType == DialogueType.PLAYER_BUBBLE && dialogueSet[dialogueType.ToInt()].activeSelf)
        {
            dialogueSet[dialogueType.ToInt()].transform.position =
                Camera.main.WorldToScreenPoint(playerTransform.position + bubbleOffset);
        }

        // NPC 말풍선
        if (dialogueType == DialogueType.NPC_BUBBLE && dialogueSet[dialogueType.ToInt()].activeSelf)
        {
            dialogueSet[dialogueType.ToInt()].transform.position =
                Camera.main.WorldToScreenPoint(npcTransform.position + bubbleOffset);
        }
    }

    Transform FindSpeakerByName(string name)
    {
        GameObject speaker = GameObject.Find(name);
        if (speaker != null) return speaker.transform;
        Debug.LogWarning("Speaker를 찾을 수 없습니다: " + name);
        return null;
    }

    // ---------------------------------------------- Dialogue methods ----------------------------------------------
    public void StartDialogue(string dialogueID)
    {
        // 대사 시작: 이미 대화가 진행 중이면 큐에 저장
        if (isDialogueActive)
        {
            Debug.Log($"dialogue ID: {dialogueID} queued!");

            dialogueQueue.Enqueue(dialogueID);
            return;
        }

        isDialogueActive = true;

        // 대사가 2개 이상이고, 시작이 마지막이 아닐 때만 skip 버튼 활성화
        int lineCount = dialogues[dialogueID].Lines.Count;
        if (lineCount > 1)
        {
            foreach (GameObject skip in skipText)
                skip.SetActive(true);
        }
        else
        {
            foreach (GameObject skip in skipText)
                skip.SetActive(false);
        }

        dialogues[dialogueID].SetCurrentLineIndex(0);
        currentDialogueID = dialogueID;
        DialogueLine initialDialogueLine = dialogues[dialogueID].Lines[0];

        DisplayDialogueLine(initialDialogueLine);
    }

    // 대사 출력을 second초 후에 출력을 시작함.
    // 기본값 second 0으로 넣기
    public IEnumerator StartDialogue(string dialogueID, float second = 0f)
    {
        yield return new WaitForSeconds(second);

        if (isDialogueActive)  // 이미 대화가 진행중이면 큐에 넣음
        {
            Debug.Log($"dialogue ID: {dialogueID} queued!");

            dialogueQueue.Enqueue(dialogueID);
            yield break;
        }

        isDialogueActive = true;

        // 대사가 2개 이상이라면 skip 버튼 활성화
        if (dialogues[dialogueID].Lines.Count > 1)
            foreach (GameObject skip in skipText)
                skip.SetActive(true);

        dialogues[dialogueID].SetCurrentLineIndex(0);
        currentDialogueID = dialogueID;
        DialogueLine initialDialogueLine = dialogues[dialogueID].Lines[0];

        DisplayDialogueLine(initialDialogueLine);
    }

    private void ClearPreviousChoices()
    {
        if (choicesContainer.Length > dialogueType.ToInt())
            foreach (Transform child in choicesContainer[dialogueType.ToInt()])
                Destroy(child.gameObject);
    }

    private void SetupCanvasAndSpeakerText(DialogueLine dialogueLine)
    {
        ChangeDialogueCanvas(dialogueLine.SpeakerID, dialogueLine.BubbleMode, dialogueLine.ImageID);

        // Deactivate all canvases and then activate the selected one.
        foreach (GameObject canvas in dialogueSet)
            if (canvas)
                canvas.SetActive(false);
        dialogueSet[dialogueType.ToInt()].SetActive(true);

        // Update speaker text
        foreach (TextMeshProUGUI speakerText in speakerTexts)
        {
            switch (dialogueLine.SpeakerID)
            {
                case "Player":
                    speakerText.text = GameManager.Instance.GetVariable("PlayerName").ToString();
                    break;

                case "YourCat":
                    speakerText.text = GameManager.Instance.GetVariable("YourCatName").ToString();
                    break;

                case "InnerThoughts":
                case "Monolog":
                case "PlayerBubble":
                    speakerText.text = "";
                    break;

                default:
                    speakerText.text = dialogueLine.SpeakerID;
                    break;
            }
            if (dialogueLine.BubbleMode)
                speakerText.text = "";
        }
    }

    private string ProcessTextEffect(DialogueLine dialogueLine, out bool auto, out bool fast)
    {
        auto = false;
        fast = false;

        var sentence = dialogueLine.GetScript();

        if (dialogueLine.TextEffect.Length > 0)
        {
            var effects = dialogueLine.TextEffect.Split('/');
            foreach (var effect in effects)
                switch (effect)
                {
                    case "RED":
                        // 문장 전체를 빨갛게 함
                        sentence = $"<color=red>{sentence}</color>";
                        break;
                    case "AUTO":
                        auto = true;
                        isAutoDelayed = false;
                        foreach (GameObject skip in skipText)
                            skip.SetActive(false);
                        break;
                    case "AUTO_DELAYED":
                        // 대사 다 출력 후 몇초 대기 후 다음 대사 자동 출력
                        auto = true;
                        isAutoDelayed = true;
                        foreach (GameObject skip in skipText)
                            skip.SetActive(false);
                        break;
                    case "FADE_OUT":
                        // 대사 다 출력 후 다음 대사로 넘어가기 전에 투명하게 텍스트가 사라짐.
                        isFadeOut = true;
                        foreach (GameObject skip in skipText)
                            skip.SetActive(false);
                        break;
                    case "FAST":
                        fast = true;
                        break;
                    case "TRUE":
                        var playerName = (string)GameManager.Instance.GetVariable("PlayerName");
                        var yourCatName = (string)GameManager.Instance.GetVariable("YourCatName");

                        // 1) 한 번에 모든 패턴(괄호/슬래시/단일조사/단독)을 처리
                        sentence = KoreanJosa.Apply(
                            sentence,
                            ("PlayerName", playerName),
                            ("YourCatName", yourCatName)
                        );

                        // 끝. 별도의 Replace("{Var}", name) 불필요 (위에서 처리)
                        break;
                    case "SHAKE":
                        StartCoroutine(TextShakeEffectCoroutine(dialogueType.ToInt()));
                        break;
                }
        }
        return sentence;
    }

    private void UpdateCutScene(DialogueLine dialogueLine)
    {
        var cutSceneID = dialogueLine.CutSceneID;

        foreach (var currentCutSceneImage in cutSceneImages)
        {
            if (string.IsNullOrWhiteSpace(cutSceneID))
            {
                // 비우는 처리는 CoverTransitionThen에서 일괄 정리
                continue;
            }
            else
            {
                switch (cutSceneID)
                {
                    case "BLACK":
                        // [변경] 스킵 상황에선 '즉시' 검은 화면으로
                        if (isSkippingToLast)
                        {
                            if (currentCutSceneImage)
                            {
                                currentCutSceneImage.sprite = null;
                                currentCutSceneImage.color = Color.black;
                            }
                            isCutsceneFadingToBlack = false; // 혹시 true였다면 해제
                            lastHadCutscene = true;
                        }
                        else
                        {
                            StartCoroutine(FadeToBlack(currentCutSceneImage, 2f));
                            lastHadCutscene = true;
                        }
                        break;

                    default:
                        var cutSceneSprite = Resources.Load<Sprite>($"Art/CutScenes/{cutSceneID}");
                        currentCutSceneImage.sprite = cutSceneSprite;

                        // [보강] 스킵 시엔 즉시 반영 보장
                        currentCutSceneImage.color = Color.white;
                        lastHadCutscene = true;
                        break;
                }
            }
        }
    }

    // 컷씬 이미지를 Black으로 바꿈
    IEnumerator FadeToBlack(Image targetImage, float duration)
    {
        isCutsceneFadingToBlack = true;
        Color startColor = Color.white;
        Color endColor = Color.black;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            // 스킵 도중 강제 중지될 수 있음 → 그 경우 플래그가 false로 이미 내려갈 것.
            targetImage.color = Color.Lerp(startColor, endColor, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        targetImage.color = endColor;
        isCutsceneFadingToBlack = false;
    }

    private void EnsureCoverPanelsInitialized()
    {
        // 요구조건: 시작 시 color=black, alpha=0 유지
        foreach (var cover in cutsceneCoverPanels)
        {
            if (!cover) continue;
            var c = cover.color;
            cover.color = new Color(0f, 0f, 0f, 0f); // black, transparent
            cover.gameObject.SetActive(false);
        }
    }

    // 컷씬 이미지 위로 alpha start→end 로 페이드
    private IEnumerator FadeCoverPanels(float start, float end, float duration)
    {
        isCoverTransitioning = true;

        foreach (var cover in cutsceneCoverPanels)
        {
            if (!cover) continue;
            cover.gameObject.SetActive(true);
            cover.color = new Color(0f, 0f, 0f, start);
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(start, end, Mathf.Clamp01(t / duration));
            foreach (var cover in cutsceneCoverPanels)
            {
                if (!cover) continue;
                cover.color = new Color(0f, 0f, 0f, a);
            }
            yield return null;
        }

        // 알파 0이면 꺼줘서 클릭 막힘 방지
        if (end <= 0.001f)
        {
            foreach (var cover in cutsceneCoverPanels)
                if (cover) cover.gameObject.SetActive(false);
        }

        isCoverTransitioning = false;
    }

    // 전환 래퍼: 먼저 블랙업 → 액션 → 블랙다운
    private IEnumerator CoverTransitionThen(System.Action thenAction, bool fadeOutAfter = true)
    {
        // 1) 블랙 커버 올리기
        yield return StartCoroutine(FadeCoverPanels(0f, 1f, coverFadeDuration));

        // 2) 컷씬 이미지들 실제로 내림(사라짐)
        foreach (var img in cutSceneImages)
        {
            if (!img) continue;
            img.color = new Color(1, 1, 1, 0);
            img.sprite = null; // 완전히 제거(선택)
        }
        lastHadCutscene = false;

        // 3) 전환 작업 수행
        thenAction?.Invoke();

        // 4) 필요하면 다시 투명화(자연스럽게 복귀)
        if (fadeOutAfter)
            yield return StartCoroutine(FadeCoverPanels(1f, 0f, 0.35f)); // 복귀는 짧게
    }

    private IEnumerator TextShakeEffectCoroutine(int dialogueType)
    {
        //isTextShaking = true;
        float elapsed = 0f;

        while (true)
        {
            if (Input.GetMouseButtonDown(0))
                break;

            scriptText[dialogueType].ForceMeshUpdate();
            var textInfo = scriptText[dialogueType].textInfo;

            for (int i = 0; i < textInfo.characterCount; i++)
            {
                if (!textInfo.characterInfo[i].isVisible)
                    continue;

                int vertexIndex = textInfo.characterInfo[i].vertexIndex;
                int materialIndex = textInfo.characterInfo[i].materialReferenceIndex;
                Vector3[] vertices = textInfo.meshInfo[materialIndex].vertices;

                Vector3 offset = new Vector3(
                    Mathf.Sin(Time.time * shakeSpeed + i) * shakeAmount,
                    Mathf.Cos(Time.time * shakeSpeed + i) * shakeAmount,
                    0);

                for (int j = 0; j < 4; j++)
                {
                    vertices[vertexIndex + j] += offset;
                }
            }

            // Apply changes
            for (int i = 0; i < textInfo.meshInfo.Length; i++)
            {
                var meshInfo = textInfo.meshInfo[i];
                meshInfo.mesh.vertices = meshInfo.vertices;
                scriptText[dialogueType].UpdateGeometry(meshInfo.mesh, i);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // 흔들기 종료 후 텍스트 초기화
        scriptText[dialogueType].ForceMeshUpdate(); // 원래 버텍스로 재설정
        //isTextShaking = false;
    }

    private void PlayDialogueSound(DialogueLine dialogueLine)
    {
        if (string.IsNullOrWhiteSpace(dialogueLine.SoundID))
            return;
        var soundID = "Sound_" + dialogueLine.SoundID;
        var soundNum = (int)typeof(Constants).GetField(soundID).GetValue(null);
        SoundPlayer.Instance.UISoundPlay(soundNum);
    }

    private void UpdateCharacterImages(DialogueLine dialogueLine)
    {
        if (dialogueType == DialogueType.PLAYER_BUBBLE ||
            dialogueType == DialogueType.NPC_BUBBLE ||
            dialogueType == DialogueType.MONOLOG)
        {
            characterImages[dialogueType.ToInt()].gameObject.SetActive(false);

            bubbleOffset.y = IsCatImageID(dialogueLine.ImageID) ? catOffset_y : humanOffset_y;
            return;
        }

        var imageID = dialogueLine.ImageID;
        var emotion = dialogueLine.EmotionalState;
        if (string.IsNullOrWhiteSpace(imageID))
        {
            foreach (var characterImage in characterImages)
                characterImage.color = new Color(1, 1, 1, 0);
            return;
        }
        if (!string.IsNullOrWhiteSpace(emotion))
            imageID = new StringBuilder(imageID)
                        .Append('_')
                        .Append(emotion)
                        .ToString();

        var characterSprite = Resources.Load<Sprite>($"Art/CharacterPortrait/{imageID}");

        characterImages[dialogueType.ToInt()].color = new Color(1, 1, 1, 1);
        characterImages[dialogueType.ToInt()].sprite = characterSprite;
        characterImages[dialogueType.ToInt()].gameObject.SetActive(true);
    }

    private static bool IsCatImageID(string imageId)
    {
        if (string.IsNullOrEmpty(imageId)) return false;

        // 대소문자 무시하고 부분일치 확인
        foreach (var marker in CatIdMarkers)
        {
            if (imageId.IndexOf(marker, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }

    private void DisplayDialogueLine(DialogueLine dialogueLine)
    {
        ClearPreviousChoices();
        SetupCanvasAndSpeakerText(dialogueLine);

        // MONOLOG 타입일 때 텍스트 색상을 흰색으로 설정 (알파값 1로 초기화)
        if (dialogueType == DialogueType.MONOLOG)
            scriptText[dialogueType.ToInt()].color = new Color(1, 1, 1, 1); 

        // Process placeholders and get final sentence.
        string sentence = ProcessTextEffect(dialogueLine, out bool auto, out bool fast);
        isAuto = auto;
        isFast = fast;

        isTyping = true;
        StartCoroutine(TypeSentence(sentence));

        UpdateCutScene(dialogueLine);
        PlayDialogueSound(dialogueLine);
        UpdateCharacterImages(dialogueLine);
    }

    private void ChangeDialogueCanvas(string speaker, bool bubbleMode, string imageID)
    {
        //if (dialogueType == DialogueType.CENTER)
        //    dialogueType = DialogueType.PLAYER_TALKING;

        if (bubbleMode)
        {
            // 말풍선 모드
            if (speaker == "Player")
            {
                dialogueType = DialogueType.PLAYER_BUBBLE;
                playerTransform = FindSpeakerByName(speaker);
                // 고양이이면 offset Cat으로, 인간이면 offset Human으로 설정
                bubbleOffset.y = (imageID.Contains("CAT")) ? catOffset_y : humanOffset_y;
            }
            else
            {
                dialogueType = DialogueType.NPC_BUBBLE;
                npcTransform = FindSpeakerByName(speaker);
            }
        }
        else
        {
            switch (speaker)
            {
                case "Player":
                    dialogueType = DialogueType.PLAYER_TALKING;
                    break;

                // 회상 마지막에 독백
                case "Monolog":
                    dialogueType = DialogueType.MONOLOG;
                    break;

                case "InnerThoughts":
                    dialogueType = DialogueType.PLAYER_THINKING;
                    break;

                default:
                    dialogueType = DialogueType.NPC;
                    break;
            }
        }

    }

    public void EndDialogue()
    {
        if (isCoverTransitioning) return;

        // 컷씬 중에 바로 종료될 때도 자연스럽게
        if (lastHadCutscene)
        {
            StartCoroutine(CoverTransitionThen(() =>
            {
                ForceEndDialogueCore();
            }, fadeOutAfter: false)); // 다음 씬 전환 등으로 페이드아웃은 상위 로직에서
        }
        else
        {
            ForceEndDialogueCore();
        }
    }

    private void ForceEndDialogueCore()
    {
        SoundPlayer.Instance.UISoundPlay_LOOP(0, false);
        isDialogueActive = false;

        // 현재 타입 패널만 끄기
        dialogueSet[dialogueType.ToInt()].SetActive(false);

        // 큐가 있으면 이어서
        if (dialogueQueue.Count > 0)
        {
            string queuedDialogueID = dialogueQueue.Dequeue();
            StartDialogue(queuedDialogueID);
            return;
        }
    }

    public void SkipButtonClick()
    {
        // 스킵은 '막혀있는 상태를 해제'해야 하므로, 진행 중 페이드여도 막지 않아야 함.
        // if (isCutsceneFadingToBlack) return;  // <-- 이 가드는 제거.

        // 1) 스킵 모드 진입
        isSkippingToLast = true;

        // 2) 모든 코루틴 중지 및 플래그 초기화
        StopAllCoroutines();
        SoundPlayer.Instance.UISoundPlay_LOOP(0, false);
        isTyping = false;

        // 진행 중이던 페이드 상태 강제 해제
        isCutsceneFadingToBlack = false;
        isCoverTransitioning = false;

        // 3) 스킵 텍스트 끄기
        foreach (GameObject skip in skipText)
            skip.SetActive(false);

        // 4) 마지막 라인으로 직접 점프 + 표시
        int lastIndex = dialogues[currentDialogueID].Lines.Count - 1;
        dialogues[currentDialogueID].SetCurrentLineIndex(lastIndex);
        DialogueLine lastLine = dialogues[currentDialogueID].Lines[lastIndex];

        // 마지막 라인 즉시 출력(효과 플래그 재설정 포함)
        DisplayDialogueLine(lastLine);

        // 타이핑 없이 문장 즉시 완성
        CompleteSentence();

        // 자동 진행/페이드 아웃 등을 여기서 수행 (TypeSentence tail 공용화)
        StartCoroutine(AfterLineFullyShownRoutine());

        // 5) 스킵 모드 종료
        isSkippingToLast = false;
    }


    private IEnumerator AfterLineFullyShownRoutine()
    {
        // FAST 되돌림
        if (isFast)
        {
            typeSpeed = defaultTypeSpeed;
            isFast = false;
        }

        // AUTO / AUTO_DELAYED / FADE_OUT 처리
        if (isAuto)
        {
            // 타이핑이 끝난 뒤 대기 (스킵 경로에선 이미 끝)
            while (isTyping) yield return null;

            if (isAutoDelayed)
                yield return new WaitForSeconds(2f);

            if (isFadeOut)
                yield return StartCoroutine(FadeInOutScriptText(1f, 0f));

            // 플래그 리셋
            isAuto = false;
            isAutoDelayed = false;
            isFadeOut = false;

            // 다음으로 자동 진행
            OnDialoguePanelClick();

            // 지금이 마지막 라인인지 체크
            int currentIndex = dialogues[currentDialogueID].CurrentLineIndex;
            int totalLines = dialogues[currentDialogueID].Lines.Count;
            bool isLastLine = (currentIndex >= totalLines - 1);

            // 마지막 라인이 아닐 때만 스킵 텍스트 복구
            if (!isLastLine)
            {
                foreach (GameObject skip in skipText)
                    skip.SetActive(true);
            }

            // (선택) 말풍선/타입에 따라 컬러 복구
            if (dialogueType == DialogueType.PLAYER_TALKING || dialogueType == DialogueType.NPC)
                scriptText[dialogueType.ToInt()].color = Color.black;
        }
    }

    /// <summary>
    /// 진행 중인 다이얼로그를 즉시 강제 종료. (타이틀 화면 복귀 등에 사용)
    /// </summary>
    public void ForceAbortDialogue()
    {
        if (!isDialogueActive)
            return;

        // 1. 모든 코루틴 중단
        StopAllCoroutines();

        // 2. 사운드 정지
        SoundPlayer.Instance.UISoundPlay_LOOP(0, false);

        // 3. 모든 상태 플래그 초기화
        isDialogueActive = false;
        isTyping = false;
        isAuto = false;
        isFast = false;
        isAutoDelayed = false;
        isFadeOut = false;
        isCutsceneFadingToBlack = false;
        isCoverTransitioning = false;
        isSkippingToLast = false;
        lastHadCutscene = false;

        // 4. 대화 큐 비우기
        dialogueQueue.Clear();

        // 5. 현재 다이얼로그 ID 초기화
        currentDialogueID = "";

        // 6. 모든 다이얼로그 UI 비활성화
        foreach (GameObject canvas in dialogueSet)
        {
            if (canvas != null)
                canvas.SetActive(false);
        }

        // 7. 선택지 UI 정리
        for (int i = 0; i < choicesContainer.Length; i++)
        {
            if (choicesContainer[i] != null)
            {
                foreach (Transform child in choicesContainer[i])
                {
                    Destroy(child.gameObject);
                }
            }
        }

        // 8. 스킵 텍스트 비활성화
        foreach (GameObject skip in skipText)
        {
            if (skip != null)
                skip.SetActive(false);
        }

        // 9. 커버 패널 초기화 (투명하게)
        foreach (var cover in cutsceneCoverPanels)
        {
            if (cover != null)
            {
                cover.color = new Color(0f, 0f, 0f, 0f);
                cover.gameObject.SetActive(false);
            }
        }

        // 10. 컷씬 이미지 초기화
        foreach (var img in cutSceneImages)
        {
            if (img != null)
            {
                img.color = new Color(1, 1, 1, 0);
                img.sprite = null;
            }
        }

        // 11. 캐릭터 이미지 초기화
        foreach (var characterImage in characterImages)
        {
            if (characterImage != null)
            {
                characterImage.color = new Color(1, 1, 1, 0);
                characterImage.sprite = null;
            }
        }

        // 12. 스크립트 텍스트 초기화
        foreach (var text in scriptText)
        {
            if (text != null)
            {
                text.text = "";
                //text.color = Color.black;
            }
        }

        Debug.Log("[DialogueManager] 다이얼로그가 강제로 중단되었습니다.");
    }

    // ---------------------------------------------- Script methods ----------------------------------------------
    private void ProceedToNext()
    {
        if (isCoverTransitioning) return; // 전환 중엔 무시
        StopAllCoroutines();

        int currentDialogueLineIndex = dialogues[currentDialogueID].CurrentLineIndex;
        string next = dialogues[currentDialogueID].Lines[currentDialogueLineIndex].Next;

        // --- 전환 필요성 계산 ---
        bool willGoToEvent = EventManager.Instance.events.ContainsKey(next);
        bool willGoToChoice = choices.ContainsKey(next);
        bool willGoToOtherDialogue = dialogues.ContainsKey(next);
        bool willGoToNextLine = string.IsNullOrWhiteSpace(next);

        bool nextHasCutscene = false;

        if (willGoToOtherDialogue)
            nextHasCutscene = NextHasCutscene(next);
        else if (willGoToNextLine)
            nextHasCutscene = NextLineHasCutsceneInSameDialogue();
        else
            nextHasCutscene = false; // 이벤트/선택/종료 취급

        // 지금 컷씬이 있었고, 다음 대상에는 컷씬이 없다면 → 커버 전환 후 진행
        if (lastHadCutscene && !nextHasCutscene)
        {
            StartCoroutine(CoverTransitionThen(() =>
            {
                // 커버가 다 올라간 뒤에 실제로 '다음' 로직 수행
                ProceedToNextCore(currentDialogueLineIndex, next,
                                  willGoToEvent, willGoToChoice, willGoToOtherDialogue, willGoToNextLine);
            }));
            return;
        }

        // 컷씬 전환 필요 없다면 기존처럼 바로 진행
        ProceedToNextCore(currentDialogueLineIndex, next,
                          willGoToEvent, willGoToChoice, willGoToOtherDialogue, willGoToNextLine);
    }


    private bool NextHasCutscene(string nextId, int nextLineIndexIfSameDialogue = -1)
    {
        // 1) 다음이 Event/Choice/끝 -> 컷씬 없음
        if (string.IsNullOrEmpty(nextId))  // 다음 줄(빈)인 케이스는 아래에서 계산
            return false;
        if (EventManager.Instance.events.ContainsKey(nextId)) return false;
        if (choices.ContainsKey(nextId)) return false;
        if (!dialogues.ContainsKey(nextId)) return false;

        // 2) 다음이 다른 Dialogue 시작일 때: 첫 줄 확인
        var nextDialogue = dialogues[nextId];
        if (nextDialogue.Lines.Count == 0) return false;
        var first = nextDialogue.Lines[0];
        return !string.IsNullOrWhiteSpace(first.CutSceneID);
    }

    private bool NextLineHasCutsceneInSameDialogue()
    {
        var idx = dialogues[currentDialogueID].CurrentLineIndex + 1;
        if (idx >= dialogues[currentDialogueID].Lines.Count) return false;
        var line = dialogues[currentDialogueID].Lines[idx];
        return !string.IsNullOrWhiteSpace(line.CutSceneID);
    }

    private void ProceedToNextCore(int currentDialogueLineIndex, string next,
    bool willGoToEvent, bool willGoToChoice, bool willGoToOtherDialogue, bool willGoToNextLine)
    {
        if (willGoToEvent)
        {
            EndDialogue();
            EventManager.Instance.CallEvent(next);
            return;
        }

        if (willGoToOtherDialogue)
        {
            EndDialogue();
            StartDialogue(next);
            return;
        }

        if (willGoToNextLine)
        {
            currentDialogueLineIndex++;

            if (currentDialogueLineIndex >= dialogues[currentDialogueID].Lines.Count)
            {
                EndDialogue();
                return;
            }
            else if (currentDialogueLineIndex == dialogues[currentDialogueID].Lines.Count - 1)
            {
                foreach (GameObject skip in skipText) skip.SetActive(false);
            }

            dialogues[currentDialogueID].SetCurrentLineIndex(currentDialogueLineIndex);
            DialogueLine nextDialogueLine = dialogues[currentDialogueID].Lines[currentDialogueLineIndex];

            // 다음 줄에 컷씬ID가 비어있지 않다면, 이제서야 lastHadCutscene 갱신은 UpdateCutScene()에서 자동 반영
            DisplayDialogueLine(nextDialogueLine);
            return;
        }

        if (willGoToChoice)
        {
            DisplayChoices(next);
            return;
        }
    }

    IEnumerator TypeSentence(string sentence)
    {
        scriptText[dialogueType.ToInt()].text = "";
        fullSentence = sentence;

        // <color=red> 같은 글씨 효과들은 출력되지 않도록 변수 설정
        var isEffect = false;
        var effectText = "";

        // FAST 인 경우 두배의 속도로 타이핑
        typeSpeed = isFast ? FastTypeSpeed : defaultTypeSpeed;

        // 타자 루프 사운드 시작
        SoundPlayer.Instance.UISoundPlay_LOOP(0, true);

        foreach (char letter in sentence.ToCharArray())
        {
            if (letter == '<')
            {
                effectText = ""; // effectText 초기화
                isEffect = true;
            }
            else if (letter == '>') // > 가 나오면 scriptText에 한번에 붙인다
            {
                effectText += letter;
                scriptText[dialogueType.ToInt()].text += effectText;
                isEffect = false;
                continue;
            }

            if (isEffect) // < 가 나온 이후부터는 effectText에 붙인다
            {
                effectText += letter;
                continue;
            }

            scriptText[dialogueType.ToInt()].text += letter;
            SoundPlayer.Instance.UISoundPlay(0);
            yield return new WaitForSeconds(typeSpeed);
        }

        // 타자 루프 사운드 정지
        SoundPlayer.Instance.UISoundPlay_LOOP(0, false);

        isTyping = false;

        yield return StartCoroutine(AfterLineFullyShownRoutine());
    }
    // 스크립트 Fade In / Out 코루틴
    // start:0, end:1 -> 투명했던 text가 점점 불투명해짐
    // start:1, end:0 -> 불투명했던 text가 점점 투명해짐
    private IEnumerator FadeInOutScriptText(float start, float end)
    {
        if (dialogueType != DialogueType.MONOLOG)
            yield break;

        float current = 0, percent = 0;

        while (percent < 1 && fadeTime != 0)
        {
            current += Time.deltaTime;
            percent = current / fadeTime;

            scriptText[dialogueType.ToInt()].color = new(1, 1, 1, Mathf.Lerp(start, end, percent));

            yield return null;
        }
    }


    public void OnDialoguePanelClick()
    {
        if (choicesContainer[dialogueType.ToInt()].childCount > 0) return;

        if (!isDialogueActive || isAuto || isCutsceneFadingToBlack|| isCoverTransitioning) return;

        if (isTyping)
        {
            CompleteSentence();
        }
        else
        {
            ProceedToNext();
        }
    }

    private void CompleteSentence()
    {
        StopAllCoroutines();

        // 타자 소리 정지
        SoundPlayer.Instance.UISoundPlay_LOOP(0, false);

        scriptText[dialogueType.ToInt()].text = fullSentence;
        isTyping = false;
    }

    // ---------------------------------------------- Choice methods ----------------------------------------------
    //private void DisplayChoices(string choiceID)
    //{
    //    if (choicesContainer.Length <= dialogueType.ToInt()) return;

    //    foreach (Transform child in choicesContainer[dialogueType.ToInt()])
    //    {
    //        Destroy(child.gameObject);
    //    }

    //    List<ChoiceLine> choiceLines = choices[choiceID].Lines;

    //    foreach (ChoiceLine choiceLine in choiceLines)
    //    {
    //        var choiceButton = Instantiate(choicePrefab, choicesContainer[dialogueType.ToInt()]).GetComponent<Button>();
    //        var choiceText = choiceButton.GetComponentInChildren<TextMeshProUGUI>();

    //        choiceText.text = choiceLine.GetScript();
    //        choiceButton.onClick.AddListener(() => OnChoiceSelected(choiceLine.Next));
    //    }
    //}

    //private void OnChoiceSelected(string next)
    //{
    //    if (dialogues.ContainsKey(next))
    //    {
    //        EndDialogue();
    //        StartDialogue(next);
    //    }
    //    else if (EventManager.Instance.events.ContainsKey(next))
    //    {
    //        EventManager.Instance.CallEvent(next);
    //    }

    //    foreach (Transform child in choicesContainer[dialogueType.ToInt()])
    //    {
    //        Destroy(child.gameObject);
    //    }

    //}
    private void DisplayChoices(string choiceID)
    {
        if (choicesContainer.Length <= dialogueType.ToInt()) return;

        // 기존 선택지들 제거
        foreach (Transform child in choicesContainer[dialogueType.ToInt()])
        {
            Destroy(child.gameObject);
        }

        List<ChoiceLine> choiceLines = choices[choiceID].Lines;

        foreach (ChoiceLine choiceLine in choiceLines)
        {
            var choiceButton = Instantiate(choicePrefab, choicesContainer[dialogueType.ToInt()]).GetComponent<Button>();
            var choiceText = choiceButton.GetComponentInChildren<TextMeshProUGUI>();
            choiceText.text = choiceLine.GetScript();

            // 자식 오브젝트에서 "LockIcon"을 찾아 우선 비활성화
            Transform lockIcon = choiceButton.transform.Find("LockIcon");
            if (lockIcon != null)
            {
                lockIcon.gameObject.SetActive(false);
            }

            bool isLocked = false;
            // choiceLine에 RequiredResponsibility 값이 0보다 큰 경우에만 조건을 확인
            if (choiceLine.RequiredResponsibility > 0)
            {
                // GameManager에서 현재 책임지수 가져오기
                int currentResponsibility = (int)GameManager.Instance.GetVariable("ResponsibilityScore");

                Debug.Log($"선택지: '{choiceLine.GetScript()}', 필요 책임지수: {choiceLine.RequiredResponsibility}, 현재 책임지수: {currentResponsibility}");

                // 현재 책임지수가 요구되는 책임지수보다 낮은지 확인
                if (currentResponsibility < choiceLine.RequiredResponsibility)
                {
                    isLocked = true;
                }
            }


            // 잠금 상태에 따라 버튼 상호작용 및 아이콘 표시 결정
            if (isLocked)
            {
                choiceButton.interactable = false; // 버튼 비활성화
                if (lockIcon != null)
                {
                    lockIcon.gameObject.SetActive(true); // 잠금 아이콘 활성화
                }
            }
            else
            {
                choiceButton.interactable = true; // 버튼 활성화
                                                  // 버튼이 활성화된 경우에만 클릭 이벤트 리스너 추가
                choiceButton.onClick.AddListener(() => OnChoiceSelected(choiceLine));
            }
        }
    }

    private void OnChoiceSelected(ChoiceLine choiceLine)
    {
        Debug.Log($"[DialogueManager] 선택지 선택됨: Script={choiceLine.GetScript()}, " +
            $"Next={choiceLine.Next}, IsGoodChoice={choiceLine.IsGoodChoice},TutorialIndex={choiceLine.TutorialIndex}");

        if (!string.IsNullOrEmpty(choiceLine.IsGoodChoice))
        {
            // puzzleStates 딕셔너리에 선택된 isGoodChoice 값 반영
            int index = (int)GameManager.Instance.GetVariable("CurrentMemoryPuzzleCount");
            // Dictionary<int, bool>로 캐스팅
            var puzzleStates = GameManager.Instance.GetVariable("MemoryPuzzleStates") as Dictionary<int, bool>;

            puzzleStates[index - 1] = choiceLine.IsGoodChoice == "true" ? true : false;
        }

        // 선택지 UI 정리
        foreach (Transform child in choicesContainer[dialogueType.ToInt()])
        {
            Destroy(child.gameObject);
        }

        // 대화 종료
        EndDialogue();

        if (choiceLine.TutorialIndex == -4)
        {
            Debug.Log($"[DialogueManager] TutorialIndex가 -4입니다. Next 값 '{choiceLine.Next}'를 처리합니다.");

            string nextID = choiceLine.Next;

            // Next 값이 유효한 Dialogue ID인지 확인
            if (!string.IsNullOrEmpty(nextID) && dialogues.ContainsKey(nextID))
            {
                Debug.Log($"...'Next'는 DialogueID 입니다. 대사를 시작합니다.");
                StartDialogue(nextID);
            }
            // Next 값이 유효한 Event ID인지 확인
            else if (!string.IsNullOrEmpty(nextID) && EventManager.Instance.events.ContainsKey(nextID))
            {
                Debug.Log($"...'Next'는 EventID 입니다. 이벤트를 호출합니다.");
                EventManager.Instance.CallEvent(nextID);
            }
            else
            {
                Debug.LogWarning($"...'Next' 값 '{nextID}'는 유효한 DialogueID 또는 EventID가 아닙니다.");
            }

            return;
        }

        // 현재 활성화된 TutorialDialog에게 선택된 튜토리얼 인덱스 전달
        TutorialDialog currentTutorialDialog = FindObjectOfType<TutorialDialog>();
        if (currentTutorialDialog != null)
        {
            Debug.Log($"[DialogueManager] TutorialDialog로 튜토리얼 인덱스 {choiceLine.TutorialIndex} 전달");
            currentTutorialDialog.OnChoiceSelectedInTutorial(choiceLine.TutorialIndex);
        }
        else
        {
            Debug.LogWarning("[DialogueManager] 활성화된 TutorialDialog를 찾을 수 없습니다.");
        }

        if (choiceLine.TutorialIndex >= 0)
        {
            TutorialController controller = FindObjectOfType<TutorialController>();
            if (controller != null)
            {
                controller.SetTutorialByIndex(choiceLine.TutorialIndex);
            }
            return;
        }
    }
}