using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Debug = UnityEngine.Debug;

public class AsyncLevelGenerator {
  public struct ResultMetrics {
    public int ThreadNumber;
    public int Seed;
    public long ThreadTimeMs;
    public int Attempts;
    public int TotalStatesExplored;
  }

  public static async Task<(SokobanState?, SokobanSolution, List<ResultMetrics>)>
      GenerateLevelAsync(
          int minSize,
          int maxSize,
          int targetCount,
          int holeCount,
          bool useEntranceExit,
          int seed,
          int seedOffset,
          int threadCount,
          bool waitForFullCompletion) {
    const int timeOutMs = 65_000;
    using var cts = new CancellationTokenSource();
    cts.CancelAfter(timeOutMs);

    var result = await GenerateStateAsync(
        minSize,
        maxSize,
        targetCount,
        holeCount,
        useEntranceExit,
        seed,
        seedOffset,
        threadCount,
        waitForFullCompletion,
        cts);

    var (state, _, _) = result;

    if (state == null && cts.IsCancellationRequested) {
      Debug.Log($"Cancelled generation after {timeOutMs}ms");
    }

    return result;
  }

  private static async Task<(SokobanState?, SokobanSolution, List<ResultMetrics>)>
      GenerateStateAsync(
          int minSize,
          int maxSize,
          int targetCount,
          int holeCount,
          bool useEntranceExit,
          int baseSeed,
          int seedOffset,
          int threadCount,
          bool waitForFullCompletion,
          CancellationTokenSource cts = null) {
    var cancellation = cts?.Token ?? CancellationToken.None;

    var tasks =
        new List<Task<(SokobanState? State, SokobanSolution Solution, ResultMetrics ResultMetrics
            )>>();

    Debug.Log($"Starting generation on {threadCount} threads...");

    for (int i = 0; i < threadCount; i++) {
      // Capture loop variable
      int threadIndex = i;

      tasks.Add(
          Task.Run(
              () => {
                var timer = Stopwatch.StartNew();

                // Create a dedicated generator for this thread
                var generator = new SokobanLevelGenerator();
                // Offset seed so threads don't generate identical levels
                int threadSeed = baseSeed + (threadIndex * seedOffset);

                var state = generator.GenerateLevel(
                    out var solution,
                    out var attempts,
                    out var statesExplored,
                    minSize,
                    maxSize, // assuming width=height based on your code
                    targetCount,
                    holeCount,
                    useEntranceExit,
                    threadSeed,
                    // ReSharper disable once AccessToDisposedClosure
                    cancellation
                );
                return (state, solution,
                    new ResultMetrics() {
                        ThreadNumber = threadIndex,
                        Seed = baseSeed,
                        Attempts = attempts,
                        TotalStatesExplored = statesExplored,
                        ThreadTimeMs = timer.ElapsedMilliseconds
                    });
              },
              cancellation));
    }

    // Wait for the first task to return a valid result
    var result = await WaitForFirstSuccess(
        tasks,
        cancelOnFirstSuccess: !waitForFullCompletion,
        cts);

    if (result.Solution != null) {
      Debug.Log($"Generated level difficulty: {result.Solution.Difficulty}");
    }

    var totalAttempts = tasks.Sum(t => t.Result.ResultMetrics.Attempts);
    var totalStatesExplored = tasks.Sum(t => t.Result.ResultMetrics.TotalStatesExplored);
    Debug.Log($"Total attempts: {totalAttempts}");
    Debug.Log($"Solver explored {totalStatesExplored} total states");
    return (result.State, result.Solution, tasks.Select(t => t.Result.ResultMetrics).ToList());
  }

  private static async Task<T> WaitForFirstSuccess<T>(
      List<Task<T>> tasks,
      bool cancelOnFirstSuccess,
      CancellationTokenSource cts) {
    var remainingTasks = new List<Task<T>>(tasks);

    while (remainingTasks.Count > 0) {
      // Wait for any task to complete
      Task<T> completedTask = await Task.WhenAny(remainingTasks);
      remainingTasks.Remove(completedTask);

      // If it completed successfully (didn't crash/cancel)
      if (completedTask.Status == TaskStatus.RanToCompletion) {
        var result = completedTask.Result;
        if (result != null) {
          if (cancelOnFirstSuccess) {
            // We found a level! Cancel all other threads.
            cts.Cancel();
          }

          await Task.WhenAll(remainingTasks);
          return result;
        }
      }

      // If we are here, the task either failed, was cancelled,
      // or returned null (exhausted attempts). We loop and wait for the next one.
    }

    return default; // All threads failed
  }
}
