```

BenchmarkDotNet v0.14.0, macOS 26.5 (25F71) [Darwin 25.5.0]
Apple M1 Pro, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.201
  [Host]     : .NET 10.0.5 (10.0.526.15411), Arm64 RyuJIT AdvSIMD
  DefaultJob : .NET 10.0.5 (10.0.526.15411), Arm64 RyuJIT AdvSIMD


```
| Method  | Mean     | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Gen1   | Allocated | Alloc Ratio |
|-------- |---------:|---------:|---------:|------:|--------:|-----:|-------:|-------:|----------:|------------:|
| Dormant | 17.03 μs | 0.175 μs | 0.155 μs |  1.00 |    0.01 |    1 | 1.0681 | 0.0916 |   6.73 KB |        1.00 |
| Dapper  | 17.48 μs | 0.041 μs | 0.038 μs |  1.03 |    0.01 |    1 | 1.0986 | 0.0916 |    6.8 KB |        1.01 |
| EfCore  | 47.25 μs | 0.179 μs | 0.158 μs |  2.78 |    0.03 |    2 | 8.3008 | 0.4883 |  51.24 KB |        7.62 |
