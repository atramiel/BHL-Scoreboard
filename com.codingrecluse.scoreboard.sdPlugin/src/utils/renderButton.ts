/** Creates a base64 SVG data URI for use with ev.action.setImage() */

function toDataUri(svg: string): string {
    return "data:image/svg+xml;base64," + Buffer.from(svg).toString("base64");
}

function esc(str: string): string {
    return str
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;");
}

function clamp(str: string, max = 7): string {
    return str.length > max ? str.substring(0, max) : str;
}

export function renderScoreButton(teamName: string, score: number, bgColor: string): string {
    const name = esc(clamp(teamName));
    const svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 72 72">
  <rect width="72" height="72" rx="6" fill="${bgColor}"/>
  <text x="36" y="24" font-family="Arial,sans-serif" font-size="13" font-weight="bold"
        fill="white" text-anchor="middle">${name}</text>
  <text x="36" y="54" font-family="Arial,sans-serif" font-size="30" font-weight="bold"
        fill="white" text-anchor="middle">${score}</text>
</svg>`;
    return toDataUri(svg);
}

export function renderClockButton(clock: string, isRunning: boolean, gameDone: boolean): string {
    const bg = gameDone ? "#5a0000" : isRunning ? "#1a6b1a" : "#7a4500";
    const label = gameDone ? "GAME OVER" : isRunning ? "▶  RUNNING" : "⏸  PAUSED";
    const svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 72 72">
  <rect width="72" height="72" rx="6" fill="${bg}"/>
  <text x="36" y="22" font-family="Arial,sans-serif" font-size="9" font-weight="bold"
        fill="white" text-anchor="middle">${esc(label)}</text>
  <text x="36" y="48" font-family="Arial,sans-serif" font-size="21" font-weight="bold"
        fill="white" text-anchor="middle">${esc(clock)}</text>
  <text x="36" y="64" font-family="Arial,sans-serif" font-size="9"
        fill="rgba(255,255,255,0.7)" text-anchor="middle">tap to toggle</text>
</svg>`;
    return toDataUri(svg);
}

export function renderPenaltyButton(teamName: string): string {
    const name = esc(clamp(teamName));
    const svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 72 72">
  <rect width="72" height="72" rx="6" fill="#8b0000"/>
  <text x="36" y="30" font-family="Arial,sans-serif" font-size="11" font-weight="bold"
        fill="white" text-anchor="middle">PENALTY</text>
  <text x="36" y="50" font-family="Arial,sans-serif" font-size="14" font-weight="bold"
        fill="white" text-anchor="middle">${name}</text>
</svg>`;
    return toDataUri(svg);
}

export function renderLabelButton(label: string, subLabel = "", bgColor = "#2d2d2d"): string {
    const y1 = subLabel ? "30" : "40";
    const extra = subLabel
        ? `<text x="36" y="50" font-family="Arial,sans-serif" font-size="11" font-weight="bold"
        fill="rgba(255,255,255,0.8)" text-anchor="middle">${esc(subLabel)}</text>`
        : "";
    const svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 72 72">
  <rect width="72" height="72" rx="6" fill="${bgColor}"/>
  <text x="36" y="${y1}" font-family="Arial,sans-serif" font-size="13" font-weight="bold"
        fill="white" text-anchor="middle">${esc(label)}</text>${extra}
</svg>`;
    return toDataUri(svg);
}
