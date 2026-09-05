using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

#if ANDROID
using System.Collections;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
#endif

namespace VoiceChatPlugin.VoiceChat;

/// <summary>Pure decisions shared by the runtime preview and deterministic tests.</summary>
internal static class FirstRunOutputPreviewPolicy
{
    internal static bool ShouldTryDefaultFallback(string? requestedDevice, bool alreadyAttempted)
        => !alreadyAttempted && !string.IsNullOrEmpty(requestedDevice);

    internal static bool IsTonePlaybackTerminal(string? state)
        => string.Equals(state, "error", StringComparison.Ordinal) ||
           string.Equals(state, "stopped", StringComparison.Ordinal);

    internal static bool ShouldApplyTonePlaybackTerminal(
        long activeGeneration,
        ulong eventGeneration,
        string? state,
        bool correlatedTerminalPending = false,
        long correlatedTerminalGeneration = 0)
        => IsTonePlaybackTerminal(state) &&
           ((activeGeneration != 0 &&
             eventGeneration == unchecked((ulong)activeGeneration)) ||
            (correlatedTerminalPending &&
             correlatedTerminalGeneration != 0 &&
             eventGeneration == unchecked((ulong)correlatedTerminalGeneration)));

    internal static bool RequestedOutputMatches(
        string? pendingDevice,
        string? eventDevice,
        bool eventRequestedDefault)
        => string.IsNullOrEmpty(pendingDevice)
            ? eventRequestedDefault || string.IsNullOrEmpty(eventDevice)
            : string.Equals(pendingDevice, eventDevice, StringComparison.Ordinal);
    internal static bool CanReuseConfirmedOutput(
        bool commandChanged,
        bool running,
        string? pendingDevice,
        string? confirmedDevice,
        ulong confirmedGeneration)
        => !commandChanged &&
           running &&
           confirmedGeneration != 0 &&
           string.Equals(
               pendingDevice ?? string.Empty,
               confirmedDevice ?? string.Empty,
               StringComparison.Ordinal);
#if WINDOWS
    internal static (string Device, ulong Generation) ApplyPlaybackConfirmation(
        SidecarPlaybackState state,
        string confirmedDevice,
        ulong confirmedGeneration)
    {
        if (state.State == "command-accepted" && state.Changed)
            return (string.Empty, 0);
        if (state.State == "first-callback" && state.Running)
            return (
                state.FellBackToDefault || state.RequestedDefault
                    ? string.Empty
                    : state.RequestedDevice ?? string.Empty,
                state.StreamGeneration);
        if (IsTonePlaybackTerminal(state.State) &&
            (state.StreamGeneration == 0 || state.StreamGeneration == confirmedGeneration))
            return (string.Empty, 0);
        return (confirmedDevice, confirmedGeneration);
    }
#endif


    internal static string DescribeNativeOutputFailure(
        string? reason,
        string? errorCode = null,
        bool duringPlayback = false)
    {
        string detail = SanitizeNativeReason(reason);
        string code = (errorCode ?? string.Empty).Trim().ToLowerInvariant();
        if (code == "device-unavailable")
            return "Ese altavoz ya no está disponible. Reconéctalo, selecciónalo de nuevo o usa Predeterminado.";
        if (code == "device-busy")
            return "Ese altavoz está ocupado en otra aplicación. Cierra la otra sesión de audio o usa Predeterminado.";
        if (code == "permission-denied")
            return "El sistema denegó el acceso a ese altavoz. Revisa los permisos de audio o usa Predeterminado..";
        if (code == "unsupported-config")
            return detail.Length == 0
                ? "Ese altavoz rechazó todos los formatos de reproducción compatibles."
                : "Ese altavoz rechazó el formato de reproducción: " + detail;
        if (code == "timeout")
            return "El altavoz no respondió a tiempo. Reconéctalo o usa Predeterminado.";
        if (code == "stream-error")
            return duringPlayback
                ? "El altavoz seleccionado dejó de responder durante la prueba. Reconéctalo o usa Predeterminado."
                : "El altavoz seleccionado dejó de responder. Reconéctalo o usa Predeterminado.";
        if (detail.Length == 0)
            return duringPlayback
                ? "El altavoz seleccionado se detuvo durante la prueba"
                : "No se pudo abrir el altavoz seleccionado";

        string lower = detail.ToLowerInvariant();
        if (lower.Contains("device unavailable", StringComparison.Ordinal) ||
            lower.Contains("device is unavailable", StringComparison.Ordinal) ||
            lower.Contains("selected output device is unavailable", StringComparison.Ordinal) ||
            lower.Contains("no output device", StringComparison.Ordinal))
            return "Ese altavoz ya no está disponible. Reconéctalo, selecciónalo de nuevo o usa Predeterminado.";
        if (lower.Contains("unsupported default output sample format", StringComparison.Ordinal) ||
            lower.Contains("default output config", StringComparison.Ordinal))
            return "Ese altavoz no expone un formato de reproducción compatible: " + detail;
        if (lower.Contains("build output stream", StringComparison.Ordinal))
            return "El sistema no pudo abrir ese altavoz: " + TrimKnownPrefix(detail, "build output stream:");
        if (lower.Contains("output stream play", StringComparison.Ordinal))
            return "El altavoz se abrió, pero no se pudo iniciar la reproducción: " + TrimKnownPrefix(detail, "output stream play:");
        if (lower.Contains("output device callback failed", StringComparison.Ordinal))
            return "El altavoz dejó de responder después de comenzar la reproducción. Reconéctalo o usa Predeterminado.";
        return duringPlayback
            ? "El altavoz seleccionado se detuvo durante la prueba: " + detail
            : "No se pudo abrir el altavoz seleccionado: " + detail;
    }

    private static string TrimKnownPrefix(string value, string prefix)
    {
        int index = value.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return value;
        string trimmed = value[(index + prefix.Length)..].Trim();
        return trimmed.Length == 0 ? value : trimmed;
    }

    private static string SanitizeNativeReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) return string.Empty;
        const string stderrPrefix = "pc-capture: playback error:";
        string source = reason.Trim();
        if (source.StartsWith(stderrPrefix, StringComparison.OrdinalIgnoreCase))
            source = source[stderrPrefix.Length..].Trim();

        var builder = new StringBuilder(Math.Min(source.Length, 180));
        bool previousSpace = false;
        for (int i = 0; i < source.Length && builder.Length < 180; i++)
        {
            char c = source[i];
            bool space = char.IsWhiteSpace(c) || char.IsControl(c);
            if (space)
            {
                if (!previousSpace && builder.Length > 0) builder.Append(' ');
                previousSpace = true;
                continue;
            }
            builder.Append(c == '"' ? '\'' : c);
            previousSpace = false;
        }
        return builder.ToString().Trim();
    }
}

#if WINDOWS
internal interface IFirstRunDesktopAudioLease : IDisposable
{
    bool EnsureStarted(string microphoneDevice, string speakerDevice);
    void SetDsp(bool echoCancellation, bool noiseSuppression, bool strongerNoiseSuppression);
    void SetInput(float gain, float vadThreshold, float noiseGateThreshold);
    void SetMonitor(bool enabled, bool delayed, float gain);
    bool ConfigureAudioRoute(
        string inputDevice,
        string outputDevice,
        SidecarCaptureMode captureMode);
    void SendOutputTestFrame(float[] interleavedStereo);
    bool SendOutputTestFrameAndWait(float[] interleavedStereo);
    Task ReleaseAsync();
}

internal interface IFirstRunDesktopAudioHost
{
    IFirstRunDesktopAudioLease? TryAcquire(
        SidecarVoiceCallbacks callbacks,
        out string failure);
}

internal sealed class FirstRunDesktopAudioHost : IFirstRunDesktopAudioHost
{
    internal static readonly FirstRunDesktopAudioHost Instance = new();

    public IFirstRunDesktopAudioLease? TryAcquire(
        SidecarVoiceCallbacks callbacks,
        out string failure)
    {
        var lease = SidecarVoiceHost.TryAcquire(callbacks, out failure);
        return lease == null ? null : new FirstRunDesktopAudioLease(lease);
    }
}

internal sealed class FirstRunDesktopAudioLease : IFirstRunDesktopAudioLease
{
    private readonly SidecarVoiceLease _lease;

    internal FirstRunDesktopAudioLease(SidecarVoiceLease lease)
    {
        _lease = lease;
    }

    public bool EnsureStarted(string microphoneDevice, string speakerDevice)
        => _lease.EnsureStarted(microphoneDevice, speakerDevice);

    public void SetDsp(bool echoCancellation, bool noiseSuppression, bool strongerNoiseSuppression)
        => _lease.SetDsp(
            aec: echoCancellation,
            agc: false,
            ns: noiseSuppression,
            nsVeryHigh: strongerNoiseSuppression,
            hpf: true);

