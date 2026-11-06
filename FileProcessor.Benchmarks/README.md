# FileProcessor benchmarks

Run from the solution directory in Release without a debugger. Benchmarks are sequential; avoid other CPU/disk workloads while running.

## Compare parallelism and merge fan-in (default)

```powershell
dotnet run --project FileProcessor.Benchmarks -c Release -- --profile tuning --artifacts BenchmarkDotNet.Artifacts/merge-tuning
```

This runs **16 Merge cases**: max concurrent merges **1, 2, 4, 8** crossed with files per merge **2, 4, 8, 16**. Input is fixed at **128 MiB**, chunks at **8 MiB**, and baseline preparation uses four concurrent sorts. The number of chunks is sufficient to exercise multiple merge groups/passes. `merge-tuning` is an alias for `tuning`. A run without arguments also selects `tuning` and Merge only.

Each case gets identical deterministic input (seed 1729). Baseline sorted chunks are copied before every iteration because merging deletes its inputs. Generation, copying, correctness checks and cleanup are excluded from measured time.

Concurrency is a maximum number of simultaneous operations, not a guarantee of that many OS threads. Later merge passes may have fewer groups than the concurrency limit.

## Compare sort parallelism separately

```powershell
dotnet run --project FileProcessor.Benchmarks -c Release -- --profile sort-tuning --artifacts BenchmarkDotNet.Artifacts/sort-tuning
```

This runs **four Divide cases**, with max concurrent sorts **1, 2, 4, 8**, at the same 128 MiB input and 8 MiB chunks. Merge settings remain fixed and are not measured. Separating these scenarios avoids confounding sort parallelism with merge parallelism.

`Settings` labels in reports use `C8-S4-M2-F16`: chunk MiB, concurrent sorts, concurrent merges and files per merge, respectively.

## Other profiles

```powershell
# Three scenarios at 128, 256, 512 and 1024 MiB; fixed C64-S4-M4-F4 settings
dotnet run --project FileProcessor.Benchmarks -c Release -- --profile scaling

# Harness verification only: 4 MiB, three scenarios, one measured iteration
dotnet run --project FileProcessor.Benchmarks -c Release -- --profile smoke --warmupCount 1 --iterationCount 1
```

Normally BenchmarkDotNet Monitoring runs one launch, one warmup and **five measured iterations**, one operation per iteration. Explicit `--filter` overrides the profile's scenario selection, e.g. `--profile tuning --filter '*FullPipeline'` measures the full pipeline across the 16 merge configurations with sort parallelism fixed at four.

## CSV and plotting

The chosen artifacts directory contains `results/` with:

- `*-report.csv`: aggregate statistics.
- `*-measurements.csv`: individual measurements, including warmup (filter to Workload/Actual when plotting).
- `*-graph.csv`: invariant numeric values for plotting:

```text
Scenario,Job,InputMiB,ChunkMiB,ParallelSorts,ParallelMerges,FilesPerMerge,MeanSeconds,MedianSeconds,StdDevSeconds,MiBPerSecond
```

MiBPerSecond is input MiB divided by mean elapsed seconds, not physical disk bandwidth. Five iterations provide an initial comparison, not a reliable tail-latency estimate.

Curated charts from previous runs are kept in the solution's `BenchmarkResults/` directory as PNG and SVG files.

## Measurement boundaries

Input is deterministic ASCII text with repeated keys and random numbers. Each line is 128 bytes including LF; input has exactly the requested size. Results check sortedness, record count and the sum of numbers outside the measured operation. This is a sanity check; integration tests provide separate correctness coverage. Output file formatting can differ from the generated input (number padding and newline length).

The OS file cache is **not flushed**. Setup and validation influence cache contents. These are repeated-run, cache-influenced measurements, not cold-disk measurements. No peak-memory metric is collected.

Scratch files are removed after successful runs. Interrupted runs can leave `FileProcessor.Benchmarks-*` directories. The default scratch location is the OS temporary directory. To use another drive:

```powershell
$env:FILEPROCESSOR_BENCHMARK_WORKDIR = 'D:\BenchmarkScratch'
```

Allow several times the input size in free disk space. Managed-memory usage per chunk can substantially exceed on-disk text size and grows with sort parallelism. Keep the drive, runtime, power settings and workload consistent between comparisons.
