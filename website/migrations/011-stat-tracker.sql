-- Migration 011 — Live Stat Tracker: per-game bot/driver stat sessions and events.
-- Website-only feature (website/stats.html); no app or relay changes. Safe to re-run.

create table if not exists stat_sessions (
  id uuid primary key default gen_random_uuid(),
  event_name text not null default '',
  home_team text not null,
  visitor_team text not null,
  status text not null default 'live' check (status in ('live', 'unconfirmed', 'validated')),
  created_at timestamptz default now()
);
alter table stat_sessions enable row level security;
drop policy if exists stat_sessions_public_read on stat_sessions;
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
drop policy if exists game_events_public_read on game_events;
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

-- Called when the stats keeper finishes live tracking: session moves from
-- 'live' to 'unconfirmed' and waits for a review pass (same person or someone else).
create or replace function admin_end_stat_session(p_admin_key text, p_session_id uuid)
returns boolean language plpgsql security definer as $$
begin
  if not is_admin(p_admin_key) then raise exception 'bad admin key'; end if;
  update stat_sessions set status = 'unconfirmed' where id = p_session_id and status = 'live';
  return found;
end $$;

-- Bulk "Validate All": marks every event in the session validated and the
-- session itself validated. Individual confirm/edit/delete happens beforehand
-- via admin_set_stat_event_validated / admin_update_stat_event / admin_delete_stat_event.
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
