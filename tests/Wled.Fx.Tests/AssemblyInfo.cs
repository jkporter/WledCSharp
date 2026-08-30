using Xunit;

// The engine keeps a single global clock (Clock.Millis), which the tests freeze and advance to make
// rendering deterministic. Running collections in parallel would let them fight over it.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
