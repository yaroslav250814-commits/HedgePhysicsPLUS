# Unity 6.3 port notes

The original project targeted Unity 5.6. This fork targets Unity **6000.3.26f1**, URP **17.3.0**, Input System **1.19.0** and Unity UI **2.0.0**. Use the pinned editor version for the first import.

The active project contains converted FBX models and native Unity meshes, avatars and animation assets. Blender is not needed to open or play it. The port repaired incompatible mesh/bone bindings, material assignments and missing environment colliders. Rendering uses URP rather than the original Built-in effects.

The project uses the new Input System and `Rigidbody.linearVelocity`. Gameplay tuning inherited from HedgePhysics generally retains its original per-physics-step units; the customization guide identifies these units. Drop Dash charge is measured per second, and combo windows are measured in scaled game-time seconds.

The normal maximum fall speed is 120 in the demonstration player. The temporary slower-fall experiment was reverted. Extra air attacks and rebounds following Drop Dash are disabled in `PlayerAbilityFeatures.cs`.

Historical model-conversion, migration and diagnostic scripts remain in `Assets/Editor`. Their repair commands may expect local migration/reference inputs and should not be run as a project setup step: the generated runtime assets are already included. Normal project use requires no migration command.

The public source package excludes local editor caches, builds, machine-specific settings, migration backups, logs and research captures. `Migration/.gitkeep` keeps a writable report root for editor checks.

The full original Unity 5.6 runtime comparison was not completed because that old editor could not be activated in the migration environment. This fork's Unity 6.3 gameplay and editor checks have been run; identical behavior to Unity 5.6 is not claimed.
