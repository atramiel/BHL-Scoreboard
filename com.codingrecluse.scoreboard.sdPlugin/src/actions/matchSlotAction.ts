import { action, KeyDownEvent, KeyUpEvent, WillAppearEvent, WillDisappearEvent, SingletonAction, JsonValue } from "@elgato/streamdeck";
import { sendCommand } from "../client";
import { gameState } from "../gameState";

// Held this long or longer counts as a long press (manual "On Deck" ping)
// instead of a normal tap (select this match to run next).
const LONG_PRESS_MS = 500;

abstract class MatchSlotActionBase extends SingletonAction {
    protected abstract slotIndex: number;
    private _unsubscribe?: () => void;
    private _longPressTimer?: ReturnType<typeof setTimeout>;
    private _longPressFired = false;

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
        clearTimeout(this._longPressTimer);
        this._longPressTimer = undefined;
    }

    override onKeyDown(ev: KeyDownEvent<JsonValue>): void | Promise<void> {
        this._longPressFired = false;
        // Fires the instant the hold crosses the threshold, while still pressed —
        // not on release, so the confirmation lands the moment it's actually earned.
        this._longPressTimer = setTimeout(() => {
            this._longPressFired = true;
            sendCommand(`OnDeckMatch${this.slotIndex}`);
            ev.action.showOk();
        }, LONG_PRESS_MS);
    }

    override onKeyUp(ev: KeyUpEvent<JsonValue>): void | Promise<void> {
        clearTimeout(this._longPressTimer);
        this._longPressTimer = undefined;
        if (this._longPressFired) return; // already handled at the threshold crossing
        sendCommand(`SelectMatch${this.slotIndex}`);
        ev.action.showOk();
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
