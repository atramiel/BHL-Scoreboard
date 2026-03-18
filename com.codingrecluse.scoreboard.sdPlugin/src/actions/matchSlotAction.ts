import { action, KeyDownEvent, WillAppearEvent, WillDisappearEvent, SingletonAction, JsonValue } from "@elgato/streamdeck";
import { sendCommand } from "../client";
import { gameState } from "../gameState";

abstract class MatchSlotActionBase extends SingletonAction {
    protected abstract slotIndex: number;
    private _unsubscribe?: () => void;

    override onWillAppear(ev: WillAppearEvent<JsonValue>): void | Promise<void> {
        this._unsubscribe?.();
        this._unsubscribe = gameState.subscribe(state => {
            const label = state.pendingMatches?.[this.slotIndex] ?? "";
            if (!label) {
                ev.action.setTitle("");
                return;
            }
            const parts = label.split(" vs ");
            const shorten = (s: string) => s.length > 9 ? s.substring(0, 8) + "…" : s;
            const title = parts.length === 2
                ? `${shorten(parts[0])}\nvs\n${shorten(parts[1])}`
                : label;
            ev.action.setTitle(title);
        });
    }

    override onWillDisappear(_ev: WillDisappearEvent<JsonValue>): void | Promise<void> {
        this._unsubscribe?.();
        this._unsubscribe = undefined;
    }

    override onKeyDown(_ev: KeyDownEvent<JsonValue>): void | Promise<void> {
        sendCommand(`SelectMatch${this.slotIndex}`);
    }
}

@action({ UUID: "com.codingrecluse.scoreboard.matchslot1" })
export class MatchSlot1Action extends MatchSlotActionBase { protected slotIndex = 0; }

@action({ UUID: "com.codingrecluse.scoreboard.matchslot2" })
export class MatchSlot2Action extends MatchSlotActionBase { protected slotIndex = 1; }

@action({ UUID: "com.codingrecluse.scoreboard.matchslot3" })
export class MatchSlot3Action extends MatchSlotActionBase { protected slotIndex = 2; }

@action({ UUID: "com.codingrecluse.scoreboard.matchslot4" })
export class MatchSlot4Action extends MatchSlotActionBase { protected slotIndex = 3; }

@action({ UUID: "com.codingrecluse.scoreboard.matchslot5" })
export class MatchSlot5Action extends MatchSlotActionBase { protected slotIndex = 4; }

@action({ UUID: "com.codingrecluse.scoreboard.matchslot6" })
export class MatchSlot6Action extends MatchSlotActionBase { protected slotIndex = 5; }
