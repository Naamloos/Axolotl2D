# Render Textures

`RenderTexture` is a fixed-size GPU texture that can receive a camera's world output. Use it for minimaps, security cameras, portals, and low-resolution composition. It can be sampled anywhere a normal `Texture2D` is accepted.

## Render a camera off screen

Create render textures after the game window loads. A scene's `Load` method runs at the correct time:

```csharp
public sealed class MinimapScene(
    Rendering rendering,
    CameraManager cameras,
    SpriteBatch sprites) : BaseScene
{
    private RenderTexture minimap = null!;
    private Camera2D minimapCamera = null!;

    public override void Load()
    {
        minimap = rendering.CreateRenderTexture(256, 256);
        minimapCamera = cameras.Create("Minimap");
        minimapCamera.RenderTarget = minimap;
        minimapCamera.Zoom = 0.25f;
        minimapCamera.Priority = -1;
    }

    public override void Draw(double frameDelta, double frameRate) =>
        sprites.Draw(minimap.Texture, new Vector2(920, 140), new Vector2(240, 240),
            space: CoordinateSpace.Screen);

    public override void Unload()
    {
        minimapCamera.RenderTarget = null;
        cameras.Remove(minimapCamera);
        minimap.Dispose();
    }
}
```

A camera with `RenderTarget` set writes to that texture instead of the window. Other cameras continue rendering normally. Camera priorities determine production order, so assign an earlier priority when its texture is sampled later in the same frame.

Use `CullingMask` and sprite lighting layers to choose which world objects the target camera sees.

## Fixed-resolution output

Nearest filtering keeps low-resolution pixels sharp when the texture is enlarged:

```csharp
var pixelView = rendering.CreateRenderTexture(
    320, 180, RenderTextureFilter.Nearest);
```

Change `Filter` at runtime or call `Resize(width, height)`. Resizing preserves the public `Texture` reference, so existing sprites remain valid.

The camera retains its normal window-based logical viewport while the target controls raster resolution. Adjust camera zoom to change the visible world area.

## Post-processing and lifetime

Camera post-processing works with render textures. Intermediate targets use the render texture dimensions, so `uResolution` and `uTexelSize` describe the final off-screen image.

`RenderTexture` owns its framebuffer and GPU texture. Dispose it when its scene or owning system unloads. Drawing its `Texture` after disposal throws. The renderer also releases remaining render textures during game shutdown.

See the `TARGETS` screen in `Axolotl2D.Example` for a moving camera rendered at 320x180 and enlarged with nearest filtering.
