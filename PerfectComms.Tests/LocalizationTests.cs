using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using VoiceChatPlugin.VoiceChat;
using Xunit;

public sealed class LocalizationTests
{
    private static readonly Regex Placeholder = new(@"\{(\d+)[^}]*\}", RegexOptions.Compiled);

    [Theory]
    [InlineData(SupportedLangs.English, "en_US")]
    [InlineData(SupportedLangs.Latam, "es_419")]
    [InlineData(SupportedLangs.Spanish, "es_ES")]
    [InlineData(SupportedLangs.French, "en_US")]
    [InlineData(SupportedLangs.SChinese, "en_US")]
    public void GameLanguageMapsToAShippedLocale(SupportedLangs language, string expected)
    {
        Assert.Equal(expected, Loc.LocaleFor(language));
        Assert.Contains(Loc.LocaleFor(language), Loc.ShippedLocales);
    }

    [Fact]
    public void WithoutAGameLanguageEnglishIsUsed()
    {
        // No DataManager in the test process: must not throw, must be English.
        WithLocale(null, () => Assert.Equal("en_US", Loc.CurrentLocale));
    }

    [Fact]
    public void EnglishAlwaysReturnsTheCodeLiteral()
    {
        WithLocale("en_US", () => Assert.Equal("Whatever", Loc.T("hud.muted", "Whatever")));
    }

    [Fact]
    public void SpanishReturnsTheTranslationAndFallsBackWhenMissing()
    {
        foreach (var locale in new[] { "es_419", "es_ES" })
            WithLocale(locale, () =>
            {
                Assert.Equal("Silenciado", Loc.T("hud.muted", "Muted"));
                Assert.Equal("Not translated yet", Loc.T("no.such.key", "Not translated yet"));
            });
    }

    [Fact]
    public void UnknownLocaleFallsBackToEnglishText()
    {
        WithLocale("xx_XX", () => Assert.Equal("Muted", Loc.T("hud.muted", "Muted")));
    }

    [Fact]
    public void PlaceholdersAreFormattedAndBrokenFormatsFallBack()
    {
        WithLocale("en_US", () => Assert.Equal("Step 2 of 5", Loc.T("k", "Step {0} of {1}", 2, 5)));
        WithLocale("es_419", () =>
            // Missing key -> English literal, still formatted.
            Assert.Equal("Step 2 of 5", Loc.T("no.such.key", "Step {0} of {1}", 2, 5)));
    }

    [Fact]
    public void ParseHandlesNewlinesAndEntities()
    {
        var table = Loc.Parse("<resources><string name=\"a\">One\\nTwo &amp; {0} C:\\\\x</string><string name=\"\">x</string></resources>");
        Assert.Equal("One\nTwo & {0} C:\\x", table["a"]);
        Assert.Single(table);
    }

    [Fact]
    public void CompactStatusFollowsTheLanguage()
    {
        WithLocale("en_US", () => Assert.Equal("Muted / Deafened", VoiceCompactStatusPolicy.StateText(true, true)));
        WithLocale("es_419", () =>
        {
            Assert.Equal("Silenciado / Ensordecido", VoiceCompactStatusPolicy.StateText(true, true));
            Assert.Equal("Ensordecido", VoiceCompactStatusPolicy.StateText(false, true));
            Assert.Equal("Silenciado", VoiceCompactStatusPolicy.StateText(true, false));
        });
    }

    [Fact]
    public void EveryShippedLocaleIsEmbeddedAndNotEmpty()
    {
        foreach (var locale in Loc.ShippedLocales)
            Assert.NotEmpty(Loc.Table(locale));
    }

    [Fact]
    public void AllLocalesHaveTheSameKeysAndPlaceholders()
    {
        var english = Loc.Table("en_US");
        foreach (var locale in Loc.ShippedLocales.Where(l => l != "en_US"))
        {
            var table = Loc.Table(locale);
            Assert.Empty(english.Keys.Except(table.Keys));   // untranslated keys
            Assert.Empty(table.Keys.Except(english.Keys));   // orphan keys
            foreach (var (key, text) in table)
            {
                Assert.False(string.IsNullOrWhiteSpace(text), $"{locale}:{key} is empty");
                Assert.Equal(Placeholders(english[key]), Placeholders(text));
            }
        }
    }

