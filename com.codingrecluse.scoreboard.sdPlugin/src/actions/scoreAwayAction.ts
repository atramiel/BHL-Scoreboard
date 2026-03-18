import { action, KeyDownEvent, SingletonAction, WillAppearEvent, WillDisappearEvent } from "@elgato/streamdeck";
import { sendCommand } from "../client";
import { gameState, GameState } from "../gameState";
import { renderScoreButton } from "../utils/renderButton";

@action({ UUID: "com.codingrecluse.scoreboard.scoreaway" })
export class ScoreAwayAction extends SingletonAction {
    private readonly unsubscribers = new Map<object, () => void>();

    async onKeyDown(_ev: KeyDownEvent): Promise<void> {
        sendCommand("IncreaseAway");
    }

    async onWillAppear(ev: WillAppearEvent): Promise<void> {
        const unsub = gameState.subscribe((state: GameState) => {
            ev.action.setImage(renderScoreButton(state.awayTeam, state.awayScore, "#B71C1C")).catch(() => {});
        });
        this.unsubscribers.set(ev.action, unsub);
    }

    async onWillDisappear(ev: WillDisappearEvent): Promise<void> {
        this.unsubscribers.get(ev.action)?.();
        this.unsubscribers.delete(ev.action);
    }
}
