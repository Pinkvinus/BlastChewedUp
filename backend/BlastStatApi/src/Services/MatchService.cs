using BlastStatApi.Models;
using BlastStatApi.Parsers;

namespace BlastStatApi.Services;

/// <summary>
/// Reads the log file from disk once and caches the parsed result.
/// In a real deployment this would be replaced with a repository pattern
/// backed by a database populated by an import pipeline.
/// </summary>
public class MatchService
{
    private readonly CSLogParser _parser;
    private readonly ILogger<MatchService> _logger;
    private readonly string _logPath;

    private MatchData? _cached;

    public MatchService(CSLogParser parser, ILogger<MatchService> logger, IConfiguration config)
    {
        _parser = parser;
        _logger = logger;
        _logPath = config["LogFilePath"] ?? "match.log";
    }

    public MatchData GetMatch()
    {
        if (_cached is not null) return _cached;

        _logger.LogInformation("Parsing log file: {Path}", _logPath);
        var content = File.ReadAllText(_logPath);
        _cached = _parser.Parse(content);
        _logger.LogInformation(
            "Parsed {Rounds} rounds, {Kills} kills across {Players} players",
            _cached.Rounds.Count, _cached.Kills.Count, _cached.PlayerStats.Count);

        return _cached;
    }
}