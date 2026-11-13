# Server (Unity Gaming Services)

- `cloudcode/` — the Cloud Code C# module `UnpaidCeo` (it compiles the game's Core and replays each career's log).
  Tests: `cd server/cloudcode && ~/.dotnet/dotnet test`.
- `ugs/` — leaderboards (`*.lb`), Remote Config (`flags.rc`) and access control (`no-client-scores.ac`).

## Deploy from the command line through the Unity Editor (no keys, no `ugs login`)
`tools/server/deploy-from-editor.sh` — prepares the files, opens Unity (window mode), selects the **production**
environment, opens the Deployment window and deploys everything in `game/Assets/UnpaidCeo/Server/Deploy`
(`Editor/ServerDeploy.cs`). Needs the owner signed in to **Unity Hub** (Unity account); Unity must be closed for the
project. Without a sign-in it stops with "environment not found / access token unavailable".

## Deploy from the Unity Editor by hand (no keys, no `ugs login`)
1. `tools/server/sync_editor_deploy.sh` — checks the config, runs the server tests, copies the boards and Remote Config
   (weekly boards stamped to reset next Monday) into `game/Assets/UnpaidCeo/Server/Deploy/`, next to `UnpaidCeo.ccmr`
   (the Cloud Code Module Reference to `cloudcode/UnpaidCeo.CloudCode.sln`).
2. In Unity (package `com.unity.services.deployment` is in the project), signed in with the owner's Unity account,
   Edit → Project Settings → Services → Environments → **production**, then **Services → Deployment** (or Window → select all
   items under `Deploy` (module UnpaidCeo, the 8 leaderboards, flags), environment **production**, press **Deploy**.
   If Unity asks for .NET: Preferences → Cloud Code → .NET path `~/.dotnet/dotnet`.
3. `no-client-scores.ac` (players cannot write scores themselves) is not handled by the Editor's Deployment window in
   this project's packages; it keeps its CLI path (`tools/server/deploy.sh`) and only needs deploying again if it changes.

`tools/server/deploy.sh` (CLI, needs `ugs login`) still works and deploys everything at once.
