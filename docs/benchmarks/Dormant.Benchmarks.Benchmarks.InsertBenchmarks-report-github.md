```

BenchmarkDotNet v0.14.0, macOS 26.5 (25F71) [Darwin 25.5.0]
Apple M1 Pro, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.201
  [Host]     : .NET 10.0.5 (10.0.526.15411), Arm64 RyuJIT AdvSIMD
  DefaultJob : .NET 10.0.5 (10.0.526.15411), Arm64 RyuJIT AdvSIMD


```
| Method  | Mean     | Error    | StdDev    | Median   | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|-------- |---------:|---------:|----------:|---------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| Dormant | 57.51 μs | 5.167 μs | 15.234 μs | 52.64 μs |  1.07 |    0.41 |    2 | 1.2207 | 0.1221 |   7.52 KB |        1.00 |
| Dapper  | 46.44 μs | 5.016 μs | 14.789 μs | 41.83 μs |  0.87 |    0.37 |    1 | 0.5798 |      - |   3.63 KB |        0.48 |
| EfCore  | 57.36 μs | 0.825 μs |  0.772 μs | 57.14 μs |  1.07 |    0.29 |    2 | 8.7891 | 0.8545 |  54.42 KB |        7.23 |
| Insight | 47.20 μs | 5.057 μs | 14.910 μs | 42.50 μs |  0.88 |    0.37 |    1 | 0.7019 |      - |   4.34 KB |        0.58 |
