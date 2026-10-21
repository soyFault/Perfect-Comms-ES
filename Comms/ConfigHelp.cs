using System.Collections.Generic;
using BepInEx.Configuration;

namespace VoiceChatPlugin.VoiceChat;

/// <summary>
/// Config descriptions are written to the .cfg file once, in English, for people who edit it by hand.
/// The settings panel shows the same text as the row help, and that copy is translated: the English
/// description is the lookup key for its <see cref="Loc.LocText"/>.
/// </summary>
internal static class ConfigHelp
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, Loc.LocText> ByEnglish = new(System.StringComparer.Ordinal);

    /// <summary>Creates the (English) <see cref="ConfigDescription"/> and remembers its translation.</summary>
    internal static ConfigDescription D(Loc.LocText help, AcceptableValueBase? acceptable = null)
    {
        lock (Gate) ByEnglish[help.English] = help;
        return new ConfigDescription(help.English, acceptable);
    }

    /// <summary>Help text for a setting row, in the current language.</summary>
    internal static string Text(ConfigEntryBase entry)
    {
        string english = entry.Description?.Description ?? string.Empty;
        if (english.Length == 0) return english;
        lock (Gate)
            return ByEnglish.TryGetValue(english, out var help) ? help.Text : english;
    }
}
