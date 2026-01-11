using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;

[TestFixture]
public class BenchMarkAsyncLevelGeneratorTest {
  private const int MIN_SIZE = 40;
  private const int MAX_SIZE = 40;
  private const int TARGET_COUNT = 4;
  private const int HOLE_COUNT = 2;
  private const bool USE_ENTRANCE_EXIT = true;
  private const int TEST_SEED = 123456;
  private const int SEED_OFFSET = 0;

  private const int CURRENT_CONFIGURED_THREAD_COUNT = 2;

  private readonly int[] ThreadCounts = { 1, 2, 4, 6 };
  private Dictionary<int, long> _benchmarkResults;
  private Dictionary<int, List<AsyncLevelGenerator.ResultMetrics>> _benchmarkMetrics;
  private Dictionary<int, double> _explorationSpeeds;

  [SetUp]
  public void Setup() {
    _benchmarkResults = new Dictionary<int, long>();
    _benchmarkMetrics = new Dictionary<int, List<AsyncLevelGenerator.ResultMetrics>>();
    _explorationSpeeds = new Dictionary<int, double>();
  }

  [UnityTest]
  public IEnumerator BenchmarkAsyncLevelGenerator_1Thread() {
    yield return RunBenchmarkForThreadCount(1);
  }

  [UnityTest]
  public IEnumerator BenchmarkAsyncLevelGenerator_2Threads() {
    yield return RunBenchmarkForThreadCount(2);
  }

  [UnityTest]
  public IEnumerator BenchmarkAsyncLevelGenerator_4Threads() {
    yield return RunBenchmarkForThreadCount(4);
  }

  [UnityTest]
  public IEnumerator BenchmarkAsyncLevelGenerator_6Threads() {
    yield return RunBenchmarkForThreadCount(6);
  }

  [UnityTest]
  public IEnumerator BenchmarkComparison_VerifyScalingAndPrintReport() {
    // Run all benchmarks sequentially to populate results
    foreach (var threadCount in ThreadCounts) {
      yield return RunBenchmarkForThreadCount(threadCount);
    }

    foreach (var threadCount in ThreadCounts) {
      long time = _benchmarkResults[threadCount];
      var metrics = _benchmarkMetrics[threadCount];
      var totalStatesExplored = metrics.Sum(x => x.TotalStatesExplored);
      var explorationSpeed = (double)totalStatesExplored / time;

      _explorationSpeeds[threadCount] = explorationSpeed;
    }

    // Generate and log the report
    string report = GeneratePerformanceReport();
    Debug.Log(report);

    // Also output to console for external capture
    Console.WriteLine(report);

    // Verify that time decreases as thread count increases
    var previousSpeed = double.MinValue;
    foreach (var threadCount in ThreadCounts) {
      if (threadCount <= CURRENT_CONFIGURED_THREAD_COUNT) {
        var currentSpeed = _explorationSpeeds[threadCount];

        Assert.That(
            currentSpeed >= previousSpeed,
            $"Performance degraded: {threadCount} threads ran at {currentSpeed} states/s, " +
            $"but previous configuration ran at {previousSpeed} states/s"
        );

        previousSpeed = currentSpeed;
      }
    }
  }

  [UnityTest]
  public IEnumerator Diagnose_GC_Pressure() {
    foreach (int threadCount in ThreadCounts) {
      GC.Collect();
      GC.WaitForPendingFinalizers();

      long beforeBytes = GC.GetTotalMemory(false);

      yield return RunBenchmarkForThreadCount(threadCount);

      long afterBytes = GC.GetTotalMemory(false);
      long allocated = afterBytes - beforeBytes;

      Debug.Log(
          $"{threadCount} threads: Allocated {allocated / 1024 / 1024}MB, " +
          $"Rate: {allocated / (double)_benchmarkResults[threadCount]:F1} bytes/ms");
    }
  }

  [UnityTest]
  public IEnumerator Diagnose_DeadSquareMap_Cache_Behavior() {
    var task = AsyncLevelGenerator.GenerateLevelAsync(
        MIN_SIZE,
        MAX_SIZE,
        TARGET_COUNT,
        HOLE_COUNT,
        USE_ENTRANCE_EXIT,
        TEST_SEED,
        SEED_OFFSET,
        1,
        waitForFullCompletion: true
    );

    // Wait for the async operation to complete
    while (!task.IsCompleted) {
      yield return null;
    }

    var (maybeState, _, _) = task.Result;

    if (maybeState is not { } state) yield break;

    foreach (int threadCount in ThreadCounts) {
      Debug.Log($"\n=== DeadSquareMap Test: {threadCount} threads ===");

      var tasks = new List<Task>();
      var deadSquareTimings = new Dictionary<int, long>();

      for (int i = 0; i < threadCount; i++) {
        int threadIdx = i;
        tasks.Add(
            Task.Run(() => {
              var timer = Stopwatch.StartNew();
              _ = new DeadSquareMap(state);
              timer.Stop();

              lock (deadSquareTimings) {
                deadSquareTimings[threadIdx] = timer.ElapsedMilliseconds;
              }
            }));
      }

      Task.WaitAll(tasks.ToArray());

      var avg = deadSquareTimings.Values.Average();
      Debug.Log($"DeadSquareMap avg per thread: {avg:F1}ms");

      yield return null;
    }
  }

