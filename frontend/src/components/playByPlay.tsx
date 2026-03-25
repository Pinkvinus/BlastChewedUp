import { useState, useEffect, useRef, useCallback } from 'react';
import type { MatchEvent, Round, ScorelineEntry } from '../types/match';

interface Props {
  events: MatchEvent[];
  rounds: Round[];
  scoreline: ScorelineEntry[];
  teamCT: string;
  teamT: string;
  totalDuration: number;
  onMatchEnd: () => void;
}

// ── Icons ──────────────────────────────────────────────────────────────────────
function Icon({ src, alt }: { src: string; alt: string }) {
  return <img src={src} alt={alt} className="pbp__icon" />;
}

const BULLET = <Icon src="/bullet-svgrepo-com.svg" alt="bullet" />;
const GRENADE = <Icon src="/grenade-svgrepo-com.svg" alt="grenade" />;
const MOLOTOV = <Icon src="/molotov-cocktail-svgrepo-com.svg" alt="molotov" />;
const MONEY = <Icon src="/money-svgrepo-com.svg" alt="money" />;
const KNIFE = <Icon src="/knife-war-svgrepo-com.svg" alt="knife" />;
const KEVLAR = <Icon src="/bulletproof-vest-svgrepo-com.svg" alt="kevlar" />;
const BOMB = <Icon src="/bomb-svgrepo-com.svg" alt="bomb" />;
const DEFUSE_KIT = <Icon src="/noun-battery-jumper-54665.svg" alt="defuse kit" />;
const DEFUSE = <Icon src="/noun-defuse-6557048.svg" alt="defuse" />;
const HEADSHOT = <Icon src="/aim-sniper-svgrepo-com.svg" alt="headshot" />;
const BELL = <Icon src="/bell-svgrepo-com.svg" alt="round" />;

const UTIL_ICONS: Record<string, React.ReactNode> = {
  smokegrenade: GRENADE, flashbang: GRENADE,
  hegrenade: GRENADE, molotov: MOLOTOV, incgrenade: MOLOTOV,
};

const WEAPON_ICONS: Record<string, React.ReactNode> = {
  awp: BULLET, ssg08: BULLET, g3sg1: BULLET, scar20: BULLET,
  knife: KNIFE, knife_outdoor: KNIFE, knife_butterfly: KNIFE,
  knife_flip: KNIFE, knife_m9_bayonet: KNIFE, bayonet: KNIFE,
  hegrenade: GRENADE, molotov: MOLOTOV, inferno: MOLOTOV,
};

const ITEM_ICONS: Record<string, React.ReactNode> = {
  'Kevlar + Helmet': KEVLAR, 'Defuse Kit': DEFUSE_KIT,
  ak47: BULLET, m4a1: BULLET, awp: BULLET,
  smokegrenade: GRENADE, flashbang: GRENADE, hegrenade: GRENADE,
  molotov: MOLOTOV, incgrenade: MOLOTOV,
};

function weaponIcon(w: string) { return WEAPON_ICONS[w.toLowerCase()] ?? BULLET; }
function itemIcon(name: string) {
  const key = Object.keys(ITEM_ICONS).find(k => name.toLowerCase().includes(k));
  return key ? ITEM_ICONS[key] : MONEY;
}

