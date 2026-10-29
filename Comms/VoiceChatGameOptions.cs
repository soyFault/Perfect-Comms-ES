using BepInEx.Configuration;

namespace VoiceChatPlugin.VoiceChat;

public class VoiceChatGameOptions
{
    public const string GroupName = "Perfect Comms";
    public const uint GroupPriority = 1000;
    private const string Section = "Host.VoiceChat";

    public ToggleHolder PublicVoiceLobby { get; }
    public NumberHolder MaxChatDistance { get; }
    public EnumHolder FalloffMode { get; }
    public EnumHolder OcclusionMode { get; }
    public ToggleHolder WallsBlockSound { get; }
    public ToggleHolder OnlyHearInSight { get; }
    public ToggleHolder ImpostorHearGhosts { get; }
    public ToggleHolder HearInVent { get; }
    public ToggleHolder VentPrivateChat { get; }
    public ToggleHolder CommsSabDisables { get; }
    public ToggleHolder CameraCanHear { get; }
    public ToggleHolder TeamRadio { get; }
    public ToggleHolder TeamRadioImpostors { get; }
    public ToggleHolder TeamRadioInMeetings { get; }
    public ToggleHolder TeamRadioInTasks { get; }
    public ToggleHolder OnlyGhostsCanTalk { get; }
    public ToggleHolder GhostsHearLivingDuringTasks { get; }
    public ToggleHolder GhostsHearEachOtherUnlimited { get; }
    public ToggleHolder OnlyMeetingOrLobby { get; }
    public ToggleHolder OnlyMeetingOrLobbyAffectsGhosts { get; }
    public ToggleHolder MeetingOnlyImpostorChat { get; }
    public ToggleHolder ImpostorsTalkAcrossDeath { get; }
    public ToggleHolder ImpostorsTalkAcrossDeathInMeetings { get; }
    public ToggleHolder GracePeriodEnabled { get; }
    public NumberHolder GracePeriodSeconds { get; }

