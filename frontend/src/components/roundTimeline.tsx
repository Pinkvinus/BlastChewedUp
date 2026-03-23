import type { Round } from '../types/match';

interface Props {
  rounds: Round[];
  teamCT: string;
  teamT: string;
}

const WIN_ICONS: Record<string, string> = {
  'Elimination':   '💀',
  'Bomb Exploded': '💥',
  'Bomb Defused':  '🛡️',
};

export function RoundTimeline({ rounds, teamCT, teamT }: Props) {
  const maxDuration = Math.max(...rounds.map(r => r.durationSeconds));

  return (
    <section className="card">
      <h2 className="card__title">Round Timeline</h2>
      <div className="round-timeline">
        {rounds.map(r => {
          const duration = r.durationSeconds;
          const widthPct = (duration / maxDuration) * 100;
          const isCT = r.winnerSide === teamCT;
          return (
            <div key={r.number} className="round-row">
              <span className="round-num">R{r.number}</span>
              <div className="round-bar-wrap">
                <div
                  className={`round-bar ${isCT ? 'round-bar--ct' : 'round-bar--t'}`}
                  style={{ width: `${widthPct}%` }}
                  title={`${r.winnerSide} – ${r.winCondition} – ${duration}s`}
                />
              </div>
              <span className="round-meta">
                {WIN_ICONS[r.winCondition] ?? '•'} {Math.round(duration)}s · {r.killCount}💀
              </span>
              <span className="round-score">{r.scoreCT}–{r.scoreT}</span>
            </div>
          );
        })}
      </div>
    </section>
  );
}