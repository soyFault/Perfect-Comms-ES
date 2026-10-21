namespace VoiceChatPlugin.VoiceChat;

internal enum VoiceSettingsCategory
{
    Audio,
    Devices,
    Keybinds,
    Hud,
    Advanced,
}

internal static class VoiceSettingsCategoryText
{
    /// <summary>
    /// Text shown for a category in the rail. The enum name doubles as the icon resource name
    /// (<see cref="VoiceUiBranding.CategoryIcon"/>), so it must never be shown through
    /// <c>ToString()</c> once captions are localized.
    /// </summary>
    internal static string Caption(VoiceSettingsCategory category)
        => category == VoiceSettingsCategory.Hud ? "HUD" : category.ToString();
}