    [Fact]
    public void EnglishXmlMatchesTheLiteralsInTheSource()
    {
        var calls = LocScanner.Scan(LocScanner.FindRepoRoot());
        var english = Loc.Table("en_US");

        Assert.Empty(calls.Problems);
        Assert.Empty(calls.Strings.Keys.Except(english.Keys));   // Loc.T key missing from en_US.xml: run scripts/localization.py
        Assert.Empty(english.Keys.Except(calls.Strings.Keys));   // stale key in en_US.xml
        foreach (var (key, text) in calls.Strings)
            Assert.Equal(text, english[key]);
    }

    [Fact]
    public void OffThreadCallersReuseTheCachedLocaleInsteadOfReadingTheGame()
    {
        try
        {
            Loc.Initialize();   // this thread is now "the main thread"
            string? seen = null;
            var worker = new System.Threading.Thread(() => seen = Loc.CurrentLocale);
            worker.Start();
            worker.Join();
            Assert.Equal("en_US", seen);   // cached value; no DataManager access off the main thread
            Assert.Equal("Muted", Loc.T("hud.muted", "Muted"));
        }
        finally { Loc.ResetThreadState(); }
    }

    // ---- Stage 2: pure policies and HUD text ----

    [Fact]
    public void ConnectionStatusKeepsEnglishAndTranslatesEveryStage()
    {
        var connecting = new VoiceConnectionProgress(VoiceConnectionStage.ConnectingPlayers, 2, 5);
        var starting = VoiceConnectionProgress.Starting;
        var retrying = new VoiceConnectionProgress(VoiceConnectionStage.RetryingAudio, 0, 0);
        var syncing = new VoiceConnectionProgress(VoiceConnectionStage.SyncingSession, 0, 0);
        var noCount = new VoiceConnectionProgress(VoiceConnectionStage.ConnectingPlayers, 0, 0);

        WithLocale("en_US", () =>
        {
            Assert.Equal("Connecting voice... 2/5 players connected", VoiceConnectionStatusPolicy.BuildText(connecting, 0));
            Assert.Equal("Connecting voice.... starting audio", VoiceConnectionStatusPolicy.BuildText(starting, 1));
            Assert.Equal("Voice unavailable - retrying audio.....", VoiceConnectionStatusPolicy.BuildText(retrying, 2));
        });
        WithLocale("es_419", () =>
        {
            Assert.Equal("Conectando voz... 2/5 jugadores conectados", VoiceConnectionStatusPolicy.BuildText(connecting, 0));
            Assert.Equal("Conectando voz.... iniciando audio", VoiceConnectionStatusPolicy.BuildText(starting, 1));
            Assert.Equal("Voz no disponible - reintentando audio.....", VoiceConnectionStatusPolicy.BuildText(retrying, 2));
            Assert.Equal("Conectando voz... sincronizando sesión", VoiceConnectionStatusPolicy.BuildText(syncing, 0));
            Assert.Equal("Conectando voz...", VoiceConnectionStatusPolicy.BuildText(noCount, 0));
        });
    }

    [Fact]
    public void TeamRadioChannelNamesFollowTheLanguage()
    {
        WithLocale("en_US", () => Assert.Equal("All Teams", VoiceTeamRadioChannels.DisplayName(VoiceTeamRadioChannel.All)));
        WithLocale("es_ES", () =>
        {
            Assert.Equal("Impostores", VoiceTeamRadioChannels.DisplayName(VoiceTeamRadioChannel.Impostors));
            Assert.Equal("Todos los equipos", VoiceTeamRadioChannels.DisplayName(VoiceTeamRadioChannel.All));
            Assert.Equal("No disponible", VoiceTeamRadioChannels.DisplayName(VoiceTeamRadioChannel.None));
        });
    }

    [Fact]
    public void PreviewRosterNamesAreNumberedInBothLanguages()
    {
        WithLocale("en_US", () =>
        {
            Assert.Equal("Player 01", SpeakingBarPreviewRoster.Name(0));
            Assert.Equal("Player 10", SpeakingBarPreviewRoster.Name(9));
            Assert.Equal("Ghost 11", SpeakingBarPreviewRoster.Name(10));
            Assert.Equal("Ghost 15", SpeakingBarPreviewRoster.Name(14));
        });
        WithLocale("es_419", () =>
        {
            Assert.Equal("Jugador 01", SpeakingBarPreviewRoster.Name(0));
            Assert.Equal("Fantasma 11", SpeakingBarPreviewRoster.Name(10));
        });
    }

