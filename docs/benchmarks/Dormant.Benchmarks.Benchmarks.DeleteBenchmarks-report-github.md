```

BenchmarkDotNet v0.14.0, macOS 26.5 (25F71) [Darwin 25.5.0]
Apple M1 Pro, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.201
  [Host]     : .NET 10.0.5 (10.0.526.15411), Arm64 RyuJIT AdvSIMD
  Job-MVEHAJ : .NET 10.0.5 (10.0.526.15411), Arm64 RyuJIT AdvSIMD

InvocationCount=1  UnrollFactor=1  

```
| Method  | Mean      | Error     | StdDev    | Median    | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|-------- |----------:|----------:|----------:|----------:|------:|--------:|-----:|----------:|------------:|
| Dormant |  44.66 μs |  3.360 μs |  9.641 μs |  43.75 μs |  1.04 |    0.30 |    3 |         - |          NA |
| Dapper  |  31.98 μs |  1.820 μs |  5.013 μs |  30.31 μs |  0.75 |    0.19 |    1 |         - |          NA |
| EfCore  | 316.69 μs | 31.617 μs | 91.727 μs | 288.04 μs |  7.39 |    2.59 |    4 |   55800 B |          NA |
| Insight |  38.07 μs |  2.683 μs |  7.740 μs |  35.79 μs |  0.89 |    0.25 |    2 |         - |          NA |
