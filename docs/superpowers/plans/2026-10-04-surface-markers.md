# Surface marker extension
User authorized immediate implementation, bounded extension of the existing map.

Design: marker presentation assets use extensible string type IDs; sector content carries event objectives and numeric overrides. Mutable mission/marker state lives in a separate service using the existing ISurfaceMapStorage adapter. A resolver combines base RunConfig with active content. Generic gameplay event completion reports source prefab/tag; pending objectives are committed only on confirmed production victory. Abort/death/dev discard pending progress. D1 Unknown Activity guarantees the existing False Signal site event. UI renders icons/states, with clipped drag/pointer-centred zoom.

Plan: 1. Targeted red contract test. 2. Content models, resolver, saved state and lifecycle hooks. 3. Guaranteed exploration event and source identity. 4. Marker UI/pulse and pan/zoom. 5. Author existing assets and run focused data + actual event smoke tests. No football/bunker changes, no broad regression.

Completed: all five steps implemented. Unity marker target run passed 4/4 in 18.6 seconds: data/save guards, dynamic mission assignment/rewards, pan/zoom, actual exploration FalseSignal completion + site reward + final boss victory + reload. Independent review completed; debug-created event completion excluded. Existing signal point placement constraints handled by reserving largest normal sites. No football changes or broad suite.
