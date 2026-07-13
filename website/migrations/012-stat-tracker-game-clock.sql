-- Migration 012 — capture the in-game clock (not wall-clock time) on each stat event,
-- so the review screen and live log show "9:40 left" instead of a real-world timestamp.
-- Safe to re-run.

alter table game_events add column if not exists game_clock text not null default '';

drop function if exists admin_log_stat_event(text, uuid, text, text, text, text, text);
create or replace function admin_log_stat_event(
  p_admin_key text, p_session_id uuid, p_team_name text, p_event_type text,
  p_bot_name text default '', p_related_bot_name text default '', p_driver_name text default '',
  p_game_clock text default ''
) returns uuid language plpgsql security definer as $$
declare v_id uuid;
begin
  if not is_admin(p_admin_key) then raise exception 'bad admin key'; end if;
  insert into game_events (session_id, team_name, event_type, bot_name, related_bot_name, driver_name, game_clock)
  values (p_session_id, p_team_name, p_event_type, p_bot_name, p_related_bot_name, p_driver_name, p_game_clock)
  returning id into v_id;
  return v_id;
end $$;