    public void SetInput(float gain, float vadThreshold, float noiseGateThreshold)
        => _lease.SetInput(gain, vadThreshold, noiseGateThreshold);

    public void SetMonitor(bool enabled, bool delayed, float gain)
        => _lease.SetMonitor(enabled, delayed, gain);

    public bool ConfigureAudioRoute(
        string inputDevice,
        string outputDevice,
        SidecarCaptureMode captureMode)
        => _lease.ConfigureAudioRouteAndWait(
            inputDevice,
            outputDevice,
            captureMode,
            synthetic: false);

    public void SendOutputTestFrame(float[] interleavedStereo)
        => _lease.SendOutputTestFrame(interleavedStereo);

    public bool SendOutputTestFrameAndWait(float[] interleavedStereo)
        => _lease.SendOutputTestFrameAndWait(interleavedStereo);

    public Task ReleaseAsync()
        => _lease.ReleaseAsync();

    public void Dispose()
        => _lease.Dispose();
}
#endif

/// <summary>
/// Local-only setup probe. Desktop capture owns a peerless sidecar lease and never adds ICE or
/// peers; Android reads Unity's microphone directly. Output tests use the exact selected sidecar
/// output on desktop and Unity's current platform route on Android.
/// </summary>
internal sealed class FirstRunAudioPreview : IDisposable
{
    private enum MicrophoneRouteResolution
    {
        Ready,
        FellBackToDefault,
        WaitingForDeviceList,
    }

#if WINDOWS
    private enum DesktopFailureChannel
    {
        Microphone = 1,
        Output = 2,
    }
#endif

    private volatile float _level;
    private volatile bool _speaking;
    private readonly object _microphoneStateGate = new();
    private int _microphoneTestActive;
    private int _listening;
    private int _playingTone;
    private volatile string _microphoneStatus = "La prueba de micrófono está desactivada";
    private volatile string _outputStatus = "Aún no se ha reproducido el sonido de prueba";
    private long _lastLevelTick;
    private long _lastSignalTick;
    private CancellationTokenSource? _toneCancellation;
    private bool _disposed;
    private long _microphonePriorityStatusUntilTick;
    private long _legacyOutputStatusUntilTick;
    private int _toneGeneration;
    private int _uiRefreshPending;
    private int _microphoneSignalDetected;
    private int _outputTestCompleted;
    private bool _micPausedForTone;
    private bool _monitorPlayback;
    private bool _monitorDelayed;
    private float _monitorGain = 1f;
    private VoiceChatRoom? _monitorRoom;

#if WINDOWS
    private enum DesktopOperationKind
    {
        Microphone,
        Output,
        OutputFallback,
    }

    private readonly record struct DesktopConfiguration(
        string InputDevice,
        string OutputDevice,
        bool EchoCancellation,
        bool NoiseSuppression,
        bool StrongerNoiseSuppression,
        float InputGain,
        float VadThreshold,
        float NoiseGateThreshold,
        bool MonitorEnabled,
        bool MonitorDelayed,
        float MonitorGain,
        SidecarCaptureMode CaptureMode,
        bool InputFellBackToDefault);

    private readonly record struct DesktopOperation(
        int OperationGeneration,
        int LeaseGeneration,
        DesktopOperationKind Kind,
        DesktopConfiguration Configuration,
        SidecarVoiceCallbacks? Callbacks,
        IFirstRunDesktopAudioLease? ExistingLease,
        CancellationToken Cancellation);
    private sealed class DesktopLeaseRetirement
    {
        private readonly TaskCompletionSource<bool> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal DesktopLeaseRetirement(Task previous)
        {
            Previous = previous;
            Barrier = Task.WhenAll(previous, _completion.Task);
        }

        internal Task Previous { get; }
        internal Task Barrier { get; }

        internal void Complete()
            => _completion.TrySetResult(true);
    }


    private sealed class DesktopOperationCompletion
    {
        private IFirstRunDesktopAudioLease? _lease;
        private CancellationTokenRegistration _cancellationRegistration;
        private int _cancellationArmed;
        private readonly DesktopLeaseRetirement? _retirement;

        internal DesktopOperationCompletion(
            int operationGeneration,
            int leaseGeneration,
            DesktopOperationKind kind,
            DesktopConfiguration configuration,
            IFirstRunDesktopAudioLease? lease,
            bool acquiredLease,
            string failure,
            DesktopLeaseRetirement? retirement = null)
        {
            OperationGeneration = operationGeneration;
            LeaseGeneration = leaseGeneration;
            Kind = kind;
            Configuration = configuration;
            _lease = lease;
            AcquiredLease = acquiredLease;
            Failure = failure;
            _retirement = retirement;
        }

        internal int OperationGeneration { get; }
        internal int LeaseGeneration { get; }
        internal DesktopOperationKind Kind { get; }
        internal DesktopConfiguration Configuration { get; }
        internal bool AcquiredLease { get; }
        internal string Failure { get; }
        internal DesktopLeaseRetirement? Retirement => _retirement;

        internal IFirstRunDesktopAudioLease? TakeLease()
        {
            var lease = Interlocked.Exchange(ref _lease, null);
            if (Volatile.Read(ref _cancellationArmed) != 0)
                _cancellationRegistration.Unregister();
            return lease;
        }
        internal void CompleteRetirement()
            => _retirement?.Complete();


        internal void ReleaseOnCancellation(
            CancellationToken cancellation,
            Action<
                IFirstRunDesktopAudioLease,
                DesktopConfiguration,
                DesktopLeaseRetirement> release)
        {
            if (!AcquiredLease ||
                _retirement == null ||
                Volatile.Read(ref _lease) == null)
                return;
            _cancellationRegistration = cancellation.Register(() =>
            {
                var lease = Interlocked.Exchange(ref _lease, null);
                if (lease != null)
                    release(lease, Configuration, _retirement);
            });
            Volatile.Write(ref _cancellationArmed, 1);
            if (Volatile.Read(ref _lease) == null)
                _cancellationRegistration.Dispose();
        }
    }

    private readonly record struct DesktopPlaybackEvent(
        int LeaseGeneration,
        SidecarPlaybackState State);

    private readonly record struct DesktopToneCompletion(
        int LeaseGeneration,
        int ToneGeneration,
        bool Succeeded,
        bool UsedDefaultFallback);

    private readonly record struct DesktopFailureEvent(
        int LeaseGeneration,
        string Message,
        DesktopFailureChannel Channel);

    private readonly IFirstRunDesktopAudioHost _desktopHost;
    private readonly SemaphoreSlim _desktopAcquisitionGate = new(1, 1);
    private readonly SemaphoreSlim _desktopWorkerGate = new(1, 1);
    private readonly ConcurrentQueue<DesktopOperationCompletion> _desktopCompletions = new();
    private readonly ConcurrentQueue<DesktopPlaybackEvent> _playbackStates = new();
    private readonly ConcurrentQueue<DesktopToneCompletion> _toneCompletions = new();
    private readonly ConcurrentQueue<DesktopFailureEvent> _desktopFailures = new();
    private IFirstRunDesktopAudioLease? _lease;
    private CancellationTokenSource? _desktopOperationCancellation;
    private CancellationTokenSource? _desktopSettingsCancellation;
    private Task _desktopLeaseRelease = Task.CompletedTask;
    private readonly object _desktopLeaseReleaseGate = new();
    private readonly object _desktopToneStateGate = new();
    private DesktopConfiguration? _lastDesktopSettings;
    private int _desktopOperationGeneration;
    private int _desktopLeaseGeneration;
    private FirstRunSetupDraft? _pendingSpeakerDraft;
    private string _pendingOutputDevice = string.Empty;
    private ulong _pendingPlaybackGeneration;
    private long _outputReadyDeadlineTick;
    private int _waitingForOutput;
    private bool _desktopRouteAdmitted;
    private bool _outputSelectionAccepted;
    private string _confirmedOutputDevice = string.Empty;
    private ulong _confirmedPlaybackGeneration;
    private bool _outputFallbackAttempted;
    private long _activeTonePlaybackGeneration;
    private long _desktopToneTerminalGeneration;
    private int _desktopTonePlaybackTerminated;
    private int _desktopToneLeaseGeneration;
    private string _desktopInputDevice = string.Empty;
    private string _desktopOutputDevice = string.Empty;
#endif
#if ANDROID
    private AndroidMicrophone? _androidMicrophone;
    private GameObject? _toneObject;
    private AudioSource? _toneSource;
    private AudioClip? _toneClip;
    private AndroidMicrophoneMonitor? _androidMonitor;
    private AndroidMicrophoneMonitorOutput? _androidMonitorOutput;
    private int _permissionGeneration;
#endif

#if WINDOWS
    internal FirstRunAudioPreview()
        : this(FirstRunDesktopAudioHost.Instance)
    {
    }

