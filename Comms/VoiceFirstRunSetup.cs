using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace VoiceChatPlugin.VoiceChat;

/// <summary>
/// Revision-gated, staged first-run setup. Fresh installs and upgrades use the exact same clean
/// recommended draft; only Finish applies it, while Use existing settings preserves local values.
/// </summary>
internal static class VoiceFirstRunSetup
{
    private const int WelcomePage = 0;
    private const int AudioPage = 1;
    private const int ControlsPage = 2;
    private const int HudPage = 3;
    private const int ReadyPage = 4;
    private const int PageCount = 5;

    private const float PanelWidth = 1040f;
    private const float PanelHeight = 680f;
    private const float FooterY = -496f;
    private const float ContentTopY = -130f;
    private const float ShellClipInset = 16f;
    private const float ContentSideInset = 12f;

    private static readonly Color32 SetupSurface = new(18, 24, 33, 255);
    private static readonly Color32 SetupSurfaceRaised = new(23, 31, 42, 255);
    private static readonly Color32 SetupSurfaceHover = new(29, 39, 52, 255);
    private static readonly Color32 SetupBorder = new(69, 86, 108, 105);
    private static readonly Color32 SetupTextSecondary = new(194, 204, 218, 255);
    private static readonly Color32 SetupTextTertiary = new(156, 173, 196, 255);
    private static readonly Color32 SetupSuccess = new(77, 225, 141, 255);
    private static readonly Color32 SetupWarning = new(245, 184, 68, 255);

    // Property, not a static field: resolved when the page header is built, in the current language.
    private static string[] StepNames => new[]
    {
        Loc.T("setup.step_welcome", "Welcome"), Loc.T("setup.step_audio", "Audio"), Loc.T("setup.step_controls", "Controls"), Loc.T("setup.step_hud", "HUD"), Loc.T("setup.step_review", "Review"),
    };

    private static VoiceUiKit.PanelShell? _shell;
    private static RectTransform? _chromeRoot;
    private static RectTransform? _hudPreviewHost;
    private static RectTransform? _pageRoot;
    private static CanvasGroup? _pageGroup;
    private static RectTransform? _closePrompt;
    private static RectTransform? _closePromptCard;
    private static CanvasGroup? _closePromptGroup;
    private static FirstRunSetupDraft? _draft;
    private static FirstRunAudioPreview? _audioPreview;
    private static SpeakingBarLivePreview? _hudPreview;
    private static VoiceUiKit.LiveLevelMeter? _micLevelMeter;
    private static readonly List<VoiceUiKit.Row> Rows = new();
    private static readonly List<SetupButton> Buttons = new();
    private static readonly List<SetupButton> PromptButtons = new();
    private static readonly List<HudPresetCard> HudCards = new();
    private static readonly List<CelebrationDot> CelebrationDots = new();

    private static TextMeshProUGUI? _outputStatus;
    private static TextMeshProUGUI? _micModeHelpText;
    private static TextMeshProUGUI? _hudDescriptionText;
    private static SetupButton? _micTestButton;
    private static SetupButton? _speakerTestButton;
    private static TextMeshProUGUI? _finishError;
    private static TextMeshProUGUI? _promptMessage;
    private static VoiceUiKit.Row? _pushToTalkRow;
    private static CanvasGroup? _pushToTalkGroup;
    private static bool _builtHudSpeakingBarHidden;

    private static int _page;
    private static int _inputLockedUntilFrame;
    private static int _micListVersion;
    private static int _speakerListVersion;
    private static SpeakingBarPreviewSettings? _hoveredHudSettings;
    private static string? _hoveredHudDescription;
    private static string _verifiedMicrophoneDevice = string.Empty;
    private static string _verifiedSpeakerDevice = string.Empty;
    private static float _appear;
    private static float _pageAppear;
    private static float _hudReveal;
    private static float _promptAppear;
    private static bool _microphoneSignalDetected;
    private static bool _outputTestCompleted;
    private static bool _shown;
    private static bool _completed;
    private static bool _automaticAttemptedThisSession;

    internal static bool IsOpen => _shown && _shell != null && _shell.Root != null;
    private static float ContentWidth => Mathf.Max(1f,
        (_shell?.PaneWidth ?? PanelWidth) - ShellClipInset - ContentSideInset * 2f);

    internal static bool ShouldDeferMainMenuPopups
    {
        get
        {
            if (IsOpen) return true;
            return !_automaticAttemptedThisSession &&
                   VoiceSettings.Instance?.NeedsFirstRunSetup == true;
        }
    }

    internal static bool TryShowAutomatic()
    {
        if (IsOpen) return true;
        var settings = VoiceSettings.Instance;
        if (settings == null || !settings.NeedsFirstRunSetup || _automaticAttemptedThisSession)
            return false;
        if (VoiceSettingsPanel.IsOpen || HostSettingsPanel.IsOpen || VoiceVolumeMenu.IsOpen)
            return false;

        bool shown = Show(settings, manual: false);
        if (shown) _automaticAttemptedThisSession = true;
        return shown;
    }

    internal static void ShowManual()
    {
        var settings = VoiceSettings.Instance;
        if (settings == null) return;

        VoiceSettingsPanel.ForceClose();
        HostSettingsPanel.ForceClose();
        VoiceVolumeMenu.ForceClose();
        if (Show(settings, manual: true))
            _automaticAttemptedThisSession = true;
    }

    /// <summary>
    /// Session/scene boundaries cannot leave a persistent setup preview holding platform audio.
    /// This bypasses the interactive close confirmation because voice-room privacy and capture
    /// ownership take precedence once gameplay is starting or ending.
    /// </summary>
    internal static void ForceClose()
    {
        if (!_shown && _shell == null && _audioPreview == null && _hudPreview == null) return;
        CloseInternal(destroy: true);
    }

    private static bool Show(VoiceChatLocalSettings settings, bool manual)
    {
        try
        {
            if (IsOpen) CloseInternal(destroy: true);
            VoiceUiKit.EnsureCanvas();
            VoiceUiKit.EnsureDriver();
            VoiceChatLocalSettings.MaybeRefreshDeviceLists(resolveSavedIndices: false);

            // Automatic first-run setup starts from the recommended v4 baseline. A manual rerun
            // is an editor for the user's current configuration and must not silently reset every
            // page to defaults before they have chosen a change.
            _draft = manual
                ? FirstRunSetupDraft.CaptureExisting(settings)
                : FirstRunSetupDraft.CreateFresh(settings);
            _completed = false;
            _page = WelcomePage;
            _appear = 0f;
            _pageAppear = 0f;
            _hudReveal = 0f;
            _promptAppear = 0f;
            _microphoneSignalDetected = false;
            _outputTestCompleted = false;
            _verifiedMicrophoneDevice = string.Empty;
            _verifiedSpeakerDevice = string.Empty;
            _shown = true;
            VoiceChatPatches.ReleaseHeldTransmitInputs();
            _inputLockedUntilFrame = Time.frameCount + 2;

            _shell = new VoiceUiKit.PanelShell(
                "VC_FirstRunSetup",
                Loc.T("setup.perfect_comms_setup", "Perfect Comms Setup"),
                PanelWidth,
                PanelHeight,
                RequestClose,
                rail: false,
                backdrop: true,
                guided: true);
            EnsureHudPreview();
            BuildPage();
            VoiceUiKit.RaiseAbove(_shell.RootRect);
            return true;
        }
        catch (Exception ex)
        {
            VoiceChatPlugin.VoiceChatPluginMain.Logger.LogError(
                "[PC-SETUP] Could not open first-run setup: " + ex);
            CloseInternal(destroy: true);
            return false;
        }
    }

    /// <summary>
    /// Re-renders the current page in the new language, keeping the draft and the page the player is on.
    /// </summary>
    internal static void RefreshLanguage()
    {
        if (!_shown || _shell == null || _shell.Root == null || _draft == null) return;
        bool prompt = _closePrompt != null;
        _shell.SetTitle(Loc.T("setup.perfect_comms_setup", "Perfect Comms Setup"));
        // The embedded HUD preview builds its title and "LIVE" badge once; recreate it too.
        if (_hudPreview != null)
        {
            try { _hudPreview.Dispose(); } catch { }
            _hudPreview = null;
        }
        if (_hudPreviewHost != null)
        {
            Object.Destroy(_hudPreviewHost.gameObject);
            _hudPreviewHost = null;
        }
        EnsureHudPreview();
        BuildPage();
        if (prompt) { HideClosePrompt(); ShowClosePrompt(); }
    }

    internal static void Tick()
    {
        if (!_shown || _shell == null) return;
        if (_shell.Root == null)
        {
            CloseInternal(destroy: false);
            return;
        }

        float dt = Mathf.Max(0f, Time.unscaledDeltaTime);
        _appear = Mathf.MoveTowards(_appear, 1f, dt / 0.22f);
        _pageAppear = Mathf.MoveTowards(_pageAppear, 1f, dt / 0.20f);
        _hudReveal = Mathf.MoveTowards(
            _hudReveal,
            !_completed && _page == HudPage && _draft?.HideSpeakingBar != true ? 1f : 0f,
            dt / 0.28f);

        // Keep local audio cleanup and Android tone completion running even while the close
        // confirmation owns input.
        _audioPreview?.Tick();
        ApplyPresentation();
        if (_closePrompt == null) _shell.TickHeader();
        if (!_shown || _shell == null) return;

        if (_closePrompt != null)
        {
            _promptAppear = Mathf.MoveTowards(_promptAppear, 1f, dt / 0.16f);
            if (_closePromptGroup != null) _closePromptGroup.alpha = Smooth(_promptAppear);
            if (_closePromptCard != null)
            {
                float promptScale = Mathf.Lerp(0.96f, 1f, Smooth(_promptAppear));
                _closePromptCard.localScale = new Vector3(promptScale, promptScale, 1f);
            }
            if (Input.GetKeyDown(KeyCode.Escape)) HideClosePrompt();
            TickButtons(PromptButtons, dt);
            TickHudPreview(dt, render: false);
            return;
        }

        if (!VoiceUiKit.RebindRow.IsCapturing && Input.GetKeyDown(KeyCode.Escape))
        {
            RequestClose();
            return;
        }

        if (_pageGroup != null && _pageRoot != null)
        {
            float eased = Smooth(_pageAppear);
            _pageGroup.alpha = eased;
            _pageRoot.anchoredPosition = new Vector2((1f - eased) * 14f, 0f);
        }

        if (VoiceUiKit.RebindRow.IsCapturing && Input.GetMouseButtonDown(0) &&
            PointerOverAnyButton(Buttons))
            VoiceUiKit.RebindRow.CancelCaptureForExternalPointer();

        HandleRows();
        for (int i = 0; i < Rows.Count; i++) Rows[i].Tick(dt);
        TickButtons(Buttons, dt);

        if (_page == AudioPage) TickAudioPage(dt);
        else if (_page == ControlsPage) TickControlsPage();
        else if (_page == HudPage) TickHudCards(dt);
        else if (_completed) TickCelebration(dt);

        TickHudPreview(dt, render: _page == HudPage && !_completed);
    }

