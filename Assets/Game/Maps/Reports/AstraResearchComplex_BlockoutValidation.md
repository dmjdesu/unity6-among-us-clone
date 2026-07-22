# Astra Research Complex Blockout Validation

Scene: `Assets/Game/Maps/AstraResearchComplex_Blockout.unity`
MapDefinition: `Assets/Game/Maps/Definitions/astra_research_complex_map_definition.json`
Runtime Map: `Assets/Resources/Maps/astra_research_complex_blockout_01.json`
Map Size: 150 x 116 Unity units
Rooms: 11
Connections: 16
TaskPoints: 22
SabotagePoints: 4
VentPoints: 9

## Summary

Overall: PASS

| Check | Result | Detail |
|---|---|---|
| Scene load | PASS | Assets/Game/Maps/AstraResearchComplex_Blockout.unity |
| 11 rooms exist | PASS | definition=11, scene=11 |
| 16 connections exist | PASS | connections=16 |
| All rooms reachable | PASS | 11/11 rooms reachable |
| West-east routes >= 3 | PASS | routes=5 |
| North-south routes >= 3 | PASS | routes=4 |
| Dead-end rooms <= 1 | PASS | deadEnds=0 |
| No graph bridge dependency | PASS | bridges= |
| Cell walkability connected | PASS | BFS over generated floor cells |
| SpawnPoint count >= 15 | PASS | definition=15, scene=15 |
| MeetingPoint count == 1 | PASS | definition=1, scene=1 |
| TaskPoint count >= 22 | PASS | definition=22, scene=22 |
| SabotagePoint count == 4 | PASS | definition=4, scene=4 |
| VentPoint count == 9 | PASS | definition=9, scene=9 |
| SpawnPoint placement | PASS | open and not inside non-trigger colliders |
| TaskPoint placement | PASS | open and not inside non-trigger colliders |
| MapBoundary sealed | PASS | four continuous boundary colliders around play bounds |
| Tilemap composite colliders | PASS | tilemapCompositeColliders=2 |
| NetworkRunner not duplicated | PASS | scene NetworkRunner components=0; BasicSpawner creates one at runtime |
| BasicSpawner exists | PASS | BasicSpawner components=1 |
| BasicSpawner PlayerPrefab assigned | PASS | Assets/Prefabs/PlayerPrefab.prefab |
| PlayerPrefab spawnable | PASS | NetworkObject + Player behaviour |
| No NetworkObject on static map | PASS | staticNetworkObjects=0 |
| Missing Script | PASS | missingScripts=0 |
| Missing Sprite | PASS | missingSprites=0 |
| Runtime map saved | PASS | Assets/Resources/Maps/astra_research_complex_blockout_01.json |
| Orthographic size remains 6 | PASS | orthographicSize=6.0 |
| Global Light 2D present | PASS | prevents Sprite-Lit tilemaps from rendering black |
| Visible blockout floor backing | PASS | unlit floor meshes under Tilemaps |

## Route Metrics

- West-east independent routes: 5
- North-south independent routes: 4
- Dead-end rooms: 0
- Bridge dependencies: none

## Manual Checks

- Open `Assets/Game/Maps/AstraResearchComplex_Blockout.unity`.
- Enter Play Mode, Host, then Start Game; players should spawn from the 15 Central Lounge spawn points.
- Move with WASD or arrow keys and confirm walls, obstacles, cut corners, and the map boundary block traversal.
- Verify camera follow at Orthographic Size 6; the full 150 x 116 map should not fit on screen.
- Use E for tasks/meeting/repairs, F for sabotage, V for vents, Q for kill, and R for report.
