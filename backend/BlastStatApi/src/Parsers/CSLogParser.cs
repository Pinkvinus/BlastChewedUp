using System.Text.RegularExpressions;
using BlastStatApi.Models;

namespace BlastStatApi.Parsers;

/// <summary>
/// Parses a CS:GO server log file into structured match data.
/// Strategy:
///   1. Find the LAST Match_Start event (per the hint, only the last one is real).
///   2. From that point, collect Round_Start / Round_End pairs, kills, and team/score lines.
///   3. Build per-player stats from the collected kill events.
/// </summary>
public class CSLogParser
{
    // ── Regex patterns ─────────────────────────────────────────────────────────
    private static readonly Regex TimestampPattern =
        new(@"^(\d{2}/\d{2}/\d{4} - \d{2}:\d{2}:\d{2}): (.*)$", RegexOptions.Compiled);

    private static readonly Regex MatchStartPattern =
        new(@"World triggered ""Match_Start"" on ""(.+)""", RegexOptions.Compiled);

    private static readonly Regex TeamPlayingPattern =
        new(@"^Team playing ""(CT|TERRORIST)"": (.+)$", RegexOptions.Compiled);

    private static readonly Regex RoundStartPattern =
        new(@"World triggered ""Round_Start""", RegexOptions.Compiled);

    private static readonly Regex RoundEndPattern =
        new(@"World triggered ""Round_End""", RegexOptions.Compiled);

    private static readonly Regex TeamWinPattern =
        new(@"Team ""(CT|TERRORIST)"" triggered ""(SFUI_Notice_\w+)"" \(CT ""(\d+)""\) \(T ""(\d+)""\)",
            RegexOptions.Compiled);