    internal static void CloseForSceneChange()
    {
        if (!IsOpen) return;
        CloseInternal(destroy: true);
    }

    private static void BuildPage()
    {
        if (_shell == null || _draft == null) return;

        VoiceUiKit.RebindRow.CancelCapture();
        Rows.Clear();
        Buttons.Clear();
        HudCards.Clear();
        CelebrationDots.Clear();
        _outputStatus = null;
        _micModeHelpText = null;
        _hudDescriptionText = null;
        _micLevelMeter = null;
        _micTestButton = null;
        _speakerTestButton = null;
        _finishError = null;
        _pushToTalkRow = null;
        _pushToTalkGroup = null;
        _hoveredHudSettings = null;
        _hoveredHudDescription = null;

        if (_chromeRoot != null) Object.Destroy(_chromeRoot.gameObject);
        _chromeRoot = VoiceUiKit.Rect("SetupChrome", _shell.PaneRoot);
        _chromeRoot.Anchor(new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
        _chromeRoot.sizeDelta = new Vector2(-ContentSideInset * 2f, 0f);
        _chromeRoot.anchoredPosition = Vector2.zero;

        _pageRoot = VoiceUiKit.Rect("SetupPage", _chromeRoot);
        _pageRoot.Anchor(new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
        _pageRoot.sizeDelta = Vector2.zero;
        _pageRoot.anchoredPosition = new Vector2(14f, 0f);
        _pageGroup = _pageRoot.gameObject.AddComponent<CanvasGroup>();
        _pageGroup.alpha = 0f;
        _pageAppear = 0f;
        _inputLockedUntilFrame = Time.frameCount + 1;

        if (_completed)
        {
            BuildCompletedPage();
            return;
        }

        BuildStepProgress();
        switch (_page)
        {
            case WelcomePage: BuildWelcomePage(); break;
            case AudioPage: BuildAudioPage(); break;
            case ControlsPage: BuildControlsPage(); break;
            case HudPage: BuildHudPage(); break;
            default: BuildReadyPage(); break;
        }
        BuildFooter();
    }

    private static void BuildStepProgress()
    {
        if (_chromeRoot == null || _shell == null) return;
        AddText(_chromeRoot, Loc.T("setup.step_progress", "Step {0} of {1}", _page + 1, PageCount), 17f, VoiceUiKit.Accent,
            TextAlignmentOptions.Left, FontStyles.Bold,
            new Vector2(2f, -5f), new Vector2(150f, 24f));
        AddText(_chromeRoot, StepNames[_page], 17f, SetupTextSecondary,
            TextAlignmentOptions.Right, FontStyles.Bold,
            new Vector2(ContentWidth - 180f, -5f), new Vector2(178f, 24f));

        const float gap = 8f;
        float segment = (ContentWidth - gap * (PageCount - 1)) / PageCount;
        for (int i = 0; i < PageCount; i++)
        {
            bool done = i < _page;
            bool current = i == _page;
            Color32 color = current
                ? VoiceUiKit.Accent
                : done ? new Color32(34, 211, 238, 135) : new Color32(54, 66, 84, 210);
            var segmentImage = VoiceUiKit.Panel("StepSegment", _chromeRoot, color,
                rounded: true, soft: true);
            PlaceTopLeft(segmentImage.rectTransform, i * (segment + gap), -35f, segment, 5f);
        }
    }

    private static void BuildWelcomePage()
    {
        if (_pageRoot == null || _shell == null) return;
        const float leftW = 568f;
        const float gap = 26f;
        float rightW = ContentWidth - leftW - gap;

        AddText(_pageRoot, Loc.T("setup.welcome_eyebrow", "Welcome"), 17f, VoiceUiKit.Accent,
            TextAlignmentOptions.Left, FontStyles.Bold,
            new Vector2(4f, -78f), new Vector2(leftW, 26f));
        AddText(_pageRoot, Loc.T("setup.set_up_perfect_comms", "Set up Perfect Comms."), 40f,
            VoiceUiKit.TextBright, TextAlignmentOptions.Left, FontStyles.Bold,
            new Vector2(4f, -111f), new Vector2(leftW, 58f));
        AddText(_pageRoot,
            Loc.T("setup.choose_your_audio_devices_controls_and_hud_everyone", "Choose your audio devices, controls, and HUD. Everyone starts from the same clean recommended setup."),
            20f, SetupTextSecondary, TextAlignmentOptions.TopLeft, FontStyles.Normal,
            new Vector2(4f, -181f), new Vector2(leftW - 18f, 92f), wrap: true);

        var recommended = BuildCard(_pageRoot, "RecommendedStart", 4f, -306f, leftW - 22f, 92f);
        BuildCheckBadge(recommended, new Vector2(24f, -24f), 38f, SetupSuccess);
        AddText(recommended, Loc.T("setup.recommended_defaults_are_ready", "Recommended defaults are ready"), 21f, VoiceUiKit.TextBright,
            TextAlignmentOptions.Left, FontStyles.Bold,
            new Vector2(76f, -18f), new Vector2(leftW - 122f, 28f));
        AddText(recommended, Loc.T("setup.adjust_what_matters_to_you_then_save_everything_toge", "Adjust what matters to you, then save everything together."), 17f,
            SetupTextSecondary, TextAlignmentOptions.Left, FontStyles.Normal,
            new Vector2(76f, -49f), new Vector2(leftW - 122f, 26f));

        var time = VoiceUiKit.Panel("SetupTime", _pageRoot,
            new Color32(34, 211, 238, 20), rounded: true, soft: true);
        PlaceTopLeft(time.rectTransform, 4f, -421f, 286f, 42f);
        AddText(time.rectTransform, Loc.T("setup.about_2_minutes", "About 2 minutes"), 17f, VoiceUiKit.Accent,
            TextAlignmentOptions.Center, FontStyles.Bold,
            Vector2.zero, new Vector2(286f, 42f));

        var journey = BuildCard(_pageRoot, "SetupJourney", leftW + gap, -82f, rightW, 368f);
        AddCardTitle(journey, Loc.T("setup.what_you_ll_set_up", "What you'll set up"), Loc.T("setup.three_focused_steps_then_one_review", "Three focused steps, then one review."));
        BuildJourneyRow(journey, -76f, "A", Loc.T("setup.step_audio", "Audio"), Loc.T("setup.choose_your_input_output_and_playback_level", "Choose your input, output, and playback level."));
#if ANDROID
        BuildJourneyRow(journey, -167f, "C", Loc.T("setup.controls", "Controls"), AndroidVoiceUiPolicy.ControlsJourneyHelp);
#else
        BuildJourneyRow(journey, -167f, "C", Loc.T("setup.controls", "Controls"), Loc.T("setup.controls_journey_help", "Talk mode, startup behavior, and shortcuts."));
#endif
        BuildJourneyRow(journey, -258f, "H", Loc.T("setup.step_hud", "HUD"), Loc.T("setup.choose_a_layout_using_the_live_lobby_preview", "Choose a layout using the live lobby preview."));
    }

    private static void BuildAudioPage()
    {
        if (_pageRoot == null || _shell == null || _draft == null) return;
        BuildPageHeading(Loc.T("setup.choose_your_audio", "Choose your audio"), Loc.T("setup.select_where_your_voice_comes_from_and_where_you_hea", "Select where your voice comes from and where you hear other players."));
        VoiceChatLocalSettings.MaybeRefreshDeviceLists(resolveSavedIndices: false);
        _micListVersion = VoiceChatLocalSettings.MicDeviceListVersion;
        _speakerListVersion = VoiceChatLocalSettings.SpkDeviceListVersion;
        _audioPreview ??= new FirstRunAudioPreview();

        const float cardW = 780f;
        float cardX = (ContentWidth - cardW) * 0.5f;
        const float cardH = 338f;
        var devices = BuildCard(_pageRoot, "DevicesCard", cardX, ContentTopY, cardW, cardH);

        AddCardTitle(devices, Loc.T("setup.input_output", "Input & output"), Loc.T("setup.use_system_defaults_or_choose_a_specific_device", "Use system defaults, or choose a specific device."), 344f);
        _micTestButton = AddButton(devices, Loc.T("setup.test_mic", "Test mic"), 146f, 44f,
            cardW - 324f, -11f, ButtonKind.Secondary,
            () =>
            {
                if (_audioPreview == null || _draft == null) return;
                if (_audioPreview.IsMicrophoneTestActive) _audioPreview.StopMicrophone();
                else _audioPreview.StartMicrophone(_draft);
            },
            () => _audioPreview?.IsSpeakerTestBusy != true);
        _micLevelMeter = new VoiceUiKit.LiveLevelMeter(
            devices, "MicLiveLevel", 146f, 6f, hideWhenInactive: true);
        PlaceTopLeft(_micLevelMeter.Root, cardW - 324f, -58f, 146f, 6f);
        _speakerTestButton = AddButton(devices, Loc.T("setup.test_output", "Test output"), 146f, 44f,
            cardW - 166f, -11f, ButtonKind.Secondary,
            () => _audioPreview?.PlayTestSound(_draft),
            () => _audioPreview is { IsSpeakerTestBusy: false, IsMicrophoneTestStarting: false });
        AddRow(new VoiceUiKit.StepperRow(
                DraftMicrophoneIndexForUi,
                i =>
                {
                    if (_audioPreview?.IsSpeakerTestBusy == true ||
                        i >= VoiceChatLocalSettings.MicDeviceNames.Length) return;
                    _draft?.SetMicrophoneIndex(i);
                    if (_draft == null) return;
                    _microphoneSignalDetected = false;
                    _verifiedMicrophoneDevice = string.Empty;
                    _audioPreview?.InvalidateMicrophoneVerification();
                    _audioPreview?.RestartForDeviceChange(_draft);
                },
                DraftMicrophoneCountForUi,
                DraftMicrophoneNameForUi,
                compactFullWidth: true)
            .Build(devices, Loc.T("setup.input_device", "Input device"), cardW, -66f, 86f,
                Loc.T("setup.the_microphone_perfect_comms_captures", "The microphone Perfect Comms captures.")));

#if WINDOWS
        AddRow(new VoiceUiKit.StepperRow(
                DraftSpeakerIndexForUi,
                i =>
                {
                    if (_audioPreview?.IsSpeakerTestBusy == true ||
                        i >= VoiceChatLocalSettings.SpkDeviceNames.Length) return;
                    _draft?.SetSpeakerIndex(i);
                    _outputTestCompleted = false;
                    _verifiedSpeakerDevice = string.Empty;
                    _audioPreview?.InvalidateOutputVerification();
                },
                DraftSpeakerCountForUi,
                DraftSpeakerNameForUi,
                compactFullWidth: true)
            .Build(devices, Loc.T("setup.output_device", "Output device"), cardW, -154f, 86f,
                Loc.T("setup.the_headphones_or_speakers_used_for_voice_playback", "The headphones or speakers used for voice playback.")));
#else
        var route = BuildInlineNotice(devices, 22f, -158f, cardW - 44f, 68f,
            Loc.T("setup.android_output_route", "Output follows your current Android audio route."), VoiceUiKit.Accent);
        route.gameObject.name = "AndroidOutputRoute";
#endif
        AddRow(new VoiceUiKit.SliderRow(
                () => _draft.MasterVolume,
                v => _draft.MasterVolume = v,
                0.1f, 2f, v => $"{Mathf.RoundToInt(v * 100f)}%",
                stacked: true)
            .Build(devices, Loc.T("setup.voice_playback_volume", "Voice playback volume"), cardW, -244f, 58f,
                Loc.T("setup.overall_volume_for_other_players", "Overall volume for other players.")));

        _outputStatus = AddText(devices, Loc.T("setup.mic_and_output_tests_are_optional", "Mic and output tests are optional"), 17f,
            SetupTextSecondary, TextAlignmentOptions.Center, FontStyles.Normal,
            new Vector2(22f, -304f), new Vector2(cardW - 44f, 26f), wrap: true);
    }

    private static void BuildControlsPage()
    {
        if (_pageRoot == null || _shell == null || _draft == null) return;
#if ANDROID
        BuildPageHeading(Loc.T("setup.choose_how_you_talk", "Choose how you talk"), Loc.T("setup.set_your_talk_mode_and_learn_the_in_game_touch_contr", "Set your talk mode and learn the in-game touch controls."));
#else
        BuildPageHeading(Loc.T("setup.choose_how_you_talk", "Choose how you talk"), Loc.T("setup.set_your_talk_mode_and_the_controls_you_will_use_in", "Set your talk mode and the controls you will use in a lobby."));
#endif

        const float gap = 18f;
        float leftW = 388f;
        float rightW = ContentWidth - leftW - gap;
        const float cardH = 338f;
        var talk = BuildCard(_pageRoot, "TalkCard", 0f, ContentTopY, leftW, cardH);
        var binds = BuildCard(_pageRoot, "BindingsCard", leftW + gap, ContentTopY, rightW, cardH);

        AddCardTitle(talk, Loc.T("setup.talk_behavior", "Talk behavior"), Loc.T("setup.these_can_be_changed_later_in_settings", "These can be changed later in Settings."));
        string microphoneModeHelp =
#if ANDROID
            Loc.T("setup.mic_mode_help_android", "Open Mic uses voice activation. Push To Talk sends only while you hold the microphone button.");
#else
            Loc.T("setup.mic_mode_help", "Open Mic uses voice activation. Push To Talk only sends while its key is held.");
#endif
        AddRow(new VoiceUiKit.StepperRow(
                () => (int)_draft.MicMode,
                i => _draft.MicMode = (VoiceMicMode)Mathf.Clamp(i, 0, 1),
                () => 2,
                i => i == 0 ? Loc.T("setup.open_mic", "Open Mic") : Loc.T("setup.push_to_talk_mode", "Push To Talk"),
                compactFullWidth: true)
            .Build(talk, Loc.T("setup.microphone_mode", "Microphone mode"), leftW, -63f, 84f,
                microphoneModeHelp));
        AddRow(new VoiceUiKit.ToggleRow(
                () => _draft.StartMuted,
                v => _draft.StartMuted = v)
            .Build(talk, Loc.T("setup.start_with_mic_muted", "Start with mic muted"), leftW, -153f, 54f,
                Loc.T("setup.join_each_voice_room_with_your_microphone_muted", "Join each voice room with your microphone muted.")));
        AddRow(new VoiceUiKit.ToggleRow(
                () => _draft.StartDeafened,
                v => _draft.StartDeafened = v)
            .Build(talk, Loc.T("setup.start_deafened", "Start deafened"), leftW, -207f, 54f,
                Loc.T("setup.join_each_voice_room_with_playback_muted_and_microph", "Join each voice room with playback muted and microphone transmission paused until you undeafen.")));

#if WINDOWS
        AddRow(new VoiceUiKit.ToggleRow(
                () => _draft.AllowKeybindsWhileChatOpen,
                value => _draft.AllowKeybindsWhileChatOpen = value)
            .Build(talk, Loc.T("setup.allow_keybinds_in_chat", "Allow keybinds in chat"), leftW, -261f, 54f,
                Loc.T("setup.keeps_perfect_comms_shortcuts_active_while_typing_pr", "Keeps Perfect Comms shortcuts active while typing. Printable shortcut keys can also type into chat.")));
#else
        var modeNotice = BuildInlineNotice(talk, 20f, -270f, leftW - 40f, 59f,
            "", VoiceUiKit.Accent);
        _micModeHelpText = modeNotice.GetComponentInChildren<TextMeshProUGUI>();
        if (_micModeHelpText != null)
        {
            _micModeHelpText.enableWordWrapping = true;
            _micModeHelpText.overflowMode = TextOverflowModes.Overflow;
        }
#endif

#if WINDOWS
        AddCardTitle(binds, Loc.T("setup.keyboard_shortcuts", "Keyboard shortcuts"), Loc.T("setup.select_a_shortcut_then_press_a_key_or_chord", "Select a shortcut, then press a key or chord."));
        AddBindingRow(binds, rightW, -62f, Loc.T("setup.mute_unmute_microphone", "Mute / unmute microphone"),
            () => _draft.ToggleMute, v => _draft.ToggleMute = v,
            null);
        AddBindingRow(binds, rightW, -116f, Loc.T("setup.push_to_mute", "Push to Mute"),
            () => _draft.PushToMute, v => _draft.PushToMute = v,
            Loc.T("setup.your_microphone_stays_muted_only_while_this_shortcut", "Your microphone stays muted only while this shortcut is held."));
        _pushToTalkRow = AddBindingRow(binds, rightW, -170f, Loc.T("setup.push_to_talk_hold", "Push to Talk (hold)"),
            () => _draft.PushToTalk, v => _draft.PushToTalk = v,
            Loc.T("setup.required_only_when_push_to_talk_mode_is_selected", "Required only when Push to Talk mode is selected."));
        _pushToTalkGroup = _pushToTalkRow.Root.gameObject.AddComponent<CanvasGroup>();
        AddBindingRow(binds, rightW, -224f, Loc.T("setup.deafen_undeafen", "Deafen / undeafen"),
            () => _draft.ToggleSpeaker, v => _draft.ToggleSpeaker = v,
            null);
        AddBindingRow(binds, rightW, -278f, Loc.T("setup.open_voice_menu", "Open voice menu"),
            () => _draft.OpenVoiceSettings, v => _draft.OpenVoiceSettings = v,
            null);
#else
        AddCardTitle(binds, Loc.T("setup.touch_controls", "In-game touch controls"), Loc.T("setup.touch_controls_sub", "Open Voice Settings later from the Among Us Options menu."));
        BuildJourneyRow(binds, -74f, "M", Loc.T("setup.touch_microphone", "Microphone"), AndroidVoiceUiPolicy.MicrophoneControlHelp);
        BuildJourneyRow(binds, -160f, "R", Loc.T("setup.touch_team_radio", "Team Radio"), AndroidVoiceUiPolicy.TeamRadioControlHelp);
        BuildJourneyRow(binds, -246f, "S", Loc.T("setup.touch_deafen", "Deafen"), Loc.T("setup.touch_deafen_help", "Tap the speaker button to mute playback and pause microphone transmission."));
#endif
    }

    private static void BuildHudPage()
    {
        if (_pageRoot == null || _shell == null || _draft == null) return;
        _hudDescriptionText = BuildPageHeading(
            Loc.T("setup.choose_your_hud", "Choose your HUD"),
            Loc.T("setup.choose_what_appears_in_game_then_preview_a_speaking", "Choose what appears in game, then preview a speaking-bar layout."));
        EnsureHudPreview();

        const float visibilityGap = 10f;
        const float visibilityCardH = 62f;
        float visibilityW = (ContentWidth - visibilityGap * 3f) / 4f;

        var controlsVisibility = BuildCard(
            _pageRoot, "ControlsVisibility", 0f, ContentTopY,
            visibilityW, visibilityCardH);
        AddRow(new VoiceUiKit.ToggleRow(
                () => _draft.HideVoiceControls,
                value => _draft.HideVoiceControls = value)
            .Build(controlsVisibility, Loc.T("setup.hide_controls", "Hide controls"), visibilityW, -4f, 54f,
                Loc.T("setup.hides_the_microphone_deafen_and_radio_controls_keybo", "Hides the microphone, deafen, and radio controls. Keyboard shortcuts still work."),
                stacked: true));

        var barVisibility = BuildCard(
            _pageRoot, "SpeakingBarVisibility", visibilityW + visibilityGap, ContentTopY,
            visibilityW, visibilityCardH);
        AddRow(new VoiceUiKit.ToggleRow(
                () => _draft.HideSpeakingBar,
                value => _draft.HideSpeakingBar = value)
            .Build(barVisibility, Loc.T("setup.hide_speaking_bar", "Hide speaking bar"), visibilityW, -4f, 54f,
                Loc.T("setup.hides_the_in_game_speaking_bar_and_its_layout_previe", "Hides the in-game speaking bar and its layout preview."),
                stacked: true));

        var meetingVisibility = BuildCard(
            _pageRoot, "MeetingOverlayVisibility",
            (visibilityW + visibilityGap) * 2f, ContentTopY,
            visibilityW, visibilityCardH);
        AddRow(new VoiceUiKit.ToggleRow(
                () => _draft.HideMeetingOverlay,
                value => _draft.HideMeetingOverlay = value)
            .Build(meetingVisibility, Loc.T("setup.hide_meeting_overlay", "Hide meeting overlay"), visibilityW, -4f, 54f,
                Loc.T("setup.hides_the_colored_speaking_glow_around_meeting_cards", "Hides the colored speaking glow around meeting cards."),
                stacked: true));

        var connectionVisibility = BuildCard(
            _pageRoot, "ConnectionStatusVisibility",
            (visibilityW + visibilityGap) * 3f, ContentTopY,
            visibilityW, visibilityCardH);
        AddRow(new VoiceUiKit.ToggleRow(
                () => _draft.HideConnectionStatus,
                value => _draft.HideConnectionStatus = value)
            .Build(connectionVisibility, Loc.T("setup.hide_connection_status", "Hide connection status"), visibilityW, -4f, 54f,
                Loc.T("setup.hides_lobby_connection_progress_and_active_retry_mes", "Hides lobby connection progress and active retry messages."),
                stacked: true));

        _builtHudSpeakingBarHidden = _draft.HideSpeakingBar;
        if (_draft.HideSpeakingBar)
        {
            BuildInlineNotice(
                _pageRoot,
                120f,
                ContentTopY - 164f,
                ContentWidth - 240f,
                92f,
                Loc.T("setup.the_speaking_bar_is_hidden_turn_off_hide_speaking_ba", "The speaking bar is hidden. Turn off Hide speaking bar to choose and preview a layout."),
                SetupTextSecondary);
            return;
        }

        float previewW = SpeakingBarLivePreview.EmbeddedCardSize.x;
        const float previewGap = 18f;
        const float layoutTopY = ContentTopY - 76f;
        float pickerW = ContentWidth - previewW - previewGap;

        AddText(_pageRoot, Loc.T("setup.most_common", "MOST COMMON"), 16f, VoiceUiKit.Accent,
            TextAlignmentOptions.Left, FontStyles.Bold,
            new Vector2(1f, layoutTopY), new Vector2(pickerW - 2f, 20f));

        const float commonGap = 8f;
        const float commonCardH = 44f;
        const float commonY = layoutTopY - 22f;
        float commonCardW = (pickerW - commonGap * 2f) / 3f;
        for (int i = 0; i < FirstRunHudPresets.CommonCount; i++)
        {
            var preset = FirstRunHudPresets.All[i];
            var previewSettings = FirstRunHudPresets.Apply(_draft.Hud, preset);
            var card = new HudPresetCard(i, preset.DisplayName, preset.DisplayDescription,
                previewSettings, original: false);
            card.Build(_pageRoot,
                i * (commonCardW + commonGap), commonY,
                commonCardW, commonCardH, prominent: true);
            HudCards.Add(card);
        }

        const float moreLabelY = commonY - commonCardH - 6f;
        AddText(_pageRoot, Loc.T("setup.more_layouts", "MORE LAYOUTS"), 16f, SetupTextSecondary,
            TextAlignmentOptions.Left, FontStyles.Bold,
            new Vector2(1f, moreLabelY), new Vector2(pickerW - 2f, 20f));

        const float moreGapX = 10f;
        const float moreGapY = 5f;
        const float moreCardH = 34f;
        const float moreY = moreLabelY - 22f;
        float moreCardW = (pickerW - moreGapX) * 0.5f;
        for (int i = FirstRunHudPresets.CommonCount; i < FirstRunHudPresets.All.Count; i++)
        {
            int visualIndex = i - FirstRunHudPresets.CommonCount;
            int col = visualIndex % 2;
            int row = visualIndex / 2;
            var preset = FirstRunHudPresets.All[i];
            var previewSettings = FirstRunHudPresets.Apply(_draft.Hud, preset);
            var card = new HudPresetCard(i, preset.DisplayName, preset.DisplayDescription,
                previewSettings, original: false);
            card.Build(_pageRoot,
                col * (moreCardW + moreGapX),
                moreY - row * (moreCardH + moreGapY),
                moreCardW, moreCardH, prominent: false);
            HudCards.Add(card);
        }

        if (_hudPreviewHost != null) _hudPreviewHost.SetAsLastSibling();
    }

    private static void BuildReadyPage()
    {
        if (_pageRoot == null || _shell == null || _draft == null) return;
        BuildPageHeading(Loc.T("setup.review_and_save", "Review and save"), Loc.T("setup.check_the_essentials_then_apply_everything_together", "Check the essentials, then apply everything together."));

        const float gap = 14f;
        float w = (ContentWidth - gap * 2f) / 3f;
        BuildSummaryCard(0f, w, Loc.T("setup.step_audio", "Audio"), new[]
        {
            Loc.T("setup.sum_input", "Input: {0}", CompactDevice(_draft.MicrophoneDisplayName())),
#if WINDOWS
            Loc.T("setup.sum_output", "Output: {0}", CompactDevice(_draft.SpeakerDisplayName())),
#else
            Loc.T("setup.sum_output_android", "Output: Android audio route"),
#endif
            Loc.T("setup.sum_volume", "Playback volume: {0}%", Mathf.RoundToInt(_draft.MasterVolume * 100f)),
            AudioVerificationSummary(),
        }, () => GoTo(AudioPage));

        string talkMode = _draft.MicMode == VoiceMicMode.OpenMic ? Loc.T("setup.open_mic", "Open Mic") : Loc.T("setup.push_to_talk", "Push to Talk");
        BuildSummaryCard(w + gap, w, Loc.T("setup.controls", "Controls"), new[]
        {
            Loc.T("setup.sum_mode", "Mode: {0}", talkMode),
#if ANDROID
            _draft.MicMode == VoiceMicMode.PushToTalk
                ? Loc.T("setup.ptt_hold_the_microphone_button", "PTT: hold the microphone button")
                : Loc.T("setup.mic_button_tap_to_mute", "Mic button: tap to mute"),
            Loc.T("setup.radio_when_available_tap_to_change_channel_hold_to_t", "Radio (when available): tap to change channel / hold to talk"),
#else
            _draft.MicMode == VoiceMicMode.PushToTalk
                ? Loc.T("setup.sum_ptt", "PTT: {0}", _draft.PushToTalk.Label)
                : Loc.T("setup.sum_mute_toggle", "Mute toggle: {0}", _draft.ToggleMute.Label),
            Loc.T("setup.sum_ptm_menu", "Push to Mute: {0} / Menu: {1}", _draft.PushToMute.Label, _draft.OpenVoiceSettings.Label),
#endif
            Loc.T("setup.sum_startup", "Start muted: {0} / Start deafened: {1}", YesNo(_draft.StartMuted), YesNo(_draft.StartDeafened)),
        }, () => GoTo(ControlsPage));

        string hudName = _draft.HideSpeakingBar
            ? Loc.T("setup.speaking_bar_hidden", "Speaking bar hidden")
            : _draft.SelectedHudPreset >= 0
                ? FirstRunHudPresets.All[_draft.SelectedHudPreset].DisplayName
                : Loc.T("setup.custom_layout", "Custom layout");
        BuildSummaryCard((w + gap) * 2f, w, Loc.T("setup.step_hud", "HUD"), new[]
        {
            hudName,
            (_draft.HideVoiceControls ? Loc.T("setup.sum_controls_hidden", "Controls hidden") : Loc.T("setup.sum_controls_shown", "Controls shown")) + " / " + (_draft.HideConnectionStatus ? Loc.T("setup.sum_connection_hidden", "Connection status hidden") : Loc.T("setup.sum_connection_shown", "Connection status shown")),
            _draft.HideSpeakingBar
                ? (_draft.HideMeetingOverlay ? Loc.T("setup.sum_glow_hidden", "Meeting glow hidden") : Loc.T("setup.sum_glow_shown", "Meeting glow shown")) + " / " + Loc.T("setup.speaking_bar_hidden", "Speaking bar hidden")
                : (_draft.HideMeetingOverlay ? Loc.T("setup.sum_glow_hidden", "Meeting glow hidden") : Loc.T("setup.sum_glow_shown", "Meeting glow shown")) + " / " + HudPlacementSummary(_draft.Hud),
            _draft.HideSpeakingBar
                ? Loc.T("setup.layout_can_be_restored_from_hud_settings", "Layout can be restored from HUD settings")
                : (_draft.Hud.Backdrop
                    ? Loc.T("setup.sum_scale_backdrop_on", "Scale {0}% / Backdrop on", Mathf.RoundToInt(_draft.Hud.Scale * 100f))
                    : Loc.T("setup.sum_scale_backdrop_off", "Scale {0}% / Backdrop off", Mathf.RoundToInt(_draft.Hud.Scale * 100f))),
        }, () => GoTo(HudPage));

        string? issue = SetupValidationIssue();
        Color32 noticeColor = issue == null ? SetupSuccess : SetupWarning;
        var notice = BuildInlineNotice(_pageRoot, 84f, -390f, ContentWidth - 168f, 82f,
            issue ?? Loc.T("setup.everything_is_ready_save_finish_will_apply_this_setu", "Everything is ready. Save & finish will apply this setup in one clean save."),
            noticeColor);
        _finishError = notice.GetComponentInChildren<TextMeshProUGUI>();
    }

    private static void BuildCompletedPage()
    {
        if (_pageRoot == null || _shell == null) return;
        float center = ContentWidth * 0.5f;
        for (int i = 0; i < 10; i++)
        {
            float angle = i * (Mathf.PI * 2f / 10f);
            float radius = 108f + (i % 2) * 34f;
            var dot = new CelebrationDot();
            dot.Build(_pageRoot, new Vector2(
                center + Mathf.Cos(angle) * radius,
                -132f + Mathf.Sin(angle) * radius), i);
            CelebrationDots.Add(dot);
        }

        var glow = VoiceUiKit.GlowImage("SuccessGlow", _pageRoot, new Color32(77, 225, 141, 54));
        glow.rectTransform.Anchor(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f));
        glow.rectTransform.sizeDelta = new Vector2(154f, 154f);
        glow.rectTransform.anchoredPosition = new Vector2(center, -132f);
        BuildCheckBadge(_pageRoot, new Vector2(center - 47f, -85f), 94f, SetupSuccess);

        var title = AddText(_pageRoot, Loc.T("setup.you_re_all_set", "You're all set"), 38f, VoiceUiKit.TextBright,
            TextAlignmentOptions.Center, FontStyles.Bold,
            new Vector2(0f, -205f), new Vector2(ContentWidth, 54f));
        AddText(_pageRoot,
            Loc.T("setup.your_perfect_comms_setup_has_been_saved_and_is_ready", "Your Perfect Comms setup has been saved and is ready for the next lobby."),
            19f, SetupTextSecondary, TextAlignmentOptions.Center, FontStyles.Normal,
            new Vector2(110f, -258f), new Vector2(ContentWidth - 220f, 46f), wrap: true);

        var checklist = BuildCard(_pageRoot, "CompletionChecklist",
            center - 310f, -326f, 620f, 76f);
        BuildCheckBadge(checklist, new Vector2(20f, -19f), 34f, SetupSuccess);
        AddText(checklist, Loc.T("setup.done_checklist", "Audio, controls, and HUD saved"), 18f, VoiceUiKit.TextBright,
            TextAlignmentOptions.Left, FontStyles.Bold,
            new Vector2(70f, -14f), new Vector2(520f, 26f));
        AddText(checklist, Loc.T("setup.run_this_setup_again_anytime_from_perfect_comms_sett", "Run this setup again anytime from Perfect Comms Settings."), 17f,
            SetupTextSecondary, TextAlignmentOptions.Left, FontStyles.Normal,
            new Vector2(70f, -41f), new Vector2(520f, 24f));

        AddButton(_pageRoot, Loc.T("setup.advanced_settings", "Advanced settings"), 220f, 50f, center - 230f, -430f,
            ButtonKind.Secondary, () =>
            {
                CloseInternal(destroy: true);
                VoiceSettingsPanel.ShowDeferred();
            });
        AddButton(_pageRoot, Loc.T("setup.done", "Done"), 220f, 50f, center + 10f, -430f,
            ButtonKind.Primary, () => CloseInternal(destroy: true));
    }

    private static void BuildFooter()
    {
        if (_chromeRoot == null || _shell == null) return;
        var divider = VoiceUiKit.Panel("FooterDivider", _chromeRoot,
            new Color32(105, 124, 150, 32), rounded: false);
        PlaceTopLeft(divider.rectTransform, 0f, FooterY + 17f, ContentWidth, 1f);

        AddButton(_chromeRoot, Loc.T("setup.use_existing_settings", "Use existing settings"), 220f, 48f,
            0f, FooterY, ButtonKind.Secondary, UseExistingSettings);

        if (_page > WelcomePage)
            AddButton(_chromeRoot, Loc.T("setup.back", "Back"), 140f, 48f,
                ContentWidth - 350f, FooterY, ButtonKind.Secondary,
                () => GoTo(_page - 1));

        if (_page < ReadyPage)
        {
            string text = _page == WelcomePage ? Loc.T("setup.start_setup", "Start setup") : Loc.T("setup.continue", "Continue");
            AddButton(_chromeRoot, text, 190f, 48f,
                ContentWidth - 190f, FooterY, ButtonKind.Primary,
                () => GoTo(_page + 1));
        }
        else
        {
            AddButton(_chromeRoot, Loc.T("setup.save_finish", "Save & finish"), 190f, 48f,
                ContentWidth - 190f, FooterY, ButtonKind.Primary, FinishSetup,
                () => SetupValidationIssue() == null);
        }
    }

    private static void UseExistingSettings()
    {
        try
        {
            VoiceUiKit.RebindRow.CancelCapture();
            VoiceSettings.Instance?.UseExistingSettingsForFirstRunSetup();
            DisposeAudioPreview();
            CloseInternal(destroy: true);
        }
        catch (Exception ex)
        {
            VoiceChatPlugin.VoiceChatPluginMain.Logger.LogError(
                "[PC-SETUP] Could not keep existing settings: " + ex);
            ShowClosePrompt();
            if (_promptMessage != null)
            {
                _promptMessage.color = VoiceUiKit.Danger;
                _promptMessage.text =
                    Loc.T("setup.error_existing_not_saved", "Existing settings were not changed, but the setup choice could not be saved. Please try again.");
            }
        }
    }

    private static void GoTo(int page)
    {
        page = Mathf.Clamp(page, WelcomePage, ReadyPage);
        if (page == _page) return;
        if (_page == AudioPage)
        {
            CaptureAudioCheckResults();
            DisposeAudioPreview();
        }
        if (_page == HudPage) _hudPreview?.Suspend();
        VoiceUiKit.RebindRow.CancelCapture();
        _page = page;
        BuildPage();
    }

    private static void FinishSetup()
    {
        if (_draft == null || VoiceSettings.Instance == null) return;
        string? issue = SetupValidationIssue();
        if (issue != null)
        {
            if (_finishError != null)
            {
                _finishError.color = SetupWarning;
                _finishError.text = issue;
            }
            return;
        }
        try
        {
            VoiceUiKit.RebindRow.CancelCapture();
            DisposeAudioPreview();
            VoiceSettings.Instance.CommitFirstRunSetup(_draft);
            _completed = true;
            _page = ReadyPage;
            BuildPage();
        }
        catch (Exception ex)
        {
            VoiceChatPlugin.VoiceChatPluginMain.Logger.LogError(
                "[PC-SETUP] Could not save setup: " + ex);
            if (_finishError != null)
                _finishError.text = Loc.T("setup.error_not_saved", "Could not save your setup. Your previous settings are still active. Try again.");
        }
    }

    private static void RequestClose()
    {
        if (_completed)
        {
            CloseInternal(destroy: true);
            return;
        }
        ShowClosePrompt();
    }

    private static void ShowClosePrompt()
    {
        if (_shell == null || _closePrompt != null) return;
        VoiceUiKit.RebindRow.CancelCapture();
        CaptureAudioCheckResults();
        _audioPreview?.StopAllTests();
        _closePrompt = VoiceUiKit.Rect("SetupClosePrompt", _shell.RootRect);
        _closePrompt.Anchor(Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        _closePrompt.offsetMin = Vector2.zero;
        _closePrompt.offsetMax = Vector2.zero;
        _closePrompt.SetAsLastSibling();

        var shade = _closePrompt.gameObject.AddComponent<Image>();
        shade.sprite = VoiceUiKit.Solid(Color.white);
        shade.color = new Color32(0, 0, 0, 205);
        shade.raycastTarget = true;
        _closePromptGroup = _closePrompt.gameObject.AddComponent<CanvasGroup>();
        _closePromptGroup.alpha = 0f;
        _promptAppear = 0f;

        var card = VoiceUiKit.Panel("PromptCard", _closePrompt, SetupSurfaceRaised, rounded: true);
        card.rectTransform.Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        card.rectTransform.sizeDelta = new Vector2(620f, 300f);
        card.rectTransform.anchoredPosition = Vector2.zero;
        _closePromptCard = card.rectTransform;
        AddPanelBorder(card.rectTransform, SetupBorder);

        AddText(card.rectTransform, Loc.T("setup.leave_setup", "Leave setup?"), 31f,
            VoiceUiKit.TextBright, TextAlignmentOptions.Center, FontStyles.Bold,
            new Vector2(42f, -35f), new Vector2(536f, 44f));
        _promptMessage = AddText(card.rectTransform,
            Loc.T("setup.continue_with_the_recommended_setup_or_keep_the_sett", "Continue with the recommended setup, or keep the settings already on this device."),
            20f, SetupTextSecondary, TextAlignmentOptions.Center, FontStyles.Normal,
            new Vector2(62f, -90f), new Vector2(496f, 62f), wrap: true);

        AddText(card.rectTransform,
            Loc.T("setup.use_existing_settings_completes_this_one_time_setup", "Use existing settings completes this one-time setup without changing anything."),
            17f, SetupTextTertiary, TextAlignmentOptions.Center, FontStyles.Normal,
            new Vector2(58f, -159f), new Vector2(504f, 42f), wrap: true);

        PromptButtons.Clear();
        PromptButtons.Add(new SetupButton(card.rectTransform, Loc.T("setup.continue_setup", "Continue setup"), 230f, 50f,
            70f, -222f, ButtonKind.Primary, HideClosePrompt));
        PromptButtons.Add(new SetupButton(card.rectTransform, Loc.T("setup.use_existing_settings", "Use existing settings"), 230f, 50f,
            320f, -222f, ButtonKind.Secondary, UseExistingSettings));
        VoiceUiKit.SwallowClick();
        _inputLockedUntilFrame = Time.frameCount + 1;
    }

    private static void HideClosePrompt()
    {
        if (_closePrompt != null) Object.Destroy(_closePrompt.gameObject);
        _closePrompt = null;
        _closePromptCard = null;
        _closePromptGroup = null;
        _promptMessage = null;
        PromptButtons.Clear();
        _inputLockedUntilFrame = Time.frameCount + 1;
    }

    private static void CloseInternal(bool destroy)
    {
        _shown = false;
        VoiceUiKit.RebindRow.CancelCapture();
        DisposeAudioPreview();
        if (_hudPreview != null)
        {
            try { _hudPreview.Dispose(); } catch { }
            _hudPreview = null;
        }
        if (destroy && _shell?.Root != null) Object.Destroy(_shell.Root);
        _shell = null;
        _chromeRoot = null;
        _hudPreviewHost = null;
        _pageRoot = null;
        _pageGroup = null;
        _closePrompt = null;
        _closePromptCard = null;
        _closePromptGroup = null;
        _promptMessage = null;
        _draft = null;
        Rows.Clear();
        Buttons.Clear();
        PromptButtons.Clear();
        HudCards.Clear();
        CelebrationDots.Clear();
        VoiceUiKit.SwallowClick();
    }

    private static void TickAudioPage(float dt)
    {
        if (_draft == null || _audioPreview == null) return;
        VoiceChatLocalSettings.MaybeRefreshDeviceLists(resolveSavedIndices: false);
        _audioPreview.RefreshMicrophoneSettings(_draft);
        _micLevelMeter?.SetLevel(_audioPreview.LiveLevel, _audioPreview.IsListening);
        _micLevelMeter?.Tick(dt, _audioPreview.IsMicrophoneTestActive);
        if (_audioPreview.MicrophoneSignalDetected)
        {
            _microphoneSignalDetected = true;
            _verifiedMicrophoneDevice = _draft.MicrophoneDevice;
        }
        if (_audioPreview.OutputTestCompleted)
        {
            _outputTestCompleted = true;
            _verifiedSpeakerDevice = _draft.SpeakerDevice;
        }
        if (_outputStatus != null)
        {
            _outputStatus.text = _audioPreview.Status;
            _outputStatus.color = _audioPreview.MicrophoneSignalDetected ||
                                  _audioPreview.OutputTestCompleted
                ? SetupSuccess
                : SetupTextSecondary;
        }
        _micTestButton?.SetText(_audioPreview.IsMicrophoneTestStarting
            ? Loc.T("setup.cancel_mic_start", "Cancel mic start")
            : _audioPreview.IsMicrophoneTestActive ? Loc.T("setup.stop_mic_test", "Stop mic test") : Loc.T("setup.test_mic", "Test mic"));
        _speakerTestButton?.SetText(_audioPreview.IsPlayingTone
            ? Loc.T("setup.playing", "Playing...")
            : _audioPreview.IsPreparingTone ? Loc.T("setup.opening_output", "Opening output...") : Loc.T("setup.test_output", "Test output"));

        if (_audioPreview.ConsumeUiRefresh() ||
            (!VoiceUiKit.RebindRow.IsCapturing &&
             (_micListVersion != VoiceChatLocalSettings.MicDeviceListVersion ||
              _speakerListVersion != VoiceChatLocalSettings.SpkDeviceListVersion)))
        {
            BuildPage();
        }
    }

    private static void TickControlsPage()
    {
        if (_draft == null) return;
        bool pushToTalk = _draft.MicMode == VoiceMicMode.PushToTalk;
        if (_micModeHelpText != null)
        {
#if ANDROID
            _micModeHelpText.text = AndroidVoiceUiPolicy.MicModeHelp(pushToTalk);
#else
            _micModeHelpText.text = pushToTalk
                ? Loc.T("setup.voice_is_sent_only_while_your_npush_to_talk_shortcut", "Voice is sent only while your\nPush to Talk shortcut is held.")
                : Loc.T("setup.voice_activation_sends_speech_nautomatically_when_yo", "Voice activation sends speech\nautomatically when you talk.");
#endif
            _micModeHelpText.color = pushToTalk ? SetupWarning : VoiceUiKit.Accent;
        }
        if (_pushToTalkRow != null)
            _pushToTalkRow.Title.color = pushToTalk ? VoiceUiKit.TextPrimary : SetupTextTertiary;
        if (_pushToTalkGroup != null)
            _pushToTalkGroup.alpha = Mathf.MoveTowards(
                _pushToTalkGroup.alpha, pushToTalk ? 1f : 0.52f,
                Mathf.Max(0f, Time.unscaledDeltaTime) * 5f);
    }

    private static void TickHudCards(float dt)
    {
        if (_draft != null && _builtHudSpeakingBarHidden != _draft.HideSpeakingBar)
        {
            BuildPage();
            return;
        }
        _hoveredHudSettings = null;
        _hoveredHudDescription = null;
        for (int i = 0; i < HudCards.Count; i++) HudCards[i].Tick(dt);
        if (_hudDescriptionText != null)
        {
            _hudDescriptionText.text = _hoveredHudDescription ?? SelectedHudDescription();
            _hudDescriptionText.color = _hoveredHudDescription != null
                ? VoiceUiKit.TextPrimary
                : SetupTextSecondary;
        }
    }

    private static void EnsureHudPreview()
    {
        if (_hudPreview != null || _shell == null) return;
        try
        {
            _hudPreviewHost = VoiceUiKit.Rect("SetupHudPreviewHost", _shell.PaneRoot);
            _hudPreviewHost.Anchor(new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            _hudPreviewHost.sizeDelta = new Vector2(-ContentSideInset * 2f, 0f);
            _hudPreviewHost.anchoredPosition = Vector2.zero;

            Vector2 previewSize = SpeakingBarLivePreview.EmbeddedCardSize;
            float previewWidth = previewSize.x;
            float previewHeight = previewSize.y;
            const float previewGap = 18f;
            const float layoutTopY = ContentTopY - 76f;
            float pickerWidth = ContentWidth - previewWidth - previewGap;
            var center = new Vector2(
                pickerWidth + previewGap + previewWidth * 0.5f - ContentWidth * 0.5f,
                layoutTopY - previewHeight * 0.5f);
            _hudPreview = new SpeakingBarLivePreview(_hudPreviewHost, center, embedded: true);
        }
        catch (Exception ex)
        {
            VoiceChatPlugin.VoiceChatPluginMain.Logger.LogWarning(
                "[PC-SETUP] HUD live preview is unavailable: " + ex.Message);
            if (_hudPreviewHost != null)
            {
                _hudPreviewHost.gameObject.SetActive(false);
                Object.Destroy(_hudPreviewHost.gameObject);
                _hudPreviewHost = null;
            }
            _hudPreview = null;
        }
    }

    private static void TickHudPreview(float dt, bool render)
    {
        if (_hudPreview == null || _draft == null) return;
        float reveal = Smooth(_hudReveal);
        try
        {
            if (_draft.HideSpeakingBar)
            {
                _hudPreview.SetPresentation(0f, shouldRender: false);
                return;
            }
            var settings = _hoveredHudSettings ?? _draft.Hud;
            if (!_hudPreview.IsWarmupReady)
                _hudPreview.Prewarm(settings, dt);
            bool visible = render && reveal > 0.01f;
            _hudPreview.SetPresentation(reveal, visible);
            if (visible)
                _hudPreview.Tick(settings, dt);
        }
        catch (Exception ex)
        {
            VoiceChatPlugin.VoiceChatPluginMain.Logger.LogWarning(
                "[PC-SETUP] HUD preview was disabled: " + ex.Message);
            try { _hudPreview.Dispose(); } catch { }
            _hudPreview = null;
        }
    }

    private static void ApplyPresentation()
    {
        if (_shell == null) return;
        Rect canvas = VoiceUiKit.CanvasRect.rect;
        float availableW = canvas.width > 1f ? canvas.width : 1920f;
        float availableH = canvas.height > 1f ? canvas.height : 1080f;
        float maxScale = Mathf.Min(1f,
            Mathf.Min(availableW / (PanelWidth + 64f), availableH / (PanelHeight + 64f)));
        maxScale = Mathf.Clamp(maxScale, 0.74f, 1f);
        float scale = maxScale * Mathf.Lerp(0.96f, 1f, Smooth(_appear));
        _shell.RootRect.localScale = new Vector3(scale, scale, 1f);
        _shell.Group.alpha = Smooth(_appear);
        _shell.SetLayoutOffset(Vector2.zero);
    }

    private static void HandleRows()
    {
        if (Time.frameCount <= _inputLockedUntilFrame) return;
        if (!Input.GetMouseButton(0))
        {
            for (int i = 0; i < Rows.Count; i++) Rows[i].OnMouseUp();
            return;
        }
        if (Input.GetMouseButtonDown(0))
        {
            for (int i = 0; i < Rows.Count; i++) Rows[i].OnMouseDown();
        }
        else
        {
            for (int i = 0; i < Rows.Count; i++)
                if (Rows[i].IsDragging) Rows[i].OnMouseDrag();
        }
    }

    private static void TickButtons(List<SetupButton> buttons, float dt)
    {
        for (int i = 0; i < buttons.Count; i++)
            buttons[i].Tick(dt, Time.frameCount > _inputLockedUntilFrame);
    }

    private static bool PointerOverAnyButton(List<SetupButton> buttons)
    {
        for (int i = 0; i < buttons.Count; i++)
            if (buttons[i].IsPointerOver) return true;
        return false;
    }

    private static void TickCelebration(float dt)
    {
        for (int i = 0; i < CelebrationDots.Count; i++) CelebrationDots[i].Tick(dt);
    }

    private static void DisposeAudioPreview()
    {
        if (_audioPreview == null) return;
        try { _audioPreview.Dispose(); } catch { }
        _audioPreview = null;
    }

    private static void CaptureAudioCheckResults()
    {
        if (_audioPreview == null || _draft == null) return;
        if (_audioPreview.MicrophoneSignalDetected)
        {
            _microphoneSignalDetected = true;
            _verifiedMicrophoneDevice = _draft.MicrophoneDevice;
        }
        if (_audioPreview.OutputTestCompleted)
        {
            _outputTestCompleted = true;
            _verifiedSpeakerDevice = _draft.SpeakerDevice;
        }
    }

    private static TextMeshProUGUI? BuildPageHeading(string title, string subtitle)
    {
        if (_pageRoot == null || _shell == null) return null;
        var heading = AddText(_pageRoot, title, 33f, VoiceUiKit.TextBright,
            TextAlignmentOptions.Left, FontStyles.Bold,
            new Vector2(2f, -56f), new Vector2(ContentWidth - 4f, 38f));
        heading.characterSpacing = 0f;
        return AddText(_pageRoot, subtitle, 19f, SetupTextSecondary,
            TextAlignmentOptions.Left, FontStyles.Normal,
            new Vector2(2f, -98f), new Vector2(ContentWidth - 4f, 24f));
    }

    private static RectTransform BuildCard(
        RectTransform parent, string name, float x, float y, float w, float h)
    {
        var card = VoiceUiKit.Panel(name, parent, SetupSurfaceRaised, rounded: true);
        PlaceTopLeft(card.rectTransform, x, y, w, h);
        AddPanelBorder(card.rectTransform, SetupBorder);
        return card.rectTransform;
    }

    private static void AddPanelBorder(RectTransform panel, Color32 color)
    {
        var outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = true;
    }

    private static void AddCardTitle(
        RectTransform card, string title, string subtitle, float rightInset = 20f)
    {
        float width = Mathf.Max(80f, card.rect.width - 20f - rightInset);
        AddText(card, title, 23f, VoiceUiKit.TextBright, TextAlignmentOptions.Left,
            FontStyles.Bold, new Vector2(20f, -13f), new Vector2(width, 24f));
        AddText(card, subtitle, 16f, SetupTextSecondary, TextAlignmentOptions.Left,
            FontStyles.Normal, new Vector2(20f, -40f), new Vector2(width, 22f));
    }

    private static RectTransform BuildInlineNotice(
        RectTransform parent, float x, float y, float width, float height,
        string text, Color32 accent)
    {
        var notice = VoiceUiKit.Panel("InlineNotice", parent,
            new Color32(accent.r, accent.g, accent.b, 18), rounded: true, soft: true);
        PlaceTopLeft(notice.rectTransform, x, y, width, height);
        var bar = VoiceUiKit.Panel("NoticeBar", notice.rectTransform, accent, rounded: true, soft: true);
        bar.rectTransform.Anchor(new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f));
        bar.rectTransform.sizeDelta = new Vector2(4f, -18f);
        bar.rectTransform.anchoredPosition = new Vector2(8f, 0f);
        AddText(notice.rectTransform, text, 17f, SetupTextSecondary,
            TextAlignmentOptions.Left, FontStyles.Normal,
            new Vector2(22f, -8f), new Vector2(width - 36f, height - 16f), wrap: true);
        return notice.rectTransform;
    }

    private static void BuildJourneyRow(
        RectTransform parent, float y, string glyph, string title, string description)
    {
        var icon = VoiceUiKit.Panel("JourneyIcon", parent,
            new Color32(34, 211, 238, 28), rounded: true, soft: true);
        PlaceTopLeft(icon.rectTransform, 22f, y, 46f, 46f);
        AddText(icon.rectTransform, glyph, 18f, VoiceUiKit.Accent,
            TextAlignmentOptions.Center, FontStyles.Bold,
            Vector2.zero, new Vector2(46f, 46f));
        AddText(parent, title, 21f, VoiceUiKit.TextBright,
            TextAlignmentOptions.Left, FontStyles.Bold,
            new Vector2(84f, y + 1f), new Vector2(parent.rect.width - 108f, 26f));
        AddText(parent, description, 17f, SetupTextSecondary,
            TextAlignmentOptions.Left, FontStyles.Normal,
            new Vector2(84f, y - 26f), new Vector2(parent.rect.width - 108f, 42f), wrap: true);
    }

    private static void BuildCheckBadge(
        RectTransform parent, Vector2 topLeft, float size, Color32 color)
    {
        var badge = VoiceUiKit.Panel("CheckBadge", parent, color, rounded: true, soft: true);
        PlaceTopLeft(badge.rectTransform, topLeft.x, topLeft.y, size, size);
        Color32 checkColor = new(9, 40, 29, 255);
        var shortBar = VoiceUiKit.Panel("CheckShort", badge.rectTransform, checkColor, true, true);
        shortBar.rectTransform.Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        shortBar.rectTransform.sizeDelta = new Vector2(size * 0.26f, Mathf.Max(3f, size * 0.07f));
        shortBar.rectTransform.anchoredPosition = new Vector2(-size * 0.11f, size * -0.02f);
        shortBar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -43f);
        var longBar = VoiceUiKit.Panel("CheckLong", badge.rectTransform, checkColor, true, true);
        longBar.rectTransform.Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        longBar.rectTransform.sizeDelta = new Vector2(size * 0.46f, Mathf.Max(3f, size * 0.07f));
        longBar.rectTransform.anchoredPosition = new Vector2(size * 0.09f, size * -0.05f);
        longBar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 46f);
    }