// ── Event row renderer ─────────────────────────────────────────────────────────
function EventRow({ ev, teamCT, isLatest }: { ev: MatchEvent; teamCT: string; isLatest: boolean }) {
  const isCT = ev.playerTeam === teamCT;
  const nameClass = isCT ? 'pbp__name--ct' : 'pbp__name--t';
  const latestClass = isLatest ? 'pbp__event--latest' : '';

  switch (ev.eventType) {
    case 'round_start':
      return (
        <div className={`pbp__event pbp__event--round-start`}>
          <span className="pbp__event-icon">{BELL}</span>
          <span className="pbp__event-text">{ev.detail}</span>
        </div>
      );

    case 'kill': {
      const [weapon, victimName, victimTeam] = (ev.detail ?? '||').split('|');
      const victimIsCT = victimTeam === teamCT;
      return (
        <div className={`pbp__event pbp__event--kill ${latestClass}`}>
          <span className="pbp__event-icon">{weaponIcon(weapon)}</span>
          <span className={nameClass}>{ev.playerName}</span>
          <span className="pbp__event-sep">{weapon}{ev.headshot ? <> {HEADSHOT}</> : ''}</span>
          <span className={victimIsCT ? 'pbp__name--ct' : 'pbp__name--t'}>{victimName}</span>
        </div>
      );
    }

    case 'purchase': {
      const [item, cost, , after] = (ev.detail ?? '|||').split('|');
      return (
        <div className={`pbp__event pbp__event--purchase ${latestClass}`}>
          <span className="pbp__event-icon">{MONEY}</span>
          <span className={nameClass}>{ev.playerName}</span>
          <span className="pbp__event-sep">bought</span>
          <span className="pbp__purchase-item">{item}</span>
          <span className="pbp__purchase-cost">-${cost}</span>
          <span className="pbp__purchase-remaining">(${after} left)</span>
        </div>
      );
    }

    case 'util':
      return (
        <div className={`pbp__event pbp__event--util ${latestClass}`}>
          <span className="pbp__event-icon">{UTIL_ICONS[ev.detail ?? ''] ?? '🟢'}</span>
          <span className={nameClass}>{ev.playerName}</span>
          <span className="pbp__event-sep">threw {ev.detail}</span>
        </div>
      );

    case 'bomb_plant':
      return (
        <div className={`pbp__event pbp__event--bomb ${latestClass}`}>
          <span className="pbp__event-icon">{BOMB}</span>
          <span className={nameClass}>{ev.playerName}</span>
          <span className="pbp__event-sep">planted at {ev.detail}</span>
        </div>
      );

    case 'bomb_defuse':
      return (
        <div className={`pbp__event pbp__event--defuse ${latestClass}`}>
          <span className="pbp__event-icon">{DEFUSE}</span>
          <span className={nameClass}>{ev.playerName}</span>
          <span className="pbp__event-sep">defused the bomb</span>
        </div>
      );

    default:
      return null;
  }
}

// ── Event type filter options ──────────────────────────────────────────────────
type FilterKey = 'all' | 'kill' | 'purchase' | 'util' | 'bomb';
const FILTERS: { key: FilterKey; label: React.ReactNode }[] = [
  { key: 'all',      label: 'All'      },
  { key: 'kill',     label: <>{BULLET} Kills</>  },
  { key: 'purchase', label: <>{MONEY} Buys</>   },
  { key: 'util',     label: <>{GRENADE} Util</>   },
  { key: 'bomb',     label: <>{BOMB} Bomb</>   },
];

function matchesFilter(ev: MatchEvent, filter: FilterKey) {
  if (filter === 'all') return true;
  if (filter === 'bomb') return ev.eventType === 'bomb_plant' || ev.eventType === 'bomb_defuse';
  return ev.eventType === filter;
}

