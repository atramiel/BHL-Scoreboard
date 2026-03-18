import { action, KeyDownEvent, SingletonAction, WillAppearEvent, WillDisappearEvent } from "@elgato/streamdeck";
import { sendCommand } from "../client";
import { gameState, GameState } from "../gameState";
import { renderClockButton } from "../utils/renderButton";

@action({ UUID: "com.codingrecluse.scoreboard.playpause" })
export class PlayPauseAction extends SingletonAction {
    private readonly unsubscribers = new Map<object, () => void>();

    async onKeyDown(_ev: KeyDownEvent): Promise<void> {
        sendCommand("PlayPause");
    }

    async onWillAppear(ev: WillAppearEvent): Promise<void> {
        const unsub = gameState.subscribe((state: GameState) => {
            ev.action.setImage(renderClockButton(state.clock, state.isRunning, state.gameDone)).catch(() => {});
        });
        this.unsubscribers.set(ev.action, unsub);
    }

    async onWillDisappear(ev: WillDisappearEvent): Promise<void> {
        this.unsubscribers.get(ev.action)?.();
        this.unsubscribers.delete(ev.action);
    }
}
