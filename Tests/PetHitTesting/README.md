# Pet input regression checks

Run the deterministic alpha, gesture-state, input-thread and stalled-renderer checks:

```powershell
dotnet run --project Tests/PetHitTesting/PetHitTesting.csproj -c Release
```

For native integration, make a separate copy of the bundled renderer and its assets/configuration in a directory whose name contains `pet-hit-probe`. Launch that copy as a normal user and note its process ID. Never target your everyday pet instance or share its configuration with the probe.

```powershell
# Replace 12345 with the isolated BongoCatMver process ID.
dotnet run --project Tests/PetHitTesting/PetHitTesting.csproj -c Release -- --native 12345
dotnet run --project Tests/PetHitTesting/PetHitTesting.csproj -c Release -- --native-gestures 12345
```

`--native` checks actual renderer alpha, Windows routing, production worker preparation and capture allocations. These checks alone do **not** establish that a drag or resize works.

`--native-gestures` sends real Windows mouse input. Leave the mouse alone for about 25 seconds. It creates a separate backdrop application, starts gestures while that application owns foreground focus, and measures actual native window positions and dimensions. Coverage includes enlarging, shrinking, left dragging, leaving the silhouette, transparent-area clicks, lock/unlock, locking during a resize, immediate down/up batches and a transparent click immediately after releasing the pet. It also verifies that body clicks do not reach the application behind the pet.

The gesture check releases mouse buttons and restores the pointer and probe geometry in `finally`, then closes its own backdrop process. Stop the isolated renderer yourself after testing. Do not run concurrent UI automation during this check.

The v1.1.1 regression was exposed by this distinction: restoring the interactive window style succeeded, but the original mouse event had already selected the application behind the transparent pet. The native renderer did not gain focus, so its held-button resize loop never ran. The regression check now requires a real size change, rather than accepting a style change as evidence of a working gesture.
