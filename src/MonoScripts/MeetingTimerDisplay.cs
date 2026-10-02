using AmongUs.GameOptions;
using BetterAmongUs.Attributes;
using BetterAmongUs.Generated;
using BetterAmongUs.Modules;
using BetterAmongUs.Patches.Gameplay.UI;
using BetterAmongUs.Utilities;
using System.Text;
using TMPro;
using UnityEngine;

namespace BetterAmongUs.MonoScripts;

/// <summary>
/// Displays the meeting timer (elapsed and remaining discussion + voting time)
/// and an aggregate count of players who have not voted yet.
/// </summary>
[RegisterInIl2Cpp]
internal sealed class MeetingTimerDisplay : MonoBehaviour
{
    private MeetingHud? _meetingHud;
    private TextMeshPro? _timerText;
    private string _lastText = "";
    private int _lastUpdateFrame;
    private const int UPDATE_COOLDOWN = 5;

    /// <summary>
    /// Initializes the meeting timer display.
    /// </summary>
    /// <param name="meetingHud">The MeetingHud this display is attached to.</param>
    internal void Init(MeetingHud meetingHud)
    {
        _meetingHud = meetingHud;

        if (meetingHud.TitleText == null)
            return;

        _timerText = Instantiate(meetingHud.TitleText, meetingHud.TitleText.transform.parent);
        _timerText.name = "MeetingTimer_TMP";
        _timerText.transform.localPosition += new Vector3(0f, -0.5f, 0f);
        _timerText.transform.DestroyChildren();
        _timerText.gameObject.DestroyTextTranslators();
        _timerText.fontSize = 1.6f;
        _timerText.text = string.Empty;
        _timerText.gameObject.SetActive(true);
    }

    /// <summary>
    /// LateUpdate with cooldown for performance optimization.
    /// </summary>
    private void LateUpdate()
    {
        if (Time.frameCount - _lastUpdateFrame < UPDATE_COOLDOWN)
            return;

        _lastUpdateFrame = Time.frameCount;

        if (_timerText == null || _meetingHud == null || !GameState.IsMeeting)
        {
            UpdateText(string.Empty);
            return;
        }

        StringBuilder sb = new();
        sb.Append(BuildTimerLine());

        if (GameState.IsVoting)
        {
            int notVoted = CountPlayersNotVoted();

            // Aggregate count only: never expose who voted, respecting anonymous votes
            if (notVoted > 0)
            {
                if (sb.Length > 0)
                    sb.Append('\n');

                sb.Append(notVoted == 1
                    ? TranslationStrings.Meeting_PlayerNotVoted.Format(notVoted.ToString())
                    : TranslationStrings.Meeting_PlayersNotVoted.Format(notVoted.ToString()));
            }
        }

        UpdateText(sb.ToString());
    }

    /// <summary>
    /// Builds the elapsed/remaining timer line.
    /// </summary>
    private static string BuildTimerLine()
    {
        string elapsed = FormatSeconds(MeetingHudPatch.timeOpen);
        float total = GetMeetingTotalSeconds();

        if (total < 0f)
            return $"⏱ {elapsed}";

        string remaining = FormatSeconds(Mathf.Max(0f, total - MeetingHudPatch.timeOpen));
        return TranslationStrings.Meeting_Timer.Format(elapsed, remaining);
    }

    /// <summary>
    /// Gets the configured discussion + voting time in seconds, or -1 when unavailable.
    /// </summary>
    private static float GetMeetingTotalSeconds()
    {
        var options = GameOptionsManager.Instance?.CurrentGameOptions;

        if (options == null || !options.TryGetInt(Int32OptionNames.VotingTime, out var votingTime))
            return -1f;

        options.TryGetInt(Int32OptionNames.DiscussionTime, out var discussionTime);
        return discussionTime + votingTime;
    }

    /// <summary>
    /// Counts living, connected players who have not cast a vote yet.
    /// </summary>
    private int CountPlayersNotVoted()
    {
        if (_meetingHud == null || _meetingHud.playerStates == null)
            return 0;

        int count = 0;

        foreach (var pva in _meetingHud.playerStates)
        {
            if (pva == null || pva.AmDead || pva.DidVote)
                continue;

            var data = Utils.PlayerDataFromPlayerId(pva.PlayerId);
            if (data == null || data.Object == null)
                continue;

            count++;
        }

        return count;
    }

    /// <summary>
    /// Updates text only when it changed.
    /// </summary>
    private void UpdateText(string newText)
    {
        if (_timerText == null || newText == _lastText)
            return;

        _timerText.SetText(newText);
        _lastText = newText;
    }

    /// <summary>
    /// Formats seconds as mm:ss.
    /// </summary>
    private static string FormatSeconds(float seconds)
    {
        int total = Mathf.Max(0, (int)seconds);
        return $"{total / 60:00}:{total % 60:00}";
    }
}
