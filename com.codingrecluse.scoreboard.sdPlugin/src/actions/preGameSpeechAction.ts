import { action, KeyDownEvent, SingletonAction, WillAppearEvent, WillDisappearEvent } from "@elgato/streamdeck";
import { sendCommand } from "../client";
import { gameState, GameState } from "../gameState";
import { toDataUri } from "../utils/renderButton";

// First press opens the pre-game slide deck; every press after that advances
// one slide, including the final press which closes it and starts the
// between-game countdown (handled app-side — this button just always sends
// the same command). While the deck is open, the button shows which slide
// you're on ("3 / 10") so the operator doesn't need to glance at the big screen.
function renderProgressButton(progress: string): string {
    const svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 72 72">
  <rect width="72" height="72" rx="6" fill="#3a2a00"/>
  <text x="36" y="30" font-family="Arial,sans-serif" font-size="13" font-weight="bold"
        fill="#e8b34c" text-anchor="middle">SPEECH</text>
  <text x="36" y="50" font-family="Arial,sans-serif" font-size="17" font-weight="bold"
        fill="white" text-anchor="middle">${progress}</text>
</svg>`;
    return toDataUri(svg);
}

@action({ UUID: "com.codingrecluse.scoreboard.pregamespeech" })
export class PreGameSpeechAction extends SingletonAction {
    private unsubscribe: (() => void) | null = null;

    override onKeyDown(ev: KeyDownEvent): void | Promise<void> {
        sendCommand("PreGameSpeech");
        ev.action.showOk();
    }

    override onWillAppear(ev: WillAppearEvent): void | Promise<void> {
        this.unsubscribe = gameState.subscribe((state: GameState) => {
            if (state.preGameSpeechStatus) {
                ev.action.setImage(renderProgressButton(state.preGameSpeechStatus)).catch(() => {});
            } else {
                ev.action.setImage().catch(() => {}); // reverts to the manifest's default icon
            }
        });
    }

    override onWillDisappear(_ev: WillDisappearEvent): void | Promise<void> {
        this.unsubscribe?.();
        this.unsubscribe = null;
    }
}