    private static VoiceUiKit.Row AddBindingRow(
        RectTransform parent,
        float width,
        float y,
        string label,
        Func<FirstRunSetupBinding> get,
        Action<FirstRunSetupBinding> set,
        string? help)
    {
        return AddRow(new VoiceUiKit.RebindRow(
                () => get().Key,
                (key, modifier, match) => set(new FirstRunSetupBinding(key, modifier, match)),
                () => set(new FirstRunSetupBinding(KeyCode.None, KeyCode.None, VoiceModifierMatch.Exact)),
                () => get().Modifier,
                () => get().ModifierMatch)
            .Build(parent, label, width, y, 54f, help));
    }

    private static void BuildSummaryCard(
        float x, float width, string title, IReadOnlyList<string> lines, Action edit)
    {
        if (_pageRoot == null) return;
        var card = BuildCard(_pageRoot, "Summary" + title, x, ContentTopY, width, 232f);
        BuildCheckBadge(card, new Vector2(18f, -16f), 30f, SetupSuccess);
        AddText(card, title, 21f, VoiceUiKit.TextBright, TextAlignmentOptions.Left,
            FontStyles.Bold, new Vector2(60f, -15f), new Vector2(width - 150f, 32f));
        AddButton(card, Loc.T("setup.edit", "Edit"), 70f, 34f, width - 88f, -13f,
            ButtonKind.Ghost, edit);
        for (int i = 0; i < lines.Count && i < 4; i++)
        {
            var line = AddText(card, lines[i], i == 0 ? 18f : 17f,
                i == 0 ? VoiceUiKit.TextPrimary : SetupTextSecondary,
                TextAlignmentOptions.Left, i == 0 ? FontStyles.Bold : FontStyles.Normal,
                new Vector2(20f, -66f - i * 38f), new Vector2(width - 40f, 32f), wrap: true);
            line.enableAutoSizing = true;
            line.fontSizeMax = i == 0 ? 18f : 17f;
            line.fontSizeMin = 15f;
        }
    }

