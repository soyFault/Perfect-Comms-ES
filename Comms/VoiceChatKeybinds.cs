using System;
using BepInEx.Configuration;
using UnityEngine;

namespace VoiceChatPlugin.VoiceChat;

public static class VoiceChatKeybinds
{
    internal const string ToggleDeafenDisplayName = "Toggle Deafen";
    internal const string ToggleDeafenHelpText =
        "Deafens or undeafens Perfect Comms. Deafening mutes voice playback and pauses microphone transmission.";

    private static VoiceKeybind[] _allBindings = Array.Empty<VoiceKeybind>();
    internal static VoiceKeybind[] AllBindings => _allBindings;

    public static VoiceKeybind ToggleMute { get; private set; } = null!;
    public static VoiceKeybind PushToMute { get; private set; } = null!;
    public static VoiceKeybind TeamRadio { get; private set; } = null!;
    public static VoiceKeybind CycleTeamRadioChannel { get; private set; } = null!;
    public static VoiceKeybind ImpostorRadio => TeamRadio;
    public static VoiceKeybind PushToTalk { get; private set; } = null!;
    public static VoiceKeybind ToggleMicMode { get; private set; } = null!;
    public static VoiceKeybind ToggleSpeaker { get; private set; } = null!;
    public static VoiceKeybind VolumeMenu { get; private set; } = null!;
    public static VoiceKeybind AliveLouderDeadQuieter { get; private set; } = null!;
    public static VoiceKeybind AliveQuieterDeadLouder { get; private set; } = null!;
    public static VoiceKeybind LocalVoiceRefresh { get; private set; } = null!;
    public static VoiceKeybind OpenVoiceMenu { get; private set; } = null!;
    public static VoiceKeybind OpenHostVoiceSettings { get; private set; } = null!;

    public static void Initialize(ConfigFile config)
    {
        const string s = "Keybinds";
        ToggleMute = VoiceKeybind.Localized(config, s, Loc.L("keybind.mute", "Mute / Unmute Mic"),
            Loc.L("keybind.mute_help", "Toggles whether your microphone sends voice."),
            KeyCode.RightAlt, KeyCode.None, explicitMatch: VoiceModifierMatch.Exact);
        // Preserve the original persisted key so existing preview-build bindings survive the rename.
        PushToMute = VoiceKeybind.Localized(config, s, Loc.L("keybind.push_to_mute", "Push To Mute"),
            Loc.L("keybind.push_to_mute_help", "Mutes your microphone only while the key is held; releasing restores your previous mute state."),
            KeyCode.None, configKey: "Hold To Mute");
        TeamRadio = VoiceKeybind.Localized(config, s, Loc.L("keybind.team_radio", "Team Radio (Hold)"),
            Loc.L("keybind.team_radio_help", "While held, transmits over your selected private team channel when your role and the host settings allow it."),
            KeyCode.V);
        CycleTeamRadioChannel = VoiceKeybind.Localized(config, s, Loc.L("keybind.cycle_team_radio", "Cycle Team Radio Channel"),
            Loc.L("keybind.cycle_team_radio_help", "Cycles through the built-in and mod-registered team-radio channels available to you."),
            KeyCode.G);
        PushToTalk = VoiceKeybind.Localized(config, s, Loc.L("keybind.push_to_talk", "Push To Talk (Hold)"),
            Loc.L("keybind.push_to_talk_help", "While held, transmits your microphone when Mic Mode is set to Push To Talk."),
            KeyCode.C);
        ToggleMicMode = VoiceKeybind.Localized(config, s, Loc.L("keybind.toggle_mic_mode", "Toggle Open Mic / Push To Talk"),
            Loc.L("keybind.toggle_mic_mode_help", "Switches your microphone between Open Mic and Push To Talk mode."),
            KeyCode.None);
        // Keep the pre-v4 persisted key so the clearer deafen label does not reset existing binds.
        ToggleSpeaker = VoiceKeybind.Localized(
            config, s, Loc.L("keybind.toggle_deafen", "Toggle Deafen"),
            Loc.L("keybind.toggle_deafen_help", "Deafens or undeafens Perfect Comms. Deafening mutes voice playback and pauses microphone transmission."),
            KeyCode.RightControl, configKey: "Toggle Speaker");
        VolumeMenu = VoiceKeybind.Localized(config, s, Loc.L("keybind.player_volumes", "Player Volumes"),
            Loc.L("keybind.player_volumes_help", "Opens the local per-player volume mixer. Its adjustments affect only what you hear."),
            KeyCode.B, KeyCode.LeftShift, explicitMatch: VoiceModifierMatch.EitherSide);
        AliveLouderDeadQuieter = VoiceKeybind.Localized(config, s, Loc.L("keybind.alive_louder", "Alive Louder / Dead Quieter (Hold)"),
            Loc.L("keybind.alive_louder_help", "While held, applies this binding's configured Alive and Dead volume levels. Releasing restores both groups to 100%. If both mix bindings are held, neither profile is applied."),
            KeyCode.None);
        AliveQuieterDeadLouder = VoiceKeybind.Localized(config, s, Loc.L("keybind.alive_quieter", "Alive Quieter / Dead Louder (Hold)"),
            Loc.L("keybind.alive_quieter_help", "While held, applies this binding's separately configured Alive and Dead volume levels. Releasing restores both groups to 100%. If both mix bindings are held, neither profile is applied."),
            KeyCode.None);
        LocalVoiceRefresh = VoiceKeybind.Localized(config, s, Loc.L("keybind.refresh_voice", "Refresh Voice Connection"),
            Loc.L("keybind.refresh_voice_help", "Rejoins only your local voice session to repair stuck audio. It has a 10-second cooldown."),
            KeyCode.F7);
        RemoveRetiredHostRefreshBindings(config, s);
        OpenVoiceMenu = VoiceKeybind.Localized(config, s, Loc.L("keybind.open_voice_menu", "Open Voice Menu"),
            Loc.L("keybind.open_voice_menu_help", "Opens or closes this local Perfect Comms settings menu."),
            KeyCode.F10);
        OpenHostVoiceSettings = VoiceKeybind.Localized(config, s, Loc.L("keybind.open_host_settings", "Open Host Voice Settings"),
            Loc.L("keybind.open_host_settings_help", "Opens the host-only voice rules for the current lobby. It does nothing if you are not the host."),
            KeyCode.F11);
        _allBindings = new[]
        {
            ToggleMute,
            PushToMute,
            TeamRadio,
            CycleTeamRadioChannel,
            PushToTalk,
            ToggleMicMode,
            ToggleSpeaker,
            VolumeMenu,
            AliveLouderDeadQuieter,
            AliveQuieterDeadLouder,
            LocalVoiceRefresh,
            OpenVoiceMenu,
            OpenHostVoiceSettings,
        };

        var rightModifierDefaultsMigrated = config.Bind(s, "RightModifierDefaultsMigrated", false,
            new ConfigDescription("Internal one-time flag: changed Mute and deafen to standalone right-side modifiers. Do not edit."));
        if (!rightModifierDefaultsMigrated.Value)
        {
            ToggleMute.SetBinding(KeyCode.RightAlt, KeyCode.None, VoiceModifierMatch.Exact);
            ToggleSpeaker.SetBinding(KeyCode.RightControl, KeyCode.None, VoiceModifierMatch.Exact);
            rightModifierDefaultsMigrated.Value = true;
        }

        var playerVolumeShiftDefaultMigrated = config.Bind(s, "PlayerVolumeShiftDefaultMigrated", false,
            new ConfigDescription("Internal one-time flag: changed the untouched Player Volumes default from B to Shift+B. Do not edit."));
        if (!playerVolumeShiftDefaultMigrated.Value)
        {
            if (ShouldMigratePlayerVolumeDefault(VolumeMenu.Value, VolumeMenu.Modifier))
                VolumeMenu.SetModifier(KeyCode.LeftShift, VoiceModifierMatch.EitherSide);
            playerVolumeShiftDefaultMigrated.Value = true;
        }
    }

