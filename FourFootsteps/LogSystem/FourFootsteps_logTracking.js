const SPREADSHEET_ID = "1xwbc8Pgr39OpQudPEozyjl5PFthM0u_lexOVqmttTP4";
const SHEET_NAME = "GameResultLogs";
const API_KEY = "1xwbc8Pgr39OpQudPEozyjl5PFthM0u_FourfootstepsLog"; // Unity와 동일하게 맞추기
const EVENT_ID_COLUMN = 2; // B열(1부터 시작): timestamp(A)=1, eventId(B)=2

function doPost(e) {
  try {
    const payload = JSON.parse(e.postData.contents || "{}");

    // 1) 간단 인증(공유키)
    const receivedKey = String(payload.apiKey || "").trim();
    if (!receivedKey || receivedKey !== API_KEY) {
      return json_({ ok: false, error: "Unauthorized" });
    }

    // active가 아니라 ID로 고정 오픈
    const ss = SpreadsheetApp.openById(SPREADSHEET_ID);
    let sheet = ss.getSheetByName(SHEET_NAME);
    if (!sheet) return json_({ ok: false, error: "Sheet not found" });

    // 2) 필수값 체크
    const eventId = String(payload.eventId || "");
    if (!eventId) return json_({ ok: false, error: "Missing eventId" });


    // 3) 중복 방지: eventId가 이미 있으면 append 하지 않고 ok:true로 반환
    if (isDuplicateEventId_(sheet, eventId)) {
      return json_({ ok: true, duplicated: true });
    }

    const timestamp = new Date();

    const row = [
      timestamp,
      eventId,
      String(payload.playerKey || ""),
      String(payload.runId || ""),
      String(payload.eventType || ""),
      String(payload.playerName || ""),
      String(payload.catName || ""),
      String(payload.endingType || ""),
      String(payload.memoryPuzzleStatesJson || ""),
      String(payload.responsibilityScore || ""),
    ];

    sheet.appendRow(row);
  return json_({ ok: true });

  } catch (err) {
      return json_({ ok: false, error: String(err) });
  }
}

function isDuplicateEventId_(sheet, eventId) {
  const lastRow = sheet.getLastRow();
  if (lastRow < 2) return false; // 헤더만 있는 경우

  // B열(eventId) 전체를 가져와서 검사
  const range = sheet.getRange(2, EVENT_ID_COLUMN, lastRow - 1, 1);
  const values = range.getValues(); // [[...], [...]]
  for (let i = 0; i < values.length; i++) {
    if (String(values[i][0]) === eventId) return true;
  }
  return false;
}

function json_(obj) {
  return ContentService.createTextOutput(JSON.stringify(obj))
    .setMimeType(ContentService.MimeType.JSON);
}
