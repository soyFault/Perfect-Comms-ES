using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using VoiceChatPlugin.VoiceChat;
using Xunit;

namespace PerfectComms.Tests;

public sealed class FirstRunAudioPreviewTests
{
    [Fact]
    public async Task BlockedDesktopStartDoesNotBlockCaller()
    {
        var lease = new FakeDesktopLease();
        var preview = new FirstRunAudioPreview(new FakeDesktopHost(lease));
        try
        {
            var startCall = Task.Run(
                () => preview.StartMicrophone(Draft()),
                TestContext.Current.CancellationToken);

            await AwaitSignal(startCall);
            await AwaitSignal(lease.StartEntered.Task);
            Assert.True(preview.IsMicrophoneTestStarting);
            Assert.False(preview.IsListening);
        }
        finally
        {
            preview.Dispose();
            lease.AllowStart();
            lease.AllowRelease();
        }
    }

    [Fact]
    public async Task DisposeIgnoresLateSuccessfulStart()
    {
        var lease = new FakeDesktopLease();
        var preview = new FirstRunAudioPreview(new FakeDesktopHost(lease));
        preview.StartMicrophone(Draft());
        await AwaitSignal(lease.StartEntered.Task);

        preview.Dispose();
        lease.AllowStart();

        await AwaitSignal(lease.Released.Task);
        preview.Tick();
        Assert.False(preview.IsMicrophoneTestActive);
        Assert.False(preview.IsListening);
    }

    [Fact]
    public async Task StopIgnoresLateSuccessfulStart()
    {
        var lease = new FakeDesktopLease();
        var preview = new FirstRunAudioPreview(new FakeDesktopHost(lease));
        try
        {
            preview.StartMicrophone(Draft());
            await AwaitSignal(lease.StartEntered.Task);

            preview.StopMicrophone();
            lease.AllowStart();

            await AwaitSignal(lease.Released.Task);
            preview.Tick();
            Assert.False(preview.IsMicrophoneTestActive);
            Assert.False(preview.IsListening);
            Assert.Equal("Mic check is off", preview.MicrophoneStatus);
        }
        finally
        {
            lease.AllowStart();
            lease.AllowRelease();
            preview.Dispose();
        }
    }

    [Fact]
    public async Task DisposeAfterCompletionEnqueueReleasesUnpublishedLease()
    {
        var lease = new FakeDesktopLease();
        lease.AllowStart();
        var preview = new FirstRunAudioPreview(new FakeDesktopHost(lease));

        preview.StartMicrophone(Draft());
        await AwaitSignal(lease.RouteConfigured.Task);
        await WaitUntilAsync(() => QueueCount(preview, "_desktopCompletions") != 0);

        preview.Dispose();

        await AwaitSignal(lease.ReleaseStarted.Task);
        await AwaitSignal(lease.Released.Task);
        Assert.False(preview.IsMicrophoneTestActive);
        Assert.False(preview.IsListening);
    }

    [Fact]
    public async Task NewerDeviceAttemptSupersedesBlockedOlderAttempt()
    {
        var first = new FakeDesktopLease();
        var second = new FakeDesktopLease();
        var preview = new FirstRunAudioPreview(new FakeDesktopHost(first, second));
        try
        {
            var draft = Draft(speaker: "speaker-a");
            preview.StartMicrophone(draft);
            await AwaitSignal(first.StartEntered.Task);

            draft.SpeakerDevice = "speaker-b";
            preview.RestartForDeviceChange(draft);
            first.AllowStart();

            await AwaitSignal(first.Released.Task);
            await AwaitSignal(second.StartEntered.Task);
            second.AllowStart();
            await PumpUntilAsync(preview, () => preview.IsListening);

            Assert.Equal("speaker-b", second.StartSpeakerDevice);
            Assert.Equal("speaker-b", Assert.Single(second.Routes).OutputDevice);
            Assert.True(preview.IsListening);
        }
        finally
        {
            first.AllowStart();
            first.AllowRelease();
            second.AllowStart();
            second.AllowRelease();
            preview.Dispose();
        }
    }

