# Water Pipe Sample

This sample creates a transparent curved pipe with a separate transparent flowing water mesh inside it. It is built for Unity 2022.3 LTS or newer with Universal Render Pipeline and uses no external assets.

## Open the sample

Open:

`Assets/WaterPipeSample/Scenes/WaterPipeSample.unity`

If the scene has not been generated yet, use:

`Tools/Water Pipe Sample/Create Or Rebuild Sample`

The menu command rebuilds only assets under `Assets/WaterPipeSample/`.

## Rebuild the sample

Run `Tools/Water Pipe Sample/Create Or Rebuild Sample` to regenerate the seamless water textures, materials, generated mesh assets, prefab, and sample scene. The command asks about unsaved scene changes before it opens and saves the dedicated sample scene.

Generated assets are stored under:

`Assets/WaterPipeSample/Generated/`

## Edit the pipe

Select the `WaterPipeSample` GameObject in the sample scene. The `PipeAndWaterMeshGenerator` component exposes:

- `controlPoints`
- `closedPath`
- `pipeOuterRadius`
- `pipeWallThickness`
- `waterRadius`
- `radialSegments`
- `samplesPerSegment`
- `uvTilingPerMeter`
- `generateEndCaps`
- `waterMaterial`
- `pipeMaterial`
- `autoRegenerateInEditor`

Move the `CP_` child transforms to change the path. Use the inspector buttons to generate meshes, clear meshes, or restore the default L-shaped path.

## UV-based flow

The pipe and water meshes use object UVs. The U coordinate runs around the circumference. The V coordinate represents distance along the pipe, so texture scrolling follows bends automatically.

The water shader scrolls flow noise, detail noise, bubble masks, and two normal-map layers along UV.y. Negative flow speeds reverse direction.

## Separate water and pipe meshes

The water and glass are separate child objects:

- `Generated_Water`
- `Generated_Pipe`

Keeping them separate gives independent materials, render queues, opacity, and water radius. The default water radius leaves a small physical gap from the pipe inner wall to reduce z-fighting and sorting issues.

## Change transparency and flow

Edit `Assets/WaterPipeSample/Generated/Materials/M_TransparentWater.mat`.

Important water properties:

- `_Opacity`: default water alpha, approximately `0.38`
- `_FlowSpeed`: broad flow direction and speed
- `_DetailFlowSpeed`: secondary noise speed
- `_NormalFlowSpeedA` and `_NormalFlowSpeedB`: normal detail speeds
- `_BaseColor`: cyan/blue tint
- `_FresnelPower` and `_FresnelIntensity`: silhouette highlight

Edit `Assets/WaterPipeSample/Generated/Materials/M_TransparentPipe.mat` to change glass opacity and edge highlight.

## Transparent sorting

Transparent object sorting in Unity is object-based. This sample minimizes artifacts by rendering water first and the pipe shell second:

- Water render queue: `3000`
- Glass pipe render queue: `3100`
- Water shader: `Cull Back`
- Glass shader: `Cull Off`
- Both transparent shaders: `ZWrite Off`

Complex scenes, intersecting transparent objects, or unusual camera angles may still need manual render queue or object layout adjustments.

## Use in another scene

Drag `Assets/WaterPipeSample/Prefabs/WaterPipeSample.prefab` into a scene, or add `PipeAndWaterMeshGenerator` to a GameObject and assign the generated water and pipe materials. Create or assign control point transforms, then press `Generate Meshes`.

The generator uses a Catmull-Rom path and parallel transport frames to keep bends smooth without random twisting.
