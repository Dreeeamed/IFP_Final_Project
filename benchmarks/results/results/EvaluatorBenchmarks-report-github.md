```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9974)
13th Gen Intel Core i7-13620H 2.40GHz, 1 CPU, 16 logical and 10 physical cores
.NET SDK 10.0.401
  [Host]   : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  ShortRun : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method                                            | Mean        | Error      | StdDev    | Ratio | RatioSD | Gen0     | Allocated | Alloc Ratio |
|-------------------------------------------------- |------------:|-----------:|----------:|------:|--------:|---------:|----------:|------------:|
| &#39;Imperative (Span + loops)&#39;                       |    21.54 μs |   4.286 μs |  0.235 μs |  1.00 |    0.01 |        - |         - |          NA |
| &#39;Functional (LINQ + immutable, used by the game)&#39; | 1,161.71 μs | 278.926 μs | 15.289 μs | 53.92 |    0.80 | 287.1094 | 3608216 B |          NA |
