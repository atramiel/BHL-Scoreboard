-- Migration 010 — team-editable rivalries (max 3, self-service) and a
-- National Championship distinction separate from regular event medals.
-- Safe to re-run.

-- ===== Rivalries: track who declared each one =====
alter table rivalries add column if not exists declared_by text;

-- Team self-service: replace this team's own declared rivalries (max 3).
-- Rivalries a team declares show up on BOTH teams' pages automatically
-- (the existing team_a/team_b OR query already does this).
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

-- ===== National Championship: the one annual trophy, distinct from event medals =====
alter table champions add column if not exists is_national boolean not null default false;

drop function if exists admin_add_champion(text, text, date, text, int, text, text);
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
