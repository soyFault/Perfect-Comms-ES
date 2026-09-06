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
    public ToggleHolder GhostsHearEachOtherUnlimited { get; }
    public ToggleHolder OnlyMeetingOrLobby { get; }
    public ToggleHolder OnlyMeetingOrLobbyAffectsGhosts { get; }
    public ToggleHolder GracePeriodEnabled { get; }
    public NumberHolder GracePeriodSeconds { get; }

    private VoiceChatGameOptions(ConfigFile cfg)
    {
        PublicVoiceLobby = new ToggleHolder(cfg, Section, "PublicVoiceLobby", "Sala de Voz pública", false,
            "Publica este lobby con chat de voz en el directorio de Perfect Comms para que otros jugadores puedan encontrarlo.");
        MaxChatDistance = new NumberHolder(cfg, Section, "MaxChatDistance", "Distancia Máxima", 6f, 1.5f, 20f, 0.5f, "0.0",
            "Establece la distancia máxima a la que los jugadores cercanos pueden escucharse durante las tareas.");
        FalloffMode = new EnumHolder(cfg, Section, "FalloffMode", "Atenuación de Voz",
            (int)VoiceFalloffMode.Smooth, typeof(VoiceFalloffMode),
            new[] { "Lineal", "Suave", "Enfocado en la voz" },
            "Elige cómo disminuye el volumen de voz al acercarse a la distancia máxima de escucha.");
        OcclusionMode = new EnumHolder(cfg, Section, "OcclusionMode", "Oclusión de Voz",
            (int)VoiceOcclusionMode.VisionOnly, typeof(VoiceOcclusionMode),
            new[] { "Desactivado", "Amortiguación suave", "Atenuación suave", "Bloqueo total", "Solo visión" },
            "Elige cómo afectan las paredes y la pérdida de línea de visión al audio de voz cercano durante las tareas.");
        WallsBlockSound = new ToggleHolder(cfg, Section, "WallsBlockSound", "Paredes Bloquean el Audio", true,
            "Permite que los muros del mapa obstruyan las voces según el modo de Oclusión de voz seleccionado.");
        OnlyHearInSight = new ToggleHolder(cfg, Section, "OnlyHearInSight", "Escuchar Solo en el Campo de Visión", true,
            "Limita las voces durante las tareas a los jugadores dentro de tu rango de visión.");
        ImpostorHearGhosts = new ToggleHolder(cfg, Section, "ImpostorHearGhosts", "Impostores Escuchan a los Muertos", false,
            "Permite que los impostores vivos escuchen a los jugadores muertos cuando las demás reglas de voz de fantasmas les permitan hablar.");
        HearInVent = new ToggleHolder(cfg, Section, "HearInVent", "Escuchar Impostores en los Ductos", false,
            "Permite que los jugadores cercanos escuchen a un impostor que está actualmente dentro de un Ducto");
        VentPrivateChat = new ToggleHolder(cfg, Section, "VentPrivateChat", "Conversación Privada en los Ductos", true,
            "Evita que los jugadores fuera de los ductos escuchen a un jugador que está actualmente en un ducto, manteniendo la conversación en los ductos privada.");
        CommsSabDisables = new ToggleHolder(cfg, Section, "CommsSabDisables", "Sabotaje de Comunicaciones Desactiva la Voz", true,
            "Desactiva la comunicación por voz mientras el sabotaje de comunicaciones esté activo.");
        CameraCanHear = new ToggleHolder(cfg, Section, "CameraCanHear", "Escuchar mediante cámaras", true,
             "Permite que quien use las cámaras de seguridad escuche las voces cercanas a la cámara activa.");
        TeamRadio = new ToggleHolder(cfg, Section, "TeamRadio", "Radio de equipo", true,
            "Activa canales de radio privados de pulsar para hablar para los equipos y roles elegibles.");
        TeamRadioImpostors = new ToggleHolder(cfg, Section, "TeamRadioImpostors", "Radio de equipo - Impostores", true,
            "Activa el canal privado de radio de los impostores cuando Radio de equipo está activada.")
        {
            Visible = TeamRadioSubOptionsVisible
        };
        TeamRadioInMeetings = new ToggleHolder(cfg, Section, "TeamRadioInMeetings", "Radio de equipo - Disponible en reuniones", false,
            "Permite que los jugadores elegibles usen la radio de equipo durante las reuniones.")
        {
            Visible = TeamRadioSubOptionsVisible
        };
        TeamRadioInTasks = new ToggleHolder(cfg, Section, "TeamRadioInTasks", "Radio de equipo - Disponible durante tareas", true,
            "Permite que los jugadores elegibles usen la radio de equipo durante las tareas.")
        {
            Visible = TeamRadioInMeetingsVisible
        };
        OnlyGhostsCanTalk = new ToggleHolder(cfg, Section, "OnlyGhostsCanTalk", "Solo los fantasmas pueden hablar/escuchar", false,
            "Limita la comunicación por voz durante las tareas únicamente a los jugadores muertos.");
        GhostsHearEachOtherUnlimited = new ToggleHolder(cfg, Section, "GhostsHearEachOtherUnlimited", "Los fantasmas se escuchan desde cualquier lugar", false,
            "Permite que los jugadores muertos se escuchen por todo el mapa sin importar la distancia.");
        OnlyMeetingOrLobby = new ToggleHolder(cfg, Section, "OnlyMeetingOrLobby", "Solo reuniones/lobby", false,
            "Desactiva la voz de los jugadores vivos durante las tareas, dejándola disponible solo en el lobby y las reuniones.");
        OnlyMeetingOrLobbyAffectsGhosts = new ToggleHolder(cfg, Section, "OnlyMeetingOrLobbyAffectsGhosts", "También para fantasmas", false,
            "Aplica Solo reuniones/lobby a los jugadores muertos además de los vivos.")
        {
            Visible = MeetingLobbySubOptionsVisible
        };
        GracePeriodEnabled = new ToggleHolder(cfg, Section, "GracePeriodEnabled", "Periodo de gracia al iniciar reunión", false,
            "Da al jugador que convocó la reunión el uso exclusivo de la voz durante unos segundos al comenzar.");
        GracePeriodSeconds = new NumberHolder(cfg, Section, "GracePeriodSeconds", "Duración del periodo de gracia", 5f, 0f, 15f, 1f, "0",
            "Establece cuántos segundos dura el periodo de gracia después de convocar una reunión.")
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

    private static bool GracePeriodSubOptionVisible() => Instance.GracePeriodEnabled.Value;
}
