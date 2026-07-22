# unity6-among-us-clone

Unity 6 + Photon Fusion based prototype for an Among Us style 2D multiplayer game.

## Run

1. Open this folder in Unity `6000.3.6f1`.
2. Open `Assets/Scenes/FusionTest.unity`.
3. Press Play.
4. Use the Canvas lobby to `Host`, `Join`, or `Auto Host Or Join` with the same room name.
5. Host creates five CPU test players automatically.
6. Host presses `Start Game` in the top-left session panel.
7. Move with WASD or the arrow keys.
8. If you are the Impostor, press `Q` near a Crewmate to kill.
9. Press `R` near a body to call a meeting.
10. Hold `E` near a task station or sabotage panel to complete it.
11. Press `E` near the emergency button to call a meeting.
12. If you are the Impostor, press `F` near a sabotage panel.
13. If you are the Impostor, press `V` near a vent to move to the next vent.
14. Vote from the meeting buttons. CPU players vote automatically.
15. The macOS build is generated at `Builds/macOS/AmongUsStyle.app`.

`FusionTest.unity` is included in build settings so Fusion can load the active scene for remote clients.

## Migration Notes

The multiplayer baseline uses the Photon Fusion package already present in this project instead of directly copying the Unity Netcode + Meta/Photon Realtime layer from `Unity-Decommissioned`.

Ported concepts:

- connection state and room name handling
- runtime Canvas lobby, session HUD, action bar, roster, and meeting UI
- host/client/auto host-or-join entry points
- server-authoritative player spawning
- host-controlled round start
- simple role assignment
- placeholder impostor kill flow
- report, meeting, voting, and ejection flow
- simple crewmate task stations and task-completion win condition
- data-driven ship map loaded from `Assets/Resources/Maps/production_room_preview_01.json`
- production room prefabs for the first three rooms, loaded from `Assets/Resources/RoomPrefabs/Production/`
- room-based walkable areas, doorways, obstacles, current-room detection, and CPU navigation targets
- gameplay points loaded from map data: tasks, sabotages, vents, spawns, and emergency meeting point
- room labels and player roster/status readout for faster testing
- generated crewmate-style runtime sprites with body, visor, and backpack layers
- hold-to-complete task and repair interactions
- progress bars for tasks, repairs, and meetings
- task/repair overlay panel with a simple animated progress mini-game
- sabotage panels for Lights, Reactor, and Communications
- emergency meeting button and impostor vent travel
- basic kill flash animation
- Lights sabotage temporarily reduces Crewmate camera vision until repaired
- five CPU test participants with tasking, repair, impostor chase/kill behavior, and automatic votes
- player object registry and cleanup on leave
- networked player state for 2D movement

This keeps the prototype free of the VR, Meta account, avatar, and voice-chat dependencies while leaving room to add tasks, sabotages, maps, better AI, and real game UI on top of the same server-authoritative flow.

## Production Room Preview

The active map is `Assets/Resources/Maps/production_room_preview_01.json`.

It currently defines:

- 3 completed room prefabs: Central Meeting, Reactor, and Medical
- 120 x 85 Unity unit play bounds
- 34 x 28 central meeting room
- 24 x 24 reactor room with an L-shaped silhouette
- 22 x 20 medical room with a trapezoid silhouette
- 2 doorway regions between the first three rooms
- 11 task points
- 4 sabotage points
- 3 vent nodes in 1 preview vent group
- 10 spawn points
- 1 emergency meeting point

Each room prefab has this hierarchy:

- Background
- FloorDetails
- Walls
- PropsBack
- InteractiveObjects
- PropsFront
- Collision
- VisionOccluders
- TaskPoints
- DoorConnections

`ShipMap.cs` loads the JSON at runtime, instantiates the three room prefabs, and exposes gameplay points to `BasicSpawner.cs`. The old 11-room blockout remains in `Assets/Resources/Maps/orbital_lab_01.json` as reference data, but it is no longer the active map. The full ship layout should wait until the first three room prefabs are approved.

## Current Prototype Rules

- The host can start or restart a round.
- Host mode adds five CPU players in the lobby for local testing.
- The host can toggle whether the host is forced to be the Impostor for faster kill testing.
- One player is assigned Impostor by default.
- Other players become Crewmates.
- Late joiners become Spectators until the next restart.
- Dead players are shown as flattened dark bodies.
- Player bodies use generated placeholder sprites, so no external character art is required for testing.
- Movement is limited to rooms, corridors, and doorways instead of the full rectangular map.
- The HUD shows the local player's current room or hallway.
- Crewmates get task stations from the map data and complete nearby tasks by holding `E`.
- Crewmates win if all assigned tasks are completed.
- Impostors can trigger sabotage near red panels with `F`.
- Lights and Communications stay active until a Crewmate repairs them by holding `E`.
- Lights sabotage reduces living Crewmate vision while active.
- Reactor sabotage starts a countdown; Impostors win if it is not repaired in time.
- Communications sabotage blocks task completion until repaired.
- Impostors can vent with `V` between grey vent markers.
- Living players can call an emergency meeting at the yellow marker with `E`.
- Living players can report bodies with `R`.
- Meetings freeze movement while living players vote.
- A single top vote ejects that player; ties or skip votes eject no one.
- Meeting results are announced briefly after voting, and the roster reveals roles when appropriate.
- Voting is handled by the Canvas meeting panel; the old IMGUI panel is disabled by default.
- CPU Crewmates walk to task stations, repair active sabotage, and complete tasks by using the same hold interaction path as players.
- CPU Impostors can chase nearby Crewmates, kill when ready, vent occasionally, and start sabotage from panels.
- CPU players vote automatically in meetings.
- Crewmates win if no Impostors remain.
- Impostors win after kills reduce living Crewmates to the same count as living Impostors.
