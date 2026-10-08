# Bunker navigation reference

The current production Bunker is `Scenes/MainBuild/MainMenu.unity`. Room/station access, panels, run gate and minigames are owned by their production components; floor navigation is not part of its current authored scene.

`BunkerNavigationView` is retained under `Dev/Debug/BunkerSimple` because the BunkerSimple OLD/SIMPLE source scenes still serialize it. Its authored mesh routes subscribe to room/station/minigame availability; the gate branch is a permanent landmark. It uses no player-following/pathfinding logic.

The old navigation rebuild utility depended on an already-absent `art/BunkerNavigation/FloorLight.mat` and has been removed. The unused floor shader was removed after GUID/source audit. This cleanup does not recreate a production floor-guidance system or alter the Bunker layout.

Historical navigation screenshots/reports describe their recorded version and do not validate the current scene. Current ownership/composition is described in [Architecture_Overview](Architecture_Overview.md) and [PROJECT_MAP](PROJECT_MAP.md).