    internal FirstRunAudioPreview(IFirstRunDesktopAudioHost desktopHost)
    {
        _desktopHost = desktopHost ?? throw new ArgumentNullException(nameof(desktopHost));
    }
#else
    internal FirstRunAudioPreview()
    {
    }
#endif

    internal float Level => float.IsFinite(_level) ? Math.Clamp(_level, 0f, 1f) : 0f;
    internal float LiveLevel
    {
        get
        {
            if (!IsListening) return 0f;
            long age = Environment.TickCount64 - Volatile.Read(ref _lastLevelTick);
            return age >= 0 && age <= 300 ? Level : 0f;
        }
    }
    internal bool IsMicrophoneTestActive => Volatile.Read(ref _microphoneTestActive) != 0;
    internal bool IsListening => Volatile.Read(ref _listening) != 0;
    internal bool IsPlayingTone => Volatile.Read(ref _playingTone) != 0;
    internal bool IsPreparingTone =>
#if WINDOWS
        Volatile.Read(ref _waitingForOutput) != 0;
#else
        false;
#endif
    internal bool IsMicrophoneTestStarting => IsMicrophoneTestActive && !IsListening;
    internal bool IsSpeakerTestBusy => IsPlayingTone || IsPreparingTone;
    internal bool ConsumeUiRefresh() => Interlocked.Exchange(ref _uiRefreshPending, 0) != 0;
    internal bool MicrophoneSignalDetected => Volatile.Read(ref _microphoneSignalDetected) != 0;
    internal bool OutputTestCompleted => Volatile.Read(ref _outputTestCompleted) != 0;
    internal string OutputStatus => _outputStatus;

    internal string MicrophoneStatus
    {
        get
        {
            long now = Environment.TickCount64;
            if (!IsListening || now < Volatile.Read(ref _microphonePriorityStatusUntilTick))
                return _microphoneStatus;
            long signalSilentFor = now - Volatile.Read(ref _lastSignalTick);
            long callbackSilentFor = now - Volatile.Read(ref _lastLevelTick);
            if (signalSilentFor > 2500 || callbackSilentFor > 2500)
                return "No se detectó señal del micrófono";
            float level = Level;
            if (level >= 0.90f) return "Muy alto - baja el volumen del micrófono";
            if (level >= 0.035f || _speaking) return "Genial - tu micrófono funciona";
            return "Escuchando - habla con normalidad";
        }
    }

    /// <summary>
    /// Compatibility projection for the existing single-status setup UI. New UI should bind to
    /// MicrophoneStatus and OutputStatus separately.
    /// </summary>
    internal string Status =>
        IsSpeakerTestBusy || Environment.TickCount64 < Volatile.Read(ref _legacyOutputStatusUntilTick)
            ? OutputStatus
            : MicrophoneStatus;

    internal void InvalidateMicrophoneVerification()
    {
        lock (_microphoneStateGate)
        {
            Interlocked.Exchange(ref _microphoneSignalDetected, 0);
            _level = 0f;
            _speaking = false;
            SetMicrophoneStatus(IsMicrophoneTestActive
                ? "Reiniciando la prueba del micrófono para la entrada seleccionada...."
                : "Se necesita probar el micrófono para la entrada seleccionada");
        }
    }

    internal void InvalidateOutputVerification()
    {
        Interlocked.Exchange(ref _outputTestCompleted, 0);
        SetOutputStatus("Prueba la salida seleccionada para verificarla");
    }

    internal void Tick()
    {
        if (_monitorRoom != null)
        {
            var room = _monitorRoom;
            if (!ReferenceEquals(room, VoiceChatRoom.Current))
            {
                try { room.SetLoopBack(false); } catch { }
                _monitorRoom = null;
                FailMicrophone("La sala de voz cambió - reinicia la prueba del micrófono");
            }
            else
            {
                if (!room.SetLoopBack(true, _monitorDelayed, _monitorGain))
                {
                    _monitorRoom = null;
                    FailMicrophone("El audio de la sala de voz se detuvo - reinicia la prueba del micrófono");
                }
                else
                {
                    _level = Math.Clamp(room.LocalMicLevel, 0f, 1f);
                    _speaking = _level >= 0.035f;
                    long now = Environment.TickCount64;
                    Volatile.Write(ref _lastLevelTick, now);
                    if (_speaking) Volatile.Write(ref _lastSignalTick, now);
                }
            }
        }
#if WINDOWS
        TickDesktopCompletions();
        while (_desktopFailures.TryDequeue(out var failure))
        {
            if (failure.LeaseGeneration != Volatile.Read(ref _desktopLeaseGeneration))
                continue;
            bool affectedOutput = IsSpeakerTestBusy;
            if (failure.Channel == DesktopFailureChannel.Output)
                FailOutput(failure.Message);
            else
            {
                FailMicrophone(failure.Message);
                if (affectedOutput)
                    FailOutput("El asistente de audio se detuvo durante la prueba de salida");
            }
            StopDesktopLease();
            Interlocked.Exchange(ref _uiRefreshPending, 1);
            break;
        }
        TickDesktopOutputReadiness();
        TickDesktopToneCompletions();
#endif
#if ANDROID
        _androidMicrophone?.Tick();
        if (_toneSource != null && !_toneSource.isPlaying && IsPlayingTone)
        {
            Volatile.Write(ref _playingTone, 0);
            CleanupAndroidTone();
            CompleteOutputTest();
        }
#endif
    }

    private bool TryStartRoomMonitor()
    {
        if (!_monitorPlayback || VoiceChatRoom.Current is not { } room) return false;
        if (!room.SetLoopBack(true, _monitorDelayed, _monitorGain)) return false;
        _monitorRoom = room;
        MarkListening();
        SetMicrophoneStatus(_monitorDelayed
            ? "La prueba del micrófono está activa con un segundo de retraso"
            : "La prueba del micrófono está activa - usa audífonos para evitar acoples");
        return true;
    }

    internal void StartMicrophone(
        FirstRunSetupDraft draft,
        bool monitorPlayback = false,
        bool delayedPlayback = false)
    {
        if (_disposed) return;
        StopMicrophone();
        _monitorPlayback = monitorPlayback;
        _monitorDelayed = monitorPlayback && delayedPlayback;
        _monitorGain = float.IsFinite(draft.MasterVolume)
            ? Math.Clamp(draft.MasterVolume, 0f, 2f)
            : 1f;
        if (_monitorPlayback && VoiceChatRoom.Current != null)
        {
#if ANDROID
            if (MicrophoneTestLifecyclePolicy.RequiresRoomMicrophonePermission(
                    _monitorPlayback,
                    roomPresent: true,
                    Application.HasUserAuthorization(UserAuthorization.Microphone)))
            {
                lock (_microphoneStateGate)
                {
                    Volatile.Write(ref _microphoneTestActive, 1);
                    Volatile.Write(ref _listening, 0);
                    _micPausedForTone = false;
                    SetMicrophoneStatus("Waiting for microphone permission...");
                }
                int roomPermissionGeneration = ++_permissionGeneration;
                FirstRunSetupPermissionRequester.Request(granted =>
                {
                    if (_disposed || roomPermissionGeneration != _permissionGeneration) return;
                    if (!granted)
                    {
                        FailMicrophone("Microphone permission denied - receive-only still works");
                        return;
                    }
                    if (!TryStartRoomMonitor())
                        FailMicrophone("Voice room changed - restart the microphone test");
                });
                return;
            }
#endif
            if (TryStartRoomMonitor()) return;
            FailMicrophone("No se pudo iniciar la reproducción del micrófono en la sala de voz actual");
            return;
        }
#if WINDOWS
        var microphoneRoute = ResolveMicrophoneRoute(draft);
        if (microphoneRoute == MicrophoneRouteResolution.WaitingForDeviceList)
        {
            SetMicrophonePriorityStatus(
                "Comprobando el micrófono guardado - vuelve a probarlo en un momento", 3500);
            return;
        }
#endif
        lock (_microphoneStateGate)
        {
            Volatile.Write(ref _microphoneTestActive, 1);
            Interlocked.Exchange(ref _microphoneSignalDetected, 0);
            _micPausedForTone = false;
            SetMicrophoneStatus("Iniciando prueba del micrófono...");
        }

#if WINDOWS
        int leaseGeneration = Interlocked.Increment(ref _desktopLeaseGeneration);
        int operationGeneration = BeginDesktopOperation();
        var configuration = CaptureDesktopConfiguration(
            draft,
            _monitorPlayback,
            _monitorDelayed,
            SidecarCaptureMode.Transmit,
            microphoneRoute == MicrophoneRouteResolution.FellBackToDefault);
        _desktopInputDevice = configuration.InputDevice;
        _desktopOutputDevice = configuration.OutputDevice;
        var callbacks = CreateDesktopCallbacks(leaseGeneration, DesktopFailureChannel.Microphone);
        ScheduleDesktopOperation(new DesktopOperation(
            operationGeneration,
            leaseGeneration,
            DesktopOperationKind.Microphone,
            configuration,
            callbacks,
            ExistingLease: null,
            _desktopOperationCancellation!.Token));
        if (microphoneRoute == MicrophoneRouteResolution.FellBackToDefault)
            SetMicrophonePriorityStatus(
                "Iniciando la prueba con el micrófono predeterminado porque el guardado no está disponible...", 6500);
#elif ANDROID
        if (Application.HasUserAuthorization(UserAuthorization.Microphone))
        {
            StartAndroidCaptureAfterPermission(draft);
            return;
        }

        int generation = ++_permissionGeneration;
        FirstRunSetupPermissionRequester.Request(granted =>
        {
            if (_disposed || generation != _permissionGeneration) return;
            if (!granted)
            {
                FailMicrophone("Permiso del micrófono denegado - aún puedes escuchar");
                return;
            }
            StartAndroidCaptureAfterPermission(draft);
        });
#else
        FailMicrophone("Microphone check is unavailable on this platform build");
#endif
    }

