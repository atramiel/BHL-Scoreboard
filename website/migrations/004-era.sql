-- Migration 004 — champions get an "era" (BHL vs Legacy/pre-BHL), and teams
-- get a "date established". Safe to re-run.

alter table champions add column if not exists era text not null default 'BHL';

-- Teams: date established
alter table teams add column if not exists established text default '';

drop view if exists teams_public;
create view teams_public as
  select id, slug, name, drivers, bots, special_features,
         home_town, motto, logo_url, bot_photos, established,
         created_at, updated_at
  from teams;
grant select on teams_public to anon;

drop function if exists update_team(text, text, text, text, text, text, text, text, text, jsonb);
create or replace function update_team(
  p_slug text, p_key text,
  p_name text, p_drivers text, p_bots text,
  p_special_features text, p_home_town text,
  p_motto text, p_logo_url text, p_bot_photos jsonb,
  p_established text default ''
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
    updated_at = now()
  where slug = p_slug and edit_key = p_key;
  return found;
end $$;

-- Tag all RoboGames history as Legacy (works whether imported before or after this migration)
update champions set era = 'Legacy' where event_name like 'RoboGames%';

-- admin_add_champion learns an optional era
drop function if exists admin_add_champion(text, text, date, text, int, text);
create or replace function admin_add_champion(
  p_admin_key text, p_event_name text, p_event_date date,
  p_team_name text, p_place int, p_notes text,
  p_era text default 'BHL'
) returns uuid language plpgsql security definer as $$
declare v_id uuid;
begin
  if not is_admin(p_admin_key) then raise exception 'bad admin key'; end if;
  insert into champions (event_name, event_date, team_name, place, notes, era)
  values (p_event_name, p_event_date, p_team_name, p_place, p_notes, coalesce(p_era, 'BHL'))
  returning id into v_id;
  return v_id;
end $$;
