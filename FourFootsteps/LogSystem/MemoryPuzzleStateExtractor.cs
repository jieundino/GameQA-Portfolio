using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

// GameData.json 구조가 다양할 수 있어 "MemoryPuzzleStates"만 최대한 안전하게 추출하는 유틸
public static class MemoryPuzzleStateExtractor
{
    /// <summary>
    /// GameData.json에서 Variables.MemoryPuzzleStates를 찾아 정규화된 JSON("{\"0\":true,...}")로 반환
    /// 실패 시 "{}" 반환
    /// </summary>
    public static string ExtractNormalizedJsonFromSave(string gameDataPath)
    {
        if (string.IsNullOrEmpty(gameDataPath) || !File.Exists(gameDataPath))
            return "{}";

        try
        {
            string raw = File.ReadAllText(gameDataPath);

            // 1) "Variables" 블록 안에서 "MemoryPuzzleStates"를 찾는다.
            // 단순 JSON 파서 없이도 동작하게 "키 기준 탐색"을 사용.
            // 더 안정적으로 하려면 Newtonsoft JSON을 쓰는 게 좋지만, 기본 Unity만으로 해결하는 방향.

            // 우선 "MemoryPuzzleStates" 키의 위치 탐색
            int idx = raw.IndexOf("\"MemoryPuzzleStates\"", StringComparison.Ordinal);
            if (idx < 0) return "{}";

            // ':' 이후 값 시작 지점
            int colon = raw.IndexOf(':', idx);
            if (colon < 0) return "{}";

            int valueStart = colon + 1;

            // 공백 스킵
            while (valueStart < raw.Length && char.IsWhiteSpace(raw[valueStart])) valueStart++;

            // 2) 값이 객체('{')인지 문자열('"')인지 판단
            if (valueStart >= raw.Length) return "{}";

            if (raw[valueStart] == '{')
            {
                // 객체로 직접 들어있는 경우: {...}를 괄호 매칭으로 잘라내기
                string obj = SliceJsonObject(raw, valueStart);
                return NormalizeObjectJson(obj);
            }
            else if (raw[valueStart] == '"')
            {
                // 문자열로 들어있는 경우: " {\"0\":true...} " 형태
                string strVal = SliceJsonString(raw, valueStart);
                // 앞뒤 따옴표 제거 + escape 해제
                string unescaped = UnescapeJsonString(strVal);

                // unescaped가 다시 객체 JSON이면 정규화
                unescaped = unescaped.Trim();
                if (unescaped.StartsWith("{") && unescaped.EndsWith("}"))
                    return NormalizeObjectJson(unescaped);

                return "{}";
            }

            return "{}";
        }
        catch
        {
            return "{}";
        }
    }

    private static string SliceJsonObject(string s, int startIdx)
    {
        int depth = 0;
        for (int i = startIdx; i < s.Length; i++)
        {
            if (s[i] == '{') depth++;
            else if (s[i] == '}')
            {
                depth--;
                if (depth == 0)
                    return s.Substring(startIdx, i - startIdx + 1);
            }
        }
        return "{}";
    }

    private static string SliceJsonString(string s, int startIdx)
    {
        // startIdx는 '"'
        bool escape = false;
        for (int i = startIdx + 1; i < s.Length; i++)
        {
            char c = s[i];
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
                // startIdx..i inclusive
                return s.Substring(startIdx, i - startIdx + 1);
            }
        }
        return "\"{}\"";
    }

    private static string UnescapeJsonString(string quoted)
    {
        // quoted는 "\"....\"" 형태
        if (string.IsNullOrEmpty(quoted)) return "{}";
        if (quoted.Length < 2) return "{}";

        string inner = quoted.Substring(1, quoted.Length - 2);
        // JSON string escape를 간단 처리
        inner = inner.Replace("\\\"", "\"");
        inner = inner.Replace("\\\\", "\\");
        inner = inner.Replace("\\r", "\r").Replace("\\n", "\n").Replace("\\t", "\t");
        return inner;
    }

    private static string NormalizeObjectJson(string objJson)
    {
        // 입력 예: { "0": true, "1": false, ... }
        // 출력 예: { "1": true, "2": false, ... }

        if (string.IsNullOrEmpty(objJson))
            return "{}";

        try
        {
            // 아주 단순한 JSON 파싱 (key:int, value:bool 전제)
            Dictionary<int, bool> temp = new Dictionary<int, bool>();

            // 중괄호 제거
            string inner = objJson.Trim();
            if (inner.StartsWith("{")) inner = inner.Substring(1);
            if (inner.EndsWith("}")) inner = inner.Substring(0, inner.Length - 1);

            // "0":true,"1":false 형태 분리
            string[] pairs = inner.Split(',');

            foreach (string pair in pairs)
            {
                if (string.IsNullOrWhiteSpace(pair)) continue;

                string[] kv = pair.Split(':');
                if (kv.Length != 2) continue;

                string keyStr = kv[0].Trim().Replace("\"", "");
                string valStr = kv[1].Trim();

                if (!int.TryParse(keyStr, out int key)) continue;
                if (!bool.TryParse(valStr, out bool value)) continue;

                temp[key + 1] = value; // 0부터 시작하는 것을 1부터 시작하도록 숫자 조정
            }

            // 다시 JSON 문자열로 조립 (1~5)
            StringBuilder sb = new StringBuilder();
            sb.Append("{");

            bool first = true;
            foreach (var kv in temp)
            {
                if (!first) sb.Append(",");
                first = false;

                sb.Append($"\"{kv.Key}\":{kv.Value.ToString().ToLower()}");
            }

            sb.Append("}");
            return sb.ToString();
        }
        catch
        {
            return "{}";
        }
    }

}
