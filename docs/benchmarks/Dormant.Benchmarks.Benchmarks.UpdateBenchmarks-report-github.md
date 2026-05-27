```

BenchmarkDotNet v0.14.0, macOS 26.5 (25F71) [Darwin 25.5.0]
Apple M1 Pro, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.201
  [Host]     : .NET 10.0.5 (10.0.526.15411), Arm64 RyuJIT AdvSIMD
  DefaultJob : .NET 10.0.5 (10.0.526.15411), Arm64 RyuJIT AdvSIMD


```
| Method  | Mean     | Error    | StdDev   | Ratio | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|-------- |---------:|---------:|---------:|------:|-----:|-------:|-------:|----------:|------------:|
| Dormant | 17.98 μs | 0.029 μs | 0.025 μs |  1.00 |    1 | 0.9460 | 0.0916 |   5.83 KB |        1.00 |
| Dapper  | 18.47 μs | 0.041 μs | 0.039 μs |  1.03 |    2 | 0.9766 | 0.0916 |   6.05 KB |        1.04 |
| EfCore  | 56.14 μs | 0.205 μs | 0.192 μs |  3.12 |    3 | 8.3008 | 0.4883 |  51.49 KB |        8.83 |
| Insight | 18.73 μs | 0.099 μs | 0.093 μs |  1.04 |    2 | 1.0986 | 0.0916 |   6.83 KB |        1.17 |
