# Forgotten Plains Large Map Validation

Scene: `Assets/Game/Maps/ForgottenPlainsLargePrototype.unity`
Runtime Map: `Assets/Resources/Maps/forgotten_plains_large_prototype.json`
Layout: `Layout C: Multiple Hubs`

## Summary

Overall: PASS

| Check | Result | Detail |
|---|---|---|
| Scene load | PASS | Assets/Game/Maps/ForgottenPlainsLargePrototype.unity |
| All areas reachable | PASS | 9/9 areas reachable |
| Loop routes >= 3 | PASS | cycles=7 |
| Dead-end areas <= 2 | PASS | deadEnds=0 |
| Single blockage does not split map | PASS | bridgeEdges= |
| SpawnPoint count/open placement | PASS | count=15, blocked=0, corridorOnly=0 |
| TaskPoint count/open placement | PASS | count=18, blocked=0, corridorOnly=0 |
| MeetingPoint count | PASS | Emergency Bell |
| RoomArea count | PASS | rooms=9 |
| Kill test areas | PASS | killAreas=3 |
| Report test areas | PASS | reportAreas=3 |
| Every spawn reaches meeting | PASS | layout graph connected and spawn points are open |
| Every task reachable | PASS | layout graph connected and task points are open |
| Water and cliffs blocked | PASS | water/river/cliff rects are in obstacle collision data |
| Map boundary present | PASS | Collision/MapBoundary |
| Terrain collision present | PASS | Collision/TerrainCollision |
| Obstacle collision present | PASS | Collision/ObstacleCollision |
| NetworkRunner not duplicated | PASS | scene NetworkRunner components=0; runner is runtime-created by BasicSpawner |
| BasicSpawner exists | PASS | BasicSpawner components=1 |
| BasicSpawner PlayerPrefab assigned | PASS | Assets/Prefabs/PlayerPrefab.prefab |
| No NetworkObject on static Tilemaps | PASS | networkedTilemaps=0 |
| Missing Script | PASS | missingScripts=0 |
| Missing Sprite | PASS | missingSprites=0 |
| Missing Material | PASS | missingMaterials=0 |
| Forgotten Plains originals unchanged | PASS | dirtyOriginalAssets=0 |

## Manual Play Checks

- Hostで開始し、Start Gameを押すと15個のSpawnPoint候補へランダム転送されます。
- WASDまたは矢印キーで移動し、水、崖、壁、大型木、柱、建物風壁、外周に入れないことを確認します。
- QでKill、RでReport、EでTask/緊急会議、FでSabotage、VでVentを確認します。
- 2クライアントでは同じSceneがBuild Settingsに含まれていること、PlayerPrefabが同期Spawnすることを確認します。
