using AmongUs.Data;
using HoryTweaks.Core.Validation;
using HoryTweaks.Features.GameOptions;
using HoryTweaks.Game;
using HoryTweaks.Game.Players;
using HoryTweaks.Generated;
using HoryTweaks.Infrastructure.Configuration;
using HoryTweaks.Modules.Support;
using HoryTweaks.Plugin.Registration;
using HoryTweaks.Structs;
using HoryTweaks.Utilities;
using Il2CppInterop.Runtime.Attributes;
using TMPro;
using UnityEngine;

namespace HoryTweaks.Features.PlayerInfo;

/// <summary>
/// Displays extended player information during gameplay with time-based throttling and dirty-checks.
/// </summary>
[RegisterInIl2Cpp]
internal class PlayerInfoDisplay : MonoBehaviour
{
    private const float UpdateIntervalSeconds = 0.2f;
    private static readonly Vector3 NameParentLocalPosition = new(0f, 0.8f, -0.5f);

    protected PlayerControl? _player;
    protected TextMeshPro? _nameText;
    protected TextMeshPro? _infoText;
    protected TextMeshPro? _topText;
    protected TextMeshPro? _bottomText;

    private readonly SplitStringBuilder _ssbTag = new(100, '-');
    private readonly SplitStringBuilder _ssbTagTop = new(100, '-');
    private readonly SplitStringBuilder _ssbTagBottom = new(100, '-');
    private string _lastTopText = string.Empty;
    private string _lastBottomText = string.Empty;
    private string _lastInfoText = string.Empty;
    private string? _lastRawName;
    private string? _lastColorBlindText;
    private Vector3 _lastColorBlindPos;
    private bool _hasColorBlindPos;
    private float _nextUpdateTime;

    internal void Init(PlayerControl player)
    {
        _player = player;

        var nameTextTransform = player.gameObject.transform.Find("Names/NameText_TMP");
        if (nameTextTransform == null)
            return;

        _nameText = nameTextTransform.GetComponent<TextMeshPro>();

        _infoText = InstantiatePlayerInfoText("InfoText_Info_TMP", new Vector3(0f, 0.25f), nameTextTransform);
        _topText = InstantiatePlayerInfoText("InfoText_T_TMP", new Vector3(0f, 0.15f), nameTextTransform);
        _bottomText = InstantiatePlayerInfoText("InfoText_B_TMP", new Vector3(0f, -0.15f), nameTextTransform);
        _infoText.fontSize = 1.3f;
        _topText.fontSize = 1.3f;
        _bottomText.fontSize = 1.3f;

        if (_nameText != null && _nameText.transform.parent != null)
        {
            _nameText.transform.parent.localPosition = NameParentLocalPosition;
        }
    }

    protected TextMeshPro InstantiatePlayerInfoText(string name, Vector3 positionOffset, Transform parent)
    {
        var newTextObject = Instantiate(_nameText, parent);
        newTextObject.name = name;
        newTextObject.transform.DestroyChildren();
        newTextObject.transform.position += positionOffset;

        var textMesh = newTextObject.GetComponent<TextMeshPro>();
        textMesh.text = string.Empty;
        newTextObject.gameObject.SetActive(true);

        return textMesh;
    }

    private void ResetText()
    {
        if (_infoText != null && _lastInfoText.Length > 0)
        {
            _infoText.SetText(string.Empty);
            _lastInfoText = string.Empty;
        }

        if (_topText != null && _lastTopText.Length > 0)
        {
            _topText.SetText(string.Empty);
            _lastTopText = string.Empty;
        }

        if (_bottomText != null && _lastBottomText.Length > 0)
        {
            _bottomText.SetText(string.Empty);
            _lastBottomText = string.Empty;
        }
    }

    protected virtual void LateUpdate()
    {
        float now = Time.unscaledTime;
        if (now < _nextUpdateTime)
            return;

        _nextUpdateTime = now + UpdateIntervalSeconds;

        if (_player == null || _player.Data == null || _player.Data.ExtendedData() == null || _nameText == null)
        {
            ResetText();
            return;
        }

        _ssbTag.Clear();
        _ssbTagTop.Clear();
        _ssbTagBottom.Clear();

        UpdatePlayerInfo();
        UpdateColorBlindTextPosition();

        var parent = _nameText.transform.parent;
        if (parent != null && parent.localPosition != NameParentLocalPosition)
        {
            parent.localPosition = NameParentLocalPosition;
        }
    }

