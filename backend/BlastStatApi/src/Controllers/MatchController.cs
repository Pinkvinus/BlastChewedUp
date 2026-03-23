using Microsoft.AspNetCore.Mvc;
using BlastStatApi.Services;
using BlastStatApi.Models;

namespace BlastStatApi.Controllers;

/// <summary>
/// REST API for CS:GO match statistics.
///
/// Endpoints:
///   GET /api/match           – top-level match summary
///   GET /api/match/rounds    – per-round details (duration, winner, score, kills)
///   GET /api/match/players   – per-player kill/death/headshot stats
///   GET /api/match/kills     – raw kill feed (optionally filtered by player or round)
///   GET /api/match/scoreline – cumulative CT vs T score after each round
/// </summary>
[ApiController]
[Route("api/match")]
public class MatchController : ControllerBase
{
    private readonly MatchService _matchService;

    public MatchController(MatchService matchService)
    {
        _matchService = matchService;
    }

    // ── GET /api/match ─────────────────────────────────────────────────────────
    [HttpGet]
    public IActionResult GetSummary()
    {
        var match = _matchService.GetMatch();
        var finalRound = match.Rounds.LastOrDefault();

        return Ok(new
        {
            map = match.Map,
            teamCT = match.TeamCT.Name,
            teamT = match.TeamT.Name,
            finalScoreCT = finalRound?.ScoreCT ?? 0,
            finalScoreT = finalRound?.ScoreT ?? 0,
            totalRounds = match.Rounds.Count,
            totalKills = match.Kills.Count,
            averageRoundDurationSeconds = match.Rounds.Count > 0
                ? Math.Round(match.Rounds.Average(r => r.DurationSeconds), 1)
                : 0
        });
    }

    // ── GET /api/match/rounds ──────────────────────────────────────────────────
    [HttpGet("rounds")]
    public IActionResult GetRounds()
    {
        var match = _matchService.GetMatch();
        var rounds = match.Rounds.Select(r => new
        {
            r.RoundNumber,
            r.WinnerSide,
            r.WinCondition,
            r.ScoreCT,
            r.ScoreT,
            r.DurationSeconds,
            killCount = r.Kills.Count
        });
        return Ok(rounds);
    }

    // ── GET /api/match/players ─────────────────────────────────────────────────
    [HttpGet("players")]
    public IActionResult GetPlayers()
    {
        var match = _matchService.GetMatch();
        return Ok(match.PlayerStats);
    }

    // ── GET /api/match/kills?player=s1mple&round=3 ────────────────────────────
    [HttpGet("kills")]
    public IActionResult GetKills([FromQuery] string? player, [FromQuery] int? round)
    {
        var match = _matchService.GetMatch();
        var kills = match.Kills.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(player))
            kills = kills.Where(k =>
                k.KillerName.Equals(player, StringComparison.OrdinalIgnoreCase) ||
                k.VictimName.Equals(player, StringComparison.OrdinalIgnoreCase));

        if (round.HasValue)
            kills = kills.Where(k => k.RoundNumber == round.Value);

        return Ok(kills);
    }

    // ── GET /api/match/scoreline ───────────────────────────────────────────────
    [HttpGet("scoreline")]
    public IActionResult GetScoreline()
    {
        var match = _matchService.GetMatch();
        var scoreline = match.Rounds.Select(r => new
        {
            round = r.Number,
            scoreCT = r.ScoreCT,
            scoreT = r.ScoreT,
            winner = r.WinnerSide,
            winCondition = r.WinCondition
        });
        return Ok(scoreline);
    }
}