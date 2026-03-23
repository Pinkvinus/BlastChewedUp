export interface MatchSummary {
  map: string;
  teamCT: string;
  teamT: string;
  finalScoreCT: number;
  finalScoreT: number;
  totalRounds: number;
  totalKills: number;
  averageRoundDurationSeconds: number;
}

export interface Round {
  number: number;
  winnerSide: string;
  winCondition: string;
  scoreCT: number;
  scoreT: number;
  durationSeconds: number;
  killCount: number;
}

export interface PlayerStats {
  name: string;
  team: string;
  kills: number;
  deaths: number;
  headshots: number;
  headshotPercentage: number;
  weaponKills: Record<string, number>;
  roundsPlayed: number;
}

export interface ScorelineEntry {
  round: number;
  scoreCT: number;
  scoreT: number;
  winner: string;
  winCondition: string;
}