    private void UpdatePlayerInfo()
    {
        if (_player == null || _player.Data == null || _nameText == null ||
            _topText == null || _bottomText == null || _infoText == null)
            return;

        var betterData = _player.ExtendedData();

        if (!_player.DataIsCollected())
        {
            string loading = TranslationStrings.Player_Loading.LocalizedString;
            if (!string.Equals(_nameText.text, loading, StringComparison.Ordinal))
            {
                _nameText.text = loading;
            }
            return;
        }

        if (!BAUConfigs.LobbyPlayerInfo.Value && GameState.IsLobby)
        {
            ResetText();
            if (!BAUModdedSupportFlags.HasFlag(BAUModdedSupportFlags.Disable_NameOverride))
            {
                SetRawNameIfChanged(_player.Data.PlayerName);
            }
            return;
        }

        if (DataManager.Settings == null || DataManager.Settings.Gameplay == null)
            return;

        string newName = _player.Data.PlayerName;

        if (GameState.IsInGame && GameState.IsLobby && !GameState.IsFreePlay)
        {
            string platform = DataManager.Settings.Gameplay.StreamerMode
                ? TranslationStrings.Player_PlatformHidden.LocalizedString
                : Utils.GetPlatformName(_player, useTag: true);

            string friendCode = ValidateFriendCode(out string friendCodeColor);

            SetLobbyInfo(ref newName, betterData, _ssbTag);

            _ssbTagTop.Append($"<color=#9e9e9e>{platform}</color>")
                .Append($"<color=#ffd829>{TranslationStrings.Player_Level.LocalizedString}: {_player.Data.PlayerLevel + 1}</color>");

            _ssbTagBottom.Append($"<color={friendCodeColor}>{friendCode}</color>");
        }
        else if ((GameState.IsInGame || GameState.IsFreePlay) && !GameState.IsHideNSeek)
        {
            SetInGameInfo(_ssbTagTop);
        }

        if (!BAUModdedSupportFlags.HasFlag(BAUModdedSupportFlags.Disable_NameOverride))
        {
            if (!_player.IsInShapeshift())
            {
                if (_player.IsImpostorTeammate())
                    newName = newName.ToColor(Colors.ImpostorRed);

                SetRawNameIfChanged(newName);
            }
            else
            {
                var targetData = Utils.PlayerDataFromPlayerId((byte)_player.shapeshiftTargetPlayerId);
                if (targetData == null)
                    return;

                var betterTargetData = targetData.ExtendedData();
                string name = betterTargetData != null ? betterTargetData.RealName : targetData.PlayerName;

                if (_player.IsImpostorTeammate())
                    name = name.ToColor(Colors.ImpostorRed);

                SetRawNameIfChanged(name);
            }
        }

        UpdateTextIfChanged(_topText, _ssbTagTop, ref _lastTopText);
        UpdateTextIfChanged(_bottomText, _ssbTagBottom, ref _lastBottomText);
        UpdateTextIfChanged(_infoText, _ssbTag, ref _lastInfoText);
    }

    private void SetRawNameIfChanged(string name)
    {
        if (_player == null)
            return;

        if (!string.Equals(_lastRawName, name, StringComparison.Ordinal))
        {
            _player.RawSetName(name);
            _lastRawName = name;
        }
    }

    private static void UpdateTextIfChanged(TextMeshPro textMesh, SplitStringBuilder ssb, ref string lastValue)
    {
        if (textMesh == null)
            return;

        if (BAUModdedSupportFlags.HasFlag(BAUModdedSupportFlags.Disable_PlayerInfo))
        {
            if (lastValue.Length > 0)
            {
                textMesh.SetText(string.Empty);
                lastValue = string.Empty;
            }
            return;
        }

        string newText = ssb.ToString();
        if (!string.Equals(newText, lastValue, StringComparison.Ordinal))
        {
            textMesh.SetText(newText);
            lastValue = newText;
        }
    }

