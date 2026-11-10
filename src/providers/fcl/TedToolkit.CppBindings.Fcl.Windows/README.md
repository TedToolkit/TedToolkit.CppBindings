# FCL Windows bindings

`TedToolkit.CppBindings.Fcl.Windows` supports `net8.0` and `win-x64` for the locked
`fcl-0.7.0-obbrss-double-windows-v2` profile. Build models with copied xyz doubles and native-width
indices, check `FclModelBuildResult.Code`, and dispose every successful `Owned<FclBvhModel>`.

Continuous collision keeps the first model fixed and linearly translates the second. The primary
native path uses OBBRSS linear conservative advancement. Because FCL 0.7 can lose a pure-translation
mesh hit at zero separation, a reported miss is confirmed with FCL's exact translation solver.
Initial/pre-endpoint collisions are explicit; both exact endpoint contact and a miss are
`IsCollide == false` and `TimeOfContact == 1`.

## Agent skill

The `preserve-cpp-binding-lifetimes` skill ships in `TedToolkit.CppBindings.Runtime`, which this package restores as a dependency. If your project references only this package, get the resolved `TedToolkit.CppBindings.Runtime` version from `dotnet package list --include-transitive` and import the skill with `dotnet-package-skills install --package TedToolkit.CppBindings.Runtime@<resolved-version>`.
