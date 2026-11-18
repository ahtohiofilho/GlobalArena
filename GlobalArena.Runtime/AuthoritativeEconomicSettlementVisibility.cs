// Economic treasury credits are an internal simulation capability,
// not a public or client-facing operation. Only an audited system-event
// executor in GlobalArena.Simulation may close a market cycle.
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("GlobalArena.Simulation")]
