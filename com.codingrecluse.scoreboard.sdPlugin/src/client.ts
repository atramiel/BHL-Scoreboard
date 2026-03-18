import { createConnection, Socket } from "net";
import streamDeck from "@elgato/streamdeck";
import { gameState } from "./gameState";

const PORT = 52800;
const HOST = "127.0.0.1";
const RECONNECT_DELAY_MS = 3000;

let socket: Socket | null = null;
let buffer = "";
let reconnectTimer: ReturnType<typeof setTimeout> | null = null;

function connect(): void {
    if (reconnectTimer !== null) {
        clearTimeout(reconnectTimer);
        reconnectTimer = null;
    }

    socket = createConnection({ port: PORT, host: HOST });

    socket.on("connect", () => {
        streamDeck.logger.info("Connected to Scoreboard app");
        buffer = "";
    });

    socket.on("data", (data: Buffer) => {
        buffer += data.toString("utf8");
        const lines = buffer.split("\n");
        buffer = lines.pop() ?? "";

        for (const line of lines) {
            const trimmed = line.trim();
            if (!trimmed) continue;
            try {
                const parsed = JSON.parse(trimmed);
                gameState.update(parsed);
            } catch {
                // ignore malformed messages
            }
        }
    });

    socket.on("close", () => {
        streamDeck.logger.warn("Disconnected from Scoreboard app — retrying...");
        socket = null;
        scheduleReconnect();
    });

    socket.on("error", () => {
        socket?.destroy();
        socket = null;
        scheduleReconnect();
    });
}

function scheduleReconnect(): void {
    if (reconnectTimer === null) {
        reconnectTimer = setTimeout(connect, RECONNECT_DELAY_MS);
    }
}

export function sendCommand(action: string): void {
    if (socket && !socket.destroyed) {
        socket.write(JSON.stringify({ action }) + "\n", "utf8");
    }
}

// Start connecting immediately when the module is imported
connect();
