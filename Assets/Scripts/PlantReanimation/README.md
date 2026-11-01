# Plant Reanimation

Runtime animation playback for compiled PVZ reanim JSON and atlas JSON.

## Peashooter quick setup

1. Wait for Unity to finish compiling. Create a profile with **Assets > Create > Plant Reanimation > Profile**.
2. Assign these existing project assets to the profile:
   - Atlas JSON: `Assets/Pvz1/Atlas_Cut_JSON/LoadingImages_PlantsZombies_1_960.json`
   - Reanim JSON: `Assets/Pvz1/Studio_Json/peashooter.reanim.compiled.reanim.json`
   - Atlas Texture: `Assets/Pvz1/ATLASES/LOADINGIMAGES_PLANTSZOMBIES_1_960_00.PNG`
3. Set Pixels Per Unit to `100`, Position Scale to `2`, and Global Scale to `1` as a starting point. Leave Material empty; the controller creates a `Sprites/Default` material at runtime.
4. Create an empty GameObject in a test scene. Set its position where the plant should stand and add `PeashooterController` from `PlantReanimation/Controllers/Peashooter`.
5. Assign the profile. Keep **Play On Enable** enabled and **Initial Clip** as `full_idle`.
6. Enter Play mode. Use the component context menu **Shoot Peashooter** to test the shooting clip. Subscribe to the controller's `OnFire` event from gameplay code to create a pea at the configured `Fire Frame` (default 12).

The Peashooter controller finds `anim_full_idle` and `anim_shooting` markers in the reanim timeline and builds inclusive clips automatically. It updates all sprite tracks during both clips, so timeline visibility flags continue to hide/show the correct body and head parts. The old `PvZReanimPeashooter` component should be removed/disabled from the test object to avoid drawing the same plant twice.

## Generic plant setup

For plants that use ordinary timeline clips, attach `PlantReanimController` and create `PlantReanimClipConfig` entries in the profile. `startFrame` and `endFrame` are inclusive and refer to absolute indexes in the reanim JSON. `trackNames` lists the tracks evaluated by that clip; leave it populated with the tracks that belong to that animation. Add `PlantReanimLayerConfig` entries only when a track needs custom sorting, visibility, or scale. Plant-specific behavior belongs in a small component derived from `PlantReanimController`.

`PlantReanimPlayer` owns time and frame sampling, `PlantReanimAnimator` resolves sparse transforms and applies poses, and `PlantReanimRenderer` owns atlas meshes. Parsed JSON is shared by `PlantReanimCache` for multiple instances.

## Troubleshooting

- **No image:** confirm the three profile assets use the same atlas set, and the texture has alpha enabled.
- **Clip not found:** the Peashooter file must contain the `anim_full_idle` and `anim_shooting` marker tracks.
- **Plant too large/small or offset:** adjust Position Scale, Pixels Per Unit, then the GameObject transform.
- **Layer hidden behind another sprite:** assign a Sorting Layer and raise Sorting Order on the controller.
