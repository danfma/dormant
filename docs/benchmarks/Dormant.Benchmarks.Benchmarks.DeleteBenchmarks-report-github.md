```

BenchmarkDotNet v0.14.0, macOS 26.5 (25F71) [Darwin 25.5.0]
Apple M1 Pro, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.201
  [Host]     : .NET 10.0.5 (10.0.526.15411), Arm64 RyuJIT AdvSIMD
  Job-LHXPPO : .NET 10.0.5 (10.0.526.15411), Arm64 RyuJIT AdvSIMD

InvocationCount=1  UnrollFactor=1  

```
| Method  | Mean      | Error     | StdDev     | Median    | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|-------- |----------:|----------:|-----------:|----------:|------:|--------:|-----:|----------:|------------:|
| Dormant |  35.62 μs |  2.207 μs |   6.225 μs |  33.27 μs |  1.03 |    0.24 |    1 |         - |          NA |
| Dapper  |  35.71 μs |  2.514 μs |   7.173 μs |  32.94 μs |  1.03 |    0.26 |    1 |         - |          NA |
| EfCore  | 368.11 μs | 45.041 μs | 131.386 μs | 345.29 μs | 10.61 |    4.12 |    2 |   55536 B |          NA |
