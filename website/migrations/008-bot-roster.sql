-- Migration 008 — structured bot roster per team: each bot has a name, photo,
-- weight, weapon, primary driver, and build date. Safe to re-run.

alter table teams add column if not exists bot_roster jsonb not null default '[]';

drop view if exists teams_public;
create view teams_public as
  select id, slug, name, drivers, bots, special_features,
         home_town, motto, logo_url, bot_photos, established, bot_roster,
         created_at, updated_at
  from teams;
grant select on teams_public to anon;

drop function if exists update_team(text, text, text, text, text, text, text, text, text, jsonb, text);
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
