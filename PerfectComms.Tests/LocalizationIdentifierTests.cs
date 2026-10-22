using System;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using VoiceChatPlugin.VoiceChat;
using Xunit;

// Guards the contract that localization relies on: visible text may be translated, identifiers
// (config keys, row keys, category names) may not. If one of these fails, a translation would
// silently reset users' saved settings or break a lookup.
public sealed class LocalizationIdentifierTests
{
    private static readonly BepInPlugin Metadata = new("test.perfectcomms.localization", "test", "1.0.0");

    // The exact keys already persisted in existing users' BepInEx config files.
    private static readonly string[] PersistedKeybindIds =
    {
        "Mute / Unmute Mic",
        "Hold To Mute",
        "Team Radio (Hold)",
        "Cycle Team Radio Channel",
        "Push To Talk (Hold)",
        "Toggle Open Mic / Push To Talk",
        "Toggle Speaker",
        "Player Volumes",
        "Alive Louder / Dead Quieter (Hold)",
        "Alive Quieter / Dead Louder (Hold)",
        "Refresh Voice Connection",
        "Open Voice Menu",
        "Open Host Voice Settings",
    };

    [Fact]
    public void KeybindIdsAreTheKeysAlreadyPersistedInConfigFiles()
    {
        WithTemporaryConfig(config =>
        {
            VoiceChatKeybinds.Initialize(config);
            var ids = new[]
            {
                VoiceChatKeybinds.ToggleMute.Id,
                VoiceChatKeybinds.PushToMute.Id,
                VoiceChatKeybinds.TeamRadio.Id,
                VoiceChatKeybinds.CycleTeamRadioChannel.Id,
                VoiceChatKeybinds.PushToTalk.Id,
                VoiceChatKeybinds.ToggleMicMode.Id,
                VoiceChatKeybinds.ToggleSpeaker.Id,
                VoiceChatKeybinds.VolumeMenu.Id,
                VoiceChatKeybinds.AliveLouderDeadQuieter.Id,
                VoiceChatKeybinds.AliveQuieterDeadLouder.Id,
                VoiceChatKeybinds.LocalVoiceRefresh.Id,
                VoiceChatKeybinds.OpenVoiceMenu.Id,
                VoiceChatKeybinds.OpenHostVoiceSettings.Id,
            };
            Assert.Equal(PersistedKeybindIds, ids);

            // Every id must actually exist as a config entry in the "Keybinds" section.
            foreach (var id in PersistedKeybindIds)
                Assert.True(config.ContainsKey(new ConfigDefinition("Keybinds", id)), id);
        });
    }

    [Fact]
    public void KeybindIdDoesNotFollowTheDisplayName()
    {
        WithTemporaryConfig(config =>
        {
            var legacy = new VoiceKeybind(config, "Keybinds", "Clearer Name", "Original Key",
                UnityEngine.KeyCode.F5, "help");
            Assert.Equal("Original Key", legacy.Id);
            Assert.Equal("Clearer Name", legacy.DisplayName);
        });
    }

    [Fact]
    public void ConfigBackedOptionHoldersExposeTheirConfigKeyAsId()
    {
        WithTemporaryConfig(config =>
        {
            var toggle = new ToggleHolder(config, "Host.Test", "Flag", "Any visible text", true);
            var number = new NumberHolder(config, "Host.Test", "Range", "Another text", 1f, 0f, 2f, 0.1f, "0.0");
            var list = new EnumHolder(config, "Host.Test", "Mode", "Mode text", 0, typeof(VoiceFalloffMode),
                new[] { "A", "B" });
            Assert.Equal("Host.Test.Flag", toggle.Id);
            Assert.Equal("Host.Test.Range", number.Id);
            Assert.Equal("Host.Test.Mode", list.Id);
            Assert.NotEqual(toggle.Label, toggle.Id);
        });
    }

    [Fact]
    public void ThirdPartyOptionHoldersUseTheComposedKeyAsId()
    {
        Assert.Equal("my.mod.opt", new ModToggleHolder("my.mod.opt", "Label").Id);
        Assert.Equal("my.mod.enum", new ModEnumHolder("my.mod.enum", "Label", new[] { "x" }).Id);
        Assert.Equal("my.mod.num", new ModNumberHolder("my.mod.num", "Label", 0f, 1f, 0.1f, "0.0", "").Id);
    }

    [Theory]
    [InlineData("Mic Volume")]
    [InlineData("Mic Sensitivity")]
    [InlineData("Speaker Volume")]
    [InlineData("Meeting Spatial Audio")]
    [InlineData("Mic Mode")]
    [InlineData("Noise Gate")]
    [InlineData("Noise Suppression")]
    [InlineData("Stronger Noise Suppression")]
    [InlineData("Echo Cancellation")]
    public void SettingsRowHelpIsLookedUpByStableKey(string key)
    {
        Assert.False(string.IsNullOrWhiteSpace(VoiceSettingsPanel.RowHelp(key)));
    }

    [Fact]
    public void SettingsRowHelpIgnoresUnknownKeys()
    {
        Assert.Null(VoiceSettingsPanel.RowHelp("Volumen del micrófono"));
        Assert.Null(VoiceSettingsPanel.RowHelp(""));
    }

    [Fact]
    public void CategoryCaptionsKeepTheirCurrentText()
    {
        Assert.Equal("Audio", VoiceSettingsCategoryText.Caption(VoiceSettingsCategory.Audio));
        Assert.Equal("Devices", VoiceSettingsCategoryText.Caption(VoiceSettingsCategory.Devices));
        Assert.Equal("Keybinds", VoiceSettingsCategoryText.Caption(VoiceSettingsCategory.Keybinds));
        Assert.Equal("HUD", VoiceSettingsCategoryText.Caption(VoiceSettingsCategory.Hud));
        Assert.Equal("Advanced", VoiceSettingsCategoryText.Caption(VoiceSettingsCategory.Advanced));
    }

    private static void WithTemporaryConfig(Action<ConfigFile> test)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"PerfectComms-LocTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            test(new ConfigFile(Path.Combine(directory, "loc.cfg"), saveOnInit: false, Metadata));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