    internal void RefreshMicrophoneSettings(FirstRunSetupDraft draft)
    {
        if (!IsListening || IsPlayingTone) return;
        _monitorGain = float.IsFinite(draft.MasterVolume)
            ? Math.Clamp(draft.MasterVolume, 0f, 2f)
            : 1f;
        if (_monitorRoom != null)
        {
            if (!_monitorRoom.SetLoopBack(true, _monitorDelayed, _monitorGain))
            {
                _monitorRoom = null;
                FailMicrophone("El audio de la sala de voz se detuvo - reinicia la prueba del micrófono");
            }
            return;
        }
#if WINDOWS
        var lease = _lease;
        if (lease == null) return;
        var configuration = CaptureDesktopConfiguration(
            draft,
            _monitorPlayback,
            _monitorDelayed,
            SidecarCaptureMode.Transmit);
        if (_lastDesktopSettings == configuration) return;
        _lastDesktopSettings = configuration;
        ScheduleDesktopSettings(lease, configuration);
#elif ANDROID
        _androidMicrophone?.SetVolume(draft.MicVolume);
        _androidMonitor?.Configure(
            _monitorPlayback,
            _monitorDelayed,
            draft.MasterVolume);
#endif
    }

    internal void RestartForDeviceChange(FirstRunSetupDraft draft)
    {
        if (IsMicrophoneTestActive && !IsPlayingTone) StartMicrophone(draft);
    }

    internal void StopMicrophone()
    {
        var monitorRoom = _monitorRoom;
        _monitorRoom = null;
        try { monitorRoom?.SetLoopBack(false); } catch { }
        lock (_microphoneStateGate)
        {
            Volatile.Write(ref _microphoneTestActive, 0);
            Volatile.Write(ref _listening, 0);
            _level = 0f;
            _speaking = false;
            SetMicrophoneStatus("La prueba del micrófono está desactivada");
        }
#if WINDOWS
        StopDesktopLease();
#elif ANDROID
        _permissionGeneration++;
        if (_androidMicrophone != null)
        {
            _androidMicrophone.Dispose();
            _androidMicrophone = null;
        }
        try { _androidMonitor?.Configure(false, false, 1f); } catch { }
        try { _androidMonitorOutput?.Dispose(); } catch { }
        _androidMonitorOutput = null;
        _androidMonitor = null;
#endif
        _monitorPlayback = false;
        _monitorDelayed = false;
        _monitorGain = 1f;
    }

    internal void PlayTestSound(FirstRunSetupDraft draft)
    {
        if (_disposed || IsSpeakerTestBusy || IsMicrophoneTestStarting) return;
        if (draft.MasterVolume < 0.099f)
        {
            FailOutput("El volumen del altavoz está por debajo del mínimo soportado del 10%", 4000);
            return;
        }

#if WINDOWS
        PauseMicrophoneForTone();
        BeginDesktopOutputTest(draft);
#elif ANDROID
        PauseMicrophoneForTone();
        StartAndroidTone(draft.MasterVolume);
#else
        FailOutput("La prueba del altavoz no está disponible en esta versión de la plataforma");
#endif
    }

    private void OnLevel(float peak, bool speaking)
    {
        lock (_microphoneStateGate)
        {
            if (!IsListening) return;
            _level = float.IsFinite(peak) ? Math.Clamp(peak, 0f, 1f) : 0f;
            _speaking = speaking;
            long now = Environment.TickCount64;
            Volatile.Write(ref _lastLevelTick, now);
            if (speaking || peak >= 0.035f)
            {
                Volatile.Write(ref _lastSignalTick, now);
                Interlocked.Exchange(ref _microphoneSignalDetected, 1);
            }
        }
    }

    private void MarkListening()
    {
        lock (_microphoneStateGate)
        {
            long now = Environment.TickCount64;
            Volatile.Write(ref _lastLevelTick, now);
            Volatile.Write(ref _lastSignalTick, now);
            Volatile.Write(ref _microphoneTestActive, 1);
            Volatile.Write(ref _listening, 1);
            SetMicrophoneStatus("Escuchando - habla con normalidad");
        }
    }

    private void FailMicrophone(string message)
    {
        lock (_microphoneStateGate)
        {
            Volatile.Write(ref _microphoneTestActive, 0);
            Volatile.Write(ref _listening, 0);
            Interlocked.Exchange(ref _microphoneSignalDetected, 0);
            SetMicrophoneStatus(message);
            _level = 0f;
            _speaking = false;
        }
    }

    private void SetMicrophoneStatus(string message)
    {
        _microphoneStatus = message;
        Volatile.Write(ref _microphonePriorityStatusUntilTick, 0);
    }

    private void SetMicrophonePriorityStatus(string message, int milliseconds)
    {
        _microphoneStatus = message;
        Volatile.Write(
            ref _microphonePriorityStatusUntilTick,
            Environment.TickCount64 + Math.Max(0, milliseconds));
    }

    private void SetOutputStatus(string message, int legacyMilliseconds = 0)
    {
        _outputStatus = message;
        Volatile.Write(
            ref _legacyOutputStatusUntilTick,
            legacyMilliseconds > 0 ? Environment.TickCount64 + legacyMilliseconds : 0);
    }

    private void FailOutput(string message, int legacyMilliseconds = 6000)
    {
        Interlocked.Exchange(ref _outputTestCompleted, 0);
        SetOutputStatus(message, legacyMilliseconds);
    }

    private void CompleteOutputTest(bool usedDefaultFallback = false)
    {
        Interlocked.Exchange(ref _outputTestCompleted, 1);
        SetOutputStatus(usedDefaultFallback
            ? "Prueba de salida predeterminada completada - ¿la escuchaste?"
            : "Prueba de sonido completada - ¿la escuchaste?", 6000);
    }

    private void PauseMicrophoneForTone()
    {
        _micPausedForTone = IsMicrophoneTestActive;
        if (!_micPausedForTone) return;
        var monitorRoom = _monitorRoom;
        _monitorRoom = null;
        try { monitorRoom?.SetLoopBack(false); } catch { }
#if ANDROID
        _permissionGeneration++;
        StopAndroidCaptureOnly();
        try { _androidMonitor?.Configure(false, false, 1f); } catch { }
        try { _androidMonitorOutput?.Dispose(); } catch { }
        _androidMonitorOutput = null;
        _androidMonitor = null;
#endif
        _monitorPlayback = false;
        _monitorDelayed = false;
        _monitorGain = 1f;
        lock (_microphoneStateGate)
        {
            Volatile.Write(ref _microphoneTestActive, 0);
            Volatile.Write(ref _listening, 0);
            _level = 0f;
            _speaking = false;
            SetMicrophoneStatus("Prueba del micrófono pausada durante la prueba de salida - reiníciala cuando estés listo");
        }
    }

    internal void StopAllTests()
    {
        bool outputWasBusy = IsSpeakerTestBusy;
        CancelTone();
#if WINDOWS
        CancelDesktopOutputWait();
#elif ANDROID
        CleanupAndroidTone();
#endif
        StopMicrophone();
        if (outputWasBusy)
            FailOutput("Prueba de salida pausada", 2500);
    }

    private static float EffectiveVadThreshold(FirstRunSetupDraft draft)
        => draft.BaseVadThreshold / Math.Max(0.25f, draft.MicSensitivity);

    private static float EffectiveNoiseGateThreshold(FirstRunSetupDraft draft)
        => (VoiceSettings.Instance?.NoiseGateThreshold.Value ?? 0.003f) /
           Math.Max(0.25f, draft.MicSensitivity);

