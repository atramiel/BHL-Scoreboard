import streamDeck from "@elgato/streamdeck";

// Initialize TCP connection to the WPF app (side-effect import)
import "./client";

// Register all actions
import { ScoreHomeAction } from "./actions/scoreHomeAction";
import { ScoreAwayAction } from "./actions/scoreAwayAction";
import { PenaltyHomeAction } from "./actions/penaltyHomeAction";
import { PenaltyAwayAction } from "./actions/penaltyAwayAction";
import { PlayPauseAction } from "./actions/playPauseAction";
import { ResetAction, UndoAction, RedoAction, SwapSidesAction, ResetClockAction } from "./actions/simpleActions";

streamDeck.actions.registerAction(new ScoreHomeAction());
streamDeck.actions.registerAction(new ScoreAwayAction());
streamDeck.actions.registerAction(new PenaltyHomeAction());
streamDeck.actions.registerAction(new PenaltyAwayAction());
streamDeck.actions.registerAction(new PlayPauseAction());
streamDeck.actions.registerAction(new ResetAction());
streamDeck.actions.registerAction(new UndoAction());
streamDeck.actions.registerAction(new RedoAction());
streamDeck.actions.registerAction(new SwapSidesAction());
streamDeck.actions.registerAction(new ResetClockAction());

streamDeck.connect();
