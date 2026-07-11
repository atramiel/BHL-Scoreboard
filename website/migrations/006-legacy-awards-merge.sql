-- Migration 006 — awards get an era (legacy vs BHL), and admins can delete a
-- team profile (the cleanup half of merging duplicates). Safe to re-run.

alter table awards add column if not exists era text not null default 'BHL';

drop function if exists admin_add_award(text, text, text, text, text);
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

-- Merge workflow: alias the duplicate's name to the canonical team, then
-- delete the duplicate profile with this.
create or replace function admin_delete_team(p_admin_key text, p_slug text)
returns boolean language plpgsql security definer as $$
begin
  if not is_admin(p_admin_key) then raise exception 'bad admin key'; end if;
  delete from teams where slug = p_slug;
  return found;
end $$;

-- ===== Legacy era awards (derived from the RoboGames record book) =====
insert into awards (event_name, award_name, team_name, notes, era) values
('Legacy Era', 'First Champions',      'Team USA',              'Won the first RoboGames hockey gold, 2007', 'Legacy'),
('Legacy Era', 'Dynasty Award',        'Uai!rrior Hockey Team', 'Four podiums in five events, 2012–2016 (golds in 2012 and 2015)', 'Legacy'),
('Legacy Era', 'Longest Comeback',     'Calculos',              'Titles twelve years apart — 2011 and 2023', 'Legacy'),
('Legacy Era', 'Iron Bridesmaid',      'Team Ice',              'Five podiums across 13 years (2010–2023), still chasing gold', 'Legacy'),
('Legacy Era', 'Legacy Workhorse',     'Team Kick Me',          'Two golds (2009, 2013) and four total podiums', 'Legacy');
