using AmongUs.GameOptions;

using HoryTweaks.Utilities;
using Hazel;
using InnerNet;
using HoryTweaks.Plugin.Registration;
using HoryTweaks.Game;

namespace HoryTweaks.Networking.Rpc.Handlers;

[RegisterRPCHandler]
internal sealed class CheckShapeshiftHandler : RPCHandler
{
    internal override byte CallId => (byte)RpcCalls.CheckShapeshift;

    internal override bool BetterHandle(PlayerControl? sender, MessageReader reader)
    {
        PlayerControl target = reader.ReadNetObject<PlayerControl>();
        bool flag = reader.ReadBoolean();

        if (target != null)
        {
            if (sender.Is(RoleTypes.Shapeshifter)
                && sender.IsAlive()
                && sender.IsImpostorTeam()
                && !sender.inMovingPlat
                && !sender.shapeshifting
                && !sender.onLadder
                && !sender.MyPhysics.Animations.IsPlayingAnyLadderAnimation())
            {
                if (!sender.IsInVent() && !GameState.IsMeeting && !GameState.IsExilling && flag == false)
                {
                    LogRpcInfo($"Shapeshifter attempted to shapeshift without animation while not in vent");
                    return false;
                }

                sender.RpcShapeshift(target, !sender.IsInVent() && !GameState.IsMeeting && !GameState.IsExilling);
            }
            else
            {
                string senderIssue = GetSenderIssue(sender);
                LogRpcInfo($"Invalid shapeshift attempt: {senderIssue}");
            }
        }
        else
        {
            LogRpcInfo($"Shapeshift target is null");
        }

        return false;
    }

    private static string GetSenderIssue(PlayerControl sender)
    {
        if (!sender.Is(RoleTypes.Shapeshifter)) return "Sender not Shapeshifter role";
        if (!sender.IsAlive()) return "Sender not alive";
        if (!sender.IsImpostorTeam()) return "Sender not impostor team";
        if (sender.inMovingPlat) return "Sender in moving platform";
        if (sender.shapeshifting) return "Sender already shapeshifting";
        if (sender.onLadder) return "Sender on ladder";
        if (sender.MyPhysics.Animations.IsPlayingAnyLadderAnimation()) return "Sender in ladder animation";

        return "Unknown sender issue";
    }
}