    private MicrophoneRouteResolution ResolveMicrophoneRoute(FirstRunSetupDraft draft)
    {
        if (string.IsNullOrEmpty(draft.MicrophoneDevice) || draft.MicrophoneIndex() >= 0)
            return MicrophoneRouteResolution.Ready;
#if WINDOWS
        if (VoiceChatLocalSettings.SidecarDeviceProbePending)
            return MicrophoneRouteResolution.WaitingForDeviceList;
#endif
        draft.MicrophoneDevice = string.Empty;
        Interlocked.Exchange(ref _uiRefreshPending, 1);
        return MicrophoneRouteResolution.FellBackToDefault;
    }

#if WINDOWS
    private void BeginDesktopOutputTest(FirstRunSetupDraft draft)
    {
        CancelTone();
        int currentLeaseGeneration = Volatile.Read(ref _desktopLeaseGeneration);
        while (_playbackStates.TryDequeue(out var queued))
        {
            if (queued.LeaseGeneration == currentLeaseGeneration)
                UpdateConfirmedOutput(queued.State);
        }
        _pendingSpeakerDraft = draft;
        _pendingOutputDevice = draft.SpeakerDevice ?? string.Empty;
        _pendingPlaybackGeneration = 0;
        _desktopRouteAdmitted = false;
        _outputSelectionAccepted = false;
        _outputFallbackAttempted = false;
        lock (_desktopToneStateGate)
        {
            _activeTonePlaybackGeneration = 0;
            _desktopToneTerminalGeneration = 0;
            _desktopTonePlaybackTerminated = 0;
        }
        Volatile.Write(ref _waitingForOutput, 1);
        Interlocked.Exchange(ref _outputTestCompleted, 0);
        SetOutputStatus("Abriendo la salida seleccionada...");

        var existingLease = _lease;
        int leaseGeneration = existingLease == null
            ? Interlocked.Increment(ref _desktopLeaseGeneration)
            : currentLeaseGeneration;
        int operationGeneration = BeginDesktopOperation();
        var configuration = CaptureDesktopConfiguration(
            draft,
            monitorEnabled: false,
            monitorDelayed: false,
            SidecarCaptureMode.Stopped);
        _desktopInputDevice = configuration.InputDevice;
        _desktopOutputDevice = configuration.OutputDevice;
        var callbacks = existingLease == null
            ? CreateDesktopCallbacks(leaseGeneration, DesktopFailureChannel.Output)
            : null;
        ScheduleDesktopOperation(new DesktopOperation(
            operationGeneration,
            leaseGeneration,
            DesktopOperationKind.Output,
            configuration,
            callbacks,
            existingLease,
            _desktopOperationCancellation!.Token));
    }

    private void OnPlaybackState(int leaseGeneration, SidecarPlaybackState state)
    {
        lock (_desktopToneStateGate)
        {
            if (leaseGeneration == Volatile.Read(ref _desktopLeaseGeneration) &&
                FirstRunOutputPreviewPolicy.IsTonePlaybackTerminal(state.State))
            {
                long activeGeneration = _activeTonePlaybackGeneration;
                if (activeGeneration != 0 &&
                    state.StreamGeneration == unchecked((ulong)activeGeneration))
                {
                    _desktopToneTerminalGeneration = activeGeneration;
                    _desktopTonePlaybackTerminated = 1;
                }
            }
        }
        _playbackStates.Enqueue(new DesktopPlaybackEvent(leaseGeneration, state));
    }
    private void UpdateConfirmedOutput(SidecarPlaybackState state)
    {
        var confirmation = FirstRunOutputPreviewPolicy.ApplyPlaybackConfirmation(
            state,
            _confirmedOutputDevice,
            _confirmedPlaybackGeneration);
        _confirmedOutputDevice = confirmation.Device;
        _confirmedPlaybackGeneration = confirmation.Generation;
    }


    private void TickDesktopOutputReadiness()
    {
        while (_playbackStates.TryPeek(out var queued))
        {
            if (queued.LeaseGeneration != Volatile.Read(ref _desktopLeaseGeneration))
            {
                _playbackStates.TryDequeue(out _);
                continue;
            }
            if (Volatile.Read(ref _waitingForOutput) != 0 && !_desktopRouteAdmitted)
                break;
            if (!_playbackStates.TryDequeue(out queued)) continue;
            var state = queued.State;
            UpdateConfirmedOutput(state);
            if (Volatile.Read(ref _waitingForOutput) == 0)
            {
                long activeGeneration;
                bool terminalPending;
                long terminalGeneration;
                lock (_desktopToneStateGate)
                {
                    activeGeneration = _activeTonePlaybackGeneration;
                    terminalPending = _desktopTonePlaybackTerminated != 0;
                    terminalGeneration = _desktopToneTerminalGeneration;
                }
                if (FirstRunOutputPreviewPolicy.ShouldApplyTonePlaybackTerminal(
                        activeGeneration,
                        state.StreamGeneration,
                        state.State,
                        terminalPending,
                        terminalGeneration))
                {
                    CancelTone();
                    lock (_desktopToneStateGate)
                    {
                        _activeTonePlaybackGeneration = 0;
                        _desktopToneTerminalGeneration = 0;
                        _desktopTonePlaybackTerminated = 0;
                    }
                    FailOutput(FirstRunOutputPreviewPolicy.DescribeNativeOutputFailure(
                        state.Error, state.ErrorCode, duringPlayback: true));
                }
                continue;
            }

            if (state.State == "command-accepted" &&
                (state.Action is "select-output-device" or "configure-audio-route") &&
                RequestedOutputMatches(state))
            {
                _outputSelectionAccepted = true;
                if (state.Action == "configure-audio-route" &&
                    FirstRunOutputPreviewPolicy.CanReuseConfirmedOutput(
                        state.Changed,
                        state.Running,
                        _pendingOutputDevice,
                        _confirmedOutputDevice,
                        _confirmedPlaybackGeneration))
                {
                    StartDesktopToneForReadyOutput(_confirmedPlaybackGeneration);
                }
                else
                {
                    SetOutputStatus("Esperando la salida seleccionada...");
                }
                continue;
            }
            if (!_outputSelectionAccepted) continue;

            if (state.State == "error" && RequestedOutputMatches(state))
            {
                if (TryFallbackToDefault(state)) continue;
                CancelDesktopOutputWait();
                FailOutput(FirstRunOutputPreviewPolicy.DescribeNativeOutputFailure(
                    state.Error, state.ErrorCode), 6500);
                continue;
            }
            if (state.State == "stream-started" && RequestedOutputMatches(state))
            {
                if (state.FellBackToDefault || (!_pendingOutputDevice.Equals(string.Empty) && !state.RequestedMatched))
                {
                    if (_pendingSpeakerDraft != null) _pendingSpeakerDraft.SpeakerDevice = string.Empty;
                    Interlocked.Exchange(ref _uiRefreshPending, 1);
                    _pendingOutputDevice = string.Empty;
                    _outputFallbackAttempted = true;
                    SetOutputStatus("Esa salida no está disponible - probando la predeterminada...");
                }
                _pendingPlaybackGeneration = state.StreamGeneration;
                if (!_outputFallbackAttempted)
                    SetOutputStatus("La salida está lista - iniciando el sonido...");
                continue;
            }
            if (state.State == "first-callback" &&
                _pendingPlaybackGeneration != 0 &&
                state.StreamGeneration == _pendingPlaybackGeneration)
            {
                StartDesktopToneForReadyOutput(_pendingPlaybackGeneration);
            }
        }

        if (Volatile.Read(ref _waitingForOutput) != 0 &&
            _desktopRouteAdmitted &&
            Volatile.Read(ref _outputReadyDeadlineTick) != 0 &&
            Environment.TickCount64 >= Volatile.Read(ref _outputReadyDeadlineTick))
        {
            CancelDesktopOutputWait();
            FailOutput("La salida seleccionada no está lista - inténtalo de nuevo o elige Predeterminado");
        }
    }

    private void StartDesktopToneForReadyOutput(ulong playbackGeneration)
    {
        var draft = _pendingSpeakerDraft;
        bool usedDefaultFallback = _outputFallbackAttempted;
        CancelDesktopOutputWait();
        if (draft == null) return;
        lock (_desktopToneStateGate)
        {
            _desktopToneTerminalGeneration = 0;
            _desktopTonePlaybackTerminated = 0;
            _activeTonePlaybackGeneration = unchecked((long)playbackGeneration);
        }
        StartDesktopTone(draft.MasterVolume, usedDefaultFallback);
    }

