using AmongUs.Data;
using HoryTweaks.Game;
using HoryTweaks.Game.Players;
using HoryTweaks.Generated;
using HoryTweaks.Infrastructure.Configuration;
using HoryTweaks.Infrastructure.UnityInterop;
using HoryTweaks.Modules.Support;
using HoryTweaks.Plugin.Registration;
using HoryTweaks.Structs;
using HoryTweaks.Utilities;
using TMPro;
using UnityEngine;

namespace HoryTweaks.Features.PlayerInfo;

/// <summary>
/// Displays extended player information during meetings with time-based updates and cached child transforms.
/// </summary>
[RegisterInIl2Cpp]
internal sealed class MeetingInfoDisplay : PlayerInfoDisplay
{
    private const float MeetingUpdateIntervalSeconds = 0.2f;

    private NetworkedPlayerInfo? _playerInfo;
    private PlayerVoteArea? _pva;
    private Transform? _votePlayerBase;
    private Transform? _deadXBorder;
    private Vector3 _namePos;
    private Vector3 _infoPos;
    private Vector3 _topPos;

    private readonly SplitStringBuilder _ssbTag = new(100, '-');
    private readonly SplitStringBuilder _ssbInfo = new(100, '-');
    private string _lastInfoText = string.Empty;
    private string _lastTopText = string.Empty;
    private float _nextMeetingUpdateTime;
    private bool _disconnectApplied;
    private int _lastLayoutState = -1;

    internal void Init(NetworkedPlayerInfo? playerInfo, PlayerVoteArea pva)
    {
        _playerInfo = playerInfo;
        _pva = pva;

        _nameText = pva.NameText;
        _infoText = InstantiatePlayerInfoText("InfoText_Info_TMP", new Vector3(0f, 0.28f), pva.transform);
        _topText = InstantiatePlayerInfoText("InfoText_T_TMP", new Vector3(0f, 0.15f), pva.transform);
        _infoText.fontSize = 1.3f;
        _topText.fontSize = 1.3f;
        _namePos = _nameText.transform.localPosition - new Vector3(0f, 0.02f, 0f);
        _infoPos = _infoText.transform.localPosition;
        _topPos = _topText.transform.localPosition;

        _votePlayerBase = pva.transform.Find("votePlayerBase");
        _deadXBorder = pva.transform.Find("deadX_border");

        var playerLevel = pva.transform.Find("PlayerLevel");
        if (playerLevel != null)
        {
            playerLevel.localPosition = new Vector3(playerLevel.localPosition.x, playerLevel.localPosition.y, -2f);
            var levelDisplay = Instantiate(playerLevel, pva.transform);
            levelDisplay.transform.SetSiblingIndex(playerLevel.GetSiblingIndex() + 1);
            levelDisplay.gameObject.name = "PlayerId";
            levelDisplay.GetComponent<SpriteRenderer>().color = new Color(1f, 0f, 1f, 1f);
            var idLabel = levelDisplay.transform.Find("LevelLabel");
            var idNumber = levelDisplay.transform.Find("LevelNumber");
            idLabel.gameObject.DestroyTextTranslators();
            idLabel.GetComponent<TextMeshPro>().text = TranslationStrings.Command_PlayerInfo_ID.LocalizedString;
            idNumber.GetComponent<TextMeshPro>().text = pva.PlayerId.Value.ToString();
            idLabel.name = "IdLabel";
            idNumber.name = "IdNumber";
            playerLevel.transform.position += new Vector3(0.23f, 0f);
        }
    }

    protected override void LateUpdate()
    {
        float now = Time.unscaledTime;
        if (now < _nextMeetingUpdateTime)
            return;

        _nextMeetingUpdateTime = now + MeetingUpdateIntervalSeconds;

        if (_pva == null)
            return;

        _ssbTag.Clear();
        _ssbInfo.Clear();

        if (_playerInfo != null && _playerInfo.Object != null)
        {
            UpdateInfo();
        }
        else
        {
            UpdateDisconnect();
        }

        UpdateTextPositions();
        UpdateColorBlindName();
    }

    private void UpdateColorBlindName()
    {
        if (_pva == null || _pva.ColorBlindName == null || BAUModdedSupportFlags.HasFlag(BAUModdedSupportFlags.Disable_CustomColorBlindText))
            return;

        var cbObj = _pva.ColorBlindName.gameObject;
        if (!DataManager.Settings.Accessibility.ColorBlindMode)
        {
            if (cbObj.activeSelf)
                cbObj.SetActive(false);
            return;
        }

        if (!cbObj.activeSelf)
            cbObj.SetActive(true);

        float offsetY = BAUConfigs.ColorblindTextOnTop.Value ? 0.19f : -0.19f;
        Vector3 targetPos = BAUConfigs.BetterColorblindText.Value
            ? new Vector3(-0.91f, offsetY, -0.05f)
            : _pva.NameText.transform.localPosition + new Vector3(0f, offsetY, 0f);

        if (_pva.ColorBlindName.transform.localPosition != targetPos)
        {
            _pva.ColorBlindName.transform.localPosition = targetPos;
        }
    }

