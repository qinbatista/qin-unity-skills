# Woodland image optimization examples

![Two platforms and two oaks rendered in Unity, with their original art pieces underneath](Captures/World-and-pieces.png)

Both ledges use the same 384 × 240 ground sprite. At 1,000 authored pixels per meter, its horizontal pattern repeats every 0.384 m. The ledges are 1.312 m and 1.632 m wide, so both end partway through a tile. The trees share one base, one 144 × 144 trunk period and one crown. Their trunk renderers are 0.160 m and 0.400 m tall. Only the body repeats. The crown moves up with the upper join.

Import [Woodland.unitypackage](Captures/Woodland.unitypackage) into a Unity project to try it directly. It includes the prefab, its five world PNGs and their importer settings. The replay also saves an editable `Assets/ImageExamples/Woodland.prefab` in its Unity receiver. Inspect the ledges and trunks, then change the **Size** field on a Tiled, Continuous SpriteRenderer. Keep the Transform at unit scale. When changing tree height manually, move the crown to the new join. The native test reopens the prefab, changes the ground width and trunk height by one period, and checks the resulting bounds. [World art](Art/World) · [World renderer and checks](Editor/WorldArtExample.cs)

## One backing for a panel and two buttons

![Trail journal rendered with a tiled panel and two sliced buttons in Unity UI Toolkit](Captures/UI.png)

A 735 × 154 panel and two buttons measuring 160 × 60 and 267 × 60 share one 32 × 32 backing with eight-pixel borders. UI Toolkit tiles the patterned panel center and stretches the button centers to fit their labels. The 32 × 32 watchtower icon is a separate sprite. The replay presses both buttons near their extended edges and checks their callbacks. The Canvas capture below shows the same backing rendered by native uGUI Images. [Frame and icon](Art/UI) · [UI implementation](Editor/ControlArtExample.cs) · [Packaged stylesheet](../../unity-ui-skill/assets/ResizableBacking.uss)

![Native Unity Canvas rendering of the backing in sliced and tiled controls](Captures/UI-Canvas.png)

## Import size at the game camera

The import study renders an isolated copy of the crown twice with the same camera and world size. The baseline uses 100 PPU and a 0.1 renderer scale. The candidate uses 1,000 authored PPU and unit scale. The importer chooses the target cap from the crown’s largest projected footprint of 192 × 144 pixels. On Unity 6000.6.7f1 with the macOS Standalone target, the imported texture went from 768 × 576 to 256 × 192. Its measured Editor texture-object allocation fell from 3,539,952 to 394,224 bytes (88.9%). Both renders covered 0.768 × 0.576 meters and their visible silhouettes overlapped by 99.965%. [Native measurements](Captures/ImportAtlas.json) · [Import and atlas implementation](Editor/ImportAtlasExample.cs)

| Original import | Display-sized import |
| --- | --- |
| ![Crown before optimization](Captures/Import-Before.png) | ![Crown after optimization](Captures/Import-After.png) |

## The packed tree page

![Actual packed Unity Sprite Atlas page with the crown, base and trunk](Captures/Atlas.png)

Unity packed the crown, trunk and base from three source textures onto one 1024 × 1024 DXT5 Sprite Atlas page. The ground stays outside the atlas so that using the tree does not pull in ground artwork. The replay reads the packed texture from Unity, checks each sprite's binding and non-overlapping rectangle, then exports that page. The measurements include page dimensions, format and sprite rectangles. A device residency or draw-call claim needs a run on the receiving game's target.

## Reproduce the captures

Use Python 3, the Unity CLI, a licensed Unity 6000.6.7f1 Editor with a graphics device, and the repository checkout. From the repository root, choose a new child directory of an allocated `Cache/temp-*` run. Use `py -3` on Windows.

```text
python3 -B examples/image-optimization/replay.py --work "<task-run>/image-examples"
```

The runner copies the original artwork, published importer, stylesheet and three Editor test fixtures into a fresh receiver. It runs the actual Unity Test Framework, verifies the capture inventory and writes images, measurements and source hashes under `<task-run>/image-examples/captures/`. To reproduce the annotated source strip, use Pillow 10.1 or later and run the following command.

```text
python3 -B examples/image-optimization/annotate.py "<task-run>/image-examples/captures"
```

The original PNGs are included under [Art](Art). The source strip places those PNGs beneath the native world frame with independent previews for readability. All gameplay and UI frames come from Unity. [Replay receipt](Captures/Replay.json)
