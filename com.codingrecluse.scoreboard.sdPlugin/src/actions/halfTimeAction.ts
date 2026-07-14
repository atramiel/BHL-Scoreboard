import { action, KeyDownEvent, SingletonAction, WillAppearEvent, WillDisappearEvent } from "@elgato/streamdeck";
import { sendCommand } from "../client";
import { gameState, GameState } from "../gameState";
import { toDataUri } from "../utils/renderButton";

// Between games, halftime has no meaning at all — this button shows schedule
// pace instead ("Ahead" / "On Pace" / "Behind ~12m"), reverting to normal
// halftime duty the instant a match starts.
function renderPaceButton(paceStatus: string): string {
    let bg = "#2a2a3e";
    let label = "PACE";
    let subLabel = paceStatus || "—";

    if (paceStatus.startsWith("Behind")) bg = "#7a4500";
    else if (paceStatus.startsWith("Ahead")) bg = "#0d4f2c";
    else if (paceStatus === "On Pace") bg = "#1a3a5c";

    const svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 72 72">
  <rect width="72" height="72" rx="6" fill="${bg}"/>
  <text x="36" y="30" font-family="Arial,sans-serif" font-size="16" font-weight="bold"
        fill="white" text-anchor="middle">${label}</text>
  <text x="36" y="52" font-family="Arial,sans-serif" font-size="12" font-weight="bold"
        fill="rgba(255,255,255,0.85)" text-anchor="middle">${subLabel}</text>
</svg>`;
    return toDataUri(svg);
}

function renderHalfTimeButton(isHalfTime: boolean, halfTimeWarning: boolean, halfTimeReached: boolean, flashOn: boolean): string {
    let bg: string;
    let label: string;
    let subLabel: string;
    let labelColor = "white";

    if (isHalfTime) {
        bg = "#1a1a2e";
        label = "HALF";
        subLabel = "ACTIVE";
        labelColor = "#f4943d";
    } else if (halfTimeReached && flashOn) {
        bg = "#cc0000";
        label = "HALF";
        subLabel = "NOW!";
    } else if (halfTimeReached && !flashOn) {
        bg = "#550000";
        label = "HALF";
        subLabel = "NOW!";
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
    private flashInterval = 500;
    private flashOn = false;

    async onKeyDown(_ev: KeyDownEvent): Promise<void> {
        sendCommand("HalfTime");
    }

    async onWillAppear(ev: WillAppearEvent): Promise<void> {
        this.unsubscribe = gameState.subscribe((state: GameState) => {
            if (state.isBetweenGames) {
                this.stopFlash();
                ev.action.setImage(renderPaceButton(state.paceStatus)).catch(() => {});
                return;
            }

            const needsFlash = state.halfTimeWarning || state.halfTimeReached;
            const interval = state.halfTimeReached ? 200 : 500;

            if (needsFlash) {
                // Restart timer if interval changed (switching from warning to reached)
                if (!this.flashTimer || this.flashInterval !== interval) {
                    this.stopFlash();
                    this.flashInterval = interval;
                    this.flashTimer = setInterval(() => {
                        this.flashOn = !this.flashOn;
                        const s = gameState.current;
                        ev.action.setImage(renderHalfTimeButton(s.isHalfTime, s.halfTimeWarning, s.halfTimeReached, this.flashOn)).catch(() => {});
                    }, interval);
                }
            } else {
                this.stopFlash();
            }
            ev.action.setImage(renderHalfTimeButton(state.isHalfTime, state.halfTimeWarning, state.halfTimeReached, this.flashOn)).catch(() => {});
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
