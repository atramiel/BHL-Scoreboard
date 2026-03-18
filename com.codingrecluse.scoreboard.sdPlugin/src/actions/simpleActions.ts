import { action, KeyDownEvent, SingletonAction, WillAppearEvent } from "@elgato/streamdeck";
import { sendCommand } from "../client";
import { renderLabelButton } from "../utils/renderButton";

/** Base class for actions that just send a fixed command and show a static label. */
abstract class LabelAction extends SingletonAction {
    protected abstract readonly command: string;
    protected abstract readonly label: string;
    protected abstract readonly subLabel: string;
    protected abstract readonly bgColor: string;

    async onKeyDown(_ev: KeyDownEvent): Promise<void> {
        sendCommand(this.command);
    }

    async onWillAppear(ev: WillAppearEvent): Promise<void> {
        await ev.action.setImage(renderLabelButton(this.label, this.subLabel, this.bgColor));
    }
}

@action({ UUID: "com.codingrecluse.scoreboard.reset" })
export class ResetAction extends LabelAction {
    protected readonly command = "Reset";
    protected readonly label = "RESET";
    protected readonly subLabel = "GAME";
    protected readonly bgColor = "#3d3d3d";
}

@action({ UUID: "com.codingrecluse.scoreboard.undo" })
export class UndoAction extends LabelAction {
    protected readonly command = "Undo";
    protected readonly label = "UNDO";
    protected readonly subLabel = "";
    protected readonly bgColor = "#3d3d3d";
}

@action({ UUID: "com.codingrecluse.scoreboard.redo" })
export class RedoAction extends LabelAction {
    protected readonly command = "Redo";
    protected readonly label = "REDO";
    protected readonly subLabel = "";
    protected readonly bgColor = "#3d3d3d";
}

@action({ UUID: "com.codingrecluse.scoreboard.swapsides" })
export class SwapSidesAction extends LabelAction {
    protected readonly command = "SwapSides";
    protected readonly label = "SWAP";
    protected readonly subLabel = "SIDES";
    protected readonly bgColor = "#1a3a5c";
}

@action({ UUID: "com.codingrecluse.scoreboard.resetclock" })
export class ResetClockAction extends LabelAction {
    protected readonly command = "ResetClock";
    protected readonly label = "RESET";
    protected readonly subLabel = "CLOCK";
    protected readonly bgColor = "#3d3d3d";
}
