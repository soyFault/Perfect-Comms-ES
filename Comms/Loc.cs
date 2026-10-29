using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;

namespace VoiceChatPlugin.VoiceChat;

/// <summary>
/// Text localization. The language is the one selected in Among Us; translations live in
/// <c>Resources/Locale/&lt;locale&gt;.xml</c> (embedded). The pattern is adapted from MiraAPI's
/// <c>MiraLocaleManager</c> (All-Of-Us-Mods/MiraAPI, LGPL-2.1, same license as this project), without
/// depending on MiraAPI or Reactor.
///
/// Rules:
/// <list type="bullet">
/// <item>English is always the literal at the call site: <c>Loc.T("key", "English text")</c>. English
/// players, unit tests and any missing key therefore get exactly the original text.</item>
/// <item>Identifiers (config keys, row keys, anything sent over the network or logged) are never passed
/// through <see cref="T(string,string)"/>.</item>
/// <item>The XML carries plain text only: <c>{0}</c> placeholders and <c>\n</c> for a newline (<c>\\</c> for a backslash).
/// TextMeshPro markup (<c>&lt;color&gt;</c>, <c>&lt;size&gt;</c>) stays in C#.</item>
/// <item>Resolve at UI build time, never in a constructor, <c>const</c> or static initializer that
/// runs while the plugin loads (the language is not known yet).</item>
/// </list>
/// </summary>
internal static class Loc
{
    internal const string EnglishLocale = "en_US";

    /// <summary>Locale files shipped in <c>Resources/Locale</c>. English is documentation + fallback source.</summary>
    internal static readonly string[] ShippedLocales = { EnglishLocale, "es_419", "es_ES" };

    private const string ResourcePrefix = "VoiceChatPlugin.Resources.Locale.";

    private static readonly object Gate = new();
    private static readonly Dictionary<string, Dictionary<string, string>> Cache = new(StringComparer.Ordinal);

    /// <summary>Overrides the game language (tests, and a possible future "Language" option).</summary>
    internal static string? ForcedLocale;

    /// <summary>Locale used for lookups right now: forced, else the Among Us language, else English.</summary>
    internal static bool BisectForceEnglish = true;

    internal static string CurrentLocale
    {
        get
        {
            if (BisectForceEnglish) return EnglishLocale;
            var forced = ForcedLocale;
            if (forced != null) return forced;
            try
            {
                return LocaleFor(AmongUs.Data.DataManager.Settings.Language.CurrentLanguage);
            }
            catch
            {
                // DataManager is not ready (plugin Load, unit tests, headless harness).
                return EnglishLocale;
            }
        }
    }

    /// <summary>Maps the game's language to a shipped locale. Unknown languages use English.</summary>
    internal static string LocaleFor(SupportedLangs language) => language switch
    {
        SupportedLangs.Latam => "es_419",
        SupportedLangs.Spanish => "es_ES",
        _ => EnglishLocale,
    };

    /// <summary>Translates <paramref name="key"/>; returns <paramref name="english"/> when there is no translation.</summary>
    internal static string T(string key, string english)
    {
        var locale = CurrentLocale;
        if (locale == EnglishLocale) return english;
        return Table(locale).TryGetValue(key, out var text) && text.Length > 0 ? text : english;
    }

    /// <summary>
    /// Translates and formats with <see cref="string.Format(string, object[])"/>. A broken translation
    /// (bad placeholder) falls back to the English format instead of throwing in the UI.
    /// </summary>
    internal static string T(string key, string english, params object[] args)
    {
        var text = T(key, english);
        try { return string.Format(text, args); }
        catch (FormatException)
        {
            VoiceDiagnostics.Log("loc.format", $"key={key} locale={CurrentLocale}");
            return string.Format(english, args);
        }
    }

    /// <summary>All strings of a shipped locale (loaded once). Used by tests and tooling.</summary>
    internal static IReadOnlyDictionary<string, string> Table(string locale)
    {
        lock (Gate)
        {
            if (!Cache.TryGetValue(locale, out var table))
            {
                table = Load(locale);
                Cache[locale] = table;
            }
            return table;
        }
    }

    /// <summary>Drops cached tables (tests).</summary>
    internal static void ClearCache()
    {
        lock (Gate) Cache.Clear();
    }

    internal static Dictionary<string, string> Parse(string xml)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var doc = new XmlDocument();
        doc.LoadXml(xml);
        var nodes = doc.SelectNodes("/resources/string");
        if (nodes == null) return result;
        foreach (XmlNode node in nodes)
        {
            var name = node.Attributes?["name"]?.Value;
            if (string.IsNullOrEmpty(name)) continue;
            result[name] = Unescape(node.InnerText);
        }
        return result;
    }

    // XML text uses C#-style escapes: \n = newline, \\ = backslash.
    private static string Unescape(string text)
    {
        if (text.IndexOf('\\') < 0) return text;
        var sb = new System.Text.StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\\' && i + 1 < text.Length && (text[i + 1] == 'n' || text[i + 1] == '\\'))
            {
                sb.Append(text[i + 1] == 'n' ? '\n' : '\\');
                i++;
            }
            else sb.Append(text[i]);
        }
        return sb.ToString();
    }

    private static Dictionary<string, string> Load(string locale)
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(ResourcePrefix + locale + ".xml");
            if (stream == null) return new Dictionary<string, string>(StringComparer.Ordinal);
            using var reader = new StreamReader(stream);
            return Parse(reader.ReadToEnd());
        }
        catch (Exception ex)
        {
            VoiceDiagnostics.Log("loc.error", $"locale={locale} error=\"{ex.Message}\"");
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }
}
