```

BenchmarkDotNet v0.14.0, macOS 26.5 (25F71) [Darwin 25.5.0]
Apple M1 Pro, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.201
  [Host]     : .NET 10.0.5 (10.0.526.15411), Arm64 RyuJIT AdvSIMD
  DefaultJob : .NET 10.0.5 (10.0.526.15411), Arm64 RyuJIT AdvSIMD


```
| Method  | Mean     | Error   | StdDev  | Ratio | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
|-------- |---------:|--------:|--------:|------:|-----:|--------:|-------:|----------:|------------:|
| Dormant | 152.3 μs | 0.95 μs | 0.80 μs |  1.00 |    1 |  5.8594 | 0.4883 |  36.39 KB |        1.00 |
| Dapper  | 165.6 μs | 0.66 μs | 0.61 μs |  1.09 |    2 |  6.5918 | 0.2441 |  41.75 KB |        1.15 |
| EfCore  | 195.3 μs | 1.51 μs | 1.41 μs |  1.28 |    3 | 17.5781 | 1.9531 | 110.18 KB |        3.03 |
| Insight | 155.1 μs | 1.28 μs | 1.19 μs |  1.02 |    1 |  6.1035 | 0.2441 |   37.5 KB |        1.03 |
