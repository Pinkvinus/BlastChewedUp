import type { ScorelineEntry } from '../types/match';

interface Props {
  scoreline: ScorelineEntry[];
  teamCT: string;
  teamT: string;
}

const W = 800;
const H = 260;
const PAD = { top: 20, right: 24, bottom: 40, left: 36 };

export function ScorelineChart({ scoreline, teamCT, teamT }: Props) {
  const maxScore = Math.max(...scoreline.flatMap(s => [s.scoreCT, s.scoreT]));
  const rounds = scoreline.length;

  const xScale = (i: number) =>
    PAD.left + (i / (rounds - 1)) * (W - PAD.left - PAD.right);
  const yScale = (v: number) =>
    H - PAD.bottom - (v / maxScore) * (H - PAD.top - PAD.bottom);

  const makePath = (getter: (s: ScorelineEntry) => number) =>
    scoreline
      .map((s, i) => `${i === 0 ? 'M' : 'L'} ${xScale(i).toFixed(1)} ${yScale(getter(s)).toFixed(1)}`)
      .join(' ');

  const ctPath = makePath(s => s.scoreCT);
  const tPath  = makePath(s => s.scoreT);

  // Grid lines
  const gridY = Array.from({ length: maxScore + 1 }, (_, i) => i);

  return (
    <section className="card">
      <h2 className="card__title">Score Progression</h2>
      <div className="chart-legend">
        <span className="legend--ct">■ {teamCT} (CT start)</span>
        <span className="legend--t">■ {teamT} (T start)</span>
      </div>
      <svg
        viewBox={`0 0 ${W} ${H}`}
        className="scoreline-svg"
        aria-label="Score progression chart"
      >
        {/* Grid */}
        {gridY.map(v => (
          <line
            key={v}
            x1={PAD.left} x2={W - PAD.right}
            y1={yScale(v)} y2={yScale(v)}
            className="grid-line"
          />
        ))}

        {/* Halftime divider */}
        {rounds > 12 && (
          <line
            x1={xScale(11)} x2={xScale(11)}
            y1={PAD.top} y2={H - PAD.bottom}
            className="halftime-line"
          />
        )}

        {/* Score paths */}
        <path d={ctPath} className="score-path score-path--ct" />
        <path d={tPath}  className="score-path score-path--t" />

        {/* Dots */}
        {scoreline.map((s, i) => (
          <g key={i}>
            <circle cx={xScale(i)} cy={yScale(s.scoreCT)} r="4" className="dot dot--ct" />
            <circle cx={xScale(i)} cy={yScale(s.scoreT)}  r="4" className="dot dot--t"  />
          </g>
        ))}

        {/* X axis labels */}
        {scoreline.filter((_, i) => i % 2 === 0).map((s, i) => (
          <text
            key={s.round}
            x={xScale(i * 2)}
            y={H - 8}
            className="axis-label"
            textAnchor="middle"
          >
            {s.round}
          </text>
        ))}

        {/* Y axis labels */}
        {gridY.filter(v => v % 2 === 0).map(v => (
          <text key={v} x={PAD.left - 6} y={yScale(v) + 4} className="axis-label" textAnchor="end">
            {v}
          </text>
        ))}

        {/* Axis labels */}
        <text x={W / 2} y={H} className="axis-title" textAnchor="middle">Round</text>
      </svg>
    </section>
  );
}