    private VoiceChatGameOptions(ConfigFile cfg)
    {
        PublicVoiceLobby = new ToggleHolder(cfg, Section, "PublicVoiceLobby", Loc.L("host.public_voice_lobby", "Public Voice Lobby"), false,
            Loc.L("host.public_voice_lobby_help", "Publishes this voice-enabled lobby to the Perfect Comms live directory so other Perfect Comms players can find it."));
        MaxChatDistance = new NumberHolder(cfg, Section, "MaxChatDistance", Loc.L("host.max_chat_distance", "Max Distance"), 6f, 1.5f, 20f, 0.5f, "0.0",
            Loc.L("host.max_chat_distance_help", "Sets the maximum task-phase distance at which nearby players can hear one another."));
        FalloffMode = new EnumHolder(cfg, Section, "FalloffMode", Loc.L("host.falloff_mode", "Voice Falloff"),
            (int)VoiceFalloffMode.Smooth, typeof(VoiceFalloffMode),
            new[] { Loc.L("host.falloff_mode_linear", "Linear"), Loc.L("host.falloff_mode_smooth", "Smooth"), Loc.L("host.falloff_mode_voice_focused", "Voice Focused") },
            Loc.L("host.falloff_mode_help", "Chooses how voice volume fades as a player approaches the maximum hearing distance."));
        OcclusionMode = new EnumHolder(cfg, Section, "OcclusionMode", Loc.L("host.occlusion_mode", "Voice Occlusion"),
            (int)VoiceOcclusionMode.VisionOnly, typeof(VoiceOcclusionMode),
            new[] { Loc.L("host.occlusion_mode_off", "Off"), Loc.L("host.occlusion_mode_soft_muffle", "Soft Muffle"), Loc.L("host.occlusion_mode_soft_fade", "Soft Fade"), Loc.L("host.occlusion_mode_hard_block", "Hard Block"), Loc.L("host.occlusion_mode_vision_only", "Vision Only") },
            Loc.L("host.occlusion_mode_help", "Chooses how walls and lost line of sight affect nearby voice audio during tasks."));
        WallsBlockSound = new ToggleHolder(cfg, Section, "WallsBlockSound", Loc.L("host.walls_block_sound", "Walls Block Audio"), true,
            Loc.L("host.walls_block_sound_help", "Lets map walls obstruct voice audio according to the selected Voice Occlusion mode."));
        OnlyHearInSight = new ToggleHolder(cfg, Section, "OnlyHearInSight", Loc.L("host.only_hear_in_sight", "Hear People in Vision Only"), true,
            Loc.L("host.only_hear_in_sight_help", "Restricts normal task-phase voice to players inside the listener's current vision range."));
        ImpostorHearGhosts = new ToggleHolder(cfg, Section, "ImpostorHearGhosts", Loc.L("host.impostor_hear_ghosts", "Impostors Hear Dead"), false,
            Loc.L("host.impostor_hear_ghosts_help", "Allows living impostors to hear dead players when the other ghost voice rules permit them to speak."));
        HearInVent = new ToggleHolder(cfg, Section, "HearInVent", Loc.L("host.hear_in_vent", "Hear Impostors in Vents"), false,
            Loc.L("host.hear_in_vent_help", "Allows nearby players to hear an impostor who is currently inside a vent."));
        VentPrivateChat = new ToggleHolder(cfg, Section, "VentPrivateChat", Loc.L("host.vent_private_chat", "Private Talk in Vents"), true,
            Loc.L("host.vent_private_chat_help", "Prevents players outside vents from hearing a player who is currently vented, keeping vent speech private."));
        CommsSabDisables = new ToggleHolder(cfg, Section, "CommsSabDisables", Loc.L("host.comms_sab_disables", "Comms Sabotage Disables Voice"), true,
            Loc.L("host.comms_sab_disables_help", "Disables normal voice communication while the Communications sabotage is active."));
        CameraCanHear = new ToggleHolder(cfg, Section, "CameraCanHear", Loc.L("host.camera_can_hear", "Hear Through Cameras"), true,
            Loc.L("host.camera_can_hear_help", "Lets a player using security cameras hear nearby voice around the active camera position."));
        TeamRadio = new ToggleHolder(cfg, Section, "TeamRadio", Loc.L("host.team_radio", "Team Radio"), true,
            Loc.L("host.team_radio_help", "Enables private hold-to-talk radio channels for eligible teams and roles."));
        TeamRadioImpostors = new ToggleHolder(cfg, Section, "TeamRadioImpostors", Loc.L("host.team_radio_impostors", "Team Radio - Impostors"), true,
            Loc.L("host.team_radio_impostors_help", "Enables the private impostor team-radio channel when Team Radio is on."))
        {
            Visible = TeamRadioSubOptionsVisible
        };
        TeamRadioInMeetings = new ToggleHolder(cfg, Section, "TeamRadioInMeetings", Loc.L("host.team_radio_in_meetings", "Team Radio - Usable in Meetings"), false,
            Loc.L("host.team_radio_in_meetings_help", "Allows eligible players to use team radio during meetings."))
        {
            Visible = TeamRadioSubOptionsVisible
        };
        TeamRadioInTasks = new ToggleHolder(cfg, Section, "TeamRadioInTasks", Loc.L("host.team_radio_in_tasks", "Team Radio - Usable in Tasks Phase"), true,
            Loc.L("host.team_radio_in_tasks_help", "Allows eligible players to use team radio during normal task gameplay."))
        {
            Visible = TeamRadioInMeetingsVisible
        };
        OnlyGhostsCanTalk = new ToggleHolder(cfg, Section, "OnlyGhostsCanTalk", Loc.L("host.only_ghosts_can_talk", "Only Ghosts can Talk/Hear"), false,
            Loc.L("host.only_ghosts_can_talk_help", "Restricts task-phase voice communication to dead players only."));
        GhostsHearLivingDuringTasks = new ToggleHolder(cfg, Section, "GhostsHearLivingDuringTasks", Loc.L("host.ghosts_hear_living_during_tasks", "Ghosts Hear Living During Tasks"), false,
            Loc.L("host.ghosts_hear_living_during_tasks_help", "During Tasks only, ghosts hear nearby living players despite Meetings/Lobby Only or Only Ghosts can Talk/Hear. Does not enable living-to-living speech. Normal ghost range, falloff and vent privacy apply. Ghosts Also Meeting/Lobby Only takes priority while Meetings/Lobby Only is on. Mute, push-to-talk, deafen and host role mutes still apply."))
        {
            Visible = GhostsHearLivingVisible
        };
        GhostsHearEachOtherUnlimited = new ToggleHolder(cfg, Section, "GhostsHearEachOtherUnlimited", Loc.L("host.ghosts_hear_each_other_unlimited", "Ghosts Hear Each Other Anywhere"), false,
            Loc.L("host.ghosts_hear_each_other_unlimited_help", "Lets dead players hear one another across the entire map instead of using proximity distance."));
        OnlyMeetingOrLobby = new ToggleHolder(cfg, Section, "OnlyMeetingOrLobby", Loc.L("host.only_meeting_or_lobby", "Meetings/Lobby Only"), false,
            Loc.L("host.only_meeting_or_lobby_help", "Disables living-player voice during tasks so normal voice is available only in lobbies and meetings."));
        OnlyMeetingOrLobbyAffectsGhosts = new ToggleHolder(cfg, Section, "OnlyMeetingOrLobbyAffectsGhosts", Loc.L("host.only_meeting_or_lobby_affects_ghosts", "Ghosts Also Meeting/Lobby Only"), false,
            Loc.L("host.only_meeting_or_lobby_affects_ghosts_help", "Applies Meetings/Lobby Only to dead players as well as living players."))
        {
            Visible = MeetingLobbySubOptionsVisible
        };
        MeetingOnlyImpostorChat = new ToggleHolder(cfg, Section, "MeetingOnlyImpostorChat", Loc.L("host.meeting_only_impostor_chat", "Meeting Only - Impostor Chat"), false,
            Loc.L("host.meeting_only_impostor_chat_help", "During tasks, living actual impostors can speak globally. All ghosts and spectators can listen; dead impostors can speak only with Impostors Talk Across Death. Living crew and other role voice privileges do not gain access. Normal meeting rules apply unless Impostors Talk Across Death in Meetings enables radio-held chat."))
        {
            Visible = MeetingLobbySubOptionsVisible
        };
        ImpostorsTalkAcrossDeath = new ToggleHolder(cfg, Section, "ImpostorsTalkAcrossDeath", Loc.L("host.impostors_talk_across_death", "Impostors Talk Across Death"), false,
            Loc.L("host.impostors_talk_across_death_help", "Lets dead impostors talk back to living impostors in task-phase impostor chat and impostor team radio. Crew ghosts and spectators remain listen-only. The In Meetings child option also allows radio-held private chat during meetings."));
        ImpostorsTalkAcrossDeathInMeetings = new ToggleHolder(cfg, Section, "ImpostorsTalkAcrossDeathInMeetings", Loc.L("host.impostors_talk_across_death_in_meetings", "Impostors Talk Across Death in Meetings"), true,
            Loc.L("host.impostors_talk_across_death_in_meetings_help", "While Impostors Talk Across Death is on, living and dead actual impostors can hold the radio key to speak privately during meetings, not exile. Living actual impostors, all ghosts and spectators listen; living crew are excluded. Crew ghosts and spectators cannot transmit. Works independently of Team Radio and Meeting Only - Impostor Chat; other mute and meeting-floor rules still apply."))
        {
            Visible = ImpostorsTalkAcrossDeathSubOptionsVisible
        };
        GracePeriodEnabled = new ToggleHolder(cfg, Section, "GracePeriodEnabled", Loc.L("host.grace_period_enabled", "Meeting Floor Grace Period"), false,
            Loc.L("host.grace_period_enabled_help", "Gives the player who called the meeting an exclusive voice floor for a short time when the meeting begins."));
        GracePeriodSeconds = new NumberHolder(cfg, Section, "GracePeriodSeconds", Loc.L("host.grace_period_seconds", "Grace Period Seconds"), 5f, 0f, 15f, 1f, "0",
            Loc.L("host.grace_period_seconds_help", "Sets how many seconds the meeting-floor grace period lasts after a meeting is called."))
        {
            Visible = GracePeriodSubOptionVisible
        };
    }

    private static VoiceChatGameOptions? _instance;
    public static VoiceChatGameOptions Instance => _instance ??= new VoiceChatGameOptions(VoiceChatPluginMain.PluginConfig);
    internal static VoiceChatGameOptions GetInstance() => Instance;

    private static bool TeamRadioSubOptionsVisible() => Instance.TeamRadio.Value;

    private static bool TeamRadioInMeetingsVisible() =>
        Instance.TeamRadio.Value && Instance.TeamRadioInMeetings.Value;

    private static bool MeetingLobbySubOptionsVisible() => Instance.OnlyMeetingOrLobby.Value;

    private static bool GhostsHearLivingVisible() => VoiceGhostListeningPolicy.CanApply(
        Instance.OnlyMeetingOrLobby.Value, Instance.OnlyGhostsCanTalk.Value,
        Instance.OnlyMeetingOrLobbyAffectsGhosts.Value);

    private static bool ImpostorsTalkAcrossDeathSubOptionsVisible() => Instance.ImpostorsTalkAcrossDeath.Value;

    private static bool GracePeriodSubOptionVisible() => Instance.GracePeriodEnabled.Value;
}