  [UnityTest]
  public IEnumerator Diagnose_Solver_Cache_Thrashing() {
    var task = AsyncLevelGenerator.GenerateLevelAsync(
        MIN_SIZE,
        MAX_SIZE,
        TARGET_COUNT,
        HOLE_COUNT,
        USE_ENTRANCE_EXIT,
        TEST_SEED,
        SEED_OFFSET,
        1,
        waitForFullCompletion: true
    );

    // Wait for the async operation to complete
    while (!task.IsCompleted) {
      yield return null;
    }

    var (maybeState, _, _) = task.Result;

    if (maybeState is not { } state) yield break;

    foreach (int threadCount in ThreadCounts) {
      Debug.Log($"\n=== Solver Search Test: {threadCount} threads ===");

      var stopWatch = Stopwatch.StartNew();

      var tasks = new List<Task<int>>(); // States explored

      for (int i = 0; i < threadCount; i++) {
        tasks.Add(
            Task.Run(() => {
              var solver = new SokobanSolver();

              solver.IsSolvable(state, out _, out var statesExplored);
              return statesExplored;
            }));
      }

      var whenAll = Task.WhenAll(tasks);
      while (!whenAll.IsCompleted) {
        yield return null;
      }

      int totalStatesExplored = tasks.Sum(t => t.Result);
      long wallClockMs = stopWatch.ElapsedMilliseconds;
      double statesPerMs = totalStatesExplored / (double)wallClockMs;

      Debug.Log(
          $"Solver throughput: {statesPerMs:F2} states/ms " +
          $"({totalStatesExplored} total states in {wallClockMs}ms)");

      yield return null;
    }
  }


  private IEnumerator RunBenchmarkForThreadCount(int threadCount) {
    Debug.Log($"Starting benchmark for {threadCount} thread(s)...");

    var stopwatch = Stopwatch.StartNew();

    var task = AsyncLevelGenerator.GenerateLevelAsync(
        MIN_SIZE,
        MAX_SIZE,
        TARGET_COUNT,
        HOLE_COUNT,
        USE_ENTRANCE_EXIT,
        TEST_SEED,
        SEED_OFFSET,
        threadCount,
        waitForFullCompletion: true
    );

    // Wait for the async operation to complete
    while (!task.IsCompleted) {
      yield return null;
    }

    stopwatch.Stop();

    var (result, _, metrics) = task.Result;
    long elapsedMs = stopwatch.ElapsedMilliseconds;

    _benchmarkResults[threadCount] = elapsedMs;
    _benchmarkMetrics[threadCount] = metrics;

    Assert.That(result, Is.Not.Null, $"Level generation failed with {threadCount} thread(s)");

    Debug.Log($"Benchmark completed for {threadCount} thread(s): {elapsedMs}ms");

    yield return null;
  }

  private string GeneratePerformanceReport() {
    var sb = new StringBuilder();

    sb.AppendLine("\n" + new string('=', 70));
    sb.AppendLine("ASYNC LEVEL GENERATOR PERFORMANCE BENCHMARK REPORT");
    sb.AppendLine(new string('=', 70));
    sb.AppendLine($"Test Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    sb.AppendLine(
        $"Parameters: MinSize={MIN_SIZE}, MaxSize={MAX_SIZE}, " +
        $"TargetCount={TARGET_COUNT}, HoleCount={HOLE_COUNT}, Seed={TEST_SEED}");
    sb.AppendLine(new string('-', 70));

    // Results table
    sb.AppendLine(
        $"{"Thread Count",-15} {"Time (ms)",-15} {"Exploration Speed",-20} {"Speed-up",-15}");
    sb.AppendLine(new string('-', 70));

    double singleThreadedExplorationSpeed = _explorationSpeeds[1];

    foreach (var threadCount in ThreadCounts.OrderBy(x => x)) {
      var time = _benchmarkResults[threadCount];
      double explorationSpeed = _explorationSpeeds[threadCount];
      double speedup = explorationSpeed / singleThreadedExplorationSpeed;

      sb.AppendLine(
          $"{threadCount,-15} " +
          $"{time,-15} " +
          $"{explorationSpeed:F2}{"",-20} " +
          $"{speedup:F2}x"
      );
    }

    sb.AppendLine(new string('-', 70));

    sb.AppendLine($"{"Thread Count",-15} {"Thread 0 Run Time (ms)",-25} {"Efficiency",-15}");

    long baseLineThread0RunTime = _benchmarkMetrics[1]
        .OrderBy(x => x.ThreadNumber)
        .First()
        .ThreadTimeMs;

    foreach (var threadCount in ThreadCounts.OrderBy(x => x)) {
      if (_benchmarkMetrics.TryGetValue(threadCount, out var metrics)) {
        long thread0RunTime = metrics.OrderBy(x => x.ThreadNumber).First().ThreadTimeMs;
        double ratio = (double)thread0RunTime / baseLineThread0RunTime;
        double efficiency = 1f / ratio;
        double efficiencyPercent = efficiency * 100;

        sb.AppendLine(
            $"{threadCount,-15} " +
            $"{thread0RunTime,-25} " +
            $"{efficiencyPercent:F1}%{"",-15} "
        );
      }
    }

    sb.AppendLine(new string('-', 70));

    return sb.ToString();
  }
}
