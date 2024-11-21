# Axolotl2D
Axolotl2D is a small lightweight 2D game engine based on [Silk.NET](https://github.com/dotnet/Silk.NET). It was build as a personal practice project, intended for smaller indie games. It makes heavy use of Microsoft's hosting extensions and thus supports and makes heavy use of dependency injection.

## Getting started
To create a new Axolotl2D game, you'll want to reference the Axolotl2D library and create a new class inheriting the abstract `Game` class. This is very much prone to changes and may or may not be outdated at the time of reading! Once Axolotl2D is more ready for "general use", a proper documentation will be written.

```cs
using System.Numerics;
using Microsoft.Extensions.Logging;
using Axolotl2D.Drawable;
using Axolotl2D.Entities;
using Axolotl2D.Input;

public class MyGame : Game
{
    private Sprite? _sprite;
    private Mouse? _mouse;

    private ILogger<ExampleGame> _logger;

    // You are required to pass the IServiceProvider servise as a 
    // dependency to the base Game class.
    // Other things like the Mouse, or the ILogger<MyGame> are free to 
    // inject as needed.
    // You are also able to set the max framerate and max update rate 
    // through the base constructor.
    // These will later be changeable at runtime, but for the time being 
    // these can only be updated
    // as your game gets constructed.
    public ExampleGame(IServiceProvider services, Mouse mouse, 
    ILogger<MyGame> logger) 
        : base(services, maxDrawRate: 240, maxUpdateRate: 240)
    {
        // Set a title for the window
        Title = "Axolotl2D Game";
        // Set a clear color
        ClearColor = Color.FromHTML("#0088FF");

        // Axolotl2D uses events for it's game loop. You'll want to hook 
        // these manually as you see fit.
        OnLoad += Load;
        OnUpdate += Update;
        OnDraw += Draw;
        OnResize += Resize;

        // Assigning properties from dependencies as needed
        this._logger = logger;
        this._mouse = mouse;
    }

    // This is the place where you would pre-load all your assets.
    public void Load()
    {
        // We can load sprites from embedded resources. 
        // You'll have to mark your texture as an Embedded Resource
        _sprite = Sprite.FromManifestResource(this, 
        "MyAwesomeGame.Sprites.MySprite.png");
        // Of course, it is also possible to load a Sprite from any other 
        // type of Stream.
        // This is done with Sprites constructor:
        // _sprite = new Sprite(this, imageStream);
        _logger.LogInformation("Loaded Game Assets");
    }

    // This is our draw method. You'll want to draw your sprites here.
    public void Draw(double frameDelta, double frameRate)
    {
        // This will draw your sprite at coordinates 50, 50 
        // with a size of 50x50
        _sprite.Draw(50, 50, 50, 50);
    }

    // This is our update method. We can use this method to 
    // update values without interrupting our draw loop. 
    // For example, you'll want to manipulate your Sprite's 
    // position and size here.
    public void Update(double frameDelta)
    {

    }

    // This is the Resize method. 
    // It is called when the game window resizes.
    public void Resize(Vector2D<int> size)
    {
    }

    // When your game is closed, 
    // you'll want to clean up everything nicely. 
    // This can be done right here.
    public override void Cleanup()
    {
        _logger.LogInformation("Cleaned up events and unloading game...");
        // unhook events
        OnLoad -= Load;
        OnUpdate -= Update;
        OnDraw -= Draw;
        OnResize -= Resize;
    }
}
```

Once you're done creating your Game class, you'll want to create a Host that houses your game and injects dependencies where you need them.
You'll want to do this in your Program.cs.

```cs
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Axolotl2D;

internal class Program
{
    static void Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                services.AddGame<ExampleGame>();
                services.UseMouse();
                services.AddLogging();
            })
            .Build();

        host.Start();
    }
}
```

