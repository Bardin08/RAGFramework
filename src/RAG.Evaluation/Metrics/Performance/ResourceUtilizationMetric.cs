using System.Diagnostics;
using RAG.Evaluation.Interfaces;
using RAG.Evaluation.Models;

namespace RAG.Evaluation.Metrics.Performance;

/// <summary>
/// Tracks resource utilization (CPU, memory) during evaluation.
/// </summary>
public class ResourceUtilizationMetric : IEvaluationMetric
{
    public string Name => "MemoryUsageMB";
    public string Description => "Memory usage in megabytes";

    public Task<double> CalculateAsync(EvaluationContext context, CancellationToken cancellationToken = default)
    {
        var process = Process.GetCurrentProcess();
        var memoryMB = process.WorkingSet64 / (1024.0 * 1024.0);
        return Task.FromResult(memoryMB);
    }
}

/// <summary>
/// Tracks and samples resource utilization over time.
/// </summary>
public class ResourceUtilizationTracker : IDisposable
{
    private readonly List<ResourceSample> _samples = new();
    private readonly Timer? _timer;
    private readonly Process _process;
    private TimeSpan _lastCpuTime;
    private DateTime _lastSampleTime;
    private bool _disposed;

    public ResourceUtilizationTracker(TimeSpan? sampleInterval = null)
    {
        _process = Process.GetCurrentProcess();
        _lastCpuTime = _process.TotalProcessorTime;
        _lastSampleTime = DateTime.UtcNow;

        if (sampleInterval.HasValue)
        {
            _timer = new Timer(
                _ => TakeSample(),
                null,
                TimeSpan.Zero,
                sampleInterval.Value);
        }
    }

    /// <summary>
    /// Takes a resource utilization sample.
    /// </summary>
    public void TakeSample()
    {
        if (_disposed) return;

        _process.Refresh();

        var now = DateTime.UtcNow;
        var currentCpuTime = _process.TotalProcessorTime;
        var elapsedTime = now - _lastSampleTime;
        var elapsedCpu = currentCpuTime - _lastCpuTime;

        var cpuPercent = elapsedTime.TotalMilliseconds > 0
            ? (elapsedCpu.TotalMilliseconds / elapsedTime.TotalMilliseconds) * 100 / Environment.ProcessorCount
            : 0;

        var sample = new ResourceSample
        {
            Timestamp = now,
            CpuPercent = cpuPercent,
            WorkingSetMB = _process.WorkingSet64 / (1024.0 * 1024.0),
            PrivateBytesMB = _process.PrivateMemorySize64 / (1024.0 * 1024.0),
            GcTotalMemoryMB = GC.GetTotalMemory(false) / (1024.0 * 1024.0)
        };

        lock (_samples)
        {
            _samples.Add(sample);
        }

        _lastCpuTime = currentCpuTime;
        _lastSampleTime = now;
    }

    /// <summary>
    /// Gets all recorded samples.
    /// </summary>
    public IReadOnlyList<ResourceSample> GetSamples()
    {
        lock (_samples)
        {
            return _samples.ToList();
        }
    }

    /// <summary>
    /// Gets summary statistics.
    /// </summary>
    public ResourceSummary GetSummary()
    {
        List<ResourceSample> samples;
        lock (_samples)
        {
            samples = _samples.ToList();
        }

        if (samples.Count == 0)
            return new ResourceSummary();

        return new ResourceSummary
        {
            SampleCount = samples.Count,
            AvgCpuPercent = samples.Average(s => s.CpuPercent),
            MaxCpuPercent = samples.Max(s => s.CpuPercent),
            AvgWorkingSetMB = samples.Average(s => s.WorkingSetMB),
            MaxWorkingSetMB = samples.Max(s => s.WorkingSetMB),
            AvgGcMemoryMB = samples.Average(s => s.GcTotalMemoryMB),
            MaxGcMemoryMB = samples.Max(s => s.GcTotalMemoryMB)
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer?.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// A single resource utilization sample.
/// </summary>
public record ResourceSample
{
    public DateTime Timestamp { get; init; }
    public double CpuPercent { get; init; }
    public double WorkingSetMB { get; init; }
    public double PrivateBytesMB { get; init; }
    public double GcTotalMemoryMB { get; init; }
}

/// <summary>
/// Summary of resource utilization.
/// </summary>
public record ResourceSummary
{
    public int SampleCount { get; init; }
    public double AvgCpuPercent { get; init; }
    public double MaxCpuPercent { get; init; }
    public double AvgWorkingSetMB { get; init; }
    public double MaxWorkingSetMB { get; init; }
    public double AvgGcMemoryMB { get; init; }
    public double MaxGcMemoryMB { get; init; }
}
