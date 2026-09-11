# FCL Runtime

This package exposes the FCL generated-code facade over Shared native-error projection. Shared owns
the common exception kinds and exact-once carrier cleanup; FCL currently defines no local exception
kind. The package contains no dependency on another geometry provider.