    [Fact]
    public async Task ReplacementWaitsForFullLeaseRetirement()
    {
        var first = new FakeDesktopLease(holdRelease: true);
        var second = new FakeDesktopLease();
        first.AllowStart();
        second.AllowStart();
        var preview = new FirstRunAudioPreview(new FakeDesktopHost(first, second));
        try
        {
            var draft = Draft(speaker: "speaker-a");
            preview.StartMicrophone(draft);
            await PumpUntilAsync(preview, () => preview.IsListening);

            draft.SpeakerDevice = "speaker-b";
            preview.RestartForDeviceChange(draft);
            await AwaitSignal(first.ReleaseStarted.Task);

            Assert.False(second.StartEntered.Task.IsCompleted);
            first.AllowRelease();
            await AwaitSignal(first.Released.Task);
            await AwaitSignal(second.StartEntered.Task);
            await PumpUntilAsync(preview, () => preview.IsListening);

            Assert.Equal("speaker-b", second.StartSpeakerDevice);
        }
        finally
        {
            first.AllowRelease();
            second.AllowRelease();
            preview.Dispose();
        }
    }
    [Fact]
    public async Task ReplacementScheduledDuringCancellationWaitsForLateRetirement()
    {
        var first = new FakeDesktopLease(holdRelease: true);
        var second = new FakeDesktopLease();
        first.AllowStart();
        second.AllowStart();
        var preview = new FirstRunAudioPreview(new FakeDesktopHost(first, second));
        var allowCancellation = new ManualResetEventSlim();
        Task? cancellationTask = null;
        CancellationTokenRegistration cancellationRegistration = default;
        try
        {
            preview.StartMicrophone(Draft(speaker: "speaker-a"));
            await AwaitSignal(first.RouteConfigured.Task);
            await WaitUntilAsync(() => QueueCount(preview, "_desktopCompletions") != 0);

            var cancellation = GetPrivateField<CancellationTokenSource>(
                preview,
                "_desktopOperationCancellation");
            var cancellationEntered = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            cancellationRegistration = cancellation.Token.Register(() =>
            {
                cancellationEntered.TrySetResult(true);
                allowCancellation.Wait(TestContext.Current.CancellationToken);
            });

            cancellationTask = Task.Run(
                () => InvokePrivate(preview, "CancelDesktopOperation"),
                TestContext.Current.CancellationToken);
            await AwaitSignal(cancellationEntered.Task);

            preview.StartMicrophone(Draft(speaker: "speaker-b"));
            var acquisitionGate = GetPrivateField<SemaphoreSlim>(
                preview,
                "_desktopAcquisitionGate");
            await WaitUntilAsync(() => acquisitionGate.CurrentCount == 0);
            Assert.False(second.StartEntered.Task.IsCompleted);

            allowCancellation.Set();
            await AwaitSignal(cancellationTask);
            await AwaitSignal(first.ReleaseStarted.Task);
            Assert.False(second.StartEntered.Task.IsCompleted);

            first.AllowRelease();
            await AwaitSignal(first.Released.Task);
            await AwaitSignal(second.StartEntered.Task);
            await PumpUntilAsync(preview, () => preview.IsListening);

            Assert.Equal("speaker-b", second.StartSpeakerDevice);
            Assert.DoesNotContain("available from the main menu", preview.MicrophoneStatus);
        }
        finally
        {
            allowCancellation.Set();
            if (cancellationTask != null)
                await AwaitSignal(cancellationTask);
            cancellationRegistration.Dispose();
            first.AllowRelease();
            second.AllowRelease();
            preview.Dispose();
        }
    }


    [Fact]
    public async Task StaleCallbacksCannotFailNewerAttempt()
    {
        var first = new FakeDesktopLease();
        var second = new FakeDesktopLease();
        first.AllowStart();
        second.AllowStart();
        var preview = new FirstRunAudioPreview(new FakeDesktopHost(first, second));
        try
        {
            var draft = Draft(speaker: "speaker-a");
            preview.StartMicrophone(draft);
            await PumpUntilAsync(preview, () => preview.IsListening);

            draft.SpeakerDevice = "speaker-b";
            preview.RestartForDeviceChange(draft);
            await PumpUntilAsync(preview, () => second.Routes.Count != 0 && preview.IsListening);

            first.Callbacks!.OnDead("late old failure");
            preview.Tick();

            Assert.True(preview.IsMicrophoneTestActive);
            Assert.True(preview.IsListening);
            Assert.DoesNotContain("late old failure", preview.MicrophoneStatus, StringComparison.Ordinal);
        }
        finally
        {
            preview.Dispose();
        }
    }

