-- One-shot import: migration 005 + aliases + three 2025 BHL events
-- (Nexus Knockout 2, BHL at Open Sauce 2025, Norcal Robotics Expo 2025).
-- Sourced from Challonge brackets bothockey032025, 975lvhkk, fx9nwvom.
-- Run ONCE in the Supabase SQL editor. Migration/alias parts are re-runnable;
-- the game/champion inserts are NOT — running twice duplicates them.
-- (To redo: select admin_delete_event_games('<admin key>', '<event name>');)

-- ===== Migration 005: aliases + uncounted scores (safe to re-run) =====
create table if not exists team_aliases (
  id uuid primary key default gen_random_uuid(),
  alias text unique not null,
  canonical text not null
);
alter table team_aliases enable row level security;
drop policy if exists aliases_public_read on team_aliases;
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

alter table games add column if not exists scores_counted boolean not null default true;

-- ===== Aliases (safe to re-run) =====
insert into team_aliases (alias, canonical) values
('Team No-Ice',   'No Ice'),
('Evac',          'Team EVAC'),
('Shiny EVAC',    'Team EVAC'),
('Party Animals', 'Party Animals!')
on conflict (alias) do update set canonical = excluded.canonical;

-- ===== Nexus Knockout 2 — Mar 22, 2025 (double elim; goals not tracked) =====
insert into games (event_name, team1_name, team2_name, team1_score, team2_score, overtime, championship, played_at, scores_counted) values
('Nexus Knockout 2','Magic Smoke','Hockeymaniacs',       1, 0, false, false, '2025-03-22 13:43:00-04', false),
('Nexus Knockout 2','ScottBot','Toon Town Trouble',      1, 0, false, false, '2025-03-22 14:08:00-04', false),
('Nexus Knockout 2','Shiny EVAC','Party Animals!',       1, 0, false, false, '2025-03-22 15:12:00-04', false),
('Nexus Knockout 2','Team Ice','Magic Smoke',            1, 0, false, false, '2025-03-22 15:53:00-04', false),
('Nexus Knockout 2','Toon Town Trouble','Party Animals!',1, 0, false, false, '2025-03-22 16:56:00-04', false),
('Nexus Knockout 2','ScottBot','Shiny EVAC',             1, 0, false, false, '2025-03-22 17:19:00-04', false),
('Nexus Knockout 2','Magic Smoke','Toon Town Trouble',   1, 0, false, false, '2025-03-22 18:10:00-04', false),
('Nexus Knockout 2','Team Ice','ScottBot',               1, 0, false, false, '2025-03-22 19:14:00-04', false),
('Nexus Knockout 2','Hockeymaniacs','Shiny EVAC',        1, 0, false, false, '2025-03-22 19:15:00-04', false),
('Nexus Knockout 2','Magic Smoke','Hockeymaniacs',       1, 0, false, false, '2025-03-22 20:37:00-04', false),
('Nexus Knockout 2','ScottBot','Magic Smoke',            1, 0, false, false, '2025-03-22 20:38:00-04', false),
('Nexus Knockout 2','ScottBot','Team Ice',               1, 0, false, true,  '2025-03-22 21:21:00-04', false);

insert into champions (event_name, event_date, team_name, place, notes) values
('Nexus Knockout 2','2025-03-22','ScottBot',   1,'Won grand final over Team Ice (goals not tracked)'),
('Nexus Knockout 2','2025-03-22','Team Ice',   2,''),
('Nexus Knockout 2','2025-03-22','Magic Smoke',3,'');