    private void UpdateTextPositions()
    {
        if (_nameText == null || _infoText == null || _topText == null)
            return;

        bool hasInfoText = !string.IsNullOrEmpty(_lastInfoText);
        bool hasTopText = !string.IsNullOrEmpty(_lastTopText);
        int state = (hasInfoText ? 1 : 0) | (hasTopText ? 2 : 0);

        if (state == _lastLayoutState)
            return;

        _lastLayoutState = state;

        if (hasInfoText && hasTopText)
        {
            _nameText.transform.localPosition = _namePos + new Vector3(0f, -0.1f, 0f);
            _infoText.transform.localPosition = _infoPos + new Vector3(0f, -0.1f, 0f);
            _topText.transform.localPosition = _topPos + new Vector3(0f, -0.1f, 0f);
        }
        else if (hasInfoText || hasTopText)
        {
            _nameText.transform.localPosition = _namePos;
            _infoText.transform.localPosition = _topPos;
            _topText.transform.localPosition = _topPos;
        }
        else
        {
            _nameText.transform.localPosition = _namePos;
            _infoText.transform.localPosition = _infoPos;
            _topText.transform.localPosition = _topPos;
        }
    }

    private void UpdateInfo()
    {
        if (_playerInfo == null || _playerInfo.ExtendedData() == null)
            return;

        _ssbInfo.Append(_playerInfo.GetRoleInfo(true));
        string infoText = _ssbInfo.ToString();
        string topText = _ssbTag.ToString();

        UpdateNameTextPosition(infoText);
        UpdateTextIfChanged(_infoText, infoText, ref _lastInfoText);
        UpdateTextIfChanged(_topText, topText, ref _lastTopText);
    }

    private void UpdateNameTextPosition(string infoText)
    {
        if (_pva == null || BAUModdedSupportFlags.HasFlag(BAUModdedSupportFlags.Disable_PlayerMeetingInfo))
            return;

        float targetY = !string.IsNullOrEmpty(infoText) ? -0.045f : 0.015f;
        var current = _pva.NameText.transform.localPosition;
        if (Mathf.Abs(current.y - targetY) > 0.0001f)
        {
            _pva.NameText.transform.localPosition = new Vector3(current.x, targetY, current.z);
        }
    }

    private static void UpdateTextIfChanged(TextMeshPro? textMesh, string newText, ref string lastValue)
    {
        if (textMesh == null)
            return;

        if (BAUModdedSupportFlags.HasFlag(BAUModdedSupportFlags.Disable_PlayerMeetingInfo))
        {
            if (lastValue.Length > 0)
            {
                textMesh.SetText(string.Empty);
                lastValue = string.Empty;
            }
            return;
        }

        if (!string.Equals(newText, lastValue, StringComparison.Ordinal))
        {
            textMesh.SetText(newText);
            lastValue = newText;
        }
    }

    private void UpdateDisconnect()
    {
        string disconnectText = GetDisconnectText();

        if (!string.Equals(disconnectText, _lastInfoText, StringComparison.Ordinal) && _infoText != null)
        {
            _infoText.SetText($"<color=#6b6b6b>{disconnectText}</color>");
            _lastInfoText = disconnectText;
        }

        if (_lastTopText.Length > 0 && _topText != null)
        {
            _topText.SetText(string.Empty);
            _lastTopText = string.Empty;
        }

        if (_disconnectApplied || _pva == null)
            return;

        _disconnectApplied = true;

        if (_votePlayerBase != null && _votePlayerBase.gameObject.activeSelf)
        {
            _votePlayerBase.gameObject.SetActive(false);
        }

        if (_deadXBorder != null && _deadXBorder.gameObject.activeSelf)
        {
            _deadXBorder.gameObject.SetActive(false);
        }

        _pva.ClearForResults();
        _pva.SetDisabled();
    }

    private string GetDisconnectText()
    {
        if (_pva == null || GameData.Instance == null)
            return string.Empty;

        var playerData = GameData.Instance.GetPlayerById(_pva.PlayerId);
        if (playerData == null)
            return string.Empty;

        var betterData = playerData.ExtendedData();
        if (betterData == null)
            return string.Empty;

        return betterData.DisconnectReason switch
        {
            DisconnectReasons.ExitGame => TranslationStrings.DisconnectReasonMeeting_Left.LocalizedString,
            DisconnectReasons.Banned => TranslationStrings.DisconnectReasonMeeting_Banned.LocalizedString,
            DisconnectReasons.Kicked => TranslationStrings.DisconnectReasonMeeting_Kicked.LocalizedString,
            DisconnectReasons.Hacking => TranslationStrings.DisconnectReasonMeeting_Cheater.LocalizedString,
            _ => TranslationStrings.DisconnectReasonMeeting_Left.LocalizedString
        };
    }
}
