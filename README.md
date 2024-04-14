# Entities Selection

This is a fork of @JonasDeM's EntitySelection package.

Modified for runtime use and compatibility with Entities 1.2.0

## Usage

```csharp
// Simply,
var entity = EntitySelectionSystem.GetEntityAtPoint(new Vector2(Input.mousePosition.x, Input.mousePosition.y), Camera.main);
```

```csharp
// or with more options.
var entity = EntitySelectionSystem.GetEntityAtPoint(
    World.DefaultGameObjectInjectionWorld,
    new Vector2(Input.mousePosition.x, Input.mousePosition.y),
    Camera.main.pixelWidth, Camera.main.pixelHeight,
    Camera.main.worldToCameraMatrix, Camera.main.projectionMatrix
);
```

## Installation

1. Click the green "Clone or download" button and copy the url.
2. In Unity go to Window>Package Manager and Press the + sign in the left-top corner of the Package Manager window.
3. Select "Add package from git URL...", Paste the URL and press "Add".
Done!

Or manually add the dependency to the Packages/manifest.json file.

```
{
    "dependencies": {
        "io.github.jimchan.entitiesselection": "https://github.com/jimchan3301/EntitySelection.git"
    }
}
```