    [Fact]
    public async Task SpeakerReadinessWaitsForCurrentRouteCompletion()
    {
        var lease = new FakeDesktopLease();
        var preview = new FirstRunAudioPreview(new FakeDesktopHost(lease));
        try
        {
            preview.PlayTestSound(Draft(speaker: "speaker-a"));
            await AwaitSignal(lease.StartEntered.Task);

            lease.Callbacks!.OnPlaybackState(Playback("command-accepted", "configure-audio-route", 0));
            lease.Callbacks.OnPlaybackState(Playback("stream-started", string.Empty, 41));
            lease.Callbacks.OnPlaybackState(Playback("first-callback", string.Empty, 41));
            preview.Tick();

            Assert.True(preview.IsPreparingTone);
            Assert.False(preview.IsPlayingTone);
            Assert.Equal(0, lease.OutputFrames);

            lease.AllowStart();
            await AwaitSignal(lease.RouteConfigured.Task);
            await PumpUntilAsync(preview, () => preview.IsPlayingTone);

            Assert.True(preview.IsPlayingTone);
        }
        finally
        {
            lease.AllowStart();
            preview.Dispose();
        }
    }

    [Fact]
    public async Task TerminalCallbackBeforeToneCompletionInvalidatesSuccess()
    {
        var lease = new FakeDesktopLease();
        lease.AllowStart();
        var preview = new FirstRunAudioPreview(new FakeDesktopHost(lease));
        try
        {
            await StartToneAsync(preview, lease);
            await WaitUntilAsync(() => QueueCount(preview, "_toneCompletions") != 0);

            InvokePrivate(preview, "TickDesktopOutputReadiness");
            lease.Callbacks!.OnPlaybackState(Playback(
                "error",
                string.Empty,
                41,
                error: "native playback failed",
                errorCode: "stream-error"));
            InvokePrivate(preview, "TickDesktopToneCompletions");

            Assert.False(preview.OutputTestCompleted);
            Assert.True(preview.IsPlayingTone);

            preview.Tick();
            Assert.False(preview.OutputTestCompleted);
            Assert.False(preview.IsSpeakerTestBusy);
            Assert.Contains("stopped responding", preview.OutputStatus, StringComparison.Ordinal);
        }
        finally
        {
            preview.Dispose();
        }
    }
    [Fact]
    public async Task TerminalCallbackAtSuccessCommitBoundaryInvalidatesSuccess()
    {
        var lease = new FakeDesktopLease();
        lease.AllowStart();
        var preview = new FirstRunAudioPreview(new FakeDesktopHost(lease));
        try
        {
            await StartToneAsync(preview, lease);
            await WaitUntilAsync(() => QueueCount(preview, "_toneCompletions") != 0);

            var toneStateGate = GetPrivateField<object>(preview, "_desktopToneStateGate");
            using var tickStarted = new ManualResetEventSlim();
            Task completionTick = Task.CompletedTask;
            Monitor.Enter(toneStateGate);
            try
            {
                completionTick = Task.Run(() =>
                {
                    tickStarted.Set();
                    InvokePrivate(preview, "TickDesktopToneCompletions");
                }, TestContext.Current.CancellationToken);
                tickStarted.Wait(TestContext.Current.CancellationToken);

                lease.Callbacks!.OnPlaybackState(Playback(
                    "stopped",
                    string.Empty,
                    41,
                    error: "native playback stopped",
                    errorCode: "stream-error"));
                Assert.False(preview.OutputTestCompleted);
            }
            finally
            {
                Monitor.Exit(toneStateGate);
            }

            await AwaitSignal(completionTick);
            Assert.False(preview.OutputTestCompleted);
            preview.Tick();
            Assert.False(preview.OutputTestCompleted);
            Assert.False(preview.IsSpeakerTestBusy);
        }
        finally
        {
            preview.Dispose();
        }
    }


