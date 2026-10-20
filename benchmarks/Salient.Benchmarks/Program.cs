using BenchmarkDotNet.Running;

// Run all benchmarks, or pick some interactively: dotnet run -c Release -- --filter *Convolution*
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
