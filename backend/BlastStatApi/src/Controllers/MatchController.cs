using Microsoft.AspNetCore.Mvc;
using BlastStatApi.Services;
using BlastStatApi.Models;

namespace BlastStatApi.Controllers;

[ApiController]
[Route("api/match")]
public class MatchController : ControllerBase
{
    private readonly MatchService _matchService;
    public MatchController(MatchService matchService) => _matchService = matchService;

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
                ? Math.Round(match.Rounds.Average(r => r.DurationSeconds), 1) : 0
        });
    }

    [HttpGet("rounds")]
    public IActionResult GetRounds() =>
        Ok(_matchService.GetMatch().Rounds.Select(r => new
        {
            r.Number, r.WinnerSide, r.WinCondition,
            r.ScoreCT, r.ScoreT, r.DurationSeconds,
            r.MatchOffsetSeconds, killCount = r.Kills.Count
        }));

    [HttpGet("players")]
    public IActionResult GetPlayers() => Ok(_matchService.GetMatch().PlayerStats);

    [HttpGet("kills")]
    public IActionResult GetKills([FromQuery] string? player, [FromQuery] int? round)
    {
        var kills = _matchService.GetMatch().Kills.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(player))
            kills = kills.Where(k =>
                k.KillerName.Equals(player, StringComparison.OrdinalIgnoreCase) ||
                k.VictimName.Equals(player, StringComparison.OrdinalIgnoreCase));
        if (round.HasValue)
            kills = kills.Where(k => k.RoundNumber == round.Value);
        return Ok(kills);
    }

    [HttpGet("scoreline")]
    public IActionResult GetScoreline() =>
        Ok(_matchService.GetMatch().Rounds.Select(r => new
        {
            round = r.Number, scoreCT = r.ScoreCT, scoreT = r.ScoreT,
            winner = r.WinnerSide, winCondition = r.WinCondition
        }));

    /// <summary>
    /// Unified chronological event feed used by the play-by-play timeline.
    /// EventType: "round_start" | "kill" | "util" | "bomb_plant" | "bomb_defuse"
    /// </summary>
    [HttpGet("events")]
    public IActionResult GetEvents() => Ok(_matchService.GetMatch().Events);
}