    [Fact]
    public async Task StaleFailureDoesNotCancelCurrentTone()
    {
        var first = new FakeDesktopLease();
        var second = new FakeDesktopLease();
        first.AllowStart();
        second.AllowStart();
        var preview = new FirstRunAudioPreview(new FakeDesktopHost(first, second));
        try
        {
            var draft = Draft(speaker: "speaker-a");
            preview.StartMicrophone(draft);
            await PumpUntilAsync(preview, () => preview.IsListening);

            draft.SpeakerDevice = "speaker-b";
            preview.RestartForDeviceChange(draft);
            await PumpUntilAsync(preview, () => second.Routes.Count != 0 && preview.IsListening);
            await StartToneAsync(preview, second);

            first.Callbacks!.OnDead("late old failure");
            await PumpUntilAsync(preview, () => preview.OutputTestCompleted);

            Assert.True(preview.OutputTestCompleted);
            Assert.Contains("complete", preview.OutputStatus, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            preview.Dispose();
        }
    }

    [Fact]
    public async Task BlockedFinalFramePreventsEarlySuccess()
    {
        var lease = new FakeDesktopLease(blockFinalFrame: true);
        lease.AllowStart();
        var preview = new FirstRunAudioPreview(new FakeDesktopHost(lease));
        try
        {
            await StartToneAsync(preview, lease);
            await AwaitSignal(lease.FinalFrameEntered.Task);

            preview.Tick();
            Assert.False(preview.OutputTestCompleted);
            Assert.True(preview.IsPlayingTone);

            lease.AllowFinalFrame();
            await PumpUntilAsync(preview, () => preview.OutputTestCompleted);
            Assert.Equal(FirstRunToneGenerator.FrameCount, lease.OutputFrames);
        }
        finally
        {
            lease.AllowFinalFrame();
            preview.Dispose();
        }
    }

    [Fact]
    public async Task RejectedFinalFramePublishesFailure()
    {
        var lease = new FakeDesktopLease(rejectFinalFrame: true);
        lease.AllowStart();
        var preview = new FirstRunAudioPreview(new FakeDesktopHost(lease));
        try
        {
            await StartToneAsync(preview, lease);
            await AwaitSignal(lease.FinalFrameEntered.Task);
            await PumpUntilAsync(preview, () => !preview.IsSpeakerTestBusy);

            Assert.False(preview.OutputTestCompleted);
            Assert.Equal(FirstRunToneGenerator.FrameCount, lease.OutputFrames);
            Assert.Equal("Could not play the test sound", preview.OutputStatus);
        }
        finally
        {
            preview.Dispose();
        }
    }

    private static FirstRunSetupDraft Draft(string speaker = "")
        => new()
        {
            MicrophoneDevice = string.Empty,
            SpeakerDevice = speaker,
            MicVolume = 1f,
            MicSensitivity = 1f,
            BaseVadThreshold = 0.01f,
            MasterVolume = 1f,
            EchoCancellation = true,
            NoiseSuppression = true,
        };

    private static SidecarPlaybackState Playback(
        string state,
        string action,
        ulong generation,
        string error = "",
        string errorCode = "")
        => new(
            state,
            action,
            generation,
            "speaker-a",
            "speaker-a",
            RequestedDefault: false,
            RequestedMatched: true,
            FellBackToDefault: false,
            Running: state is "stream-started" or "first-callback",
            Error: error,
            ErrorCode: errorCode,
            Changed: action == "configure-audio-route");

    private static async Task StartToneAsync(
        FirstRunAudioPreview preview,
        FakeDesktopLease lease)
    {
        preview.PlayTestSound(Draft(speaker: "speaker-a"));
        await AwaitSignal(lease.RouteConfigured.Task);
        lease.Callbacks!.OnPlaybackState(Playback("command-accepted", "configure-audio-route", 0));
        lease.Callbacks.OnPlaybackState(Playback("stream-started", string.Empty, 41));
        lease.Callbacks.OnPlaybackState(Playback("first-callback", string.Empty, 41));
        await PumpUntilAsync(preview, () => preview.IsPlayingTone);
    }

    private static async Task AwaitSignal(Task signal)
        => await signal.WaitAsync(
            TimeSpan.FromSeconds(3),
            TestContext.Current.CancellationToken);

    private static async Task PumpUntilAsync(
        FirstRunAudioPreview preview,
        Func<bool> condition)
    {
        await WaitUntilAsync(() =>
        {
            preview.Tick();
            return condition();
        });
        preview.Tick();
        Assert.True(condition());
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var timeout = Stopwatch.StartNew();
        while (!condition() && timeout.Elapsed < TimeSpan.FromSeconds(4))
            await Task.Delay(1, TestContext.Current.CancellationToken);
        Assert.True(condition());
    }

    private static int QueueCount(FirstRunAudioPreview preview, string fieldName)
    {
        var field = typeof(FirstRunAudioPreview).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        var queue = Assert.IsAssignableFrom<object>(field!.GetValue(preview));
        var count = queue.GetType().GetProperty("Count", BindingFlags.Instance | BindingFlags.Public);
        return Assert.IsType<int>(count!.GetValue(queue));
    }

    private static void InvokePrivate(FirstRunAudioPreview preview, string methodName)
    {
        var method = typeof(FirstRunAudioPreview).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        method!.Invoke(preview, null);
    }
    private static T GetPrivateField<T>(FirstRunAudioPreview preview, string fieldName)
    {
        var field = typeof(FirstRunAudioPreview).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        return Assert.IsAssignableFrom<T>(field!.GetValue(preview));
    }


    private sealed class FakeDesktopHost : IFirstRunDesktopAudioHost
    {
        private readonly ConcurrentQueue<FakeDesktopLease> _leases;

        internal FakeDesktopHost(params FakeDesktopLease[] leases)
        {
            _leases = new ConcurrentQueue<FakeDesktopLease>(leases);
        }

        public IFirstRunDesktopAudioLease? TryAcquire(
            SidecarVoiceCallbacks callbacks,
            out string failure)
        {
            if (!_leases.TryDequeue(out var lease))
            {
                failure = "lease-active:test";
                return null;
            }
            lease.Callbacks = callbacks;
            failure = string.Empty;
            return lease;
        }
    }

    private sealed class FakeDesktopLease : IFirstRunDesktopAudioLease
    {
        private readonly ManualResetEventSlim _allowStart = new();
        private readonly TaskCompletionSource<bool> _allowRelease =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ManualResetEventSlim _allowFinalFrame;
        private readonly bool _blockFinalFrame;
        private readonly bool _rejectFinalFrame;

        internal FakeDesktopLease(
            bool holdRelease = false,
            bool blockFinalFrame = false,
            bool rejectFinalFrame = false)
        {
            _allowFinalFrame = new ManualResetEventSlim(!blockFinalFrame);
            _blockFinalFrame = blockFinalFrame;
            _rejectFinalFrame = rejectFinalFrame;
            if (!holdRelease) _allowRelease.TrySetResult(true);
        }

        internal TaskCompletionSource<bool> StartEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource<bool> RouteConfigured { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource<bool> ReleaseStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource<bool> Released { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource<bool> FinalFrameEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal ConcurrentQueue<(string InputDevice, string OutputDevice, SidecarCaptureMode Mode)>
            Routes
        { get; } = new();
        internal SidecarVoiceCallbacks? Callbacks;
        internal string StartSpeakerDevice = string.Empty;
        internal int OutputFrames;

        internal void AllowStart()
            => _allowStart.Set();

        internal void AllowRelease()
            => _allowRelease.TrySetResult(true);

        internal void AllowFinalFrame()
            => _allowFinalFrame.Set();

        public bool EnsureStarted(string microphoneDevice, string speakerDevice)
        {
            StartSpeakerDevice = speakerDevice;
            StartEntered.TrySetResult(true);
            _allowStart.Wait(TestContext.Current.CancellationToken);
            return true;
        }

        public void SetDsp(bool echoCancellation, bool noiseSuppression, bool strongerNoiseSuppression)
        {
        }

        public void SetInput(float gain, float vadThreshold, float noiseGateThreshold)
        {
        }

        public void SetMonitor(bool enabled, bool delayed, float gain)
        {
        }

        public bool ConfigureAudioRoute(
            string inputDevice,
            string outputDevice,
            SidecarCaptureMode captureMode)
        {
            Routes.Enqueue((inputDevice, outputDevice, captureMode));
            RouteConfigured.TrySetResult(true);
            return true;
        }

        public void SendOutputTestFrame(float[] interleavedStereo)
            => Interlocked.Increment(ref OutputFrames);

        public bool SendOutputTestFrameAndWait(float[] interleavedStereo)
        {
            int frame = Interlocked.Increment(ref OutputFrames);
            if (frame != FirstRunToneGenerator.FrameCount) return true;
            FinalFrameEntered.TrySetResult(true);
            if (_blockFinalFrame)
                _allowFinalFrame.Wait(TestContext.Current.CancellationToken);
            return !_rejectFinalFrame;
        }

        public async Task ReleaseAsync()
        {
            ReleaseStarted.TrySetResult(true);
            await _allowRelease.Task.ConfigureAwait(false);
            Released.TrySetResult(true);
        }

        public void Dispose()
            => AllowRelease();
    }
}
