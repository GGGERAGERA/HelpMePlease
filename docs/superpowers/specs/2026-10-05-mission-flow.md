# Mission flow vertical slice
NPC -> Accept -> sector ! -> existing guaranteed event -> provisional completion -> confirmed production victory -> bunker turn-in -> one-time Gold.

Mission catalog/definitions are immutable assets. MissionService is owned by the existing MetaProgressionManager; no new singleton. Objective definitions create independent IMissionObjectiveHandler runtimes; handlers observe typed signals and capture/restore opaque progress. Only CompleteEvent is implemented in production. Partial progress can be committed on victory without teaching MissionService any new objective type. Mission state uses META_MISSIONS_V1_<mapId>, independently of map/content JSON. Committed objectives and claimed reward are saved; provisional run-bound progress is discarded on death/abort/dev/unconfirmed victory. Victory persists objectives, and actual bunker arrival promotes to ReadyToTurnIn.

SurfaceContentService accepts priority-resolved content sources; mission source overlays ! without deleting underlying ?. Existing RunContentResolver merges all sources and deduplicates guaranteed events. Gameplay reports the existing generic event callback and consumes RunConfig only.

CurrencyManager commits Gold, a durable reward receipt and mission JSON together using one PlayerPrefs.Save before notifications. NPC/panel only call service actions. MissionProvider has missionIds, proximity interaction and a reusable panel via BunkerPanelManager.

MVP: MISSION_SIGNAL_TRACE, D1, False Signal, 100 Gold. Operator uses existing terminal art; no football/room changes. Verify one production flow including event, real boss victory/return, claim/reload/duplicate claim; targeted service cases death, abort, unconfirmed/dev, wrong sector/event/run ID.
