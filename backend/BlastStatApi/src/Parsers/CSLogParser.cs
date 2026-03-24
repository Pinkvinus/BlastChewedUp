using System.Text.RegularExpressions;
using BlastStatApi.Models;

namespace BlastStatApi.Parsers;

public class CSLogParser
{
    // ── Regex patterns ─────────────────────────────────────────────────────────
    private static readonly Regex TimestampPattern =
        new(@"^(\d{2}/\d{2}/\d{4} - \d{2}:\d{2}:\d{2}): (.*)$", RegexOptions.Compiled);

    private static readonly Regex WorldTriggerPattern =
        new(@"World triggered ""(?<action>[^""]+)""(?: on ""(?<map>[^""]+)"")?", RegexOptions.Compiled);
    
    private static readonly Regex AdminPattern =
        new(@"^[^\]]*\]\s*(?<team1>.+?)\s*\[(?<score1>\d+)\s*-\s*(?<score2>\d+)\]\s*(?<team2>.+)$",
            RegexOptions.Compiled);
    private static readonly Regex TeamPlayingPattern =
        new(@"^Team playing ""(CT|TERRORIST)"": (.+)$", RegexOptions.Compiled);

    private static readonly Regex TeamWinPattern =
        new(@"Team ""(CT|TERRORIST)"" triggered ""(SFUI_Notice_\w+)"" \(CT ""(\d+)""\) \(T ""(\d+)""\)",
            RegexOptions.Compiled);

    private static readonly Regex KillPattern =
        new(@"""(.+?)<\d+><[^>]*><(CT|TERRORIST)>"" \[.+?\] killed ""(.+?)<\d+><[^>]*><(CT|TERRORIST)>"" \[.+?\] with ""(\w+)""(?: \(headshot\))?",
            RegexOptions.Compiled);

    private static readonly Regex UtilPattern =
        new(@"""(.+?)<\d+><[^>]*><(CT|TERRORIST)>"" threw (smokegrenade|flashbang|hegrenade|molotov|incgrenade)",
            RegexOptions.Compiled);

