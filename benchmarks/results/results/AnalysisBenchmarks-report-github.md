```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9974)
13th Gen Intel Core i7-13620H 2.40GHz, 1 CPU, 16 logical and 10 physical cores
.NET SDK 10.0.401
  [Host]   : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  ShortRun : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method                                | Mean     | Error     | StdDev    | Ratio | Gen0      | Gen1    | Allocated | Alloc Ratio |
|-------------------------------------- |---------:|----------:|----------:|------:|----------:|--------:|----------:|------------:|
| &#39;AnalyzeRound without memoization&#39;    | 4.988 ms | 0.3637 ms | 0.0199 ms |  1.00 | 1210.9375 | 85.9375 |  14.56 MB |        1.00 |
| &#39;AnalyzeRound with local memoization&#39; | 1.699 ms | 0.3118 ms | 0.0171 ms |  0.34 |  335.9375 | 82.0313 |   4.04 MB |        0.28 |
