```

BenchmarkDotNet v0.14.0, macOS 26.5 (25F71) [Darwin 25.5.0]
Apple M1 Pro, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.201
  [Host]     : .NET 10.0.5 (10.0.526.15411), Arm64 RyuJIT AdvSIMD
  DefaultJob : .NET 10.0.5 (10.0.526.15411), Arm64 RyuJIT AdvSIMD


```
| Method  | Mean     | Error   | StdDev  | Ratio | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
|-------- |---------:|--------:|--------:|------:|-----:|--------:|-------:|----------:|------------:|
| Dormant | 123.5 μs | 0.19 μs | 0.16 μs |  1.00 |    1 |  5.8594 | 0.4883 |  36.39 KB |        1.00 |
| Dapper  | 128.7 μs | 0.27 μs | 0.24 μs |  1.04 |    2 |  7.3242 | 0.4883 |  45.55 KB |        1.25 |
| EfCore  | 157.4 μs | 0.45 μs | 0.42 μs |  1.28 |    3 | 17.5781 | 1.9531 | 110.18 KB |        3.03 |
