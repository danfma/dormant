```

BenchmarkDotNet v0.14.0, macOS 26.5 (25F71) [Darwin 25.5.0]
Apple M1 Pro, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.201
  [Host]     : .NET 10.0.5 (10.0.526.15411), Arm64 RyuJIT AdvSIMD
  DefaultJob : .NET 10.0.5 (10.0.526.15411), Arm64 RyuJIT AdvSIMD


```
| Method  | Mean     | Error    | StdDev   | Ratio | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|-------- |---------:|---------:|---------:|------:|-----:|-------:|-------:|----------:|------------:|
| Dormant | 15.00 μs | 0.067 μs | 0.056 μs |  1.00 |    1 | 0.9460 | 0.0916 |   5.83 KB |        1.00 |
| Dapper  | 15.37 μs | 0.022 μs | 0.019 μs |  1.02 |    1 | 0.9766 | 0.0916 |   6.05 KB |        1.04 |
| EfCore  | 47.08 μs | 0.156 μs | 0.139 μs |  3.14 |    2 | 8.3008 | 0.4883 |  51.49 KB |        8.83 |