// ── Component ──────────────────────────────────────────────────────────────────
export function PlayByPlay({
  events, rounds, scoreline, teamCT, teamT, totalDuration, onMatchEnd,
}: Props) {
  const [currentTime, setCurrentTime] = useState(0);
  const [isPlaying, setIsPlaying]     = useState(false);
  const [filter, setFilter]           = useState<FilterKey>('all');
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const feedRef     = useRef<HTMLDivElement>(null);

  const PLAYBACK_SPEED = 8;
  const TICK_MS        = 100;

  const visibleEvents = events.filter(e =>
    e.matchOffsetSeconds <= currentTime &&
    e.eventType !== 'round_start' &&
    matchesFilter(e, filter)
  );

  // Insert round_start separators regardless of filter
  const visibleRoundStarts = events.filter(e =>
    e.eventType === 'round_start' && e.matchOffsetSeconds <= currentTime
  );

  // Merge and sort for display
  const feedItems = [...visibleEvents, ...visibleRoundStarts]
    .sort((a, b) => a.matchOffsetSeconds - b.matchOffsetSeconds);

  // Live score
  const currentScore = (() => {
    const done = scoreline.filter(s => {
      const r = rounds.find(r => r.number === s.round);
      return r && r.matchOffsetSeconds + r.durationSeconds <= currentTime;
    });
    const last = done[done.length - 1];
    return last ? { ct: last.scoreCT, t: last.scoreT } : { ct: 0, t: 0 };
  })();

  // Current round
  const currentRound = rounds.find(
    r => currentTime >= r.matchOffsetSeconds &&
         currentTime <= r.matchOffsetSeconds + r.durationSeconds
  )?.number ?? (currentTime >= totalDuration ? rounds[rounds.length - 1]?.number ?? 0 : 0);

  const tick = useCallback(() => {
    setCurrentTime(prev => {
      const next = prev + (PLAYBACK_SPEED * TICK_MS) / 1000;
      if (next >= totalDuration) {
        setIsPlaying(false);
        onMatchEnd();
        return totalDuration;
      }
      return next;
    });
  }, [totalDuration, onMatchEnd]);

  useEffect(() => {
    if (isPlaying) intervalRef.current = setInterval(tick, TICK_MS);
    else if (intervalRef.current) clearInterval(intervalRef.current);
    return () => { if (intervalRef.current) clearInterval(intervalRef.current); };
  }, [isPlaying, tick]);

  useEffect(() => {
    if (feedRef.current)
      feedRef.current.scrollTop = feedRef.current.scrollHeight;
  }, [feedItems.length]);

  function handleScrub(e: React.ChangeEvent<HTMLInputElement>) {
    setIsPlaying(false);
    setCurrentTime(parseFloat(e.target.value));
  }

  // Step backward through non-round-start events
  function stepBack() {
    setIsPlaying(false);
    const actionEvents = events.filter(e => e.eventType !== 'round_start');
    const prev = [...actionEvents].reverse().find(e => e.matchOffsetSeconds < currentTime - 0.1);
    setCurrentTime(prev ? Math.max(0, prev.matchOffsetSeconds - 0.1) : 0);
  }

  // Step forward through non-round-start events
  function stepForward() {
    setIsPlaying(false);
    const actionEvents = events.filter(e => e.eventType !== 'round_start');
    const next = actionEvents.find(e => e.matchOffsetSeconds > currentTime);
    if (next) setCurrentTime(next.matchOffsetSeconds + 0.1);
    else { setCurrentTime(totalDuration); onMatchEnd(); }
  }

  // Jump to the start of the next round
  function skipToNextRound() {
    setIsPlaying(false);
    const nextRound = rounds.find(r => r.matchOffsetSeconds > currentTime + 0.5);
    if (nextRound) setCurrentTime(nextRound.matchOffsetSeconds);
    else { setCurrentTime(totalDuration); onMatchEnd(); }
  }

  // Jump to the start of the previous round
  function skipToPrevRound() {
    setIsPlaying(false);
    const prevRound = [...rounds].reverse().find(r => r.matchOffsetSeconds < currentTime - 0.5);
    if (prevRound) setCurrentTime(prevRound.matchOffsetSeconds);
    else setCurrentTime(0);
  }

  function formatTime(secs: number) {
    const m = Math.floor(secs / 60).toString().padStart(2, '0');
    const s = Math.floor(secs % 60).toString().padStart(2, '0');
    return `${m}:${s}`;
  }

  return (
    <div className="pbp">
      {/* ── Live scoreboard ── */}
      <div className="pbp__header">
        <div className="pbp__team pbp__team--ct">
          <span className="pbp__team-name">{teamCT}</span>
          <span className="pbp__team-score">{currentScore.ct}</span>
        </div>
        <div className="pbp__center">
          <span className="pbp__round-label">
            {currentRound > 0 ? `Round ${currentRound}` : 'Pre-match'}
          </span>
        </div>
        <div className="pbp__team pbp__team--t">
          <span className="pbp__team-score">{currentScore.t}</span>
          <span className="pbp__team-name">{teamT}</span>
        </div>
      </div>

      {/* ── Scrubber with round markers ── */}
      <div className="pbp__scrubber-wrap">
        <div className="pbp__round-markers" aria-hidden="true">
          {rounds.map(r => (
            <div
              key={r.number}
              className={`pbp__round-marker ${r.winnerSide === teamCT ? 'pbp__round-marker--ct' : 'pbp__round-marker--t'}`}
              style={{ left: `${(r.matchOffsetSeconds / totalDuration) * 100}%` }}
              title={`R${r.number}: ${r.winnerSide} (${r.winCondition})`}
            />
          ))}
        </div>
        <input
          type="range" className="pbp__scrubber"
          min={0} max={totalDuration} step={0.5}
          value={currentTime} onChange={handleScrub}
        />
        <div className="pbp__time-labels">
          <span>{formatTime(currentTime)}</span>
          <span>{formatTime(totalDuration)}</span>
        </div>
      </div>

      {/* ── Controls ── */}
      <div className="pbp__controls">
        <div className="pbp__controls-group">
          <button className="pbp__btn" onClick={skipToPrevRound} title="Previous round">⏮ Round</button>
          <button className="pbp__btn" onClick={stepBack}        title="Previous event">◀ Event</button>
          <button className="pbp__btn pbp__btn--primary" onClick={() => setIsPlaying(p => !p)}>
            {isPlaying ? '⏸ Pause' : '▶ Play'}
          </button>
          <button className="pbp__btn" onClick={stepForward}    title="Next event">Event ▶</button>
          <button className="pbp__btn" onClick={skipToNextRound} title="Next round">Round ⏭</button>
        </div>
        <button className="pbp__btn pbp__btn--end" onClick={() => { setCurrentTime(totalDuration); onMatchEnd(); }}>
          Skip to stats →
        </button>
      </div>

      {/* ── Filter tabs ── */}
      <div className="pbp__filters">
        {FILTERS.map(f => (
          <button
            key={f.key}
            className={`pbp__filter ${filter === f.key ? 'pbp__filter--active' : ''}`}
            onClick={() => setFilter(f.key)}
          >
            {f.label}
          </button>
        ))}
      </div>

      {/* ── Event feed ── */}
      <div className="pbp__feed" ref={feedRef}>
        {feedItems.length === 0 && (
          <p className="pbp__feed-empty">Press Play or scrub forward to start</p>
        )}
        {feedItems.map((ev, i) => (
          <div key={i} className="pbp__feed-row">
            {ev.eventType !== 'round_start' && (
              <span className="pbp__kill-time">{formatTime(ev.matchOffsetSeconds)}</span>
            )}
            <EventRow ev={ev} teamCT={teamCT} isLatest={i === feedItems.length - 1} />
          </div>
        ))}
      </div>

      {/* ── Completed round pills ── */}
      <div className="pbp__mini-scores">
        {scoreline
          .filter(s => {
            const r = rounds.find(r => r.number === s.round);
            return r && r.matchOffsetSeconds + r.durationSeconds <= currentTime;
          })
          .map(s => (
            <div
              key={s.round}
              className={`pbp__mini-round ${s.winner === teamCT ? 'pbp__mini-round--ct' : 'pbp__mini-round--t'}`}
              title={`R${s.round}: ${s.winner} (${s.winCondition}) ${s.scoreCT}–${s.scoreT}`}
              onClick={() => {
                const r = rounds.find(r => r.number === s.round);
                if (r) { setIsPlaying(false); setCurrentTime(r.matchOffsetSeconds); }
              }}
            >
              {s.round}
            </div>
          ))}
      </div>
    </div>
  );
}