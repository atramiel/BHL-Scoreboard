using CommunityToolkit.Mvvm.ComponentModel;
using Scoreboard.Services;
using System.Linq;
using System.Windows.Media;

namespace Scoreboard.ViewModels;

public class TrophyItem
{
    public string AwardName { get; set; } = "";
    public string TeamName { get; set; } = "";
    public string Notes { get; set; } = "";
}

/// <summary>
/// The closing podium — champion and runner-up need no lookup (they're just the
/// championship game that just ended), 3rd place and custom trophies are best-effort
/// live lookups that gracefully show nothing if they can't be determined.
/// </summary>
public class AwardsCeremonyViewModel : ObservableObject
{
    public string Champion { get; }
    public string RunnerUp { get; }
    public ImageSource? ChampionLogo { get; }
    public ImageSource? RunnerUpLogo { get; }

    /// <summary>Empty if 3rd place couldn't be determined — the ceremony just skips that slot.</summary>
    public string ThirdPlaceText { get; }
    public bool HasThirdPlace => ThirdPlaceText.Length > 0;

    public List<TrophyItem> Trophies { get; }
    public bool HasTrophies => Trophies.Count > 0;

    public AwardsCeremonyViewModel(
        string champion, string runnerUp,
        ImageSource? championLogo, ImageSource? runnerUpLogo,
        List<string> thirdPlace, List<LeagueSiteService.AwardEntry> trophies)
    {
        Champion = champion;
        RunnerUp = runnerUp;
        ChampionLogo = championLogo;
        RunnerUpLogo = runnerUpLogo;
        ThirdPlaceText = thirdPlace.Count switch
        {
            0 => "",
            1 => thirdPlace[0],
            _ => string.Join(" & ", thirdPlace) + " (tie)",
        };
        Trophies = [.. trophies.Select(a => new TrophyItem { AwardName = a.AwardName, TeamName = a.TeamName, Notes = a.Notes })];
    }
}
