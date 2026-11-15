```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9974)
13th Gen Intel Core i7-13620H 2.40GHz, 1 CPU, 16 logical and 10 physical cores
.NET SDK 10.0.401
  [Host]   : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  ShortRun : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method                                                  | Mean        | Error      | StdDev   | Ratio | RatioSD | Gen0     | Allocated  | Alloc Ratio |
|-------------------------------------------------------- |------------:|-----------:|---------:|------:|--------:|---------:|-----------:|------------:|
| &#39;foreach + List + imperative evaluator&#39;                 |    21.75 μs |   9.625 μs | 0.528 μs |  1.00 |    0.03 |   0.1526 |    2.23 KB |        1.00 |
| &#39;LINQ Where + curried predicate + functional evaluator&#39; | 1,126.46 μs | 111.712 μs | 6.123 μs | 51.82 |    1.11 | 287.1094 | 3524.66 KB |    1,583.00 |
| &#39;LINQ Where + curried predicate + imperative evaluator&#39; |    24.97 μs |  11.247 μs | 0.617 μs |  1.15 |    0.03 |   0.0610 |    1.01 KB |        0.45 |
