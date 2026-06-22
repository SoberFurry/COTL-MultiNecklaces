using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx.Logging;
using Newtonsoft.Json.Linq;

namespace Sam.MultiNecklaces;

/// <summary>Minimal localization: embedded ru/en json, English fallback.</summary>
internal static class Localizer
{
    private static Dictionary<string, string> _strings = new();
    private static Dictionary<string, string> _fallback = new();
    private static ManualLogSource? _log;

    public static void Init(ManualLogSource log)
    {
        _log = log;
        _fallback = Load("en");
        string lang = DetectLanguage();
        _strings = lang == "en" ? _fallback : Load(lang);
        _log.LogInfo($"Localization language = {lang} ({_strings.Count} strings)");
    }

    public static string Get(string key)
    {
        if (_strings.TryGetValue(key, out var v)) return v;
        if (_fallback.TryGetValue(key, out var f)) return f;
        return key;
    }

    private static string DetectLanguage()
    {
        try
        {
            string code = I2.Loc.LocalizationManager.CurrentLanguageCode ?? string.Empty;
            if (code.StartsWith("ru", StringComparison.OrdinalIgnoreCase)) return "ru";
            if (!string.IsNullOrEmpty(code)) return "en";
        }
        catch { }
        try
        {
            if (System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
                .Equals("ru", StringComparison.OrdinalIgnoreCase))
                return "ru";
        }
        catch { }
        return "en";
    }

    private static Dictionary<string, string> Load(string lang)
    {
        var result = new Dictionary<string, string>();
        try
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            string resName = $"{typeof(Localizer).Namespace}.Localization.{lang}.json";
            using Stream? s = asm.GetManifestResourceStream(resName);
            if (s == null) { _log?.LogWarning($"Localization resource not found: {resName}"); return result; }
            using var reader = new StreamReader(s);
            var obj = JObject.Parse(reader.ReadToEnd());
            foreach (var kv in obj)
                result[kv.Key] = kv.Value?.ToString() ?? string.Empty;
        }
        catch (Exception e)
        {
            _log?.LogError($"Failed to load localization '{lang}': {e}");
        }
        return result;
    }
}
