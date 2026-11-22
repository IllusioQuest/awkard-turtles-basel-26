# T-81: Site-type icons on the admin network map

- **Owner:** Ennio / Claude · **Status:** done (committed directly to `main` at the user's request)
- **Depends on:** T-17 (admin map), T-67 (local fleet scenarios).

## Goal

The admin map (`/admin/:runId`) draws every site with a house icon. Sites that are not houses, such as
the indoor pool of the Dreispitz run (`pool-dreispitz`), should show a fitting icon.

## Scope

- Optional `site_type` per site in fleet house files (default: house), set by
  `scripts/make_local_fleet.py` from `extra_sites[].site_type`.
- Fleet `HouseSpec.SiteType`, FleetApi `HouseInfo.siteType`, web map icon by type.
- Existing house files and runs stay valid; no simulation re-run is needed.
