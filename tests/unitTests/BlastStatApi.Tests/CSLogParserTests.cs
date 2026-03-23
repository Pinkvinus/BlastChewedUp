using BlastStatApi.Parsers;

namespace BlastStatApi.Tests.src.Parsers;

/// <summary>
/// Unit tests for CsgoLogParser.
/// Each test builds a minimal synthetic log string — no file I/O needed.
/// </summary>
public class CsgoLogParserTests
{
    private readonly CsgoLogParser _parser = new();

    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Wraps a list of log lines with a valid timestamp prefix.
    /// All lines share the same base time; each is offset by one second.
    /// </summary>
    private static string BuildLog(params string[] lines)
    {
        var base_ = new DateTime(2021, 11, 28, 20, 0, 0);
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < lines.Length; i++)
            sb.AppendLine($"{(base_ + TimeSpan.FromSeconds(i)):MM/dd/yyyy - HH:mm:ss}: {lines[i]}");
        return sb.ToString();
    }

    /// <summary>Minimal single-round log that results in one completed round.</summary>
    private static string SingleRoundLog(
        string ctTeam = "TeamVitality",
        string tTeam  = "NAVI",
        string winner = "CT",
        string sfui   = "SFUI_Notice_CTs_Win",
        int scoreCT   = 1,
        int scoreT    = 0) => BuildLog(
        $"World triggered \"Match_Start\" on \"de_nuke\"",
        $"Team playing \"CT\": {ctTeam}",
        $"Team playing \"TERRORIST\": {tTeam}",
        "World triggered \"Round_Start\"",
        $"Team \"{winner}\" triggered \"{sfui}\" (CT \"{scoreCT}\") (T \"{scoreT}\")",
        "World triggered \"Round_End\""
    );

    // ── Map extraction ──────────────────────────────────────────────────────────

    [Fact]
    public void Parse_ExtractsMapName()
    {
        var result = _parser.Parse(SingleRoundLog());
        Assert.Equal("de_nuke", result.Map);
    }

    // ── Team extraction ────────────────────────────────────────────────────────

    [Fact]
    public void Parse_ExtractsCTTeamName()
    {
        var result = _parser.Parse(SingleRoundLog(ctTeam: "TeamVitality"));
        Assert.Equal("TeamVitality", result.TeamCT.Name);
    }

    [Fact]
    public void Parse_ExtractsTTeamName()
    {
        var result = _parser.Parse(SingleRoundLog(tTeam: "NAVI"));
        Assert.Equal("NAVI", result.TeamT.Name);
    }

    // ── Last Match_Start wins ──────────────────────────────────────────────────

    [Fact]
    public void Parse_UsesLastMatchStart_IgnoresEarlierRounds()
    {
        // One round before the second Match_Start — should be ignored
        var log = BuildLog(
            "World triggered \"Match_Start\" on \"de_nuke\"",
            "Team playing \"CT\": EarlyTeam",
            "Team playing \"TERRORIST\": EarlyTeam2",
            "World triggered \"Round_Start\"",
            "Team \"CT\" triggered \"SFUI_Notice_CTs_Win\" (CT \"1\") (T \"0\")",
            "World triggered \"Round_End\"",
            // Real match starts here
            "World triggered \"Match_Start\" on \"de_nuke\"",
            "Team playing \"CT\": TeamVitality",
            "Team playing \"TERRORIST\": NAVI",
            "World triggered \"Round_Start\"",
            "Team \"CT\" triggered \"SFUI_Notice_CTs_Win\" (CT \"1\") (T \"0\")",
            "World triggered \"Round_End\""
        );

        var result = _parser.Parse(log);

        // Only 1 round after the last Match_Start
        Assert.Equal(1, result.Rounds.Count);
        Assert.Equal("TeamVitality", result.TeamCT.Name);
    }

    // ── Round parsing ──────────────────────────────────────────────────────────

    [Fact]
    public void Parse_CountsRoundsCorrectly()
    {
        var log = BuildLog(
            "World triggered \"Match_Start\" on \"de_nuke\"",
            "Team playing \"CT\": TeamVitality",
            "Team playing \"TERRORIST\": NAVI",
            "World triggered \"Round_Start\"",
            "Team \"CT\" triggered \"SFUI_Notice_CTs_Win\" (CT \"1\") (T \"0\")",
            "World triggered \"Round_End\"",
            "World triggered \"Round_Start\"",
            "Team \"TERRORIST\" triggered \"SFUI_Notice_Target_Bombed\" (CT \"1\") (T \"1\")",
            "World triggered \"Round_End\"",
            "World triggered \"Round_Start\"",
            "Team \"CT\" triggered \"SFUI_Notice_Bomb_Defused\" (CT \"2\") (T \"1\")",
            "World triggered \"Round_End\""
        );

        var result = _parser.Parse(log);
        Assert.Equal(3, result.Rounds.Count);
    }

    [Fact]
    public void Parse_RoundsHaveCorrectNumbers()
    {
        var log = BuildLog(
            "World triggered \"Match_Start\" on \"de_nuke\"",
            "Team playing \"CT\": TeamVitality",
            "Team playing \"TERRORIST\": NAVI",
            "World triggered \"Round_Start\"",
            "Team \"CT\" triggered \"SFUI_Notice_CTs_Win\" (CT \"1\") (T \"0\")",
            "World triggered \"Round_End\"",
            "World triggered \"Round_Start\"",
            "Team \"TERRORIST\" triggered \"SFUI_Notice_Terrorists_Win\" (CT \"1\") (T \"1\")",
            "World triggered \"Round_End\""
        );

        var result = _parser.Parse(log);
        Assert.Equal(1, result.Rounds[0].RoundNumber);
        Assert.Equal(2, result.Rounds[1].RoundNumber);
    }

    [Fact]
    public void Parse_RoundScoresAreCorrect()
    {
        var result = _parser.Parse(SingleRoundLog(scoreCT: 3, scoreT: 2));
        Assert.Equal(3, result.Rounds[0].ScoreCT);
        Assert.Equal(2, result.Rounds[0].ScoreT);
    }

    [Fact]
    public void Parse_RoundDurationIsPositive()
    {
        // Round_Start and Round_End are 5 seconds apart in BuildLog (indices 3 and 5)
        var result = _parser.Parse(SingleRoundLog());
        Assert.True(result.Rounds[0].DurationSeconds > 0);
    }

    // ── Win conditions ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("SFUI_Notice_CTs_Win",        "Elimination")]
    [InlineData("SFUI_Notice_Terrorists_Win", "Elimination")]
    [InlineData("SFUI_Notice_Target_Bombed",  "Bomb Exploded")]
    [InlineData("SFUI_Notice_Bomb_Defused",   "Bomb Defused")]
    public void Parse_MapsWinConditionsCorrectly(string sfui, string expected)
    {
        string winner = sfui.Contains("CT") ? "CT" : "TERRORIST";
        var log = SingleRoundLog(winner: winner, sfui: sfui);
        var result = _parser.Parse(log);
        Assert.Equal(expected, result.Rounds[0].WinCondition);
    }

    [Fact]
    public void Parse_CTWin_WinnerSideIsCTTeamName()
    {
        var result = _parser.Parse(SingleRoundLog(ctTeam: "TeamVitality", winner: "CT"));
        Assert.Equal("TeamVitality", result.Rounds[0].WinnerSide);
    }

    [Fact]
    public void Parse_TerroristWin_WinnerSideIsTTeamName()
    {
        var result = _parser.Parse(SingleRoundLog(
            tTeam: "NAVI", winner: "TERRORIST",
            sfui: "SFUI_Notice_Terrorists_Win", scoreCT: 0, scoreT: 1));
        Assert.Equal("NAVI", result.Rounds[0].WinnerSide);
    }

    // ── Kill parsing ───────────────────────────────────────────────────────────

    [Fact]
    public void Parse_CountsKillsCorrectly()
    {
        var log = BuildLog(
            "World triggered \"Match_Start\" on \"de_nuke\"",
            "Team playing \"CT\": TeamVitality",
            "Team playing \"TERRORIST\": NAVI",
            "World triggered \"Round_Start\"",
            "\"s1mple<30><STEAM_1:1:1><TERRORIST>\" [0 0 0] killed \"ZywOo<26><STEAM_1:1:2><CT>\" [0 0 0] with \"ak47\"",
            "\"s1mple<30><STEAM_1:1:1><TERRORIST>\" [0 0 0] killed \"apEX<25><STEAM_1:1:3><CT>\" [0 0 0] with \"ak47\"",
            "Team \"TERRORIST\" triggered \"SFUI_Notice_Terrorists_Win\" (CT \"0\") (T \"1\")",
            "World triggered \"Round_End\""
        );

        var result = _parser.Parse(log);
        Assert.Equal(2, result.Kills.Count);
    }

    [Fact]
    public void Parse_HeadshotFlagSetCorrectly()
    {
        var log = BuildLog(
            "World triggered \"Match_Start\" on \"de_nuke\"",
            "Team playing \"CT\": TeamVitality",
            "Team playing \"TERRORIST\": NAVI",
            "World triggered \"Round_Start\"",
            "\"s1mple<30><STEAM_1:1:1><TERRORIST>\" [0 0 0] killed \"ZywOo<26><STEAM_1:1:2><CT>\" [0 0 0] with \"ak47\" (headshot)",
            "\"s1mple<30><STEAM_1:1:1><TERRORIST>\" [0 0 0] killed \"apEX<25><STEAM_1:1:3><CT>\" [0 0 0] with \"ak47\"",
            "Team \"TERRORIST\" triggered \"SFUI_Notice_Terrorists_Win\" (CT \"0\") (T \"1\")",
            "World triggered \"Round_End\""
        );

        var result = _parser.Parse(log);
        Assert.True(result.Kills[0].Headshot);
        Assert.False(result.Kills[1].Headshot);
    }

    [Fact]
    public void Parse_KillsOutsideRound_AreIgnored()
    {
        // Kill appears before any Round_Start — should not be counted
        var log = BuildLog(
            "World triggered \"Match_Start\" on \"de_nuke\"",
            "Team playing \"CT\": TeamVitality",
            "Team playing \"TERRORIST\": NAVI",
            "\"s1mple<30><STEAM_1:1:1><TERRORIST>\" [0 0 0] killed \"ZywOo<26><STEAM_1:1:2><CT>\" [0 0 0] with \"ak47\"",
            "World triggered \"Round_Start\"",
            "Team \"CT\" triggered \"SFUI_Notice_CTs_Win\" (CT \"1\") (T \"0\")",
            "World triggered \"Round_End\""
        );

        var result = _parser.Parse(log);
        Assert.Empty(result.Kills);
    }

    [Fact]
    public void Parse_KillOther_IsNotCountedAsPlayerKill()
    {
        // "killed other" events (breaking props) must not appear in the kill feed
        var log = BuildLog(
            "World triggered \"Match_Start\" on \"de_nuke\"",
            "Team playing \"CT\": TeamVitality",
            "Team playing \"TERRORIST\": NAVI",
            "World triggered \"Round_Start\"",
            "\"s1mple<30><STEAM_1:1:1><TERRORIST>\" [0 0 0] killed other \"func_breakable<209>\" [0 0 0] with \"ak47\"",
            "Team \"CT\" triggered \"SFUI_Notice_CTs_Win\" (CT \"1\") (T \"0\")",
            "World triggered \"Round_End\""
        );

        var result = _parser.Parse(log);
        Assert.Empty(result.Kills);
    }

    // ── Player stats ───────────────────────────────────────────────────────────

    [Fact]
    public void Parse_PlayerKillsAggregatedCorrectly()
    {
        var log = BuildLog(
            "World triggered \"Match_Start\" on \"de_nuke\"",
            "Team playing \"CT\": TeamVitality",
            "Team playing \"TERRORIST\": NAVI",
            "World triggered \"Round_Start\"",
            "\"s1mple<30><STEAM_1:1:1><TERRORIST>\" [0 0 0] killed \"ZywOo<26><STEAM_1:1:2><CT>\" [0 0 0] with \"ak47\"",
            "\"s1mple<30><STEAM_1:1:1><TERRORIST>\" [0 0 0] killed \"apEX<25><STEAM_1:1:3><CT>\" [0 0 0] with \"ak47\"",
            "Team \"TERRORIST\" triggered \"SFUI_Notice_Terrorists_Win\" (CT \"0\") (T \"1\")",
            "World triggered \"Round_End\""
        );

        var result = _parser.Parse(log);
        var s1mple = result.PlayerStats.First(p => p.Name == "s1mple");
        Assert.Equal(2, s1mple.Kills);
    }

    [Fact]
    public void Parse_PlayerDeathsAggregatedCorrectly()
    {
        var log = BuildLog(
            "World triggered \"Match_Start\" on \"de_nuke\"",
            "Team playing \"CT\": TeamVitality",
            "Team playing \"TERRORIST\": NAVI",
            "World triggered \"Round_Start\"",
            "\"s1mple<30><STEAM_1:1:1><TERRORIST>\" [0 0 0] killed \"ZywOo<26><STEAM_1:1:2><CT>\" [0 0 0] with \"ak47\"",
            "\"apEX<25><STEAM_1:1:3><CT>\" [0 0 0] killed \"ZywOo<26><STEAM_1:1:2><CT>\" [0 0 0] with \"m4a1\"",
            "Team \"TERRORIST\" triggered \"SFUI_Notice_Terrorists_Win\" (CT \"0\") (T \"1\")",
            "World triggered \"Round_End\""
        );

        var result = _parser.Parse(log);
        var zywoo = result.PlayerStats.First(p => p.Name == "ZywOo");
        Assert.Equal(2, zywoo.Deaths);
    }

    [Fact]
    public void Parse_HeadshotPercentageCalculatedCorrectly()
    {
        var log = BuildLog(
            "World triggered \"Match_Start\" on \"de_nuke\"",
            "Team playing \"CT\": TeamVitality",
            "Team playing \"TERRORIST\": NAVI",
            "World triggered \"Round_Start\"",
            "\"s1mple<30><STEAM_1:1:1><TERRORIST>\" [0 0 0] killed \"ZywOo<26><STEAM_1:1:2><CT>\" [0 0 0] with \"ak47\" (headshot)",
            "\"s1mple<30><STEAM_1:1:1><TERRORIST>\" [0 0 0] killed \"apEX<25><STEAM_1:1:3><CT>\" [0 0 0] with \"ak47\"",
            "Team \"TERRORIST\" triggered \"SFUI_Notice_Terrorists_Win\" (CT \"0\") (T \"1\")",
            "World triggered \"Round_End\""
        );

        var result = _parser.Parse(log);
        var s1mple = result.PlayerStats.First(p => p.Name == "s1mple");
        Assert.Equal(50.0, s1mple.HeadshotPercentage);
    }

    [Fact]
    public void Parse_WeaponKillsTrackedPerPlayer()
    {
        var log = BuildLog(
            "World triggered \"Match_Start\" on \"de_nuke\"",
            "Team playing \"CT\": TeamVitality",
            "Team playing \"TERRORIST\": NAVI",
            "World triggered \"Round_Start\"",
            "\"s1mple<30><STEAM_1:1:1><TERRORIST>\" [0 0 0] killed \"ZywOo<26><STEAM_1:1:2><CT>\" [0 0 0] with \"ak47\"",
            "\"s1mple<30><STEAM_1:1:1><TERRORIST>\" [0 0 0] killed \"apEX<25><STEAM_1:1:3><CT>\" [0 0 0] with \"ak47\"",
            "\"s1mple<30><STEAM_1:1:1><TERRORIST>\" [0 0 0] killed \"misutaaa<24><STEAM_1:1:4><CT>\" [0 0 0] with \"glock\"",
            "Team \"TERRORIST\" triggered \"SFUI_Notice_Terrorists_Win\" (CT \"0\") (T \"1\")",
            "World triggered \"Round_End\""
        );

        var result = _parser.Parse(log);
        var s1mple = result.PlayerStats.First(p => p.Name == "s1mple");
        Assert.Equal(2, s1mple.WeaponKills["ak47"]);
        Assert.Equal(1, s1mple.WeaponKills["glock"]);
    }

    [Fact]
    public void Parse_PlayerStatsOrderedByKillsDescending()
    {
        var log = BuildLog(
            "World triggered \"Match_Start\" on \"de_nuke\"",
            "Team playing \"CT\": TeamVitality",
            "Team playing \"TERRORIST\": NAVI",
            "World triggered \"Round_Start\"",
            "\"s1mple<30><STEAM_1:1:1><TERRORIST>\" [0 0 0] killed \"ZywOo<26><STEAM_1:1:2><CT>\" [0 0 0] with \"ak47\"",
            "\"s1mple<30><STEAM_1:1:1><TERRORIST>\" [0 0 0] killed \"apEX<25><STEAM_1:1:3><CT>\" [0 0 0] with \"ak47\"",
            "\"ZywOo<26><STEAM_1:1:2><CT>\" [0 0 0] killed \"b1t<32><STEAM_1:1:5><TERRORIST>\" [0 0 0] with \"m4a1\"",
            "Team \"TERRORIST\" triggered \"SFUI_Notice_Terrorists_Win\" (CT \"0\") (T \"1\")",
            "World triggered \"Round_End\""
        );

        var result = _parser.Parse(log);
        var kills = result.PlayerStats.Select(p => p.Kills).ToList();
        Assert.Equal(kills.OrderByDescending(k => k).ToList(), kills);
    }

    // ── Edge cases ─────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_EmptyLog_ReturnsEmptyMatch()
    {
        var result = _parser.Parse(string.Empty);
        Assert.Empty(result.Rounds);
        Assert.Empty(result.Kills);
        Assert.Empty(result.PlayerStats);
    }

    [Fact]
    public void Parse_NoMatchStart_StillParsesRounds()
    {
        // No Match_Start at all — parser falls back to index 0 and still processes rounds
        var log = BuildLog(
            "Team playing \"CT\": TeamVitality",
            "Team playing \"TERRORIST\": NAVI",
            "World triggered \"Round_Start\"",
            "Team \"CT\" triggered \"SFUI_Notice_CTs_Win\" (CT \"1\") (T \"0\")",
            "World triggered \"Round_End\""
        );

        var result = _parser.Parse(log);
        Assert.Equal(1, result.Rounds.Count);
    }

    [Fact]
    public void Parse_KillRoundNumberMatchesRound()
    {
        var log = BuildLog(
            "World triggered \"Match_Start\" on \"de_nuke\"",
            "Team playing \"CT\": TeamVitality",
            "Team playing \"TERRORIST\": NAVI",
            "World triggered \"Round_Start\"",
            "\"s1mple<30><STEAM_1:1:1><TERRORIST>\" [0 0 0] killed \"ZywOo<26><STEAM_1:1:2><CT>\" [0 0 0] with \"ak47\"",
            "Team \"TERRORIST\" triggered \"SFUI_Notice_Terrorists_Win\" (CT \"0\") (T \"1\")",
            "World triggered \"Round_End\"",
            "World triggered \"Round_Start\"",
            "\"ZywOo<26><STEAM_1:1:2><CT>\" [0 0 0] killed \"s1mple<30><STEAM_1:1:1><TERRORIST>\" [0 0 0] with \"m4a1\"",
            "Team \"CT\" triggered \"SFUI_Notice_CTs_Win\" (CT \"1\") (T \"1\")",
            "World triggered \"Round_End\""
        );

        var result = _parser.Parse(log);
        Assert.Equal(1, result.Kills[0].RoundNumber);
        Assert.Equal(2, result.Kills[1].RoundNumber);
    }
}