using HoryTweaks.Core.Rejoin;
using Xunit;

namespace HoryTweaks.Tests.Rejoin;

public class RejoinPolicyTests
{
    // DisconnectReasons values mirrored from the game enum; kept as ints so the
    // policy under test stays free of IL2CPP references.
    private const int ExitGame = 0;
    private const int GameFull = 1;
    private const int GameStarted = 2;
    private const int GameNotFound = 3;
    private const int IncorrectVersion = 5;
    private const int Banned = 6;
    private const int Kicked = 7;
    private const int Custom = 8;
    private const int InvalidName = 9;
    private const int Hacking = 10;
    private const int NotAuthorized = 11;
    private const int ConnectionLimit = 12;
    private const int Destroy = 16;
    private const int Error = 17;
    private const int MismatchedVersion = 21;
    private const int InternalConnectionToken = 102;
    private const int PlatformLock = 103;
    private const int LobbyInactivity = 104;
    private const int NoServersAvailable = 107;
    private const int Sanctions = 112;
    private const int DuplicateConnectionDetected = 115;
    private const int FocusLostBackground = 207;
    private const int IntentionalLeaving = 208;
    private const int FocusLost = 209;
    private const int NewConnection = 210;
    private const int PlatformUserBlock = 212;
    private const int ServerNotFound = 214;
    private const int ClientTimeout = 215;
    private const int Unknown = 255;

    [Theory]
    [InlineData(ExitGame)]
    [InlineData(Destroy)]
    [InlineData(FocusLostBackground)]
    [InlineData(IntentionalLeaving)]
    [InlineData(FocusLost)]
    [InlineData(NewConnection)]
    public void Classify_marks_player_driven_disconnects_voluntary(int reason)
    {
        Assert.Equal(DisconnectKind.Voluntary, RejoinPolicy.Classify(reason));
    }

    [Theory]
    [InlineData(Banned)]
    [InlineData(Kicked)]
    [InlineData(Hacking)]
    [InlineData(Sanctions)]
    [InlineData(Custom)]
    [InlineData(InvalidName)]
    [InlineData(NotAuthorized)]
    [InlineData(IncorrectVersion)]
    [InlineData(MismatchedVersion)]
    [InlineData(PlatformLock)]
    [InlineData(PlatformUserBlock)]
    public void Classify_marks_removals_and_policy_blocks(int reason)
    {
        Assert.Equal(DisconnectKind.Removal, RejoinPolicy.Classify(reason));
    }

    [Theory]
    [InlineData(Error)]
    [InlineData(ClientTimeout)]
    [InlineData(Unknown)]
    [InlineData(GameNotFound)]
    [InlineData(GameFull)]
    [InlineData(GameStarted)]
    [InlineData(ConnectionLimit)]
    [InlineData(InternalConnectionToken)]
    [InlineData(LobbyInactivity)]
    [InlineData(NoServersAvailable)]
    [InlineData(DuplicateConnectionDetected)]
    [InlineData(ServerNotFound)]
    public void Classify_treats_retryable_failures_as_transient(int reason)
    {
        Assert.Equal(DisconnectKind.Transient, RejoinPolicy.Classify(reason));
    }

    [Theory]
    [InlineData(Error, true)]
    [InlineData(ClientTimeout, true)]
    [InlineData(ExitGame, false)]
    [InlineData(IntentionalLeaving, false)]
    [InlineData(Banned, false)]
    [InlineData(Kicked, false)]
    [InlineData(Hacking, false)]
    public void ShouldAttemptRejoin_only_for_transient_reasons(int reason, bool expected)
    {
        Assert.Equal(expected, RejoinPolicy.ShouldAttemptRejoin(reason));
    }

    [Theory]
    [InlineData(Banned, true)]
    [InlineData(Kicked, true)]
    [InlineData(ExitGame, true)]
    [InlineData(GameNotFound, false)]
    [InlineData(ClientTimeout, false)]
    public void ShouldCancelRejoin_stops_on_voluntary_or_removal(int reason, bool expected)
    {
        Assert.Equal(expected, RejoinPolicy.ShouldCancelRejoin(reason));
    }

    [Fact]
    public void Attempt_interval_fits_several_retries_inside_window()
    {
        Assert.True(RejoinPolicy.RejoinWindowSeconds >= 30f);
        Assert.True(RejoinPolicy.AttemptIntervalSeconds > 0f);
        Assert.True(RejoinPolicy.RejoinWindowSeconds / RejoinPolicy.AttemptIntervalSeconds >= 3);
        Assert.True(RejoinPolicy.AttemptTimeoutSeconds >= RejoinPolicy.AttemptIntervalSeconds);
    }
}
