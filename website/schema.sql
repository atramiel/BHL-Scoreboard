-- BHL League Website — Supabase schema
-- Run this once in the Supabase SQL editor (Dashboard → SQL Editor → New query → paste → Run).
--
-- Security model:
--   * Everything is publicly READABLE through views/tables with RLS.
--   * All WRITES go through RPC functions that validate either a team's secret
--     edit key or the league admin key. The anon API key alone can never write.

-- ============================================================
-- Secrets (admin key — pick your own value below before running)
-- ============================================================
create table if not exists league_secrets (
  id int primary key default 1 check (id = 1),
  admin_key text not null
);
alter table league_secrets enable row level security;
-- No policies: not readable or writable through the API at all.

-- NOTE: this file contains NO secrets and is safe to commit.
-- After running it, set your admin key by running this ONE line separately
-- in the Supabase SQL editor (type it there directly — never save it in a file):
--
--   insert into league_secrets (id, admin_key) values (1, 'your-long-random-key')
--   on conflict (id) do update set admin_key = excluded.admin_key;
--
-- Re-running that line with a new value rotates the key.

create or replace function is_admin(p_key text) returns boolean
language sql security definer stable as
$$ select exists (select 1 from league_secrets where admin_key = p_key); $$;

-- ============================================================
-- Teams (profiles are self-service via secret edit links)
-- ============================================================
create table if not exists teams (
  id uuid primary key default gen_random_uuid(),
  slug text unique not null,                -- url-friendly, e.g. 'the-hammers'
  name text not null,                       -- must match the Challonge participant name
  drivers text default '',                  -- driver names, free text
  bots text default '',                     -- bot names, free text
  special_features text default '',
  home_town text default '',
  motto text default '',
  logo_url text default '',
  bot_photos jsonb not null default '[]',   -- array of public image URLs
  bot_roster jsonb not null default '[]',   -- [{name, photo_url, weight, weapon, driver, built}]
  established text default '',              -- when the team was founded (free text)
  edit_key text not null,                   -- secret; never exposed via the public view
  created_at timestamptz default now(),
  updated_at timestamptz default now()
);
alter table teams enable row level security;
-- No direct API access to the base table (edit_key lives here).

-- Public, safe view of teams:
create or replace view teams_public as
  select id, slug, name, drivers, bots, special_features,
         home_town, motto, logo_url, bot_photos, established, bot_roster,
         created_at, updated_at
  from teams;
grant select on teams_public to anon;

