# BHL League Website

Static site + Supabase backend for the Bot Hockey League: team self-registration via
private edit links, game history, Hall of Fame, rivalries, awards, and league lore.
No server to maintain — the site is plain HTML/JS, and Supabase provides the database
and API.

## Pages

| Page | What it does |
|---|---|
| `index.html` | Team roster with all-time records + recent results |
| `team.html?team=<slug>` | Team profile: drivers, bots, features, game history, rivalries |
| `team.html?team=<slug>&key=<secret>` | Same page with the profile **edit form** unlocked |
| `hall-of-fame.html` | Champions by event, awards, rivalries, league history |
| `about.html` | "What is Bot Hockey?" (editable content) |
| `admin.html` | Create teams, mint edit links, seed champions/rivalries/awards, edit content pages |

## Security model

- Everything is publicly **readable** (roster, results, hall of fame).
- All **writes** go through Postgres RPC functions that check a secret server-side:
  - Teams edit their own profile with the secret key baked into their edit link.
  - Everything else (creating teams, champions, rivalries, awards, content, and the
    scoreboard app recording games) requires the **admin key**.
- The `anon` API key in `config.js` is safe to publish — it can only read.
- Team edit keys live in the `teams` table, which has **no** public API access;
  the public reads through the `teams_public` view instead.

## Setup (one time, ~10 minutes)

1. **Create a Supabase project** at https://supabase.com (free tier is fine).
2. **Run the schema**: Supabase Dashboard → SQL Editor → New query → paste all of
   `schema.sql` → Run. (The file contains no secrets — it's safe to keep in git.)
3. **Set your admin key** — type this directly in the SQL editor (don't put your
   real key in any file in the repo):

   ```sql
   insert into league_secrets (id, admin_key) values (1, 'your-long-random-key')
   on conflict (id) do update set admin_key = excluded.admin_key;
   ```

   Keep the key somewhere safe (password manager) — it unlocks the admin page and
   is what the scoreboard app will use to push results. Re-run the line with a new
   value any time to rotate it.
4. **Fill in `config.js`**: Dashboard → Project Settings → API → copy the
   Project URL and the `anon` `public` key. (The anon key is read-only by design
   and safe to commit/publish.)
5. **Deploy to Vercel**:
   1. Go to https://vercel.com/new and import the `BHL-Scoreboard` GitHub repo
      (connect your GitHub account if it asks).
   2. On the configure screen:
      - **Root Directory** → click Edit → select `website`
      - **Framework Preset** → `Other`
      - **Build Command / Output Directory / Install Command** → leave empty
        (it's plain HTML — there is no build)
   3. Click **Deploy**. Your site is live at `https://<project-name>.vercel.app`.
   4. Every `git push` to `master` redeploys the site automatically. Changes
      outside `website/` (the scoreboard app itself) don't trigger deploys if you
      leave "Ignored Build Step" default — Vercel only rebuilds when the root
      directory's files change.

   For a quick local test before deploying: `npx serve website` from the repo root.

## Event workflow

1. Before the event, create each team on `admin.html` — copy each team's private
   edit link and send it to the captain.
2. Captains fill in drivers, bots, special features, home town, motto.
3. Seed past champions and rivalries on `admin.html` (one-time backfill of league
   history; add new ones after each event).
4. The scoreboard app records game results via the `record_game` RPC using the
   admin key (app-side integration is on the backlog: pre-event download + result push).

## Scoreboard app integration (planned)

The app will call Supabase's REST API directly:

- **Pull** (pre-event download): `GET {SUPABASE_URL}/rest/v1/teams_public?select=*`
  and the same for `games`, `rivalries`, `champions` — header `apikey: <anon key>`.
- **Push** (after each game): `POST {SUPABASE_URL}/rest/v1/rpc/record_game` with the
  admin key as a parameter.
