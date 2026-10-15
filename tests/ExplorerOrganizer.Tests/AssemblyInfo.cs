// Loc (UI language) is process-wide state: run test classes sequentially so language switches don't race.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
