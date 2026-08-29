# Manifest and preview reference

## Root fields

| Field | Required | Rule |
| --- | --- | --- |
| `$schema` | Yes | Exact version 1 or version 2 schema URL. |
| `schemaVersion` | Yes | Integer `1`, or integer `2` when PVE uses `pveAiProfile`. |
| `packageId` | Yes | Stable lowercase identity. |
| `displayName` | Yes | User-facing package name. |
| `version` | Yes | Change when declared content changes. |
| `maps` | Yes | One or more map definitions. |
| `files` | Yes | Closed identity list for all package payload files. |

## Map fields

| Field | Consumer |
| --- | --- |
| `mapId` | Bundle registry, selection, active operation. |
| `displayName` | Mission row and native presentation. |
| `sceneBundle` | Last bundle in ordered load. |
| `dependencyBundles` | Ordered shared asset loads. |
| `scenePath` | Exact streamed-scene address. |
| `previewImage` | All three native map-image views. |
| `externalTonemapLut` | Optional verified external HDRP LUT. |
| `runtimeTerrain` | Optional lossless runtime TerrainData declaration. |
| `operations` | PVE/PVP choices and mission data. |

## Operation fields

| Field | Rule |
| --- | --- |
| `operationId` | Stable and unique in the catalog. |
| `mode` | `pve` or `pvp`. |
| `spawnSet` | Exact scene marker-set name. |
| `timeCodes` | Allowed display and lighting choices. |
| `defaultTimeCode` | Member of `timeCodes`. |
| `minEnemies`/`maxEnemies` | PVE only; closed range from 1 through 100. The briefing selector cannot exceed `maxEnemies`. |
| `pveAiProfile` | Optional in schema v2 and PVE only; fixed map-owned native AI values. |
| `infiltrations` | UI marker records, not world transforms. |

## File identity

Each `files[]` item has a package-relative path, byte count, and SHA-256. Core
rejects an absent file, a size mismatch, a hash mismatch, an absolute path, a
path escape, and a reparse-point escape.

The preview file is subject to the same identity checks. An end user does not
select a private local image through the current UI. The package author owns
the image and distributes it as a versioned package file.