    private static readonly Regex BombPlantPattern =
        new(@"""(.+?)<\d+><[^>]*><(CT|TERRORIST)>"" triggered ""Planted_The_Bomb"" at bombsite (\w+)",
            RegexOptions.Compiled);

    private static readonly Regex BombDefusePattern =
        new(@"""(.+?)<\d+><[^>]*><(CT|TERRORIST)>"" triggered ""Defused_The_Bomb""",
            RegexOptions.Compiled);

    // "Name<id><steam><SIDE>" money change 16000-2700 = $13300 (tracked) (purchase: weapon_ak47)
    private static readonly Regex PurchasePattern =
        new(@"""(.+?)<\d+><[^>]*><(CT|TERRORIST)>"" money change (\d+)-(\d+) = \$(\d+) \(tracked\) \(purchase: ([^)]+)\)",
            RegexOptions.Compiled);

    // ── Public entry point ─────────────────────────────────────────────────────
    public MatchData Parse(string logContent)
    {
        var lines = logContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var entries = ParseTimestamps(lines);

        int lastMatchStartIndex = FindLastMatchStart(entries);
        var matchEntries = entries.Skip(lastMatchStartIndex).ToList();

        var (teamCT, teamT) = ExtractTeams(matchEntries);
        
        // Seed matchStart from the Match_Start timestamp so purchases during
        // the freeze time before Round 1's Round_Start are captured.
        DateTime matchStartTime = entries[lastMatchStartIndex].Time;
        var (rounds, kills, events) = ExtractAll(matchEntries, teamCT, teamT, matchStartTime);

        var playerStats = BuildPlayerStats(kills, rounds.Count);

        string map = ExtractMap(entries, lastMatchStartIndex);
        return new MatchData(map, teamCT, teamT, rounds, kills, events, playerStats);
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
        for (int i = 0; i < entries.Count; i++){
            var match = WorldTriggerPattern.Match(entries[i].Content);
            if (!match.Success) continue;
            if (match.Groups["action"].Value == "Match_Start") idx = i;
        }
        return idx < 0 ? 0 : idx;
    }

    private static string ExtractMap(List<(DateTime, string Content)> entries, int idx)
    {
        var m = WorldTriggerPattern.Match(entries[idx].Content);
        return m.Success ? m.Groups["map"].Value : "unknown";
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

    // ── Step 4: Walk the log ───────────────────────────────────────────────────
    private static (List<Round> Rounds, List<KillEvent> Kills, List<MatchEvent> Events)
    ExtractAll(List<(DateTime Time, string Content)> entries, TeamInfo teamCT, TeamInfo teamT,
               DateTime seededMatchStart)
    {
        var rounds    = new List<Round>();
        var allKills  = new List<KillEvent>();
        var allEvents = new List<MatchEvent>();

        DateTime? matchStart = seededMatchStart;  // pre-seeded so pre-round-1 purchases are captured
        DateTime? roundStart = null;
        var roundKills = new List<KillEvent>();
        int roundNumber = 0;

        //foreach (var (time, content) in entries)
        for (int i = 0; i < entries.Count; i++)
        {
            var (time, content) = entries[i];
            double offset() => (time - matchStart!.Value).TotalSeconds;

            // ── Round start ────────────────────────────────────────────────────
            var match = WorldTriggerPattern.Match(content);
            if (match.Success && match.Groups["action"].Value == "Round_Start")
            {
                matchStart ??= time;
                roundStart = time;
                roundKills = new List<KillEvent>();
                roundNumber++;

                allEvents.Add(new MatchEvent(
                    MatchOffsetSeconds: (roundStart.Value - matchStart.Value).TotalSeconds,
                    RoundNumber: roundNumber,
                    EventType: "round_start",
                    PlayerName: "",
                    PlayerTeam: "",
                    Detail: $"Round {roundNumber}",
                    Headshot: false
                ));
                continue;
            }

            // ── Purchase ───────────────────────────────────────────────────────
            var purchaseMatch = PurchasePattern.Match(content);
            if (purchaseMatch.Success)
            {
                string item     = purchaseMatch.Groups[6].Value.Trim();
                int before      = int.Parse(purchaseMatch.Groups[3].Value);
                int cost        = int.Parse(purchaseMatch.Groups[4].Value);
                int after       = int.Parse(purchaseMatch.Groups[5].Value);
                string friendly = CleanItemName(item);

                allEvents.Add(new MatchEvent(
                    MatchOffsetSeconds: offset(),
                    RoundNumber: roundNumber,
                    EventType: "purchase",
                    PlayerName: purchaseMatch.Groups[1].Value.Trim(),
                    PlayerTeam: ResolveTeamName(purchaseMatch.Groups[2].Value, teamCT, teamT),
                    Detail: $"{friendly}|{cost}|{before}|{after}",
                    Headshot: false
                ));
                continue;
            }
            if (!roundStart.HasValue) continue;


            // ── Kill ───────────────────────────────────────────────────────────
            var killMatch = KillPattern.Match(content);
            if (killMatch.Success)
            {
                bool headshot   = content.Contains("(headshot)");
                string killer   = killMatch.Groups[1].Value.Trim();
                string killerT  = ResolveTeamName(killMatch.Groups[2].Value, teamCT, teamT);
                string victim   = killMatch.Groups[3].Value.Trim();
                string victimT  = ResolveTeamName(killMatch.Groups[4].Value, teamCT, teamT);
                string weapon   = killMatch.Groups[5].Value;

                var kill = new KillEvent(time, killer, killerT, victim, victimT,
                    weapon, headshot, roundNumber, offset());
                roundKills.Add(kill);
                allKills.Add(kill);

                allEvents.Add(new MatchEvent(offset(), roundNumber, "kill",
                    killer, killerT,
                    Detail: $"{weapon}|{victim}|{victimT}",
                    Headshot: headshot));
                continue;
            }

            // ── Util throw ─────────────────────────────────────────────────────
            var utilMatch = UtilPattern.Match(content);
            if (utilMatch.Success)
            {
                allEvents.Add(new MatchEvent(offset(), roundNumber, "util",
                    PlayerName: utilMatch.Groups[1].Value.Trim(),
                    PlayerTeam: ResolveTeamName(utilMatch.Groups[2].Value, teamCT, teamT),
                    Detail: utilMatch.Groups[3].Value,
                    Headshot: false));
                continue;
            }

            // ── Bomb plant ─────────────────────────────────────────────────────
            var plantMatch = BombPlantPattern.Match(content);
            if (plantMatch.Success)
            {
                allEvents.Add(new MatchEvent(offset(), roundNumber, "bomb_plant",
                    PlayerName: plantMatch.Groups[1].Value.Trim(),
                    PlayerTeam: ResolveTeamName(plantMatch.Groups[2].Value, teamCT, teamT),
                    Detail: $"bombsite {plantMatch.Groups[3].Value}",
                    Headshot: false));
                continue;
            }

            // ── Bomb defuse ────────────────────────────────────────────────────
            var defuseMatch = BombDefusePattern.Match(content);
            if (defuseMatch.Success)
            {
                allEvents.Add(new MatchEvent(offset(), roundNumber, "bomb_defuse",
                    PlayerName: defuseMatch.Groups[1].Value.Trim(),
                    PlayerTeam: ResolveTeamName(defuseMatch.Groups[2].Value, teamCT, teamT),
                    Detail: null,
                    Headshot: false));
                continue;
            }

            // ── Round end ──────────────────────────────────────────────────────
            var winMatch = TeamWinPattern.Match(content);
            if (winMatch.Success)
            {
                string winnerSide   = winMatch.Groups[1].Value;
                string winCondition = MapWinCondition(winMatch.Groups[2].Value);

                var (_, adminContent) = entries[i+7]; // Admin log with final score usually appears 7 lines after round end
                var adminMatch = AdminPattern.Match(adminContent);

                int scoreCT = int.Parse(adminMatch.Groups["score2"].Value);
                int scoreT  = int.Parse(adminMatch.Groups["score1"].Value);

                rounds.Add(new Round(
                    Number: roundNumber,
                    WinnerSide: winnerSide == "CT" ? teamCT.Name : teamT.Name,
                    WinCondition: winCondition,
                    ScoreCT: scoreCT, ScoreT: scoreT,
                    StartTime: roundStart.Value, EndTime: time,
                    DurationSeconds: (time - roundStart.Value).TotalSeconds,
                    MatchOffsetSeconds: (roundStart.Value - matchStart.Value).TotalSeconds,
                    Kills: new List<KillEvent>(roundKills)
                ));
                roundStart = null;
            }
        }

        return (rounds, allKills, allEvents);
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
                Name: p.Key, Team: team, Kills: k, Deaths: d, Headshots: hs,
                HeadshotPercentage: k > 0 ? Math.Round((double)hs / k * 100, 1) : 0,
                WeaponKills: weapons, RoundsPlayed: totalRounds
            );
        }).OrderByDescending(p => p.Kills).ToList();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────
    private static string ResolveTeamName(string side, TeamInfo ct, TeamInfo t) =>
        side == "CT" ? ct.Name : t.Name;

    private static string MapWinCondition(string sfui) => sfui switch
    {
        "SFUI_Notice_CTs_Win"        => "Elimination",
        "SFUI_Notice_Terrorists_Win" => "Elimination",
        "SFUI_Notice_Target_Bombed"  => "Bomb Exploded",
        "SFUI_Notice_Bomb_Defused"   => "Bomb Defused",
        _                            => sfui
    };

    private static string CleanItemName(string item) => item
        .Replace("weapon_", "")
        .Replace("item_assaultsuit", "Kevlar + Helmet")
        .Replace("item_defuser", "Defuse Kit")
        .Replace("_silencer", " (silenced)")
        .Replace("_", " ");
}