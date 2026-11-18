// Keep the copy-on-write construction invariant testable without exposing
// mutable canonical transport storage to the actual game client.
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("GlobalArena.Tests")]