    private bool TryFallbackToDefault(SidecarPlaybackState state)
    {
        if (!FirstRunOutputPreviewPolicy.ShouldTryDefaultFallback(
                _pendingOutputDevice, _outputFallbackAttempted))
            return false;

        _outputFallbackAttempted = true;
        if (_pendingSpeakerDraft != null) _pendingSpeakerDraft.SpeakerDevice = string.Empty;
        Interlocked.Exchange(ref _uiRefreshPending, 1);
        _pendingOutputDevice = string.Empty;
        _pendingPlaybackGeneration = 0;
        _outputSelectionAccepted = false;
        _desktopRouteAdmitted = false;
        _outputReadyDeadlineTick = 0;
        string reason = FirstRunOutputPreviewPolicy.DescribeNativeOutputFailure(
            state.Error, state.ErrorCode);
        SetOutputStatus(reason + " Probando Predeterminado...");
        var lease = _lease;
        if (lease == null)
        {
            CancelDesktopOutputWait();
            FailOutput("No se pudo cambiar la prueba de salida a Predeterminado", 6500);
            return true;
        }
        _desktopOutputDevice = string.Empty;
        var configuration = (_lastDesktopSettings ?? new DesktopConfiguration(
            _desktopInputDevice,
            string.Empty,
            EchoCancellation: false,
            NoiseSuppression: false,
            StrongerNoiseSuppression: false,
            InputGain: 1f,
            VadThreshold: 0.01f,
            NoiseGateThreshold: 0.003f,
            MonitorEnabled: false,
            MonitorDelayed: false,
            MonitorGain: 1f,
            SidecarCaptureMode.Stopped,
            InputFellBackToDefault: false)) with
        {
            OutputDevice = string.Empty,
            MonitorEnabled = false,
            MonitorDelayed = false,
            MonitorGain = 1f,
            CaptureMode = SidecarCaptureMode.Stopped,
        };
        int operationGeneration = BeginDesktopOperation();
        ScheduleDesktopOperation(new DesktopOperation(
            operationGeneration,
            Volatile.Read(ref _desktopLeaseGeneration),
            DesktopOperationKind.OutputFallback,
            configuration,
            Callbacks: null,
            lease,
            _desktopOperationCancellation!.Token));
        return true;
    }

    private bool RequestedOutputMatches(SidecarPlaybackState state)
        => FirstRunOutputPreviewPolicy.RequestedOutputMatches(
            _pendingOutputDevice,
            state.RequestedDevice,
            state.RequestedDefault);

    private void CancelDesktopOutputWait()
    {
        Volatile.Write(ref _waitingForOutput, 0);
        _desktopRouteAdmitted = false;
        _outputReadyDeadlineTick = 0;
        _outputSelectionAccepted = false;
        _pendingPlaybackGeneration = 0;
        _pendingSpeakerDraft = null;
        _pendingOutputDevice = string.Empty;
    }

    private void StartDesktopTone(float volume, bool usedDefaultFallback)
    {
        CancelTone();
        var toneLease = _lease;
        int leaseGeneration = Volatile.Read(ref _desktopLeaseGeneration);
        int toneGeneration = Interlocked.Increment(ref _toneGeneration);
        var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        Volatile.Write(ref _desktopToneLeaseGeneration, leaseGeneration);
        _toneCancellation = cancellation;
        Volatile.Write(ref _playingTone, 1);
        Interlocked.Exchange(ref _outputTestCompleted, 0);
        SetOutputStatus("Reproduciendo sonido de prueba...");
        var completions = _toneCompletions;
        _ = Task.Run(async () =>
        {
            try
            {
                for (int frame = 0; frame < FirstRunToneGenerator.FrameCount; frame++)
                {
                    token.ThrowIfCancellationRequested();
                    if (toneLease == null ||
                        !toneLease.SendOutputTestFrameAndWait(
                            FirstRunToneGenerator.CreateFrame(frame, volume)))
                    {
                        completions.Enqueue(new DesktopToneCompletion(
                            leaseGeneration,
                            toneGeneration,
                            Succeeded: false,
                            usedDefaultFallback));
                        return;
                    }
                    await Task.Delay(FirstRunToneGenerator.FrameMilliseconds, token)
                        .ConfigureAwait(false);
                }
                await Task.Delay(220, token).ConfigureAwait(false);
                completions.Enqueue(new DesktopToneCompletion(
                    leaseGeneration,
                    toneGeneration,
                    Succeeded: true,
                    usedDefaultFallback));
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
                completions.Enqueue(new DesktopToneCompletion(
                    leaseGeneration,
                    toneGeneration,
                    Succeeded: false,
                    usedDefaultFallback));
            }
            finally
            {
                cancellation.Dispose();
            }
        });
    }

    private void TickDesktopToneCompletions()
    {
        while (_toneCompletions.TryDequeue(out var completion))
        {
            if (_disposed ||
                completion.LeaseGeneration != Volatile.Read(ref _desktopLeaseGeneration) ||
                completion.ToneGeneration != Volatile.Read(ref _toneGeneration))
                continue;
            if (completion.Succeeded)
            {
                lock (_desktopToneStateGate)
                {
                    if (_disposed ||
                        completion.LeaseGeneration != Volatile.Read(ref _desktopLeaseGeneration) ||
                        completion.ToneGeneration != Volatile.Read(ref _toneGeneration))
                        continue;
                    if (_desktopTonePlaybackTerminated != 0 &&
                        _desktopToneTerminalGeneration == _activeTonePlaybackGeneration)
                        continue;
                    Interlocked.Exchange(ref _toneCancellation, null);
                    Volatile.Write(ref _playingTone, 0);
                    _activeTonePlaybackGeneration = 0;
                    _desktopToneTerminalGeneration = 0;
                    _desktopTonePlaybackTerminated = 0;
                    Interlocked.Exchange(ref _outputTestCompleted, 1);
                }
                SetOutputStatus(completion.UsedDefaultFallback
                    ? "Prueba de altavoz predeterminado completada - ¿la escuchaste?"
                    : "Prueba de sonido completada - ¿la escuchaste?", 6000);
            }
            else
            {
                Interlocked.Exchange(ref _toneCancellation, null);
                Volatile.Write(ref _playingTone, 0);
                lock (_desktopToneStateGate)
                    _activeTonePlaybackGeneration = 0;
                FailOutput("No se pudo reproducir el sonido de prueba");
            }
            Interlocked.Exchange(ref _uiRefreshPending, 1);
        }
    }

    private void StopDesktopLease()
    {
        CancelTone();
        CancelDesktopOperation();
        CancelDesktopSettings();
        Interlocked.Increment(ref _desktopLeaseGeneration);
        CancelDesktopOutputWait();
        lock (_desktopToneStateGate)
        {
            _activeTonePlaybackGeneration = 0;
            _desktopToneTerminalGeneration = 0;
            _desktopTonePlaybackTerminated = 0;
        }
        Volatile.Write(ref _listening, 0);
        _lastDesktopSettings = null;
        var lease = _lease;
        _lease = null;
        if (lease == null) return;
        ReleaseDesktopLeaseAsync(lease, _desktopInputDevice, _desktopOutputDevice);
    }

    private DesktopConfiguration CaptureDesktopConfiguration(
        FirstRunSetupDraft draft,
        bool monitorEnabled,
        bool monitorDelayed,
        SidecarCaptureMode captureMode,
        bool inputFellBackToDefault = false)
        => new(
            draft.MicrophoneDevice ?? string.Empty,
            draft.SpeakerDevice ?? string.Empty,
            draft.EchoCancellation,
            draft.NoiseSuppression,
            draft.StrongerNoiseSuppression,
            draft.MicVolume,
            EffectiveVadThreshold(draft),
            EffectiveNoiseGateThreshold(draft),
            monitorEnabled,
            monitorDelayed,
            float.IsFinite(draft.MasterVolume)
                ? Math.Clamp(draft.MasterVolume, 0f, 2f)
                : 1f,
            captureMode,
            inputFellBackToDefault);

    private SidecarVoiceCallbacks CreateDesktopCallbacks(
        int leaseGeneration,
        DesktopFailureChannel channel)
        => new(
            (_, _) => { },
            reason => FailDesktop(
                channel == DesktopFailureChannel.Output
                    ? "El asistente de audio se detuvo: " + reason
                    : "El asistente del micrófono se detuvo: " + reason,
                leaseGeneration,
                channel),
            (_, message) => FailDesktop(
                channel == DesktopFailureChannel.Output
                    ? "Salida no disponible: " + message
                    : "Micrófono no disponible:: " + message,
                leaseGeneration,
                channel),
            (_, _, _, _) => { },
            (_, _, _) => { },
            (_, _, _) => { },
            (peak, speaking) =>
            {
                if (leaseGeneration == Volatile.Read(ref _desktopLeaseGeneration))
                    OnLevel(peak, speaking);
            },
            _ => { },
            _ => { },
            state => OnPlaybackState(leaseGeneration, state));

