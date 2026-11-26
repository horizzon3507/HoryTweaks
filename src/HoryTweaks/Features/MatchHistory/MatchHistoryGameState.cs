using HoryTweaks.Core.MatchHistory;
using InnerNet;

namespace HoryTweaks.Features.MatchHistory;

/// <summary>
/// Maps the live client game state to the Unity-free <see cref="ClientGameState"/>.
/// </summary>
internal static class MatchHistoryGameState
{
    /// <summary>
    /// The current client state, or <c>null</c> when there is no client.
    /// </summary>
    internal static ClientGameState? Current
    {
        get
        {
            var client = AmongUsClient.Instance;
            if (client == null)
                return null;

            return client.GameState switch
            {
                InnerNetClient.GameStates.NotJoined => ClientGameState.NotJoined,
                InnerNetClient.GameStates.Joined => ClientGameState.Joined,
                InnerNetClient.GameStates.Started => ClientGameState.Started,
                InnerNetClient.GameStates.Ended => ClientGameState.Ended,
                _ => null
            };
        }
    }
}