    private static readonly Regex KillPattern =
        new(@"""(.+?)<\d+><[^>]*><(CT|TERRORIST)>"" \[.+?\] killed ""(.+?)<\d+><[^>]*><(CT|TERRORIST)>"" \[.+?\] with ""(\w+)""(?: \(headshot\))?",
            RegexOptions.Compiled);

    // ── Public entry point ─────────────────────────────────────────────────────
    public MatchData Parse(string logContent)
    {
        var lines = logContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var entries = ParseTimestamps(lines);

        int lastMatchStartIndex = FindLastMatchStart(entries);
        var matchEntries = entries.Skip(lastMatchStartIndex).ToList();

        var (teamCT, teamT) = ExtractTeams(matchEntries);
        var (rounds, kills) = ExtractRoundsAndKills(matchEntries, teamCT, teamT);
        var playerStats = BuildPlayerStats(kills, rounds.Count);

        string map = ExtractMap(matchEntries); // was: ExtractMap(entries, lastMatchStartIndex)

        return new MatchData(map, teamCT, teamT, rounds, kills, playerStats);
    }

    // ── Step 1: Parse timestamps ───────────────────────────────────────────────
    private static List<(DateTime Time, string Content)> ParseTimestamps(string[] lines)
    {
        var entries = new List<(DateTime, string)>();
        foreach (var line in lines)
        {
            var m = TimestampPattern.Match(line.Trim());
            if (!m.Success) continue;
            if (DateTime.TryParseExact(m.Groups[1].Value, "MM/dd/yyyy - HH:mm:ss",
                    null, System.Globalization.DateTimeStyles.None, out var ts))
                entries.Add((ts, m.Groups[2].Value));
        }
        return entries;
    }

    // ── Step 2: Find the last Match_Start ─────────────────────────────────────
    private static int FindLastMatchStart(List<(DateTime, string Content)> entries)
    {
        int idx = -1;
        for (int i = 0; i < entries.Count; i++)
            if (MatchStartPattern.IsMatch(entries[i].Content))
                idx = i;
        return idx < 0 ? 0 : idx;
    }

    private static string ExtractMap(List<(DateTime Time, string Content)> entries)
    {
        var first = entries.FirstOrDefault(e => MatchStartPattern.IsMatch(e.Content));
        var m = MatchStartPattern.Match(first.Content ?? string.Empty);
        return m.Success ? m.Groups[1].Value : "unknown";
    }

    // ── Step 3: Extract CT / T team names ─────────────────────────────────────
    private static (TeamInfo CT, TeamInfo T) ExtractTeams(List<(DateTime, string Content)> entries)
    {
        string ct = "CT", t = "T";
        foreach (var (_, content) in entries.Take(20))
        {
            var m = TeamPlayingPattern.Match(content);
            if (!m.Success) continue;
            if (m.Groups[1].Value == "CT") ct = m.Groups[2].Value.Trim();
            else t = m.Groups[2].Value.Trim();
        }
        return (new TeamInfo(ct, "CT"), new TeamInfo(t, "TERRORIST"));
    }

    // ── Step 4: Walk log building rounds + kills ───────────────────────────────
    private static (List<Round> Rounds, List<KillEvent> Kills) ExtractRoundsAndKills(
        List<(DateTime Time, string Content)> entries,
        TeamInfo teamCT, TeamInfo teamT)
    {
        var rounds = new List<Round>();
        var allKills = new List<KillEvent>();

        DateTime? roundStart = null;
        var roundKills = new List<KillEvent>();
        int roundNumber = 0;

        foreach (var (time, content) in entries)
        {
            // Round start
            if (RoundStartPattern.IsMatch(content))
            {
                roundStart = time;
                roundKills = new List<KillEvent>();
                continue;
            }

            // Kill event (only player-vs-player, not "killed other")
            var killMatch = KillPattern.Match(content);
            if (killMatch.Success && roundStart.HasValue)
            {
                bool headshot = content.Contains("(headshot)");
                var kill = new KillEvent(
                    Timestamp: time,
                    KillerName: killMatch.Groups[1].Value.Trim(),
                    KillerTeam: ResolveTeamName(killMatch.Groups[2].Value, teamCT, teamT),
                    VictimName: killMatch.Groups[3].Value.Trim(),
                    VictimTeam: ResolveTeamName(killMatch.Groups[4].Value, teamCT, teamT),
                    Weapon: killMatch.Groups[5].Value,
                    Headshot: headshot,
                    RoundNumber: roundNumber + 1
                );
                roundKills.Add(kill);
                allKills.Add(kill);
                continue;
            }

            // Round end with win condition
            var winMatch = TeamWinPattern.Match(content);
            if (winMatch.Success && roundStart.HasValue)
            {
                roundNumber++;
                string winnerSide = winMatch.Groups[1].Value;
                string winCondition = MapWinCondition(winMatch.Groups[2].Value);
                int scoreCT = int.Parse(winMatch.Groups[3].Value);
                int scoreT = int.Parse(winMatch.Groups[4].Value);

                rounds.Add(new Round(
                    RoundNumber: roundNumber,
                    WinnerSide: winnerSide == "CT" ? teamCT.Name : teamT.Name,
                    WinCondition: winCondition,
                    ScoreCT: scoreCT,
                    ScoreT: scoreT,
                    StartTime: roundStart.Value,
                    EndTime: time,
                    DurationSeconds: (time - roundStart.Value).TotalSeconds,
                    Kills: new List<KillEvent>(roundKills)
                ));
                roundStart = null;
            }
        }

        return (rounds, allKills);
    }

    // ── Step 5: Aggregate player stats ────────────────────────────────────────
    private static List<PlayerStats> BuildPlayerStats(List<KillEvent> kills, int totalRounds)
    {
        var players = new Dictionary<string, (string Team, int K, int D, int HS, Dictionary<string, int> Weapons)>();

        void Ensure(string name, string team)
        {
            if (!players.ContainsKey(name))
                players[name] = (team, 0, 0, 0, new Dictionary<string, int>());
        }

        foreach (var kill in kills)
        {
            Ensure(kill.KillerName, kill.KillerTeam);
            Ensure(kill.VictimName, kill.VictimTeam);

            var (team, k, d, hs, weapons) = players[kill.KillerName];
            weapons.TryGetValue(kill.Weapon, out int wc);
            weapons[kill.Weapon] = wc + 1;
            players[kill.KillerName] = (team, k + 1, d, kill.Headshot ? hs + 1 : hs, weapons);

            var (vt, vk, vd, vhs, vw) = players[kill.VictimName];
            players[kill.VictimName] = (vt, vk, vd + 1, vhs, vw);
        }

        return players.Select(p =>
        {
            var (team, k, d, hs, weapons) = p.Value;
            return new PlayerStats(
                Name: p.Key,
                Team: team,
                Kills: k,
                Deaths: d,
                Headshots: hs,
                HeadshotPercentage: k > 0 ? Math.Round((double)hs / k * 100, 1) : 0,
                WeaponKills: weapons,
                RoundsPlayed: totalRounds
            );
        }).OrderByDescending(p => p.Kills).ToList();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────
    private static string ResolveTeamName(string side, TeamInfo ct, TeamInfo t) =>
        side == "CT" ? ct.Name : t.Name;

    private static string MapWinCondition(string sfui) => sfui switch
    {
        "SFUI_Notice_CTs_Win"         => "Elimination",
        "SFUI_Notice_Terrorists_Win"  => "Elimination",
        "SFUI_Notice_Target_Bombed"   => "Bomb Exploded",
        "SFUI_Notice_Bomb_Defused"    => "Bomb Defused",
        _                             => sfui
    };
}