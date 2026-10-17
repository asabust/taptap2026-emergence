#!/bin/bash
set -euo pipefail
PROJECT_ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
UNITY_CONTENTS="${UNITY_CONTENTS:-/Applications/Unity/Hub/Editor/2022.3.62f3/Unity.app/Contents}"
MONO_BIN="$UNITY_CONTENTS/MonoBleedingEdge/bin"
NETSTANDARD="$UNITY_CONTENTS/MonoBleedingEdge/lib/mono/4.7.1-api/Facades/netstandard.dll"
ENGINE="$UNITY_CONTENTS/Managed/UnityEngine/UnityEngine.CoreModule.dll"
EDITOR="$UNITY_CONTENTS/Managed/UnityEditor.dll"
NEWTONSOFT="$PROJECT_ROOT/Library/PackageCache/com.unity.nuget.newtonsoft-json@3.2.2/Runtime/Newtonsoft.Json.dll"
CHECK_DIR="$(mktemp -d /private/tmp/level-data-checks.XXXXXX)"
trap 'rm -rf "$CHECK_DIR"' EXIT
cd "$PROJECT_ROOT"
"$MONO_BIN/mcs" -langversion:latest -target:library -out:"$CHECK_DIR/Game.Runtime.Data.dll" Assets/Game/Runtime/Data/Levels/*.cs
"$MONO_BIN/mcs" -langversion:latest -target:library -out:"$CHECK_DIR/Core.LevelIO.dll" Assets/Game/Runtime/Core/IO/AtomicTextFile.cs
"$MONO_BIN/mcs" -langversion:latest -define:UNITY_5_3_OR_NEWER -target:library -out:"$CHECK_DIR/Gameplay.Levels.dll" -r:"$CHECK_DIR/Game.Runtime.Data.dll" -r:"$CHECK_DIR/Core.LevelIO.dll" -r:"$NEWTONSOFT" -r:"$NETSTANDARD" -r:"$ENGINE" Assets/Game/Runtime/Gameplay/Levels/*.cs Assets/Game/Runtime/Gameplay/LevelEditor/LevelEditorSession.cs Assets/Game/Runtime/Gameplay/LevelEditor/LevelEditorDocument.cs
"$MONO_BIN/mcs" -langversion:latest -define:UNITY_EDITOR -target:library -out:"$CHECK_DIR/Editor.LevelChecks.dll" -r:"$CHECK_DIR/Game.Runtime.Data.dll" -r:"$CHECK_DIR/Gameplay.Levels.dll" -r:"$NEWTONSOFT" -r:"$NETSTANDARD" -r:"$ENGINE" -r:"$EDITOR" Assets/Game/Editor/LevelDataChecks.cs Assets/Game/Editor/LevelEditorChecks.cs Assets/Game/Editor/LevelConfigurationChecks.cs
cp "$NEWTONSOFT" "$CHECK_DIR/Newtonsoft.Json.dll"
cp "$ENGINE" "$CHECK_DIR/UnityEngine.CoreModule.dll"
cp "$EDITOR" "$CHECK_DIR/UnityEditor.dll"
cat > "$CHECK_DIR/Run.cs" <<'CS'
class Run { static void Main() { System.Console.WriteLine("PASS: " + Game.Editor.LevelDataChecks.Run() + " data checks"); System.Console.WriteLine("PASS: " + Game.Editor.LevelEditorChecks.Run() + " editor checks"); System.Console.WriteLine("PASS: " + Game.Editor.LevelConfigurationChecks.Run() + " configuration checks"); } }
CS
"$MONO_BIN/mcs" -out:"$CHECK_DIR/Run.exe" -r:"$CHECK_DIR/Editor.LevelChecks.dll" "$CHECK_DIR/Run.cs"
"$MONO_BIN/mono" "$CHECK_DIR/Run.exe"
