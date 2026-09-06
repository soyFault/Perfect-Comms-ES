using System;
using BepInEx.Configuration;
using UnityEngine;

namespace VoiceChatPlugin.VoiceChat;

public static class VoiceChatKeybinds
{
    internal const string ToggleDeafenDisplayName = "Alternar ensordecimiento";
    internal const string ToggleDeafenHelpText =
        "Te ensordece o deja de ensordecerte. Ensordecerte silencia las voces y pausa la transmisión del micrófono.";

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
        ToggleMute = new VoiceKeybind(config, s, "Silenciar / activar micrófono", KeyCode.RightAlt,
            KeyCode.None, VoiceModifierMatch.Exact,
            helpText: "Alterna si tu micrófono transmite tu voz.");
        // Preserve the original persisted key so existing preview-build bindings survive the rename.
        PushToMute = new VoiceKeybind(
            config, s, "Pulsar para silenciar", "Hold To Mute", KeyCode.None,
            helpText: "Silencia tu micrófono mientras mantienes presionada la tecla; al soltarla, restaura su estado anterior.");
        TeamRadio = new VoiceKeybind(config, s, "Radio de equipo (mantener)", KeyCode.V,
            helpText: "Mientras mantengas la tecla, transmite por el canal privado de equipo seleccionado si tu rol y los ajustes del host lo permiten.");
        CycleTeamRadioChannel = new VoiceKeybind(config, s, "Cambiar canal de radio de equipo", KeyCode.G,
            helpText: "Alterna entre los canales de radio de equipo disponibles, incluidos los agregados por otros mods.");
        PushToTalk = new VoiceKeybind(config, s, "Pulsar para hablar (mantener)", KeyCode.C,
            helpText: "Mientras mantengas la tecla, transmite tu voz cuando el Modo del micrófono esté en Pulsar para hablar.");
        ToggleMicMode = new VoiceKeybind(config, s, "Alternar Micrófono abierto / Pulsar para hablar", KeyCode.None,
            helpText: "Alterna el modo del micrófono entre Micrófono abierto y Pulsar para hablar.");
        // Keep the pre-v4 persisted key so the clearer deafen label does not reset existing binds.
        ToggleSpeaker = new VoiceKeybind(
            config, s, ToggleDeafenDisplayName, "Toggle Speaker", KeyCode.RightControl,
            ToggleDeafenHelpText);
        VolumeMenu = new VoiceKeybind(
            config, s, "Volumen de jugadores", KeyCode.B, KeyCode.LeftShift, VoiceModifierMatch.EitherSide,
            helpText: "Abre el mezclador de volumen individual de los jugadores. Sus ajustes solo afectan lo que tú escuchas.");
        AliveLouderDeadQuieter = new VoiceKeybind(
            config, s, "Vivos más alto / Muertos más bajo (mantener)", KeyCode.None,
            helpText: "Mientras mantengas la tecla, aplica los niveles de volumen configurados para Vivos y Muertos. Al soltarla, ambos grupos vuelven al 100%. Si mantienes ambos atajos de mezcla, no se aplicará ninguno.");
        AliveQuieterDeadLouder = new VoiceKeybind(
            config, s, "Vivos más bajo / Muertos más alto (mantener)", KeyCode.None,
            helpText: "Mientras mantengas la tecla, aplica los niveles de volumen configurados por separado para Vivos y Muertos. Al soltarla, ambos grupos vuelven al 100%. Si mantienes ambos atajos de mezcla, no se aplicará ninguno.");
        LocalVoiceRefresh = new VoiceKeybind(config, s, "Reconectar chat de voz", KeyCode.F7,
            helpText: "Reconecta solo tu sesión de voz para solucionar problemas de audio. Tiene 10 segundos de recarga.");
        RemoveRetiredHostRefreshBindings(config, s);
        OpenVoiceMenu = new VoiceKeybind(config, s, "Abrir menú de voz", KeyCode.F10,
            helpText: "Abre o cierra este menú de ajustes de Perfect Comms.");
        OpenHostVoiceSettings = new VoiceKeybind(config, s, "Abrir ajustes de voz del host", KeyCode.F11,
            helpText: "Abre las reglas de voz del host para el lobby actual. No hace nada si no eres el host.");
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
