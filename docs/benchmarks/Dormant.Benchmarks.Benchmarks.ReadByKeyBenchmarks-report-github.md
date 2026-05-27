```

BenchmarkDotNet v0.14.0, macOS 26.5 (25F71) [Darwin 25.5.0]
Apple M1 Pro, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.201
  [Host]     : .NET 10.0.5 (10.0.526.15411), Arm64 RyuJIT AdvSIMD
  DefaultJob : .NET 10.0.5 (10.0.526.15411), Arm64 RyuJIT AdvSIMD


```
| Method  | Mean     | Error    | StdDev   | Ratio | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|-------- |---------:|---------:|---------:|------:|-----:|-------:|-------:|----------:|------------:|
| Dormant | 20.10 μs | 0.052 μs | 0.041 μs |  1.00 |    2 | 1.0681 | 0.0916 |   6.73 KB |        1.00 |
| Dapper  | 17.88 μs | 0.040 μs | 0.037 μs |  0.89 |    1 | 0.4883 |      - |   2.99 KB |        0.44 |
| EfCore  | 56.60 μs | 0.238 μs | 0.223 μs |  2.82 |    4 | 8.3008 | 0.4883 |  51.24 KB |        7.62 |
| Insight | 20.77 μs | 0.078 μs | 0.069 μs |  1.03 |    3 | 0.8240 |      - |   5.22 KB |        0.78 |