    internal static bool ShouldMigratePlayerVolumeDefault(KeyCode key, KeyCode modifier)
        => key == KeyCode.B && modifier == KeyCode.None;

    private static void RemoveRetiredHostRefreshBindings(ConfigFile config, string section)
    {
        foreach (var key in new[] { "Refresh Voice Connections (Host)", "Refresh Host Voice Connection" })
        {
            var primary = config.Bind(section, key, KeyCode.None,
                new ConfigDescription("Retired host-wide voice refresh binding."));
            PerfectCommsConfigStore.Remove(config, primary.Definition);

            var modifier = config.Bind(section, key + " Modifier", KeyCode.None,
                new ConfigDescription("Retired host-wide voice refresh modifier."));
            PerfectCommsConfigStore.Remove(config, modifier.Definition);

            var modifierMatch = config.Bind(section, key + " Modifier Match", VoiceModifierMatch.Exact,
                new ConfigDescription("Retired host-wide voice refresh modifier matching mode."));
            PerfectCommsConfigStore.Remove(config, modifierMatch.Definition);
        }
    }

    internal static bool HasConfiguredChordUsingModifier(
        KeyCode modifier,
        VoiceKeybind except)
    {
        foreach (var bind in _allBindings)
        {
            if (ReferenceEquals(bind, except) || bind.Value == KeyCode.None
                || bind.Modifier == KeyCode.None) continue;
            if (bind.MatchesModifierKey(modifier)) return true;
        }
        return false;
    }

    internal static bool HasActiveChordUsingModifier(
        KeyCode modifier,
        VoiceKeybind except)
    {
        foreach (var bind in _allBindings)
        {
            if (ReferenceEquals(bind, except) || bind.Value == KeyCode.None
                || bind.Modifier == KeyCode.None) continue;
            if (bind.MatchesModifierKey(modifier) && bind.IsPrimaryHeldRaw()) return true;
        }
        return false;
    }

    internal static bool HasActiveChordForPrimary(
        KeyCode primary,
        VoiceKeybind except)
    {
        foreach (var bind in _allBindings)
        {
            if (ReferenceEquals(bind, except) || bind.Value != primary
                || bind.Modifier == KeyCode.None) continue;
            if (bind.IsModifierSatisfied()) return true;
        }
        return false;
    }
}