    private static void BuildMicGlyph(RectTransform root)
    {
        var capsule = VoiceUiKit.Panel("MicCapsule", root, VoiceUiKit.Accent, rounded: true, soft: true);
        capsule.rectTransform.Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        capsule.rectTransform.sizeDelta = new Vector2(25f, 43f);
        capsule.rectTransform.anchoredPosition = new Vector2(0f, 5f);
        var stem = VoiceUiKit.Panel("MicStem", root, VoiceUiKit.Accent, rounded: true, soft: true);
        stem.rectTransform.Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        stem.rectTransform.sizeDelta = new Vector2(5f, 22f);
        stem.rectTransform.anchoredPosition = new Vector2(0f, -23f);
        var baseBar = VoiceUiKit.Panel("MicBase", root, VoiceUiKit.Accent, rounded: true, soft: true);
        baseBar.rectTransform.Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        baseBar.rectTransform.sizeDelta = new Vector2(36f, 5f);
        baseBar.rectTransform.anchoredPosition = new Vector2(0f, -33f);
    }

    private static VoiceUiKit.Row AddRow(VoiceUiKit.Row row)
    {
        Rows.Add(row);
        return row;
    }

    private static SetupButton AddButton(
        RectTransform parent,
        string text,
        float width,
        float height,
        float x,
        float y,
        ButtonKind kind,
        Action click,
        Func<bool>? enabled = null)
    {
        var button = new SetupButton(parent, text, width, height, x, y, kind, click, enabled);
        Buttons.Add(button);
        return button;
    }