    private int BeginDesktopOperation()
    {
        CancelDesktopOperation();
        _desktopOperationCancellation = new CancellationTokenSource();
        return Volatile.Read(ref _desktopOperationGeneration);
    }

    private void CancelDesktopOperation()
    {
        Interlocked.Increment(ref _desktopOperationGeneration);
        var cancellation = Interlocked.Exchange(ref _desktopOperationCancellation, null);
        if (cancellation == null) return;
        try { cancellation.Cancel(); } catch { }
        cancellation.Dispose();
    }

    private void ScheduleDesktopOperation(DesktopOperation operation)
    {
        var host = _desktopHost;
        var acquisitionGate = _desktopAcquisitionGate;
        var workerGate = _desktopWorkerGate;
        var completions = _desktopCompletions;
        _ = Task.Run(async () =>
        {
            bool enteredAcquisition = false;
            try
            {
                if (operation.ExistingLease == null)
                {
                    await acquisitionGate.WaitAsync(operation.Cancellation)
                        .ConfigureAwait(false);
                    enteredAcquisition = true;
                    Task leaseRelease;
                    lock (_desktopLeaseReleaseGate)
                        leaseRelease = _desktopLeaseRelease;
                    await leaseRelease.WaitAsync(operation.Cancellation)
                        .ConfigureAwait(false);
                }
                await RunDesktopOperationAsync(
                        operation,
                        host,
                        workerGate,
                        completions,
                        ReserveDesktopLeaseRetirement,
                        (lease, configuration, retirement) =>
                            ReleaseReservedDesktopLeaseAsync(
                                lease,
                                configuration.InputDevice,
                                configuration.OutputDevice,
                                retirement))
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                if (enteredAcquisition) acquisitionGate.Release();
            }
        });
    }

    private static async Task RunDesktopOperationAsync(
        DesktopOperation operation,
        IFirstRunDesktopAudioHost host,
        SemaphoreSlim workerGate,
        ConcurrentQueue<DesktopOperationCompletion> completions,
        Func<DesktopLeaseRetirement> reserveRetirement,
        Action<
            IFirstRunDesktopAudioLease,
            DesktopConfiguration,
            DesktopLeaseRetirement> release)
    {
        IFirstRunDesktopAudioLease? lease = operation.ExistingLease;
        bool acquiredLease = false;
        bool entered = false;
        DesktopLeaseRetirement? retirement = null;
        try
        {
            await workerGate.WaitAsync(operation.Cancellation).ConfigureAwait(false);
            entered = true;
            operation.Cancellation.ThrowIfCancellationRequested();
            if (lease == null)
            {
                lease = host.TryAcquire(operation.Callbacks!, out string failure);
                if (lease == null)
                {
                    completions.Enqueue(new DesktopOperationCompletion(
                        operation.OperationGeneration,
                        operation.LeaseGeneration,
                        operation.Kind,
                        operation.Configuration,
                        lease: null,
                        acquiredLease: false,
                        failure: failure));
                    return;
                }
                acquiredLease = true;
                if (!lease.EnsureStarted(
                        operation.Configuration.InputDevice,
                        operation.Configuration.OutputDevice))
                {
                    try { await lease.ReleaseAsync().ConfigureAwait(false); } catch { }
                    lease = null;
                    acquiredLease = false;
                    completions.Enqueue(new DesktopOperationCompletion(
                        operation.OperationGeneration,
                        operation.LeaseGeneration,
                        operation.Kind,
                        operation.Configuration,
                        lease: null,
                        acquiredLease: false,
                        failure: "ensure-started"));
                    return;
                }
            }

            operation.Cancellation.ThrowIfCancellationRequested();
            lease.SetDsp(
                operation.Configuration.EchoCancellation,
                operation.Configuration.NoiseSuppression,
                operation.Configuration.StrongerNoiseSuppression);
            lease.SetInput(
                operation.Configuration.InputGain,
                operation.Configuration.VadThreshold,
                operation.Configuration.NoiseGateThreshold);
            lease.SetMonitor(
                operation.Configuration.MonitorEnabled,
                operation.Configuration.MonitorDelayed,
                operation.Configuration.MonitorGain);
            if (!lease.ConfigureAudioRoute(
                    operation.Configuration.InputDevice,
                    operation.Configuration.OutputDevice,
                    operation.Configuration.CaptureMode))
            {
                if (acquiredLease)
                {
                    try { await lease.ReleaseAsync().ConfigureAwait(false); } catch { }
                    lease = null;
                    acquiredLease = false;
                }
                completions.Enqueue(new DesktopOperationCompletion(
                    operation.OperationGeneration,
                    operation.LeaseGeneration,
                    operation.Kind,
                    operation.Configuration,
                    lease,
                    acquiredLease,
                    failure: "route"));
                return;
            }
            operation.Cancellation.ThrowIfCancellationRequested();
            retirement = acquiredLease ? reserveRetirement() : null;
            var completion = new DesktopOperationCompletion(
                operation.OperationGeneration,
                operation.LeaseGeneration,
                operation.Kind,
                operation.Configuration,
                lease,
                acquiredLease,
                failure: string.Empty,
                retirement);
            completion.ReleaseOnCancellation(operation.Cancellation, release);
            completions.Enqueue(completion);
        }
        catch (OperationCanceledException)
        {
            if (acquiredLease && lease != null)
            {
                try { await lease.ReleaseAsync().ConfigureAwait(false); } catch { }
                retirement?.Complete();
            }
        }
        catch (Exception)
        {
            if (acquiredLease && lease != null)
            {
                try { await lease.ReleaseAsync().ConfigureAwait(false); } catch { }
                retirement?.Complete();
                lease = null;
                acquiredLease = false;
            }
            if (!operation.Cancellation.IsCancellationRequested)
                completions.Enqueue(new DesktopOperationCompletion(
                    operation.OperationGeneration,
                    operation.LeaseGeneration,
                    operation.Kind,
                    operation.Configuration,
                    lease,
                    acquiredLease,
                    failure: "operation-failed"));
        }
        finally
        {
            if (entered) workerGate.Release();
        }
    }

    private void TickDesktopCompletions()
    {
        while (_desktopCompletions.TryDequeue(out var completion))
        {
            var lease = completion.TakeLease();
            if (_disposed ||
                completion.OperationGeneration != Volatile.Read(ref _desktopOperationGeneration) ||
                completion.LeaseGeneration != Volatile.Read(ref _desktopLeaseGeneration))
            {
                if (completion.AcquiredLease &&
                    lease != null &&
                    completion.Retirement != null)
                {
                    ReleaseReservedDesktopLeaseAsync(
                        lease,
                        completion.Configuration.InputDevice,
                        completion.Configuration.OutputDevice,
                        completion.Retirement);
                }
                else if (!completion.AcquiredLease)
                {
                    completion.CompleteRetirement();
                }
                continue;
            }

            if (completion.Failure.Length != 0 || lease == null)
            {
                if (completion.Kind == DesktopOperationKind.Microphone)
                {
                    FailMicrophone(completion.Failure.StartsWith("lease-active", StringComparison.Ordinal)
                        ? "La prueba del micrófono está disponible desde el menú principal"
                        : completion.Failure == "route"
                            ? "No se pudo configurar la ruta del micrófono"
                            : "No se pudo iniciar el asistente de audio de Perfect Comms");
                }
                else
                {
                    CancelDesktopOutputWait();
                    FailOutput(completion.Failure.StartsWith("lease-active", StringComparison.Ordinal)
                        ? "La prueba de salida está disponible desde el menú principal"
                        : completion.Failure == "route"
                            ? "No se pudo enviar la salida seleccionada al asistente de audio"
                            : "No se pudo iniciar la prueba de salida");
                }
                if (lease != null && ReferenceEquals(_lease, lease))
                    StopDesktopLease();
                Interlocked.Exchange(ref _uiRefreshPending, 1);
                continue;
            }

            if (completion.AcquiredLease)
                _lease = lease;
            completion.CompleteRetirement();
            _desktopInputDevice = completion.Configuration.InputDevice;
            _desktopOutputDevice = completion.Configuration.OutputDevice;
            _lastDesktopSettings = completion.Configuration;
            if (completion.Kind == DesktopOperationKind.Microphone)
            {
                MarkListening();
                if (completion.Configuration.InputFellBackToDefault)
                    SetMicrophonePriorityStatus(
                        "El micrófono guardado no está disponible. La prueba de micrófono está usando el predeterminado.", 6500);
            }
            else
            {
                _desktopRouteAdmitted = true;
                _outputReadyDeadlineTick = Environment.TickCount64 + 5000;
                SetOutputStatus("Esperando a la salida seleccionada...");
            }
            Interlocked.Exchange(ref _uiRefreshPending, 1);
        }
    }