    [Fact]
    public void KickReasonsAreSentInEnglishAndLocalizedByTheReceivingClient()
    {
        var mismatch = string.Format(VoiceJoinGuard.MismatchFormat, "4.1.11", "4.0.0");
        var missing = string.Format(VoiceJoinGuard.MissingFormat, "4.1.11");

        // English (or any language without a translation) shows exactly what the host sent.
        WithLocale("en_US", () =>
        {
            Assert.Equal(mismatch, VoiceJoinGuard.LocalizeKickReason(mismatch));
            Assert.Equal(missing, VoiceJoinGuard.LocalizeKickReason(missing));
        });
        WithLocale("es_419", () =>
        {
            var translated = VoiceJoinGuard.LocalizeKickReason(mismatch);
            Assert.Contains("no coincide", translated);
            Assert.Contains("4.1.11", translated);
            Assert.Contains("4.0.0", translated);
            Assert.Contains("Instala o habilita", VoiceJoinGuard.LocalizeKickReason(missing));
            // Unknown reasons (other mods, newer hosts) pass through untouched.
            Assert.Equal("Banned by host", VoiceJoinGuard.LocalizeKickReason("Banned by host"));
        });
    }

    [Fact]
    public void NoStaticFieldOrConstResolvesTextWhileThePluginLoads()
    {
        // `static readonly string X = Loc.T(...)` would freeze the language at class-init time.
        // Use a property / Loc.L (resolved on use) instead.
        var offenders = LocScanner.FindEagerStaticLocalization(LocScanner.FindRepoRoot());
        Assert.Empty(offenders);
    }

    // ---- Stage 3: panels ----

    [Fact]
    public void HudPresetsKeepStableEnglishIdsAndLocalizeTheDisplayText()
    {
        var preset = FirstRunHudPresets.All[0];
        WithLocale("en_US", () =>
        {
            Assert.Equal("Top Middle", preset.Name);
            Assert.Equal("Top Middle", preset.DisplayName);
        });
        WithLocale("es_419", () =>
        {
            Assert.Equal("Top Middle", preset.Name);        // id never changes
            Assert.Equal("Arriba al centro", preset.DisplayName);
            Assert.NotEqual(preset.Description, preset.DisplayDescription);
        });
    }

    [Fact]
    public void EverySettingsRowKeyStillResolvesItsHelpInSpanish()
    {
        WithLocale("es_419", () =>
        {
            Assert.Equal("Qué tan fuerte te escuchan los demás jugadores.", VoiceSettingsPanel.RowHelp("Mic Volume"));
            Assert.Null(VoiceSettingsPanel.RowHelp("Volumen del micrófono"));
        });
    }

    [Fact]
    public void CategoryCaptionsFollowTheLanguage()
    {
        WithLocale("en_US", () => Assert.Equal("Keybinds", VoiceSettingsCategoryText.Caption(VoiceSettingsCategory.Keybinds)));
        WithLocale("es_ES", () =>
        {
            Assert.Equal("Atajos", VoiceSettingsCategoryText.Caption(VoiceSettingsCategory.Keybinds));
            Assert.Equal("Dispositivos", VoiceSettingsCategoryText.Caption(VoiceSettingsCategory.Devices));
        });
    }

    [Fact]
    public void LocTextResolvesWhenUsedNotWhenDeclared()
    {
        var label = Loc.L("settings.row.mic_volume", "Mic Volume");
        WithLocale("en_US", () => Assert.Equal("Mic Volume", label.Text));
        WithLocale("es_419", () =>
        {
            Assert.Equal("Volumen del micrófono", label.Text);
            Assert.Equal("Mic Volume", label.English);   // stable id
        });
    }

    // ---- Stage 4: options, keybinds, config help ----

    [Fact]
    public void KeybindNamesAndHelpAreTranslatedButTheIdsStayEnglish()
    {
        WithTemporaryConfig(config =>
        {
            VoiceChatKeybinds.Initialize(config);
            WithLocale("en_US", () =>
            {
                Assert.Equal("Mute / Unmute Mic", VoiceChatKeybinds.ToggleMute.DisplayName);
                Assert.Equal("Toggles whether your microphone sends voice.", VoiceChatKeybinds.ToggleMute.HelpText);
            });
            WithLocale("es_419", () =>
            {
                Assert.Equal("Silenciar / activar micrófono", VoiceChatKeybinds.ToggleMute.DisplayName);
                Assert.Equal("Mute / Unmute Mic", VoiceChatKeybinds.ToggleMute.Id);
                Assert.Equal("Alternar ensordecimiento", VoiceChatKeybinds.ToggleSpeaker.DisplayName);
                Assert.Equal("Toggle Speaker", VoiceChatKeybinds.ToggleSpeaker.Id);
                Assert.Equal("Pulsar para silenciar", VoiceChatKeybinds.PushToMute.DisplayName);
                Assert.Equal("Hold To Mute", VoiceChatKeybinds.PushToMute.Id);
            });
        });
    }