    private static TextMeshProUGUI AddText(
        Transform parent,
        string text,
        float size,
        Color32 color,
        TextAlignmentOptions alignment,
        FontStyles style,
        Vector2 position,
        Vector2 dimensions,
        bool wrap = false)
    {
        var tmp = VoiceUiKit.Text("SetupText", parent, text, size, color, alignment, style);
        PlaceTopLeft(tmp.rectTransform, position.x, position.y, dimensions.x, dimensions.y);
        tmp.enableWordWrapping = wrap;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        return tmp;
    }

    private static void PlaceTopLeft(RectTransform rt, float x, float y, float w, float h)
    {
        rt.Anchor(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, y);
    }

    private static string DeviceName(IReadOnlyList<string> names, int index)
        => names.Count == 0
            ? Loc.T("setup.no_devices_found", "No devices found")
            : names[Mathf.Clamp(index, 0, names.Count - 1)];

    private static int DraftMicrophoneIndexForUi()
    {
        if (_draft == null) return 0;
        int resolved = _draft.MicrophoneIndex();
        return resolved >= 0 ? resolved : VoiceChatLocalSettings.MicDeviceNames.Length;
    }

    private static int DraftMicrophoneCountForUi()
    {
        int count = VoiceChatLocalSettings.MicDeviceNames.Length;
        return _draft != null && !string.IsNullOrEmpty(_draft.MicrophoneDevice) &&
               _draft.MicrophoneIndex() < 0 ? count + 1 : count;
    }

