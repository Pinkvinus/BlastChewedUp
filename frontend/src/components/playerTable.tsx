import type { PlayerStats } from '../types/match';

interface Props {
  players: PlayerStats[];
  teamCT: string;
  teamT: string;
}

export function PlayerTable({ players, teamCT, teamT }: Props) {
  const ct = players.filter(p => p.team === teamCT);
  const t  = players.filter(p => p.team === teamT);

  return (
    <section className="card">
      <h2 className="card__title">Player Scoreboard</h2>
      <TeamBlock players={ct} teamName={teamCT} side="CT" />
      <TeamBlock players={t}  teamName={teamT}  side="T" />
    </section>
  );
}

function TeamBlock({ players, teamName, side }: {
  players: PlayerStats[];
  teamName: string;
  side: 'CT' | 'T';
}) {
  return (
    <div className={`team-block team-block--${side.toLowerCase()}`}>
      <h3 className="team-block__name">{teamName}</h3>
      <table className="stats-table">
        <thead>
          <tr>
            <th>Player</th>
            <th>K</th>
            <th>D</th>
            <th>K/D</th>
            <th>HS%</th>
            <th>Best weapon</th>
          </tr>
        </thead>
        <tbody>
          {players.map(p => {
            const kd = p.deaths === 0 ? '∞' : (p.kills / p.deaths).toFixed(2);
            const bestWeapon = Object.entries(p.weaponKills)
              .sort((a, b) => b[1] - a[1])[0];
            return (
              <tr key={p.name}>
                <td className="player-name">{p.name}</td>
                <td>{p.kills}</td>
                <td>{p.deaths}</td>
                <td className={parseFloat(kd) >= 1 ? 'positive' : 'negative'}>{kd}</td>
                <td>{p.headshotPercentage}%</td>
                <td className="weapon">{bestWeapon ? `${bestWeapon[0]} (${bestWeapon[1]})` : '–'}</td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}