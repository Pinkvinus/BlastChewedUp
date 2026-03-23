import { useQuery } from './hooks/useQuery';
import { api } from './api/matchApi';
import { ScoreHeader } from './components/scoreHeader';
import { PlayerTable } from './components/playerTable';
import { RoundTimeline } from './components/roundTimeline';
import { ScorelineChart } from './components/scorelineChart';
import { WeaponBreakdown } from './components/weaponBreakdown';

export default function App() {
  console.log('App rendering');
  const summary = useQuery(api.getSummary);
  console.log('summary state:', summary);

  const players   = useQuery(api.getPlayers);
  const rounds    = useQuery(api.getRounds);
  const scoreline = useQuery(api.getScoreline);

  const loading = summary.loading || players.loading || rounds.loading || scoreline.loading;
  const error   = summary.error   || players.error   || rounds.error   || scoreline.error;

  if (loading) return <div className="loading-screen"><div className="spinner" /><p>Loading match data…</p></div>;
  if (error)   return <div className="error-screen"><p>⚠ {error}</p></div>;

  const { teamCT, teamT } = summary.data!;

  return (
    <div className="app">
      <ScoreHeader summary={summary.data!} />
      <main className="grid">
        <div className="col col--wide">
          <ScorelineChart scoreline={scoreline.data!} teamCT={teamCT} teamT={teamT} />
        </div>
        <div className="col col--wide">
          <PlayerTable players={players.data!} teamCT={teamCT} teamT={teamT} />
        </div>
        <div className="col col--half">
          <RoundTimeline rounds={rounds.data!} teamCT={teamCT} teamT={teamT} />
        </div>
        <div className="col col--half">
          <WeaponBreakdown players={players.data!} teamCT={teamCT} />
        </div>
      </main>
    </div>
  );
}