    private static string DraftMicrophoneNameForUi(int index)
        => index < VoiceChatLocalSettings.MicDeviceNames.Length
            ? DeviceName(VoiceChatLocalSettings.MicDeviceNames, index)
            : Loc.T("setup.saved_device_suffix", "{0} (saved device)", _draft?.MicrophoneDisplayName() ?? Loc.T("setup.saved_device", "Saved device"));

#if WINDOWS
    private static int DraftSpeakerIndexForUi()
    {
        if (_draft == null) return 0;
        int resolved = _draft.SpeakerIndex();
        return resolved >= 0 ? resolved : VoiceChatLocalSettings.SpkDeviceNames.Length;
    }

    private static int DraftSpeakerCountForUi()
    {
        int count = VoiceChatLocalSettings.SpkDeviceNames.Length;
        return _draft != null && !string.IsNullOrEmpty(_draft.SpeakerDevice) &&
               _draft.SpeakerIndex() < 0 ? count + 1 : count;
    }

    private static string DraftSpeakerNameForUi(int index)
        => index < VoiceChatLocalSettings.SpkDeviceNames.Length
            ? DeviceName(VoiceChatLocalSettings.SpkDeviceNames, index)
            : Loc.T("setup.saved_device_suffix", "{0} (saved device)", _draft?.SpeakerDisplayName() ?? Loc.T("setup.saved_device", "Saved device"));
#endif

