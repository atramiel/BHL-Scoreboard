export interface GameState {
    homeTeam: string;
    awayTeam: string;
    homeScore: number;
    awayScore: number;
    clock: string;
    isRunning: boolean;
    gameDone: boolean;
    nextMatchTime: string;
}

type StateListener = (state: GameState) => void;

class GameStateStore {
    private _state: GameState = {
        homeTeam: "HOME",
        awayTeam: "AWAY",
        homeScore: 0,
        awayScore: 0,
        clock: "10:00",
        isRunning: false,
        gameDone: false,
        nextMatchTime: "--:--",
    };

    private listeners = new Set<StateListener>();

    get current(): GameState {
        return this._state;
    }

    update(partial: Partial<GameState>): void {
        this._state = { ...this._state, ...partial };
        for (const listener of this.listeners) {
            listener(this._state);
        }
    }

    /** Subscribe to state changes. Fires immediately with current state. Returns an unsubscribe function. */
    subscribe(listener: StateListener): () => void {
        this.listeners.add(listener);
        // Notify immediately so the button renders its initial image
        Promise.resolve().then(() => listener(this._state));
        return () => this.listeners.delete(listener);
    }
}

export const gameState = new GameStateStore();
