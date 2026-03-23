import type { MatchSummary } from "../types/match";

interface Props { summary: MatchSummary }

export function ScoreHeader({ summary }: Props) {
  const { teamCT, teamT, finalScoreCT, finalScoreT, map,
          totalRounds, totalKills, averageRoundDurationSeconds } = summary;
  const winner = finalScoreCT > finalScoreT ? teamCT : teamT;

  return (
    <header className="score-header">
      <div className="map-badge">{map.replace('de_', '').toUpperCase()}</div>

      <div className="teams">
        <div className="team team--ct">
          <span className="team-name">{teamCT}</span>
          <span className="team-score">{finalScoreCT}</span>
        </div>
        <div className="vs">vs</div>
        <div className="team team--t">
          <span className="team-score">{finalScoreT}</span>
          <span className="team-name">{teamT}</span>
        </div>
      </div>

      <p className="winner-label">🏆 {winner} wins</p>

      <div className="meta-stats">
        <Stat label="Rounds"   value={totalRounds} />
        <Stat label="Kills"    value={totalKills} />
        <Stat label="Avg round" value={`${averageRoundDurationSeconds}s`} />
      </div>
    </header>
  );
}

function Stat({ label, value }: { label: string; value: string | number }) {
  return (
    <div className="meta-stat">
      <span className="meta-stat__value">{value}</span>
      <span className="meta-stat__label">{label}</span>
    </div>
  );
}