    private void ScheduleDesktopSettings(
        IFirstRunDesktopAudioLease lease,
        DesktopConfiguration configuration)
    {
        CancelDesktopSettings();
        var cancellation = new CancellationTokenSource();
        _desktopSettingsCancellation = cancellation;
        var token = cancellation.Token;
        var workerGate = _desktopWorkerGate;
        _ = Task.Run(async () =>
        {
            bool entered = false;
            try
            {
                await workerGate.WaitAsync(token).ConfigureAwait(false);
                entered = true;
                token.ThrowIfCancellationRequested();
                lease.SetDsp(
                    configuration.EchoCancellation,
                    configuration.NoiseSuppression,
                    configuration.StrongerNoiseSuppression);
                lease.SetInput(
                    configuration.InputGain,
                    configuration.VadThreshold,
                    configuration.NoiseGateThreshold);
                lease.SetMonitor(
                    configuration.MonitorEnabled,
                    configuration.MonitorDelayed,
                    configuration.MonitorGain);
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
            }
            finally
            {
                if (entered) workerGate.Release();
                cancellation.Dispose();
            }
        });
    }

    private void CancelDesktopSettings()
    {
        var cancellation = Interlocked.Exchange(ref _desktopSettingsCancellation, null);
        if (cancellation == null) return;
        try { cancellation.Cancel(); } catch { }
        cancellation.Dispose();
    }

    private DesktopLeaseRetirement ReserveDesktopLeaseRetirement()
    {
        lock (_desktopLeaseReleaseGate)
        {
            var retirement = new DesktopLeaseRetirement(_desktopLeaseRelease);
            _desktopLeaseRelease = retirement.Barrier;
            return retirement;
        }
    }

    private void ReleaseDesktopLeaseAsync(
        IFirstRunDesktopAudioLease lease,
        string inputDevice,
        string outputDevice)
    {
        var retirement = ReserveDesktopLeaseRetirement();
        ReleaseReservedDesktopLeaseAsync(lease, inputDevice, outputDevice, retirement);
    }

    private void ReleaseReservedDesktopLeaseAsync(
        IFirstRunDesktopAudioLease lease,
        string inputDevice,
        string outputDevice,
        DesktopLeaseRetirement retirement)
    {
        var workerGate = _desktopWorkerGate;
        _ = Task.Run(async () =>
        {
            bool entered = false;
            try
            {
                await retirement.Previous.ConfigureAwait(false);
                await workerGate.WaitAsync().ConfigureAwait(false);
                entered = true;
                try
                {
                    lease.SetMonitor(false, false, 1f);
                    lease.ConfigureAudioRoute(
                        inputDevice,
                        outputDevice,
                        SidecarCaptureMode.Stopped);
                }
                catch
                {
                }
                try { await lease.ReleaseAsync().ConfigureAwait(false); } catch { }
            }
            finally
            {
                if (entered) workerGate.Release();
                retirement.Complete();
            }
        });
    }

    private void FailDesktop(
        string message,
        int generation,
        DesktopFailureChannel channel)
    {
        if (generation != Volatile.Read(ref _desktopLeaseGeneration)) return;
        _desktopFailures.Enqueue(new DesktopFailureEvent(generation, message, channel));
        var cancellation = Volatile.Read(ref _toneCancellation);
        if (generation != Volatile.Read(ref _desktopLeaseGeneration) ||
            generation != Volatile.Read(ref _desktopToneLeaseGeneration))
            return;
        try { cancellation?.Cancel(); } catch { }
    }
#endif

#if ANDROID
    private FirstRunSetupDraft? _androidDraft;

    private void StartAndroidCaptureAfterPermission(FirstRunSetupDraft? draft = null)
    {
        if (draft != null) _androidDraft = draft;
        var activeDraft = draft ?? _androidDraft;
        if (_disposed || activeDraft == null) return;
        if (VoiceChatRoom.Current != null)
        {
            FailMicrophone("Mic Check is available from the main menu, outside a voice room");
            StopAndroidCaptureOnly();
            return;
        }
        VoiceChatLocalSettings.RefreshDeviceLists();
        var microphoneRoute = ResolveMicrophoneRoute(activeDraft);
        StopAndroidCaptureOnly();
        _androidMicrophone = new AndroidMicrophone { ReuseBuffer = true };
        _androidMicrophone.SetVolume(activeDraft.MicVolume);
        _androidMicrophone.DataAvailable += OnAndroidSamples;
        if (!_androidMicrophone.Start(activeDraft.MicrophoneDevice))
        {
            FailMicrophone(_androidMicrophone.LeaseUnavailable
                ? "Mic Check is unavailable while voice chat is using the microphone"
                : "Could not open the Android microphone");
            StopAndroidCaptureOnly();
            return;
        }
        if (_monitorPlayback)
        {
            _androidMonitor = new AndroidMicrophoneMonitor();
            _androidMonitor.Configure(
                true,
                _monitorDelayed,
                activeDraft.MasterVolume);
            _androidMonitorOutput = new AndroidMicrophoneMonitorOutput(_androidMonitor);
        }
        MarkListening();
        if (microphoneRoute == MicrophoneRouteResolution.FellBackToDefault)
            SetMicrophonePriorityStatus(
                "The saved microphone is unavailable - Mic Check is using Default", 6500);
    }

    private void OnAndroidSamples(float[] samples, int count)
    {
        _androidMonitor?.Write(samples, count);
        float peak = 0f;
        for (int i = 0; i < count; i++)
            peak = Math.Max(peak, Math.Abs(samples[i]));
        OnLevel(peak, peak >= EffectiveVadThreshold(_androidDraft!));
    }

    private void StopAndroidCaptureOnly()
    {
        if (_androidMicrophone == null) return;
        _androidMicrophone.DataAvailable -= OnAndroidSamples;
        _androidMicrophone.Dispose();
        _androidMicrophone = null;
    }

    private void StartAndroidTone(float volume)
    {
        CancelTone();
        CleanupAndroidTone();
        var all = new float[FirstRunToneGenerator.FrameCount * SidecarProtocol.AudioOutSamples];
        for (int frame = 0; frame < FirstRunToneGenerator.FrameCount; frame++)
            Array.Copy(FirstRunToneGenerator.CreateFrame(frame, volume), 0, all,
                frame * SidecarProtocol.AudioOutSamples, SidecarProtocol.AudioOutSamples);

        _toneObject = new GameObject("PerfectComms_SetupTestSound");
        Object.DontDestroyOnLoad(_toneObject);
        _toneSource = _toneObject.AddComponent<AudioSource>();
        _toneSource.volume = 1f;
        _toneClip = AudioClip.Create("PerfectComms Setup Chime",
            all.Length / 2, 2, FirstRunToneGenerator.SampleRate, false);
        var il2cpp = new Il2CppStructArray<float>(all.Length);
        for (int i = 0; i < all.Length; i++) il2cpp[i] = all[i];
        _toneClip.SetData(il2cpp, 0);
        _toneSource.clip = _toneClip;
        Volatile.Write(ref _playingTone, 1);
        Interlocked.Exchange(ref _outputTestCompleted, 0);
        SetOutputStatus("Playing test sound...");
        _toneSource.Play();
    }

    private void CleanupAndroidTone()
    {
        if (_toneSource != null) _toneSource.Stop();
        if (_toneClip != null) Object.Destroy(_toneClip);
        if (_toneObject != null) Object.Destroy(_toneObject);
        _toneSource = null;
        _toneClip = null;
        _toneObject = null;
    }
#endif

    private void CancelTone()
    {
        Interlocked.Increment(ref _toneGeneration);
        Volatile.Write(ref _playingTone, 0);
        var cancellation = Interlocked.Exchange(ref _toneCancellation, null);
        if (cancellation == null) return;
        try { cancellation.Cancel(); } catch { }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CancelTone();
        StopMicrophone();
#if ANDROID
        CleanupAndroidTone();
#endif
    }
}

#if ANDROID
internal sealed class FirstRunSetupPermissionRequester : MonoBehaviour
{
    private static bool _registered;
    private static FirstRunSetupPermissionRequester? _instance;

    internal static void Request(Action<bool> completed)
    {
        if (!_registered)
        {
            ClassInjector.RegisterTypeInIl2Cpp<FirstRunSetupPermissionRequester>();
            _registered = true;
        }
        if (_instance == null)
            _instance = VoiceUiKit.Canvas.gameObject.AddComponent<FirstRunSetupPermissionRequester>();
        _instance.StartCoroutine(_instance.RequestRoutine(completed).WrapToIl2Cpp());
    }

    public FirstRunSetupPermissionRequester(IntPtr ptr) : base(ptr) { }

    private IEnumerator RequestRoutine(Action<bool> completed)
    {
        yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
        completed(Application.HasUserAuthorization(UserAuthorization.Microphone));
    }
}
#endif