    private string ValidateFriendCode(out string color)
    {
        color = "#FFFFFF";
        if (_player == null || _player.Data == null)
            return string.Empty;

        string friendCode = _player.Data.FriendCode;
        var invalidReason = FriendCodeRules.Validate(friendCode);
        bool isValidFriendCode = invalidReason == FriendCodeInvalidReason.None;

        if (invalidReason == FriendCodeInvalidReason.Empty)
        {
            friendCode = TranslationStrings.Player_NoFriendCode.LocalizedString;
        }

        if (!isValidFriendCode && GameState.IsHost && BetterGameSettings.InvalidFriendCode.GetBool())
        {
            string kickMessage = TranslationStrings.HostTools_KickMessage.Format(
                TranslationStrings.HostTools_Kick,
                TranslationStrings.HostTools_InvalidFriendCode);
            _player.TryKick(false, kickMessage);
        }

        color = isValidFriendCode ? "#00f7ff" : "#ff0000";

        if (DataManager.Settings.Gameplay.StreamerMode)
        {
            friendCode = new string('*', friendCode.Length);
        }

        return friendCode.Trim();
    }

    [HideFromIl2Cpp]
    private void SetLobbyInfo(ref string newName, ExtendedPlayerInfo? betterData, SplitStringBuilder ssbTag)
    {
        if (betterData == null || _player == null)
            return;

        if (_player.IsHost() && BAUConfigs.LobbyPlayerInfo.Value)
            newName = _player.GetPlayerNameAndColor();

        if ((_player.IsLocalPlayer() || betterData.IsBetterUser) && !GameState.IsInGamePlay)
        {
            bool isHoryUser = _player.IsLocalPlayer() || betterData.IsHoryUser;
            string verificationSymbol = betterData.IsVerifiedBetterUser || _player.IsLocalPlayer() ? "✓ " : "";

            ssbTag.AppendFormat("<color={2}>{1}{0}</color>",
                isHoryUser ? TranslationStrings.Player_HoryUser.LocalizedString : TranslationStrings.Player_BetterUser.LocalizedString,
                verificationSymbol,
                isHoryUser ? "#ffffbe" : "#a8d8ff");
        }

        ssbTag.Append($"<color=#b554ff>{TranslationStrings.Command_PlayerInfo_ID.LocalizedString}: {_player.PlayerId}</color>");
    }

    [HideFromIl2Cpp]
    private void SetInGameInfo(SplitStringBuilder ssbTagTop)
    {
        if (_player != null)
        {
            ssbTagTop.Append(_player.GetRoleInfo(true));
        }
    }

    private void UpdateColorBlindTextPosition()
    {
        if (_player == null || BAUModdedSupportFlags.HasFlag(BAUModdedSupportFlags.Disable_CustomColorBlindText))
            return;

        var text = _player.cosmetics.colorBlindText;
        if (text == null)
            return;

        if (!DataManager.Settings.Accessibility.ColorBlindMode)
        {
            if (text.enabled)
                text.enabled = false;
            return;
        }

        if (!text.enabled)
            return;

        Vector3 targetPos;
        if (!BAUConfigs.BetterColorblindText.Value)
        {
            targetPos = BAUConfigs.ColorblindTextOnTop.Value
                ? new Vector3(0f, 0.4f, 0f)
                : new Vector3(0f, -0.2f, 0f);
        }
        else if (BAUConfigs.ColorblindTextOnTop.Value)
        {
            string cbText = _player.cosmetics.GetColorBlindText();
            if (!string.Equals(_lastColorBlindText, cbText, StringComparison.Ordinal))
            {
                text.text = cbText;
                _lastColorBlindText = cbText;
            }
            targetPos = new Vector3(0f, 0.4f, 0.4999f);
        }
        else
        {
            targetPos = (!_player.onLadder && !_player.MyPhysics.Animations.IsPlayingAnyLadderAnimation())
                ? new Vector3(0f, -1.3f, 0.4999f)
                : new Vector3(0f, -1.5f, 0.4999f);
        }

        if (!_hasColorBlindPos || _lastColorBlindPos != targetPos)
        {
            text.transform.localPosition = targetPos;
            _lastColorBlindPos = targetPos;
            _hasColorBlindPos = true;
        }
    }
}
