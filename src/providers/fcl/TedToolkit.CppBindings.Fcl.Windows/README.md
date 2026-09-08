# FCL Windows bindings

`TedToolkit.CppBindings.Fcl.Windows` supports `net8.0` and `win-x64` for the locked
`fcl-0.7.0-obbrss-double-windows-v1` profile. Build models with copied xyz doubles and native-width
indices, check `FclModelBuildResult.Code`, and dispose every successful `Owned<FclBvhModel>`.

Continuous collision keeps the first model fixed and linearly translates the second. The primary
native path uses OBBRSS linear conservative advancement. Because FCL 0.7 can lose a pure-translation
mesh hit at zero separation, a reported miss is confirmed with FCL's exact translation solver.
Initial/pre-endpoint collisions are explicit; both exact endpoint contact and a miss are
`IsCollide == false` and `TimeOfContact == 1`.
