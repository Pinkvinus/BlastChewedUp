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
  matchOffsetSeconds: number;
  killCount: number;
}

export interface KillEvent {
  timestamp: string;
  killerName: string;
  killerTeam: string;
  victimName: string;
  victimTeam: string;
  weapon: string;
  headshot: boolean;
  roundNumber: number;
  matchOffsetSeconds: number;
}

export interface MatchEvent {
  matchOffsetSeconds: number;
  roundNumber: number;
  // "kill" | "util" | "bomb_plant" | "bomb_defuse" | "round_start" | "purchase"
  eventType: string;
  playerName: string;
  playerTeam: string;
  // kill:     "weapon|victimName|victimTeam"
  // util:     grenade type
  // bomb:     "bombsite X"
  // purchase: "itemName|cost|moneyBefore|moneyAfter"
  detail: string | null;
  headshot: boolean;
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