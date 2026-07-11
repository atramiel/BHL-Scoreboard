-- Migration 007 — proper event records: dates, venue, city/state.
-- Structure is safe to re-run; the seed inserts skip names that already exist.

create table if not exists events (
  id uuid primary key default gen_random_uuid(),
  name text unique not null,          -- must match event_name used in games/champions
  event_date date,
  end_date date,                      -- null for one-day events
  venue text default '',
  city text default '',
  state text default '',
  notes text default ''
);
alter table events enable row level security;
drop policy if exists events_public_read on events;
create policy events_public_read on events for select to anon using (true);

create or replace function admin_upsert_event(
  p_admin_key text, p_name text, p_event_date date, p_end_date date,
  p_venue text, p_city text, p_state text, p_notes text
) returns boolean language plpgsql security definer as $$
begin
  if not is_admin(p_admin_key) then raise exception 'bad admin key'; end if;
  insert into events (name, event_date, end_date, venue, city, state, notes)
  values (trim(p_name), p_event_date, p_end_date, p_venue, p_city, p_state, p_notes)
  on conflict (name) do update set
    event_date = excluded.event_date, end_date = excluded.end_date,
    venue = excluded.venue, city = excluded.city,
    state = excluded.state, notes = excluded.notes;
  return true;
end $$;

-- ===== Seed: BHL era =====
insert into events (name, event_date, end_date, venue, city, state) values
('Spartan Bell Slugfest',      '2024-05-19', null,         'Industrial Metal Supply',       'San Jose',      'CA'),
('Nexus Knockout 2',           '2025-03-22', null,         '',                              '',              ''),
('BHL at Open Sauce 2025',     '2025-07-19', '2025-07-20', 'San Mateo County Event Center', 'San Mateo',     'CA'),
('Norcal Robotics Expo 2025',  '2025-11-22', '2025-11-23', '',                              '',              ''),
('Nexus Knockout 3',           '2026-04-04', null,         '',                              '',              '')
on conflict (name) do nothing;

-- ===== Seed: Legacy era (RoboGames, venues from robogames.net) =====
insert into events (name, event_date, end_date, venue, city, state) values
('RoboGames 2007', '2007-06-15', '2007-06-17', 'Fort Mason Center',        'San Francisco', 'CA'),
('RoboGames 2008', '2008-06-13', '2008-06-15', 'Fort Mason Center',        'San Francisco', 'CA'),
('RoboGames 2009', '2009-06-12', '2009-06-14', 'Fort Mason Center',        'San Francisco', 'CA'),
('RoboGames 2010', '2010-04-23', '2010-04-25', 'San Mateo Event Center',   'San Mateo',     'CA'),
('RoboGames 2011', '2011-04-14', '2011-04-17', 'San Mateo Event Center',   'San Mateo',     'CA'),
('RoboGames 2012', '2012-04-20', '2012-04-22', 'San Mateo Event Center',   'San Mateo',     'CA'),
('RoboGames 2013', '2013-04-19', '2013-04-21', 'San Mateo Event Center',   'San Mateo',     'CA'),
('RoboGames 2015', '2015-04-03', '2015-04-05', 'San Mateo Event Center',   'San Mateo',     'CA'),
('RoboGames 2016', '2016-04-08', '2016-04-10', 'Alameda Fairgrounds',      'Pleasanton',    'CA'),
('RoboGames 2017', '2017-04-21', '2017-04-23', 'Alameda Fairgrounds',      'Pleasanton',    'CA'),
('RoboGames 2018', '2018-04-27', '2018-04-29', 'Alameda Fairgrounds',      'Pleasanton',    'CA'),
('RoboGames 2023', '2023-04-06', '2023-04-09', 'Alameda Fairgrounds',      'Pleasanton',    'CA')
on conflict (name) do nothing;
