import type { PlayerStats } from '../types/match';

interface Props {
  players: PlayerStats[];
  teamCT: string;
}

// Aggregate weapon kills across all players
function aggregateWeapons(players: PlayerStats[]) {
  const totals: Record<string, number> = {};
  for (const p of players) {
    for (const [weapon, count] of Object.entries(p.weaponKills)) {
      totals[weapon] = (totals[weapon] ?? 0) + count;
    }
  }
  return Object.entries(totals)
    .sort((a, b) => b[1] - a[1])
    .slice(0, 8);
}

export function WeaponBreakdown({ players, teamCT }: Props) {
  const ct = players.filter(p => p.team === teamCT);
  const t  = players.filter(p => p.team !== teamCT);
  const ctWeapons = aggregateWeapons(ct);
  const tWeapons  = aggregateWeapons(t);
  const maxKills  = Math.max(...ctWeapons.map(w => w[1]), ...tWeapons.map(w => w[1]));

  return (
    <section className="card">
      <h2 className="card__title">Weapon Usage</h2>
      <div className="weapon-grid">
        <WeaponCol weapons={ctWeapons} max={maxKills} side="CT" label={teamCT} />
        <WeaponCol weapons={tWeapons}  max={maxKills} side="T"  label={players.find(p => p.team !== teamCT)?.team ?? 'T'} />
      </div>
    </section>
  );
}

function WeaponCol({ weapons, max, side, label }: {
  weapons: [string, number][];
  max: number;
  side: string;
  label: string;
}) {
  return (
    <div className={`weapon-col weapon-col--${side.toLowerCase()}`}>
      <h3 className="weapon-col__team">{label}</h3>
      {weapons.map(([weapon, kills]) => (
        <div key={weapon} className="weapon-row">
          <span className="weapon-name">{weapon}</span>
          <div className="weapon-bar-wrap">
            <div
              className={`weapon-bar weapon-bar--${side.toLowerCase()}`}
              style={{ width: `${(kills / max) * 100}%` }}
            />
          </div>
          <span className="weapon-kills">{kills}</span>
        </div>
      ))}
    </div>
  );
}