-- ===== BHL at Open Sauce 2025 — Jul 19–20, 2025 (groups + bracket) =====
insert into games (event_name, team1_name, team2_name, team1_score, team2_score, overtime, championship, played_at) values
('BHL at Open Sauce 2025','Hock Stuff','Party Animals',   12,  6, false, false, '2025-07-20 01:08:00+07'),
('BHL at Open Sauce 2025','No Ice','Evac',                 8,  6, false, false, '2025-07-20 01:34:00+07'),
('BHL at Open Sauce 2025','Exile','Magic Smoke',          14, 11, false, false, '2025-07-20 01:55:00+07'),
('BHL at Open Sauce 2025','Exile','Royally Pucked',        7,  1, false, false, '2025-07-20 04:28:00+07'),
('BHL at Open Sauce 2025','No Ice','Hock Stuff',          12,  6, false, false, '2025-07-20 04:50:00+07'),
('BHL at Open Sauce 2025','Magic Smoke','Party Animals',  15, 13, false, false, '2025-07-20 05:10:00+07'),
('BHL at Open Sauce 2025','No Ice','Exile',               19,  2, false, false, '2025-07-20 06:56:00+07'),
('BHL at Open Sauce 2025','Hock Stuff','Magic Smoke',     15, 11, false, false, '2025-07-20 07:16:00+07'),
('BHL at Open Sauce 2025','Evac','Royally Pucked',        10,  3, false, false, '2025-07-20 07:41:00+07'),
('BHL at Open Sauce 2025','No Ice','Party Animals',       16, 10, false, false, '2025-07-21 00:20:00+07'),
('BHL at Open Sauce 2025','Hock Stuff','Royally Pucked',  18,  7, false, false, '2025-07-21 00:45:00+07'),
('BHL at Open Sauce 2025','Exile','Evac',                 16,  5, false, false, '2025-07-21 01:06:00+07'),
('BHL at Open Sauce 2025','Magic Smoke','Exile',          14,  8, false, false, '2025-07-21 02:53:00+07'),
('BHL at Open Sauce 2025','Hock Stuff','Royally Pucked',  13,  5, false, false, '2025-07-21 03:12:00+07'),
('BHL at Open Sauce 2025','Evac','Party Animals',         21,  2, false, false, '2025-07-21 03:33:00+07'),
('BHL at Open Sauce 2025','No Ice','Magic Smoke',         17,  5, false, false, '2025-07-21 04:58:00+07'),
('BHL at Open Sauce 2025','Hock Stuff','Evac',            18,  8, false, false, '2025-07-21 05:19:00+07'),
('BHL at Open Sauce 2025','No Ice','Hock Stuff',          13,  6, false, true,  '2025-07-21 06:50:00+07');

insert into champions (event_name, event_date, team_name, place, notes) values
('BHL at Open Sauce 2025','2025-07-20','No Ice',     1,'Beat Hock Stuff 13–6 in the final'),
('BHL at Open Sauce 2025','2025-07-20','Hock Stuff', 2,''),
('BHL at Open Sauce 2025','2025-07-20','Team EVAC',  3,'Shared 3rd (competed as Evac) — no head-to-head vs Magic Smoke'),
('BHL at Open Sauce 2025','2025-07-20','Magic Smoke',3,'Shared 3rd — no head-to-head vs Team EVAC');

-- ===== Norcal Robotics Expo 2025 — Nov 22–23, 2025 (swiss) =====
insert into games (event_name, team1_name, team2_name, team1_score, team2_score, overtime, championship, played_at, scores_counted) values
('Norcal Robotics Expo 2025','Team EVAC','Royally Pucked',       11, 4, false, false, '2025-11-23 02:52:00+08', true),
('Norcal Robotics Expo 2025','Toon Town Trouble','Spare Parts',  12, 9, false, false, '2025-11-23 03:09:00+08', true),
('Norcal Robotics Expo 2025','Team No-Ice','Party Animals!',     23, 1, false, false, '2025-11-23 03:36:00+08', true),
('Norcal Robotics Expo 2025','Royally Pucked','Spare Parts',      9, 5, false, false, '2025-11-23 05:04:00+08', true),
('Norcal Robotics Expo 2025','Magic Smoke','Toon Town Trouble',  20, 7, false, false, '2025-11-23 05:28:00+08', true),
('Norcal Robotics Expo 2025','Team No-Ice','Team EVAC',          17, 6, false, false, '2025-11-23 05:51:00+08', true),
('Norcal Robotics Expo 2025','Party Animals!','Team EVAC',        1, 0, false, false, '2025-11-23 06:38:00+08', false),
('Norcal Robotics Expo 2025','Royally Pucked','Toon Town Trouble',16, 6, false, false, '2025-11-23 07:47:00+08', true),
('Norcal Robotics Expo 2025','Team No-Ice','Magic Smoke',        19, 1, false, false, '2025-11-23 08:12:00+08', true);

insert into champions (event_name, event_date, team_name, place, notes) values
('Norcal Robotics Expo 2025','2025-11-22','No Ice',        1,'Swiss format, 3–0 (competed as Team No-Ice)'),
('Norcal Robotics Expo 2025','2025-11-22','Royally Pucked',2,''),
('Norcal Robotics Expo 2025','2025-11-22','Magic Smoke',   3,'');
