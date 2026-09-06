using VoiceChatPlugin.VoiceChat;
using Xunit;

namespace PerfectComms.Tests;

public sealed class VoiceCompactStatusPolicyTests
{
    [Fact]
    public void HiddenWhenThereIsNoExistingStatusOrEnabledStateWarning()
    {
        Assert.Equal(string.Empty, VoiceCompactStatusPolicy.Compose(null, null, false, false, true));
    }

    [Fact]
    public void ExistingTransientKeepsItsCurrentMessageAndShowsDeafenStateBelowIt()
    {
        string text = VoiceCompactStatusPolicy.Compose(
            "Voice connection refreshed", null, false, true, true);

        Assert.Equal(
            "<color=#FFCC66>Voice connection refreshed</color>\n<color=#FF7373>Ensordecido</color>",
            text);
    }

    [Fact]
    public void OperationalWarningIsRetainedWithCombinedManualStates()
    {
        string text = VoiceCompactStatusPolicy.Compose(
            null, "quien convocó tiene la palabra (5s)", true, true, true);

        Assert.Equal(
            "<color=#FFCC66>quien convocó tiene la palabra (5s)</color>\n<color=#FF7373>Silenciado / Ensordecido</color>",
            text);
    }

    [Fact]
    public void ExistingOperationalWarningRemainsVisibleDuringTransientStatus()
    {
        string text = VoiceCompactStatusPolicy.Compose(
            "Conexión de voz actualizada", "quien convocó tiene la palabra (5s)", false, true, true);

        Assert.Equal(
            "<color=#FFCC66>Conexión de voz actualizada\nquien convocó tiene la palabra (5s)</color>\n<color=#FF7373>Ensordecido</color>",
            text);
    }

    [Fact]
    public void MuteAndDeafenWarningsCanBeDisabledWithoutHidingExistingWarnings()
    {
        Assert.Equal(
            "<color=#FFCC66>Connecting voice... 1/3 players connected</color>",
            VoiceCompactStatusPolicy.Compose(
                null,
                "Connecting voice... 1/3 players connected",
                true,
                true,
                false));
        Assert.Equal(string.Empty, VoiceCompactStatusPolicy.Compose(null, null, true, true, false));
    }

    [Theory]
    [InlineData(true, false, "Silenciado")]
    [InlineData(false, true, "Ensordecido")]
    [InlineData(true, true, "Silenciado / Ensordecido")]
    public void StateWordingIsExplicit(bool muted, bool deafened, string expected)
    {
        Assert.Equal(expected, VoiceCompactStatusPolicy.StateText(muted, deafened));
    }
}
