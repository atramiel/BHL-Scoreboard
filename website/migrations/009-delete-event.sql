-- Migration 009 — full event delete: wipes games, champions, awards, and the
-- event metadata row for one event name, in a single call. Safe to re-run.

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
