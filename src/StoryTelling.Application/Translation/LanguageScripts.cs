namespace StoryTelling.Application.Translation;

public static class LanguageScripts
{
    public static CharScript For(string languageCode) => languageCode?.Trim().ToLowerInvariant() switch
    {
        "ru" or "uk" or "be" or "bg" or "sr" or "mk" => CharScript.Cyrillic,
        "zh" or "ja" or "ko" => CharScript.Cjk,
        "ar" or "fa" or "ur" => CharScript.Arabic,
        "hi" or "mr" or "ne" => CharScript.Devanagari,
        "en" or "de" or "es" or "fr" or "it" or "pt" or "nl" or "pl" or "sv" or "da" or "no"
            or "fi" or "cs" or "sk" or "hu" or "ro" or "tr" or "id" or "vi" or "cy" or "ga" or "hr"
            or "sl" or "lt" or "lv" or "et" or "sq" or "az" => CharScript.Latin,
        _ => CharScript.None,
    };
}
