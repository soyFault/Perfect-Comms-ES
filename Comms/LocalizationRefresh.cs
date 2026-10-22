using System;

namespace VoiceChatPlugin.VoiceChat;

/// <summary>
/// When the player changes Among Us' language, text that was already built must follow: the
/// settings, host and volume panels, the setup wizard, the lobby browser and the menu chips cache
/// their widgets. New text (toasts, tooltips, rows built on demand) is already resolved lazily by
/// <see cref="Loc"/>, so this only has to rebuild what is cached.
/// </summary>
internal static class LocalizationRefresh
{
    private static bool _registered;

    internal static void Register()
    {
        if (_registered) return;
        _registered = true;
        Loc.LocaleChanged += Apply;
    }

    internal static void Apply()
    {
        Run("hud warnings", VoiceHudWarnings.Invalidate);
        Run("settings panel", VoiceSettingsPanel.RefreshLanguage);
        Run("host panel", HostSettingsPanel.RefreshLanguage);
        Run("volume menu", VoiceVolumeMenu.RefreshLanguage);
        Run("setup wizard", VoiceFirstRunSetup.RefreshLanguage);
        Run("lobby browser", VoiceLobbyBrowserUi.RefreshLanguage);
        Run("options chip", VoiceOptionsMenuEntry.RefreshLanguage);
        Run("host chip", VoiceHostMenuEntry.RefreshLanguage);
        VoiceDiagnostics.Log("loc.changed", $"locale={Loc.CurrentLocale}");
    }

    private static void Run(string what, Action refresh)
    {
        try { refresh(); }
        catch (Exception ex)
        {
            VoiceChatPluginMain.Logger.LogWarning($"[PC-UI] language refresh failed ({what}): {ex.Message}");
        }
    }
}