    private static string CompactDevice(string displayName)
    {
        string value = string.IsNullOrWhiteSpace(displayName) ? Loc.T("setup.system_default", "System Default") : displayName;
        return value.Length <= 30 ? value : value.Substring(0, 27) + "...";
    }

    private static bool OutputVerifiedForCurrentDraft
        => _draft != null && _outputTestCompleted &&
           string.Equals(_verifiedSpeakerDevice, _draft.SpeakerDevice,
               StringComparison.OrdinalIgnoreCase);

    private static bool MicrophoneVerifiedForCurrentDraft
        => _draft != null && _microphoneSignalDetected &&
           string.Equals(_verifiedMicrophoneDevice, _draft.MicrophoneDevice,
               StringComparison.OrdinalIgnoreCase);

    private static string AudioVerificationSummary()
    {
        if (MicrophoneVerifiedForCurrentDraft && OutputVerifiedForCurrentDraft)
            return Loc.T("setup.mic_and_output_tested", "Mic and output tested");
        if (MicrophoneVerifiedForCurrentDraft) return Loc.T("setup.mic_tested_output_optional", "Mic tested / output optional");
        if (OutputVerifiedForCurrentDraft) return Loc.T("setup.output_tested_mic_optional", "Output tested / mic optional");
        return Loc.T("setup.audio_tests_optional", "Audio tests optional");
    }

    private static string YesNo(bool value) => value ? Loc.T("setup.yes", "Yes") : Loc.T("setup.no", "No");
    

    private static string FriendlyPosition(SpeakingBarPosition position) => position switch
    {
        SpeakingBarPosition.TopLeft => Loc.T("setup.top_left", "Top left"),
        SpeakingBarPosition.TopMiddle => Loc.T("setup.top_center", "Top center"),
        SpeakingBarPosition.TopRight => Loc.T("setup.top_right", "Top right"),
        SpeakingBarPosition.MiddleLeft => Loc.T("setup.middle_left", "Middle left"),
        SpeakingBarPosition.MiddleRight => Loc.T("setup.middle_right", "Middle right"),
        SpeakingBarPosition.BottomLeft => Loc.T("setup.bottom_left", "Bottom left"),
        SpeakingBarPosition.BottomMiddle => Loc.T("setup.bottom_center", "Bottom center"),
        SpeakingBarPosition.BottomRight => Loc.T("setup.bottom_right", "Bottom right"),
        _ => position.ToString(),
    };

    private static string HudPlacementSummary(SpeakingBarPreviewSettings settings)
        => settings.ManualLayout
            ? Loc.T("setup.hud_manual_summary", "Manual {0} / X {1}% / Y {2}%",
                settings.ManualOrientation == VoiceControlsLayout.Vertical
                    ? Loc.T("setup.orientation_vertical", "vertical")
                    : Loc.T("setup.orientation_horizontal", "horizontal"),
                Mathf.RoundToInt(settings.ManualX * 100f), Mathf.RoundToInt(settings.ManualY * 100f))
            : FriendlyPosition(settings.Position);

    private static string SelectedHudDescription()
    {
        if (_draft == null) return Loc.T("setup.hover_a_layout_to_preview_it_live_select_one_to_use", "Hover a layout to preview it live. Select one to use it.");
        int index = _draft.SelectedHudPreset;
        return index >= 0 && index < FirstRunHudPresets.All.Count
            ? FirstRunHudPresets.All[index].Description
            : Loc.T("setup.choose_a_hud_layout_to_continue", "Choose a HUD layout to continue.");
    }

    private static string? SetupValidationIssue()
    {
        if (_draft == null) return Loc.T("setup.setup_is_still_loading", "Setup is still loading.");
        if (_draft.MicrophoneSelectionChanged &&
            !string.IsNullOrEmpty(_draft.MicrophoneDevice) && _draft.MicrophoneIndex() < 0)
            return Loc.T("setup.the_selected_microphone_is_no_longer_available_retur", "The selected microphone is no longer available. Return to Audio and choose another device or Default.");
#if WINDOWS
        if (_draft.SpeakerSelectionChanged &&
            !string.IsNullOrEmpty(_draft.SpeakerDevice) && _draft.SpeakerIndex() < 0)
            return Loc.T("setup.the_selected_output_device_is_no_longer_available_re", "The selected output device is no longer available. Return to Audio and choose another device or Default.");
#endif
#if WINDOWS
        if (_draft.MicMode == VoiceMicMode.PushToTalk && _draft.PushToTalk.Key == KeyCode.None)
            return Loc.T("setup.choose_a_push_to_talk_shortcut_before_saving_or_swit", "Choose a Push to Talk shortcut before saving, or switch to Open Mic.");

        string? bindingIssue = BindingValidationIssue(_draft);
        if (bindingIssue != null) return bindingIssue;
#endif
        return null;
    }

