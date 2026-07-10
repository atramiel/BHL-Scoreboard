-- Migration 002 — team media (logo + bot photos) and storage bucket
-- Run this in the Supabase SQL editor if you already ran schema.sql before it
-- included these changes. Safe to re-run.

-- Bot photo gallery on teams
alter table teams add column if not exists bot_photos jsonb not null default '[]';

-- Postgres can't insert a column mid-view with CREATE OR REPLACE — drop first.
drop view if exists teams_public;
create view teams_public as
  select id, slug, name, drivers, bots, special_features,
         home_town, motto, logo_url, bot_photos, created_at, updated_at
  from teams;
grant select on teams_public to anon;

-- update_team now also saves the photo list
create or replace function update_team(
  p_slug text, p_key text,
  p_name text, p_drivers text, p_bots text,
  p_special_features text, p_home_town text,
  p_motto text, p_logo_url text, p_bot_photos jsonb
) returns boolean language plpgsql security definer as $$
begin
  update teams set
    name = coalesce(nullif(trim(p_name), ''), name),
    drivers = p_drivers, bots = p_bots,
    special_features = p_special_features,
    home_town = p_home_town, motto = p_motto,
    logo_url = p_logo_url,
    bot_photos = coalesce(p_bot_photos, '[]'::jsonb),
    updated_at = now()
  where slug = p_slug and edit_key = p_key;
  return found;
end $$;

-- Drop the old signature so PostgREST doesn't see two update_team functions
drop function if exists update_team(text, text, text, text, text, text, text, text, text);

-- Public storage bucket for team logos and bot photos (5 MB per file)
insert into storage.buckets (id, name, public, file_size_limit)
values ('team-media', 'team-media', true, 5242880)
on conflict (id) do nothing;

-- Anyone can view; uploads allowed with the public API key.
-- (League-scale tradeoff: uploads aren't tied to a team key. The bucket is
-- capped per-file and images are only shown where a team's profile links them.)
drop policy if exists "team media public read" on storage.objects;
create policy "team media public read" on storage.objects
  for select to anon using (bucket_id = 'team-media');
drop policy if exists "team media anon upload" on storage.objects;
create policy "team media anon upload" on storage.objects
  for insert to anon with check (bucket_id = 'team-media');
