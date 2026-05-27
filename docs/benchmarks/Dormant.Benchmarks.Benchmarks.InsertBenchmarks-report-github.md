```

BenchmarkDotNet v0.14.0, macOS 26.5 (25F71) [Darwin 25.5.0]
Apple M1 Pro, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.201
  [Host]     : .NET 10.0.5 (10.0.526.15411), Arm64 RyuJIT AdvSIMD
  DefaultJob : .NET 10.0.5 (10.0.526.15411), Arm64 RyuJIT AdvSIMD


```
| Method  | Mean     | Error    | StdDev    | Median   | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|-------- |---------:|---------:|----------:|---------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| Dormant | 49.22 μs | 4.664 μs | 13.753 μs | 43.59 μs |  1.08 |    0.43 |    1 | 1.2207 | 0.1221 |   7.52 KB |        1.00 |
| Dapper  | 46.92 μs | 4.573 μs | 13.483 μs | 39.88 μs |  1.03 |    0.42 |    1 | 1.1902 | 0.0916 |   7.43 KB |        0.99 |
| EfCore  | 48.29 μs | 0.738 μs |  0.691 μs | 48.25 μs |  1.06 |    0.30 |    1 | 8.7891 | 0.8545 |  54.42 KB |        7.23 |
