import type { MatchSummary, PlayerStats, Round, ScorelineEntry, KillEvent, MatchEvent } from '../types/match';

const BASE = import.meta.env.VITE_API_URL ?? 'http://localhost:5000/api';

async function get<T>(path: string): Promise<T> {
  const res = await fetch(`${BASE}${path}`);
  if (!res.ok) throw new Error(`API error ${res.status}: ${path}`);
  return res.json() as Promise<T>;
}

export const api = {
  getSummary:   () => get<MatchSummary>('/match'),
  getRounds:    () => get<Round[]>('/match/rounds'),
  getPlayers:   () => get<PlayerStats[]>('/match/players'),
  getScoreline: () => get<ScorelineEntry[]>('/match/scoreline'),
  getKills:     () => get<KillEvent[]>('/match/kills'),
  getEvents:    () => get<MatchEvent[]>('/match/events'),
};