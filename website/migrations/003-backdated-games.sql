-- Migration 003 — record_game accepts an optional played_at for importing
-- past matches. Safe to re-run.

drop function if exists record_game(text, text, text, text, int, int, boolean, boolean, bigint, boolean);

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

-- Admin cleanup: delete a game by id (for fixing bad imports)
create or replace function admin_delete_game(p_admin_key text, p_id uuid)
returns boolean language plpgsql security definer as $$
begin
  if not is_admin(p_admin_key) then raise exception 'bad admin key'; end if;
  delete from games where id = p_id;
  return found;
end $$;

-- Admin cleanup: delete ALL games for an event (re-import after a bad paste)
create or replace function admin_delete_event_games(p_admin_key text, p_event_name text)
returns int language plpgsql security definer as $$
declare v_count int;
begin
  if not is_admin(p_admin_key) then raise exception 'bad admin key'; end if;
  delete from games where event_name = p_event_name;
  get diagnostics v_count = row_count;
  return v_count;
end $$;
