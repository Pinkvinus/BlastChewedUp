import { useState } from 'react';
import { useQuery } from './hooks/useQuery';
import { api } from './api/matchApi';
import { ScoreHeader } from './components/scoreHeader';
import { PlayerTable } from './components/playerTable';
import { ScorelineChart } from './components/scorelineChart';
import { PlayByPlay } from './components/playByPlay';

type View = 'playbyplay' | 'stats';

export default function App() {
  const [view, setView] = useState<View>('playbyplay');

  const summary   = useQuery(api.getSummary);
  const players   = useQuery(api.getPlayers);
  const rounds    = useQuery(api.getRounds);
  const scoreline = useQuery(api.getScoreline);
  const events    = useQuery(api.getEvents);

  const loading = summary.loading || players.loading || rounds.loading || scoreline.loading || events.loading;
  const error   = summary.error   || players.error   || rounds.error   || scoreline.error   || events.error;

  if (loading) return <div className="loading-screen"><div className="spinner" /><p>Loading match data…</p></div>;
  if (error)   return <div className="error-screen"><p>⚠ {error}</p></div>;

  const { teamCT, teamT } = summary.data!;
  const lastRound = rounds.data![rounds.data!.length - 1];
  const totalDuration = lastRound.matchOffsetSeconds + lastRound.durationSeconds;

  return (
    <div className="app">
      <nav className="app-nav">
        <span className="app-nav__logo">BLAST<span>STATS</span></span>
        <div className="app-nav__tabs">
          <button
            className={`app-nav__tab ${view === 'playbyplay' ? 'app-nav__tab--active' : ''}`}
            onClick={() => setView('playbyplay')}
          >
            ▶ Play by Play
          </button>
          <button
            className={`app-nav__tab ${view === 'stats' ? 'app-nav__tab--active' : ''}`}
            onClick={() => setView('stats')}
          >
            Match Stats
          </button>
        </div>
      </nav>

      {view === 'playbyplay' ? (
        <PlayByPlay
          events={events.data!}
          rounds={rounds.data!}
          scoreline={scoreline.data!}
          teamCT={teamCT}
          teamT={teamT}
          totalDuration={totalDuration}
          onMatchEnd={() => setView('stats')}
        />
      ) : (
        <>
          <ScoreHeader summary={summary.data!} />
          <main className="grid">
            <div className="col col--wide">
              <ScorelineChart scoreline={scoreline.data!} teamCT={teamCT} teamT={teamT} />
            </div>
            <div className="col col--wide">
              <PlayerTable players={players.data!} teamCT={teamCT} teamT={teamT} />
            </div>
          </main>
        </>
      )}
    </div>
  );
}