using AmongUs.GameOptions;

using BetterAmongUs.Utilities;
using Hazel;
using BetterAmongUs.Plugin.Registration;

namespace BetterAmongUs.Networking.Rpc.Handlers;

[RegisterRPCHandler]
internal sealed class CheckAppearHandler : RPCHandler
{
    internal override byte CallId => (byte)RpcCalls.CheckAppear;

    internal override bool BetterHandle(PlayerControl? sender, MessageReader reader)
    {
        bool shouldAnimate = reader.ReadBoolean();

        if (sender.Is(RoleTypes.Phantom)
            && sender.IsAlive() && sender.IsImpostorTeam()
            && !sender.inMovingPlat
            && !sender.onLadder
            && !sender.MyPhysics.Animations.IsPlayingAnyLadderAnimation())
        {
            if (!sender.IsInVent() && shouldAnimate == false)
            {
                LogRpcInfo($"Phantom attempted to appear without animation while not in vent.");
                return false;
            }

            if (AmongUsClient.Instance.AmClient)
            {
                sender.SetRoleInvisibility(false, !sender.IsInVent(), true);
            }
            sender.RpcAppear(!sender.IsInVent());
        }

        return false;
    }

}