    [Fact]
    public void ToggleDeafenConstantsMatchTheirLocalizedEnglish()
    {
        WithTemporaryConfig(config =>
        {
            VoiceChatKeybinds.Initialize(config);
            WithLocale("en_US", () =>
            {
                Assert.Equal(VoiceChatKeybinds.ToggleDeafenDisplayName, VoiceChatKeybinds.ToggleSpeaker.DisplayName);
                Assert.Equal(VoiceChatKeybinds.ToggleDeafenHelpText, VoiceChatKeybinds.ToggleSpeaker.HelpText);
            });
        });
    }

    [Fact]
    public void HostOptionHoldersResolveLabelsChoicesAndHelpPerLanguage()
    {
        WithTemporaryConfig(config =>
        {
            var toggle = new ToggleHolder(config, "Host.Loc", "Flag", Loc.L("host.public_voice_lobby", "Public Voice Lobby"), true,
                Loc.L("host.public_voice_lobby_help", "Publishes this voice-enabled lobby to the Perfect Comms live directory so other Perfect Comms players can find it."));
            var mode = new EnumHolder(config, "Host.Loc", "Mode", Loc.L("host.falloff_mode", "Voice Falloff"), 0,
                typeof(VoiceFalloffMode),
                new[] { Loc.L("host.falloff_mode_linear", "Linear"), Loc.L("host.falloff_mode_smooth", "Smooth") },
                Loc.L("host.falloff_mode_help", "Chooses how voice volume fades as a player approaches the maximum hearing distance."));
            WithLocale("en_US", () =>
            {
                Assert.Equal("Public Voice Lobby", toggle.Label);
                Assert.Equal("Linear", mode.LabelAt(0));
            });
            WithLocale("es_ES", () =>
            {
                Assert.NotEqual("Public Voice Lobby", toggle.Label);
                Assert.Equal("Host.Loc.Flag", toggle.Id);        // id never changes
                Assert.Equal(2, mode.LabelCount);
                Assert.NotEqual("Linear", mode.LabelAt(0));
                Assert.NotEqual("Linear", mode.Labels[0]);
            });
        });
    }

    [Fact]
    public void ThirdPartyOptionTextIsNeverTranslated()
    {
        var holder = new ModToggleHolder("my.mod.opt", "Lovers Radio", "A custom help");
        WithLocale("es_419", () =>
        {
            Assert.Equal("Lovers Radio", holder.Label);
            Assert.Equal("A custom help", holder.HelpText);
        });
    }

    [Fact]
    public void ConfigFileKeepsEnglishDescriptionsWhileThePanelHelpIsTranslated()
    {
        WithTemporaryConfig(config =>
        {
            var entry = config.Bind("Audio", "MicVolume", 1f,
                ConfigHelp.D(Loc.L("help.mic_volume", "Adjusts how loudly your microphone is sent to other players. This does not change when the mic counts as speaking.")));
            Assert.Equal("Adjusts how loudly your microphone is sent to other players. This does not change when the mic counts as speaking.",
                entry.Description.Description);   // what is written to the .cfg
            WithLocale("en_US", () => Assert.Equal(entry.Description.Description, ConfigHelp.Text(entry)));
            WithLocale("es_419", () => Assert.StartsWith("Ajusta el volumen", ConfigHelp.Text(entry)));
        });
    }

    [Fact]
    public void DeviceNamesAreTranslatedForDisplayOnly()
    {
        var devices = new[]
        {
            new VoiceDeviceInfo(string.Empty, "Default", true),
            new VoiceDeviceInfo("id-1", "USB Headset", false),
            VoiceDeviceInfo.Unavailable("id-2", string.Empty, "Saved microphone"),
            VoiceDeviceInfo.Unavailable("id-3", "Old Mic", "Saved microphone"),
        };
        WithLocale("en_US", () => Assert.Equal(
            new[] { "Default", "USB Headset", "Saved microphone (unavailable)", "Old Mic (unavailable)" },
            VoiceChatLocalSettings.DeviceNames(devices)));
        WithLocale("es_419", () => Assert.Equal(
            new[] { "Predeterminado", "USB Headset", "Micrófono guardado (no disponible)", "Old Mic (no disponible)" },
            VoiceChatLocalSettings.DeviceNames(devices)));
        Assert.Equal("Default", devices[0].Name);   // the stored name is data and never changes
    }