-- Team self-edit (validates the team's secret key):
create or replace function update_team(
  p_slug text, p_key text,
  p_name text, p_drivers text, p_bots text,
  p_special_features text, p_home_town text,
  p_motto text, p_logo_url text, p_bot_photos jsonb,
  p_established text default '',
  p_bot_roster jsonb default '[]'
) returns boolean language plpgsql security definer as $$
begin
  update teams set
    name = coalesce(nullif(trim(p_name), ''), name),
    drivers = p_drivers, bots = p_bots,
    special_features = p_special_features,
    home_town = p_home_town, motto = p_motto,
    logo_url = p_logo_url,
    bot_photos = coalesce(p_bot_photos, '[]'::jsonb),
    established = coalesce(p_established, ''),
    bot_roster = coalesce(p_bot_roster, '[]'::jsonb),
    updated_at = now()
  where slug = p_slug and edit_key = p_key;
  return found;
end $$;

-- Admin: create a team and mint its secret edit key (returned once — save it!)
create or replace function admin_create_team(p_admin_key text, p_name text)
returns table (slug text, edit_key text) language plpgsql security definer as $$
declare v_slug text; v_key text;
begin
  if not is_admin(p_admin_key) then raise exception 'bad admin key'; end if;
  v_slug := regexp_replace(lower(trim(p_name)), '[^a-z0-9]+', '-', 'g');
  v_key := encode(gen_random_bytes(9), 'hex');
  insert into teams (slug, name, edit_key) values (v_slug, p_name, v_key);
  return query select v_slug, v_key;
end $$;

-- Admin: look up (or rotate) a team's edit link key
create or replace function admin_get_team_key(p_admin_key text, p_slug text)
returns text language plpgsql security definer as $$
declare v_key text;
begin
  if not is_admin(p_admin_key) then raise exception 'bad admin key'; end if;
  select edit_key into v_key from teams where slug = p_slug;
  return v_key;
end $$;

-- ============================================================
-- Game history (pushed up by the scoreboard app)
-- ============================================================
create table if not exists games (
  id uuid primary key default gen_random_uuid(),
  played_at timestamptz default now(),
  event_name text default '',
  team1_name text not null,
  team2_name text not null,
  team1_score int not null,
  team2_score int not null,
  overtime boolean default false,
  championship boolean default false,
  challonge_match_id bigint,
  reported_to_challonge boolean default true,
  scores_counted boolean not null default true  -- false: W/L real, goal counts placeholder
);
alter table games enable row level security;
create policy games_public_read on games for select to anon using (true);

-- The scoreboard app (and bulk import) records results with the admin key.
-- p_played_at allows backdating when importing past matches.
create or replace function record_game(
  p_admin_key text, p_event_name text,
  p_team1 text, p_team2 text, p_score1 int, p_score2 int,
  p_overtime boolean, p_championship boolean,
  p_challonge_match_id bigint, p_reported boolean,
  p_played_at timestamptz default null
) returns uuid language plpgsql security definer as $$
declare v_id uuid;
begin
  if not is_admin(p_admin_key) then raise exception 'bad admin key'; end if;
  insert into games (event_name, team1_name, team2_name, team1_score, team2_score,
                     overtime, championship, challonge_match_id, reported_to_challonge,
                     played_at)
  values (p_event_name, p_team1, p_team2, p_score1, p_score2,
          p_overtime, p_championship, p_challonge_match_id, p_reported,
          coalesce(p_played_at, now()))
  returning id into v_id;
  return v_id;
end $$;

create or replace function admin_delete_game(p_admin_key text, p_id uuid)
returns boolean language plpgsql security definer as $$
begin
  if not is_admin(p_admin_key) then raise exception 'bad admin key'; end if;
  delete from games where id = p_id;
  return found;
end $$;

create or replace function admin_delete_event_games(p_admin_key text, p_event_name text)
returns int language plpgsql security definer as $$
declare v_count int;
begin
  if not is_admin(p_admin_key) then raise exception 'bad admin key'; end if;
  delete from games where event_name = p_event_name;
  get diagnostics v_count = row_count;
  return v_count;
end $$;

-- ============================================================
-- Hall of Fame: champions (seeded with pre-app history, admin-editable)
-- ============================================================
create table if not exists champions (
  id uuid primary key default gen_random_uuid(),
  event_name text not null,
  event_date date,
  team_name text not null,
  place int not null default 1 check (place between 1 and 3),
  notes text default '',
  era text not null default 'BHL',   -- 'BHL' or 'Legacy' (pre-BHL history, e.g. RoboGames)
  is_national boolean not null default false  -- the annual National Championship — the one big trophy
);
alter table champions enable row level security;
create policy champions_public_read on champions for select to anon using (true);

create or replace function admin_add_champion(
  p_admin_key text, p_event_name text, p_event_date date,
  p_team_name text, p_place int, p_notes text,
  p_era text default 'BHL',
  p_is_national boolean default false
) returns uuid language plpgsql security definer as $$
declare v_id uuid;
begin
  if not is_admin(p_admin_key) then raise exception 'bad admin key'; end if;
  insert into champions (event_name, event_date, team_name, place, notes, era, is_national)
  values (p_event_name, p_event_date, p_team_name, p_place, p_notes, coalesce(p_era, 'BHL'), coalesce(p_is_national, false))
  returning id into v_id;
  return v_id;
end $$;

create or replace function admin_delete_champion(p_admin_key text, p_id uuid)
returns boolean language plpgsql security definer as $$
begin
  if not is_admin(p_admin_key) then raise exception 'bad admin key'; end if;
  delete from champions where id = p_id;
  return found;
end $$;

-- ============================================================
-- Events: dates, venue, city/state (name matches games.event_name)
-- ============================================================
create table if not exists events (
  id uuid primary key default gen_random_uuid(),
  name text unique not null,
  event_date date,
  end_date date,
  venue text default '',
  city text default '',
  state text default '',
  notes text default ''
);
alter table events enable row level security;
create policy events_public_read on events for select to anon using (true);

-- Full event wipe: games, champions, awards, and the event metadata row
create or replace function admin_delete_event(p_admin_key text, p_event_name text)
returns table (games_deleted int, champions_deleted int, awards_deleted int, event_deleted boolean)
language plpgsql security definer as $$
declare
  v_games int; v_champs int; v_awards int; v_event boolean;
begin
  if not is_admin(p_admin_key) then raise exception 'bad admin key'; end if;

  delete from games where event_name = p_event_name;
  get diagnostics v_games = row_count;

  delete from champions where event_name = p_event_name;
  get diagnostics v_champs = row_count;

  delete from awards where event_name = p_event_name;
  get diagnostics v_awards = row_count;

  delete from events where name = p_event_name;
  v_event := found;

  return query select v_games, v_champs, v_awards, v_event;
end $$;

create or replace function admin_upsert_event(
  p_admin_key text, p_name text, p_event_date date, p_end_date date,
  p_venue text, p_city text, p_state text, p_notes text
) returns boolean language plpgsql security definer as $$
begin
  if not is_admin(p_admin_key) then raise exception 'bad admin key'; end if;
  insert into events (name, event_date, end_date, venue, city, state, notes)
  values (trim(p_name), p_event_date, p_end_date, p_venue, p_city, p_state, p_notes)
  on conflict (name) do update set
    event_date = excluded.event_date, end_date = excluded.end_date,
    venue = excluded.venue, city = excluded.city,
    state = excluded.state, notes = excluded.notes;
  return true;
end $$;

-- ============================================================
-- Team aliases: renames and sub-teams resolve to a canonical name
-- ============================================================
create table if not exists team_aliases (
  id uuid primary key default gen_random_uuid(),
  alias text unique not null,
  canonical text not null
);
alter table team_aliases enable row level security;
create policy aliases_public_read on team_aliases for select to anon using (true);

create or replace function admin_add_alias(
  p_admin_key text, p_alias text, p_canonical text
) returns boolean language plpgsql security definer as $$
begin
  if not is_admin(p_admin_key) then raise exception 'bad admin key'; end if;
  insert into team_aliases (alias, canonical) values (trim(p_alias), trim(p_canonical))
  on conflict (alias) do update set canonical = excluded.canonical;
  return true;
end $$;

create or replace function admin_delete_alias(p_admin_key text, p_alias text)
returns boolean language plpgsql security definer as $$
begin
  if not is_admin(p_admin_key) then raise exception 'bad admin key'; end if;
  delete from team_aliases where alias = p_alias;
  return found;
end $$;

-- ============================================================
-- Rivalries (admin-curated, with the story of why)
-- ============================================================
create table if not exists rivalries (
  id uuid primary key default gen_random_uuid(),
  team_a text not null,
  team_b text not null,
  story text not null default '',
  declared_by text  -- team name that created this row (self-service); null = admin-curated
);
alter table rivalries enable row level security;
create policy rivalries_public_read on rivalries for select to anon using (true);

-- Team self-service: replace this team's own declared rivalries (max 3).
create or replace function team_set_rivalries(
  p_slug text, p_key text, p_rivals jsonb
) returns boolean language plpgsql security definer as $$
declare
  v_team_name text;
  v_rival jsonb;
  v_rival_team text;
begin
  select name into v_team_name from teams where slug = p_slug and edit_key = p_key;
  if v_team_name is null then raise exception 'bad edit link'; end if;

  if jsonb_array_length(coalesce(p_rivals, '[]'::jsonb)) > 3 then
    raise exception 'a team may declare at most 3 rivals';
  end if;

  delete from rivalries where declared_by = v_team_name;

  for v_rival in select * from jsonb_array_elements(coalesce(p_rivals, '[]'::jsonb))
  loop
    v_rival_team := trim(v_rival->>'team');
    if v_rival_team = '' or v_rival_team is null then continue; end if;
    if not exists (select 1 from teams where name = v_rival_team) then
      raise exception 'unknown team: %', v_rival_team;
    end if;
    insert into rivalries (team_a, team_b, story, declared_by)
    values (v_team_name, v_rival_team, coalesce(v_rival->>'story', ''), v_team_name);
  end loop;

  return true;
end $$;

create or replace function admin_add_rivalry(
  p_admin_key text, p_team_a text, p_team_b text, p_story text
) returns uuid language plpgsql security definer as $$
declare v_id uuid;
begin
  if not is_admin(p_admin_key) then raise exception 'bad admin key'; end if;
  insert into rivalries (team_a, team_b, story) values (p_team_a, p_team_b, p_story)
  returning id into v_id;
  return v_id;
end $$;

create or replace function admin_delete_rivalry(p_admin_key text, p_id uuid)
returns boolean language plpgsql security definer as $$
begin
  if not is_admin(p_admin_key) then raise exception 'bad admin key'; end if;
  delete from rivalries where id = p_id;
  return found;
end $$;

-- ============================================================
-- Awards (custom trophies per event: Best Bot, Best Driver, ...)
-- ============================================================
create table if not exists awards (
  id uuid primary key default gen_random_uuid(),
  event_name text not null,
  award_name text not null,
  team_name text not null,
  notes text default '',
  era text not null default 'BHL'   -- 'BHL' or 'Legacy'
);
alter table awards enable row level security;
create policy awards_public_read on awards for select to anon using (true);

create or replace function admin_add_award(
  p_admin_key text, p_event_name text, p_award_name text,
  p_team_name text, p_notes text,
  p_era text default 'BHL'
) returns uuid language plpgsql security definer as $$
declare v_id uuid;
begin
  if not is_admin(p_admin_key) then raise exception 'bad admin key'; end if;
  insert into awards (event_name, award_name, team_name, notes, era)
  values (p_event_name, p_award_name, p_team_name, p_notes, coalesce(p_era, 'BHL'))
  returning id into v_id;
  return v_id;
end $$;

-- Merge cleanup: delete a duplicate team profile (alias its name first)
create or replace function admin_delete_team(p_admin_key text, p_slug text)
returns boolean language plpgsql security definer as $$
begin
  if not is_admin(p_admin_key) then raise exception 'bad admin key'; end if;
  delete from teams where slug = p_slug;
  return found;
end $$;

-- ============================================================
-- Site content ("What is Bot Hockey?", league history — admin-editable pages)
-- ============================================================
create table if not exists site_content (
  key text primary key,          -- e.g. 'what-is-bot-hockey', 'league-history'
  title text not null,
  body text not null default ''  -- plain text / simple markdown
);
alter table site_content enable row level security;
create policy site_content_public_read on site_content for select to anon using (true);

create or replace function admin_set_content(
  p_admin_key text, p_key text, p_title text, p_body text
) returns boolean language plpgsql security definer as $$
begin
  if not is_admin(p_admin_key) then raise exception 'bad admin key'; end if;
  insert into site_content (key, title, body) values (p_key, p_title, p_body)
  on conflict (key) do update set title = excluded.title, body = excluded.body;
  return true;
end $$;

insert into site_content (key, title, body) values
  ('what-is-bot-hockey', 'What is Bot Hockey?', 'Write the story of bot hockey here (Admin page → Site Content).'),
  ('league-history', 'League History', 'Write the league''s history here (Admin page → Site Content).')
on conflict (key) do nothing;

-- ============================================================
-- Live Stat Tracker: per-game bot/driver stat sessions and events
-- (website/stats.html — a dedicated stats keeper's touch UI; no app/relay changes)
-- ============================================================
create table if not exists stat_sessions (
  id uuid primary key default gen_random_uuid(),
  event_name text not null default '',
  home_team text not null,
  visitor_team text not null,
  status text not null default 'live' check (status in ('live', 'unconfirmed', 'validated')),
  created_at timestamptz default now()
);
alter table stat_sessions enable row level security;
create policy stat_sessions_public_read on stat_sessions for select to anon using (true);

create table if not exists game_events (
  id uuid primary key default gen_random_uuid(),
  session_id uuid not null references stat_sessions(id) on delete cascade,
  team_name text not null,
  event_type text not null check (event_type in ('goal', 'assist', 'hit', 'own_goal', 'sub', 'lineup_start')),
  bot_name text default '',
  related_bot_name text default '',
  driver_name text default '',
  occurred_at timestamptz default now(),
  validated boolean not null default false
);
alter table game_events enable row level security;
create policy game_events_public_read on game_events for select to anon using (true);

create or replace function admin_create_stat_session(
  p_admin_key text, p_event_name text, p_home_team text, p_visitor_team text
) returns uuid language plpgsql security definer as $$
declare v_id uuid;
begin
  if not is_admin(p_admin_key) then raise exception 'bad admin key'; end if;
  insert into stat_sessions (event_name, home_team, visitor_team)
  values (trim(p_event_name), p_home_team, p_visitor_team)
  returning id into v_id;
  return v_id;
end $$;

create or replace function admin_log_stat_event(
  p_admin_key text, p_session_id uuid, p_team_name text, p_event_type text,
  p_bot_name text default '', p_related_bot_name text default '', p_driver_name text default ''
) returns uuid language plpgsql security definer as $$
declare v_id uuid;
begin
  if not is_admin(p_admin_key) then raise exception 'bad admin key'; end if;
  insert into game_events (session_id, team_name, event_type, bot_name, related_bot_name, driver_name)
  values (p_session_id, p_team_name, p_event_type, p_bot_name, p_related_bot_name, p_driver_name)
  returning id into v_id;
  return v_id;
end $$;

create or replace function admin_update_stat_event(
  p_admin_key text, p_id uuid, p_bot_name text, p_related_bot_name text, p_driver_name text
) returns boolean language plpgsql security definer as $$
begin
  if not is_admin(p_admin_key) then raise exception 'bad admin key'; end if;
  update game_events set bot_name = p_bot_name, related_bot_name = p_related_bot_name, driver_name = p_driver_name
  where id = p_id;
  return found;
end $$;

create or replace function admin_delete_stat_event(p_admin_key text, p_id uuid)
returns boolean language plpgsql security definer as $$
begin
  if not is_admin(p_admin_key) then raise exception 'bad admin key'; end if;
  delete from game_events where id = p_id;
  return found;
end $$;

create or replace function admin_set_stat_event_validated(p_admin_key text, p_id uuid, p_validated boolean)
returns boolean language plpgsql security definer as $$
begin
  if not is_admin(p_admin_key) then raise exception 'bad admin key'; end if;
  update game_events set validated = p_validated where id = p_id;
  return found;
end $$;

create or replace function admin_end_stat_session(p_admin_key text, p_session_id uuid)
returns boolean language plpgsql security definer as $$
begin
  if not is_admin(p_admin_key) then raise exception 'bad admin key'; end if;
  update stat_sessions set status = 'unconfirmed' where id = p_session_id and status = 'live';
  return found;
end $$;

create or replace function admin_validate_stat_session(p_admin_key text, p_session_id uuid)
returns boolean language plpgsql security definer as $$
begin
  if not is_admin(p_admin_key) then raise exception 'bad admin key'; end if;
  update game_events set validated = true where session_id = p_session_id;
  update stat_sessions set status = 'validated' where id = p_session_id;
  return found;
end $$;

create or replace function admin_delete_stat_session(p_admin_key text, p_session_id uuid)
returns boolean language plpgsql security definer as $$
begin
  if not is_admin(p_admin_key) then raise exception 'bad admin key'; end if;
  delete from stat_sessions where id = p_session_id;
  return found;
end $$;

-- ============================================================
-- Storage: team logos and bot photos (public bucket, 5 MB per file)
-- ============================================================
insert into storage.buckets (id, name, public, file_size_limit)
values ('team-media', 'team-media', true, 5242880)
on conflict (id) do nothing;

drop policy if exists "team media public read" on storage.objects;
create policy "team media public read" on storage.objects
  for select to anon using (bucket_id = 'team-media');
drop policy if exists "team media anon upload" on storage.objects;
create policy "team media anon upload" on storage.objects
  for insert to anon with check (bucket_id = 'team-media');
