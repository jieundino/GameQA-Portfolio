using System.Text.RegularExpressions;

public static class KoreanJosa
{
    // 한글 받침 유무
    private static bool HasJong(char ch)
    {
        if (ch < 0xAC00 || ch > 0xD7A3) return false; // 한글 음절 아님
        int jong = (ch - 0xAC00) % 28;
        return jong != 0;
    }

    // 한글 받침이 'ㄹ'인지 (으로/로 규칙)
    private static bool HasJongRieul(char ch)
    {
        if (ch < 0xAC00 || ch > 0xD7A3) return false;
        int jong = (ch - 0xAC00) % 28;
        return jong == 8; // ㄹ
    }

    // 이름의 마지막 "한글" 기준으로 받침 판정. (영문 등은 단순 휴리스틱)
    private static (bool hasJong, bool rieul) AnalyzeTail(string name)
    {
        for (int i = name.Length - 1; i >= 0; i--)
        {
            char c = name[i];
            if (c >= 0xAC00 && c <= 0xD7A3)
                return (HasJong(c), HasJongRieul(c));
            if (char.IsLetter(c))
            {
                // 영문 이름: 모음으로 끝나면 모음 취급(예: "Jae" -> 무받침), 그 외는 받침 취급
                bool vowel = "aeiouAEIOU".IndexOf(c) >= 0;
                return (!vowel, false);
            }
            // 숫자/기호는 스킵
        }
        // 아무 글자도 못 찾으면 무받침으로 가정
        return (false, false);
    }

    private static string Pick(string pair, bool hasJong) // "이/가", "은/는", "을/를", "과/와"
    {
        // "A/B" 형태를 기대
        var parts = pair.Split('/');
        if (parts.Length != 2) return pair;
        return hasJong ? parts[0] : parts[1];
    }

    private static string PickAaYa(bool hasJong) => hasJong ? "아" : "야";
    private static string PickEulReul(bool hasJong) => hasJong ? "을" : "를";
    private static string PickEunNeun(bool hasJong) => hasJong ? "은" : "는";
    private static string PickIGa(bool hasJong) => hasJong ? "이" : "가";
    private static string PickGwaWa(bool hasJong) => hasJong ? "과" : "와";
    private static string PickIRangRang(bool hasJong) => hasJong ? "이랑" : "랑";
    private static string PickEuroRo(bool hasJong, bool rieul)
    {
        // 받침이 없거나 받침이 ㄹ이면 "로", 그 외는 "으로"
        return (!hasJong || rieul) ? "로" : "으로";
    }

    /// <summary>
    /// 문장 내의 {VarName} + (이)가/(은)는/(을)를/(과)와/(으)로/은/는/이/가/을/를/과/와/으로/로 등을
    /// 실제 이름의 받침 규칙에 맞춰 자동 치환.
    /// 지원 패턴:
    ///   1) {Var}(이)가, {Var}(은)는, {Var}(을)를, {Var}(과)와, {Var}(으)로
    ///   2) {Var}은/는, {Var}이/가, {Var}을/를, {Var}과/와, {Var}아/야
    ///   3) {Var}은, {Var}는, {Var}이, {Var}가, {Var}을, {Var}를, {Var}과, {Var}와, {Var}으로, {Var}로, {Var}아, {Var}야
    ///   4) {Var} 단독
    /// </summary>
    public static string Apply(string text, params (string varToken, string value)[] vars)
    {
        foreach (var (varToken, value) in vars)
        {
            var (hasJong, rieul) = AnalyzeTail(value);

            // 1) 괄호 패턴: (이)가, (은)는, (을)를, (과)와, (으)로, (이)랑
            text = Regex.Replace(
                text,
                $@"\{{{varToken}\}}\((.)\)(.)",
                m =>
                {
                    string a = m.Groups[1].Value; // 괄호 안
                    string b = m.Groups[2].Value; // 괄호 뒤

                    // (으)로 특수 규칙
                    if (a == "으" && b == "로")
                        return value + PickEuroRo(hasJong, rieul);

                    // (이)랑 규칙
                    if (a == "이" && b == "랑")
                        return value + PickIRangRang(hasJong);

                    // 일반 2선택 조사
                    string pair = $"{a}/{b}";
                    string picked = pair switch
                    {
                        "이/가" => PickIGa(hasJong),
                        "은/는" => PickEunNeun(hasJong),
                        "을/를" => PickEulReul(hasJong),
                        "과/와" => PickGwaWa(hasJong),
                        "이랑/랑" => PickIRangRang(hasJong),
                        _ => Pick(pair, hasJong)
                    };
                    return value + picked;
                });

            // 2) 슬래시 패턴 (멀티 음절 우선)
            text = Regex.Replace(
                text,
                $@"\{{{varToken}\}}(이랑/랑|아/야|이/가|은/는|을/를|과/와)",
                m =>
                {
                    string pair = m.Groups[1].Value;
                    string picked = pair switch
                    {
                        "이랑/랑" => PickIRangRang(hasJong),
                        "아/야" => PickAaYa(hasJong),
                        "이/가" => PickIGa(hasJong),
                        "은/는" => PickEunNeun(hasJong),
                        "을/를" => PickEulReul(hasJong),
                        "과/와" => PickGwaWa(hasJong),
                        _ => Pick(pair, hasJong)
                    };
                    return value + picked;
                });

            // 3) 단일 조사 패턴 (멀티 음절 먼저, 그리고 '이|가'는 뒤가 '랑'이면 매칭 금지)
            text = Regex.Replace(
                text,
                $@"\{{{varToken}\}}(이랑|랑|으로|로|아|야|은|는|을|를|과|와|이(?!랑)|가(?!랑))",
                m =>
                {
                    string j = m.Groups[1].Value;
                    string picked = j switch
                    {
                        "이랑" => PickIRangRang(hasJong),
                        "랑" => PickIRangRang(hasJong),
                        "으로" or "로" => PickEuroRo(hasJong, rieul),
                        "아" or "야" => PickAaYa(hasJong),
                        "은" or "는" => PickEunNeun(hasJong),
                        "을" or "를" => PickEulReul(hasJong),
                        "과" or "와" => PickGwaWa(hasJong),
                        "이" or "가" => PickIGa(hasJong),
                        _ => j
                    };
                    return value + picked;
                });

            // 4) 단독 토큰 {Var}도 치환 (조사 없음)
            text = text.Replace($"{{{varToken}}}", value);
        }

        return text;
    }
}