    internal static string? BindingValidationIssue(FirstRunSetupDraft draft)
    {
        var bindings = new (string Name, FirstRunSetupBinding Binding)[]
        {
            (Loc.T("setup.bind_mute_microphone", "Mute microphone"), draft.ToggleMute),
            (Loc.T("setup.push_to_mute", "Push to Mute"), draft.PushToMute),
            (Loc.T("setup.push_to_talk", "Push to Talk"), draft.PushToTalk),
            (Loc.T("setup.bind_mute_playback", "Mute playback"), draft.ToggleSpeaker),
            (Loc.T("setup.open_voice_menu", "Open voice menu"), draft.OpenVoiceSettings),
        };
        for (int i = 0; i < bindings.Length; i++)
            for (int j = i + 1; j < bindings.Length; j++)
            {
                if (!BindingsConflict(bindings[i].Binding, bindings[j].Binding)) continue;
                return Loc.T("setup.bind_conflict", "{0} and {1} use the same shortcut. Edit Controls before saving.", bindings[i].Name, bindings[j].Name);
            }
        return null;
    }

    private static bool BindingsConflict(FirstRunSetupBinding a, FirstRunSetupBinding b)
        => a.Key != KeyCode.None && a.Key == b.Key && a.Modifier == b.Modifier;

    private static float Smooth(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private enum ButtonKind
    {
        Primary,
        Secondary,
        Ghost,
        Danger,
        DangerGhost,
    }

    private sealed class SetupButton
    {
        private readonly RectTransform _root;
        private readonly Image _surface;
        private readonly Image _glow;
        private readonly TextMeshProUGUI _label;
        private readonly ButtonKind _kind;
        private readonly Action _click;
        private readonly Func<bool> _enabled;

        internal SetupButton(
            RectTransform parent,
            string text,
            float width,
            float height,
            float x,
            float y,
            ButtonKind kind,
            Action click,
            Func<bool>? enabled = null)
        {
            _kind = kind;
            _click = click;
            _enabled = enabled ?? (() => true);
            _root = VoiceUiKit.Rect("SetupButton", parent);
            PlaceTopLeft(_root, x, y, width, height);
            _glow = VoiceUiKit.GlowImage("ButtonGlow", _root, VoiceUiKit.Clear);
            _glow.rectTransform.Anchor(Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            _glow.rectTransform.offsetMin = new Vector2(-8f, -4f);
            _glow.rectTransform.offsetMax = new Vector2(8f, 8f);
            _surface = _root.gameObject.AddComponent<Image>();
            _surface.sprite = VoiceUiKit.Rounded(true);
            _surface.type = Image.Type.Sliced;
            _surface.color = RestColor();
            _surface.raycastTarget = false;
            _label = VoiceUiKit.Text("ButtonLabel", _root, text, 18f,
                LabelColor(),
                TextAlignmentOptions.Center, FontStyles.Bold);
            _label.characterSpacing = 0f;
            _label.enableAutoSizing = true;
            _label.fontSizeMax = 18f;
            _label.fontSizeMin = 15f;
            _label.rectTransform.Anchor(Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            _label.rectTransform.offsetMin = new Vector2(8f, 0f);
            _label.rectTransform.offsetMax = new Vector2(-8f, 0f);
        }

        internal void SetText(string text)
        {
            if (_label.text != text) _label.text = text;
        }

        internal bool IsPointerOver => _root != null && _root.gameObject.activeInHierarchy &&
                                       _enabled() && VoiceUiKit.Contains(_root);

        internal void Tick(float dt, bool allowInput)
        {
            if (_root == null || !_root.gameObject.activeInHierarchy) return;
            bool enabled = _enabled();
            bool over = enabled && VoiceUiKit.Contains(_root);
            bool down = over && Input.GetMouseButton(0);
            Color32 target = !enabled
                ? VoiceUiKit.ToggleOffTrack
                : over ? HoverColor() : RestColor();
            _surface.color = VoiceUiKit.Lerp(_surface.color, target, Mathf.Clamp01(dt * 15f));
            Color32 glow = VoiceUiKit.Clear;
            if (over && _kind == ButtonKind.Primary) glow = VoiceUiKit.AccentGlow;
            else if (over && (_kind == ButtonKind.Danger || _kind == ButtonKind.DangerGhost))
                glow = new Color32(230, 88, 96, 42);
            _glow.color = VoiceUiKit.Lerp(_glow.color, glow, Mathf.Clamp01(dt * 14f));
            _label.color = VoiceUiKit.Lerp(_label.color,
                enabled ? (over ? LabelHoverColor() : LabelColor()) : VoiceUiKit.TextFaint,
                Mathf.Clamp01(dt * 14f));
            float targetScale = down ? 0.98f : over ? 1.015f : 1f;
            float scale = Mathf.Lerp(_root.localScale.x, targetScale, Mathf.Clamp01(dt * 18f));
            _root.localScale = new Vector3(scale, scale, 1f);
            if (allowInput && over && Input.GetMouseButtonDown(0))
            {
                VoiceUiKit.SwallowClick();
                _click();
            }
        }

        private Color32 RestColor() => _kind switch
        {
            ButtonKind.Primary => VoiceUiKit.Accent,
            ButtonKind.Secondary => new Color32(34, 43, 56, 255),
            ButtonKind.Danger => new Color32(91, 39, 45, 255),
            _ => VoiceUiKit.Clear,
        };

        private Color32 HoverColor() => _kind switch
        {
            ButtonKind.Primary => new Color32(93, 231, 248, 255),
            ButtonKind.Secondary => SetupSurfaceHover,
            ButtonKind.Danger => new Color32(126, 48, 57, 255),
            ButtonKind.DangerGhost => new Color32(74, 34, 39, 230),
            _ => new Color32(34, 211, 238, 18),
        };

        private Color32 LabelColor() => _kind switch
        {
            ButtonKind.Primary => new Color32(6, 28, 36, 255),
            ButtonKind.DangerGhost => new Color32(242, 143, 149, 255),
            _ => VoiceUiKit.TextPrimary,
        };

        private Color32 LabelHoverColor() => _kind switch
        {
            ButtonKind.Primary => new Color32(4, 24, 31, 255),
            ButtonKind.DangerGhost => new Color32(255, 184, 188, 255),
            _ => VoiceUiKit.TextBright,
        };
    }

    private sealed class HudPresetCard
    {
        private readonly int _index;
        private readonly string _name;
        private readonly string _description;
        private readonly SpeakingBarPreviewSettings _previewSettings;
        private readonly bool _original;
        private RectTransform _root = null!;
        private RectTransform _visual = null!;
        private Image _surface = null!;
        private Image _border = null!;
        private TextMeshProUGUI _title = null!;
        private float _hover;
        private float _selected;

        internal HudPresetCard(
            int index,
            string name,
            string description,
            SpeakingBarPreviewSettings previewSettings,
            bool original)
        {
            _index = index;
            _name = name;
            _description = description;
            _previewSettings = previewSettings;
            _original = original;
        }

        internal void Build(
            RectTransform parent, float x, float y, float width, float height, bool prominent)
        {
            _root = VoiceUiKit.Rect("HudPresetHit", parent);
            PlaceTopLeft(_root, x, y, width, height);
            _border = _root.gameObject.AddComponent<Image>();
            _border.sprite = VoiceUiKit.Rounded();
            _border.type = Image.Type.Sliced;
            _border.color = SetupBorder;
            _border.raycastTarget = false;

            _visual = VoiceUiKit.Rect("HudPresetVisual", _root);
            _visual.Anchor(Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            _visual.offsetMin = new Vector2(1.5f, 1.5f);
            _visual.offsetMax = new Vector2(-1.5f, -1.5f);
            _surface = _visual.gameObject.AddComponent<Image>();
            _surface.sprite = VoiceUiKit.Rounded();
            _surface.type = Image.Type.Sliced;
            _surface.color = SetupSurfaceRaised;
            _surface.raycastTarget = false;

            float titleHeight = prominent ? 26f : 24f;
            float titleSize = prominent ? 20f : 18f;
            _title = AddText(_visual, _name, titleSize, VoiceUiKit.TextPrimary,
                TextAlignmentOptions.Center, FontStyles.Bold,
                new Vector2(10f, -(height - titleHeight) * 0.5f),
                new Vector2(width - 20f, titleHeight));
            _title.enableAutoSizing = true;
            _title.fontSizeMax = titleSize;
            _title.fontSizeMin = 15f;
        }

        internal void Tick(float dt)
        {
            if (_draft == null || _root == null) return;
            bool over = VoiceUiKit.Contains(_root);
            bool selected = _original
                ? _draft.OriginalHudSelected
                : !_draft.OriginalHudSelected && _draft.SelectedHudPreset == _index;
            if (over)
            {
                _hoveredHudSettings = _previewSettings;
                _hoveredHudDescription = _description;
                VoiceUiKit.RequestTooltip(_name, _description, plainTextDescription: true);
            }
            if (over && Time.frameCount > _inputLockedUntilFrame && Input.GetMouseButtonDown(0))
            {
                if (_original) _draft.SelectOriginalHud();
                else _draft.SelectHudPreset(_index);
                selected = true;
                VoiceUiKit.SwallowClick();
            }

            _hover = Mathf.MoveTowards(_hover, over ? 1f : 0f, dt * 10f);
            _selected = Mathf.MoveTowards(_selected, selected ? 1f : 0f, dt * 8f);
            _visual.anchoredPosition = new Vector2(0f, _hover * 1.5f);
            _surface.color = VoiceUiKit.Lerp(
                SetupSurfaceRaised,
                VoiceUiKit.Lerp(SetupSurfaceHover, new Color32(20, 49, 59, 255), _selected),
                Mathf.Max(_hover, _selected));
            _border.color = VoiceUiKit.Lerp(
                SetupBorder,
                VoiceUiKit.Lerp(new Color32(102, 122, 148, 180), VoiceUiKit.Accent, _selected),
                Mathf.Max(_hover, _selected));
            _title.color = VoiceUiKit.Lerp(VoiceUiKit.TextPrimary, VoiceUiKit.TextBright,
                Mathf.Max(_hover, _selected));
        }

    }

    private sealed class CelebrationDot
    {
        private RectTransform _root = null!;
        private Image _image = null!;
        private Vector2 _base;
        private float _phase;

        internal void Build(RectTransform parent, Vector2 position, int index)
        {
            _base = position;
            _phase = index * 0.61f;
            _root = VoiceUiKit.Rect("CelebrationDot", parent);
            _root.Anchor(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f));
            float size = 5f + index % 4 * 2f;
            _root.sizeDelta = new Vector2(size, size);
            _root.anchoredPosition = position;
            _image = _root.gameObject.AddComponent<Image>();
            _image.sprite = VoiceUiKit.Rounded(true);
            _image.type = Image.Type.Sliced;
            _image.color = index % 3 == 0 ? VoiceUiKit.TextBright : VoiceUiKit.Accent;
            _image.raycastTarget = false;
        }

        internal void Tick(float dt)
        {
            _phase += dt * 1.8f;
            _root.anchoredPosition = _base + new Vector2(
                Mathf.Sin(_phase) * 5f,
                Mathf.Cos(_phase * 0.83f) * 7f);
            float alpha = 0.35f + (Mathf.Sin(_phase * 1.4f) + 1f) * 0.325f;
            var c = _image.color;
            c.a = alpha;
            _image.color = c;
        }
    }
}
