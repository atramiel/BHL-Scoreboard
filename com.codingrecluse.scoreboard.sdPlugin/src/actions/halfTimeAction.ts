import { action, KeyDownEvent, SingletonAction, WillAppearEvent, WillDisappearEvent } from "@elgato/streamdeck";
import { sendCommand } from "../client";
import { gameState, GameState } from "../gameState";
import { toDataUri } from "../utils/renderButton";

function renderHalfTimeButton(isHalfTime: boolean, halfTimeWarning: boolean, flashOn: boolean): string {
    let bg: string;
    let label: string;
    let subLabel: string;
    let labelColor = "white";

    if (isHalfTime) {
        bg = "#1a1a2e";
        label = "HALF";
        subLabel = "ACTIVE";
        labelColor = "#f4943d";
    } else if (halfTimeWarning && flashOn) {
        bg = "#f4943d";
        label = "HALF";
        subLabel = "SOON!";
    } else if (halfTimeWarning && !flashOn) {
        bg = "#7a4500";
        label = "HALF";
        subLabel = "SOON!";
    } else {
        bg = "#1a3a5c";
        label = "HALF";
        subLabel = "TIME";
    }

    const svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 72 72">
  <rect width="72" height="72" rx="6" fill="${bg}"/>
  <text x="36" y="30" font-family="Arial,sans-serif" font-size="18" font-weight="bold"
        fill="${labelColor}" text-anchor="middle">${label}</text>
  <text x="36" y="52" font-family="Arial,sans-serif" font-size="13" font-weight="bold"
        fill="rgba(255,255,255,0.85)" text-anchor="middle">${subLabel}</text>
</svg>`;
    return toDataUri(svg);
}

@action({ UUID: "com.codingrecluse.scoreboard.halftime" })
export class HalfTimeAction extends SingletonAction {
    private unsubscribe: (() => void) | null = null;
    private flashTimer: ReturnType<typeof setInterval> | null = null;
    private flashOn = false;

    async onKeyDown(_ev: KeyDownEvent): Promise<void> {
        sendCommand("HalfTime");
    }

    async onWillAppear(ev: WillAppearEvent): Promise<void> {
        this.unsubscribe = gameState.subscribe((state: GameState) => {
            if (state.halfTimeWarning) {
                if (!this.flashTimer) {
                    this.flashTimer = setInterval(() => {
                        this.flashOn = !this.flashOn;
                        const s = gameState.current;
                        ev.action.setImage(renderHalfTimeButton(s.isHalfTime, s.halfTimeWarning, this.flashOn)).catch(() => {});
                    }, 500);
                }
            } else {
                this.stopFlash();
            }
            ev.action.setImage(renderHalfTimeButton(state.isHalfTime, state.halfTimeWarning, this.flashOn)).catch(() => {});
        });
    }

    async onWillDisappear(_ev: WillDisappearEvent): Promise<void> {
        this.unsubscribe?.();
        this.unsubscribe = null;
        this.stopFlash();
    }

    private stopFlash(): void {
        if (this.flashTimer !== null) {
            clearInterval(this.flashTimer);
            this.flashTimer = null;
        }
        this.flashOn = false;
    }
}
