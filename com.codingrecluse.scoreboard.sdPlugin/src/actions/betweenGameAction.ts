import { action, KeyDownEvent, SingletonAction, JsonValue } from "@elgato/streamdeck";
import { sendCommand } from "../client";

@action({ UUID: "com.codingrecluse.scoreboard.betweengame" })
export class BetweenGameAction extends SingletonAction {
    override onKeyDown(_ev: KeyDownEvent<JsonValue>): void | Promise<void> {
        sendCommand("BetweenGame");
    }
}
