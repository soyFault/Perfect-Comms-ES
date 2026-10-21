#if ANDROID
namespace VoiceChatPlugin.VoiceChat;

/// <summary>
/// Android-only settings layout and user-facing touch guidance. Keeping this policy out of the
/// shared panels prevents desktop-only controls from leaking into the mobile experience.
/// </summary>
internal static class AndroidVoiceUiPolicy
{
    internal static readonly string[] SettingsCategories =
        { "AUDIO", "DEVICES", "HUD", "ADVANCED" };

    internal static readonly VoiceSettingsCategory[] SettingsCategoryOrder =
    {
        VoiceSettingsCategory.Audio,
        VoiceSettingsCategory.Devices,
        VoiceSettingsCategory.Hud,
        VoiceSettingsCategory.Advanced,
    };

    // Properties (not const): the text is resolved when the UI is built, in the game language.
    internal static string MicrophoneControlHelp =>
        Loc.T("android.help_mic_control", "Open Mic: tap to mute. Push To Talk: hold the mic to speak.");

    internal static string ControlsJourneyHelp =>
        Loc.T("android.help_controls_journey", "Talk mode, touch controls, and startup behavior.");

    internal static string TeamRadioControlHelp =>
        Loc.T("android.help_team_radio_control", "When the radio button appears, tap to change channel or hold to transmit.");

    internal static string MicModeHelp(bool pushToTalk)
        => pushToTalk
            ? Loc.T("android.help_mic_mode_ptt", "Hold the microphone button while you speak.\nRelease it to stop transmitting.")
            : Loc.T("android.help_mic_mode_open", "Open Mic sends speech automatically.\nTap the microphone button to mute.");

    internal static string MicrophoneTooltipAction(bool pushToTalk, bool radioVisible)
    {
        string microphoneAction = pushToTalk
            ? Loc.T("android.tip_hold_mic", "Hold mic: talk")
            : Loc.T("android.tip_tap_mic", "Tap mic: mute");
        return radioVisible
            ? microphoneAction + "  |  " + Loc.T("android.tip_radio", "Radio: tap to change channel / hold to talk")
            : microphoneAction;
    }

    internal static string SpeakerTooltipAction =>
        Loc.T("android.tip_speaker", "Tap to deafen or undeafen. Deafening mutes playback and pauses microphone transmission.");
}
#endif
