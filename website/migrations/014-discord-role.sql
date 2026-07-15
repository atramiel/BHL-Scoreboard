-- Migration 014 — per-team Discord role ID for the "On Deck" ping, so the
-- scoreboard app can @-tag the two upcoming teams. Admin-only in every sense:
-- not part of update_team()'s params (a team's own edit key can't touch it),
-- and deliberately kept OUT of teams_public — it's never anon-readable, only
-- fetchable via admin_get_discord_roles() with the admin key. Safe to re-run.

alter table teams add column if not exists discord_role_id text default '';

create or replace function admin_set_discord_role(p_admin_key text, p_slug text, p_role_id text)
returns boolean language plpgsql security definer as $$
begin
  if not is_admin(p_admin_key) then raise exception 'bad admin key'; end if;
  update teams set discord_role_id = coalesce(p_role_id, ''), updated_at = now() where slug = p_slug;
  return found;
end $$;

create or replace function admin_get_discord_roles(p_admin_key text)
returns table (name text, discord_role_id text) language plpgsql security definer as $$
begin
  if not is_admin(p_admin_key) then raise exception 'bad admin key'; end if;
  return query select t.name, t.discord_role_id from teams t where coalesce(t.discord_role_id, '') <> '';
end $$;
