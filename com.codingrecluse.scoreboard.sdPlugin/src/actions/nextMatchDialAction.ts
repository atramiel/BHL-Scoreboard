import streamDeck, { action, DialRotateEvent, DialDownEvent, WillAppearEvent, WillDisappearEvent, SingletonAction, JsonValue } from "@elgato/streamdeck";
import { sendCommand } from "../client";
import { gameState } from "../gameState";

@action({ UUID: "com.codingrecluse.scoreboard.nextmatch" })
export class NextMatchDialAction extends SingletonAction {
    private _unsubscribe?: () => void;

    override onWillAppear(ev: WillAppearEvent<JsonValue>): void | Promise<void> {
        this._unsubscribe = gameState.subscribe(state => {
            ev.action.setFeedback({
                title: "Next Match",
                value: state.nextMatchTime ?? "--:--",
            });
        });
    }

    override onWillDisappear(_ev: WillDisappearEvent<JsonValue>): void | Promise<void> {
        this._unsubscribe?.();
        this._unsubscribe = undefined;
    }

    override onDialRotate(ev: DialRotateEvent<JsonValue>): void | Promise<void> {
        const ticks = ev.payload.ticks;
        const cmd = ticks > 0 ? "IncreaseNextMatch" : "DecreaseNextMatch";
        for (let i = 0; i < Math.abs(ticks); i++) {
            sendCommand(cmd);
        }
    }

    override onDialDown(_ev: DialDownEvent<JsonValue>): void | Promise<void> {
        sendCommand("StartNextMatch");
    }
}
