-- Migration 005 — team aliases (renames + sub-teams) and a flag for games
-- where goals weren't actually tracked. Safe to re-run.

-- Aliases: map any name a team has competed under (old names, sub-teams like
-- "EVAC A"/"EVAC B") to the canonical team name used for stats.
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

-- Games where the win/loss is real but goal counts weren't recorded
-- (e.g. brackets scored as 1-0 sets). Excluded from GF/GA, still count as W/L.
alter table games add column if not exists scores_counted boolean not null default true;