    private static void WithTemporaryConfig(Action<BepInEx.Configuration.ConfigFile> test)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"PerfectComms-LocTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            test(new BepInEx.Configuration.ConfigFile(Path.Combine(directory, "loc.cfg"), saveOnInit: false,
                new BepInEx.BepInPlugin("test.perfectcomms.localization2", "test", "1.0.0")));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private static string Placeholders(string text)
        => string.Join(",", Placeholder.Matches(text).Select(m => m.Groups[1].Value).Distinct().OrderBy(v => v));

    private static void WithLocale(string? locale, Action body)
    {
        var previous = Loc.ForcedLocale;
        Loc.ForcedLocale = locale;
        try { body(); }
        finally { Loc.ForcedLocale = previous; }
    }
}

/// <summary>Finds <c>Loc.T("key", "English literal")</c> calls in the production sources.</summary>
internal static class LocScanner
{
    internal sealed class Result
    {
        public Dictionary<string, string> Strings { get; } = new(StringComparer.Ordinal);
        public List<string> Problems { get; } = new();
    }

    private static readonly Regex Call = new(
        "Loc\\.[TL]\\(\\s*\"(?<key>(?:[^\"\\\\]|\\\\.)*)\"\\s*,\\s*(?<english>\"(?:[^\"\\\\]|\\\\.)*\"|[^\\s\"][^,)]*)",
        RegexOptions.Compiled);

    internal static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "PerfectComms.csproj")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("PerfectComms.csproj not found above the test output.");
    }

    internal static Result Scan(string root)
    {
        var result = new Result();
        var skipped = new[] { "PerfectComms.Tests", "PerfectComms.Starlight.Tests", "native", "Libs", "docs", "wiki", "cloudflare" };
        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            var parts = relative.Split('/');
            if (parts.Any(p => p is "bin" or "obj") || skipped.Contains(parts[0])) continue;
            if (relative == "Comms/Loc.cs") continue;

            foreach (Match m in Call.Matches(File.ReadAllText(file)))
            {
                var key = m.Groups["key"].Value;
                var raw = m.Groups["english"].Value;
                if (!raw.StartsWith('"'))
                {
                    result.Problems.Add($"{relative}: Loc.T(\"{key}\", ...) needs a plain string literal as English text (got `{raw}`)");
                    continue;
                }
                var english = Unescape(raw[1..^1]);
                if (english == null)
                {
                    result.Problems.Add($"{relative}: Loc.T(\"{key}\", ...) uses an unsupported escape sequence");
                    continue;
                }
                if (result.Strings.TryGetValue(key, out var existing) && existing != english)
                    result.Problems.Add($"{relative}: key \"{key}\" is used with different English texts");
                result.Strings[key] = english;
            }
        }
        return result;
    }

    internal static List<string> FindEagerStaticLocalization(string root)
    {
        var offenders = new List<string>();
        var skipped = new[] { "PerfectComms.Tests", "PerfectComms.Starlight.Tests", "native", "Libs", "docs", "wiki", "cloudflare" };
        var eager = new Regex(@"\b(?:static\s+readonly|const)\s[^;{}]*?Loc\.T\(", RegexOptions.Singleline | RegexOptions.Compiled);
        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            var parts = relative.Split('/');
            if (parts.Any(p => p is "bin" or "obj") || skipped.Contains(parts[0]) || relative == "Comms/Loc.cs") continue;
            foreach (Match m in eager.Matches(File.ReadAllText(file)))
                offenders.Add($"{relative}: {m.Value.Trim().Split('\n')[0]}");
        }
        return offenders;
    }

    private static string? Unescape(string literal)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < literal.Length; i++)
        {
            if (literal[i] != '\\') { sb.Append(literal[i]); continue; }
            if (++i >= literal.Length) return null;
            switch (literal[i])
            {
                case 'n': sb.Append('\n'); break;
                case 't': sb.Append('\t'); break;
                case '"': sb.Append('"'); break;
                case '\'': sb.Append('\''); break;
                case '\\': sb.Append('\\'); break;
                default: return null;
            }
        }
        return sb.ToString();
    }
}
