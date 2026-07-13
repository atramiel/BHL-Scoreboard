-- Migration 013 — a separate, simpler passcode for the Stat Tracker so multiple
-- stats keepers can log stats without needing the full league admin key (which
-- can delete teams/events). The admin key still works everywhere too. Also adds
-- a "delete all stats" helper for clearing out test data. Safe to re-run.

alter table league_secrets add column if not exists stats_key text;

create or replace function is_stats_key(p_key text) returns boolean
language sql security definer stable as
$$ select p_key is not null and p_key <> '' and exists (
     select 1 from league_secrets where stats_key = p_key
   ); $$;

-- NOTE: this file contains no secrets. Set the stats passcode separately in the
-- SQL editor (type it there directly — never save it in a file):
--
--   update league_secrets set stats_key = 'something-short-and-easy' where id = 1;

create or replace function admin_create_stat_session(
  p_admin_key text, p_event_name text, p_home_team text, p_visitor_team text
) returns uuid language plpgsql security definer as $$
declare v_id uuid;
begin
  if not (is_admin(p_admin_key) or is_stats_key(p_admin_key)) then raise exception 'bad key'; end if;
  insert into stat_sessions (event_name, home_team, visitor_team)
  values (trim(p_event_name), p_home_team, p_visitor_team)
  returning id into v_id;
  return v_id;
end $$;

create or replace function admin_log_stat_event(
  p_admin_key text, p_session_id uuid, p_team_name text, p_event_type text,
  p_bot_name text default '', p_related_bot_name text default '', p_driver_name text default '',
  p_game_clock text default ''
) returns uuid language plpgsql security definer as $$
declare v_id uuid;
begin
  if not (is_admin(p_admin_key) or is_stats_key(p_admin_key)) then raise exception 'bad key'; end if;
  insert into game_events (session_id, team_name, event_type, bot_name, related_bot_name, driver_name, game_clock)
  values (p_session_id, p_team_name, p_event_type, p_bot_name, p_related_bot_name, p_driver_name, p_game_clock)
  returning id into v_id;
  return v_id;
end $$;

create or replace function admin_update_stat_event(
  p_admin_key text, p_id uuid, p_bot_name text, p_related_bot_name text, p_driver_name text
) returns boolean language plpgsql security definer as $$
begin
  if not (is_admin(p_admin_key) or is_stats_key(p_admin_key)) then raise exception 'bad key'; end if;
  update game_events set bot_name = p_bot_name, related_bot_name = p_related_bot_name, driver_name = p_driver_name
  where id = p_id;
  return found;
end $$;

create or replace function admin_delete_stat_event(p_admin_key text, p_id uuid)
returns boolean language plpgsql security definer as $$
begin
  if not (is_admin(p_admin_key) or is_stats_key(p_admin_key)) then raise exception 'bad key'; end if;
  delete from game_events where id = p_id;
  return found;
end $$;

create or replace function admin_set_stat_event_validated(p_admin_key text, p_id uuid, p_validated boolean)
returns boolean language plpgsql security definer as $$
begin
  if not (is_admin(p_admin_key) or is_stats_key(p_admin_key)) then raise exception 'bad key'; end if;
  update game_events set validated = p_validated where id = p_id;
  return found;
end $$;

create or replace function admin_end_stat_session(p_admin_key text, p_session_id uuid)
returns boolean language plpgsql security definer as $$
begin
  if not (is_admin(p_admin_key) or is_stats_key(p_admin_key)) then raise exception 'bad key'; end if;
  update stat_sessions set status = 'unconfirmed' where id = p_session_id and status = 'live';
  return found;
end $$;

create or replace function admin_validate_stat_session(p_admin_key text, p_session_id uuid)
returns boolean language plpgsql security definer as $$
begin
  if not (is_admin(p_admin_key) or is_stats_key(p_admin_key)) then raise exception 'bad key'; end if;
  update game_events set validated = true where session_id = p_session_id;
  update stat_sessions set status = 'validated' where id = p_session_id;
  return found;
end $$;

create or replace function admin_delete_stat_session(p_admin_key text, p_session_id uuid)
returns boolean language plpgsql security definer as $$
begin
  if not (is_admin(p_admin_key) or is_stats_key(p_admin_key)) then raise exception 'bad key'; end if;
  delete from stat_sessions where id = p_session_id;
  return found;
end $$;

-- Wipes every stat session and event — handy while testing, not for event night.
create or replace function admin_delete_all_stats(p_admin_key text)
returns int language plpgsql security definer as $$
declare v_count int;
begin
  if not (is_admin(p_admin_key) or is_stats_key(p_admin_key)) then raise exception 'bad key'; end if;
  delete from stat_sessions;
  get diagnostics v_count = row_count;
  return v_count;
end $$;
