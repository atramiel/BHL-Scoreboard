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
import { NextMatchDialAction } from "./actions/nextMatchDialAction";
import { BetweenGameAction } from "./actions/betweenGameAction";
import { MatchSlot1Action, MatchSlot2Action, MatchSlot3Action, MatchSlot4Action, MatchSlot5Action, MatchSlot6Action } from "./actions/matchSlotAction";

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
streamDeck.actions.registerAction(new NextMatchDialAction());
streamDeck.actions.registerAction(new BetweenGameAction());
streamDeck.actions.registerAction(new MatchSlot1Action());
streamDeck.actions.registerAction(new MatchSlot2Action());
streamDeck.actions.registerAction(new MatchSlot3Action());
streamDeck.actions.registerAction(new MatchSlot4Action());
streamDeck.actions.registerAction(new MatchSlot5Action());
streamDeck.actions.registerAction(new MatchSlot6Action());

streamDeck.connect();
