#!/usr/bin/env python3
"""Compile added editor UI and scene builder against this project's actual Unity DLLs.
This verifies C# references, not Unity import, visual layout, or Play Mode behavior.
"""
import os
from pathlib import Path
import subprocess
import tempfile

project = Path(__file__).resolve().parents[2]
unity = Path(os.environ.get('UNITY_CONTENTS', '/Applications/Unity/Hub/Editor/2022.3.62f3/Unity.app/Contents'))
mono = unity / 'MonoBleedingEdge/bin/mono'
csc = unity / 'MonoBleedingEdge/lib/mono/4.5/csc.exe'
engine = unity / 'Managed/UnityEngine'
references = list(engine.glob('UnityEngine*.dll'))
references += [unity / 'Managed/UnityEditor.dll',
               unity / 'MonoBleedingEdge/lib/mono/4.7.1-api/Facades/netstandard.dll']
references += [project / ('Library/ScriptAssemblies/' + name + '.dll')
               for name in ('UnityEngine.UI', 'Unity.TextMeshPro', 'Unity.InputSystem')]
references.append(project / 'Library/PackageCache/com.unity.nuget.newtonsoft-json@3.2.2/Runtime/Newtonsoft.Json.dll')
with tempfile.TemporaryDirectory(prefix='level-editor-compile-') as directory:
    output = Path(directory)
    def compile_dll(name, sources, deps, editor=True):
        defines = 'UNITY_EDITOR,UNITY_5_3_OR_NEWER' if editor else 'UNITY_5_3_OR_NEWER'
        command = [str(mono), str(csc), '-nologo', '-langversion:latest', '-target:library', '-define:' + defines, '-out:' + str(output / name)]
        command += ['-r:' + str(dep) for dep in deps]
        command += [str(source) for source in sources]
        subprocess.run(command, cwd=project, check=True)
    compile_dll('Game.Runtime.Data.dll', project.glob('Assets/Game/Runtime/Data/**/*.cs'), references)
    core_sources = ['IO/AtomicTextFile.cs', 'Singleton.cs', 'Enums.cs', 'EventHandler.cs', 'TransitionManager.cs', 'Attributes/SceneNameAttribute.cs']
    compile_dll('Core.LevelIO.dll', [project / ('Assets/Game/Runtime/Core/' + name) for name in core_sources], references)
    runtime_refs = references + [output / 'Game.Runtime.Data.dll', output / 'Core.LevelIO.dll']
    sources = list(project.glob('Assets/Game/Runtime/Gameplay/Levels/*.cs')) + list(project.glob('Assets/Game/Runtime/Gameplay/LevelEditor/*.cs')) + list(project.glob('Assets/Game/Runtime/Gameplay/LevelPlay/*.cs'))
    sources += list(project.glob('Assets/Game/Runtime/Gameplay/Campaign/*.cs'))
    sources += [project / ('Assets/Game/Runtime/Gameplay/' + name + '.cs') for name in ('GameManager', 'AudioManager')]
    compile_dll('Gameplay.LevelEditor.dll', sources, runtime_refs)
    compile_dll('Gameplay.Player.dll', sources, [r for r in runtime_refs if r.name != 'UnityEditor.dll'], editor=False)
    sources = [project / ('Assets/Game/Editor/' + name + '.cs')
               for name in ('LevelDataChecks', 'LevelEditorChecks', 'LevelConfigurationChecks', 'LevelPlayChecks', 'LevelHistoryChecks', 'LevelPlaytestChecks', 'LevelEditorSceneSetup', 'LevelPlaySceneSetup', 'CampaignSceneSetup', 'CampaignChecks', 'CampaignIntegrationChecks')]
    compile_dll('Editor.LevelEditor.dll', sources, runtime_refs + [output / 'Gameplay.LevelEditor.dll'])
print('PASS: editor, shared play, campaign flow and scene tools compilation')
