using System.Diagnostics.Metrics;

namespace ECommerce.Shared.Observability;

public class MetricFactory(string meterName)
{
    private readonly Meter _meter = new(meterName);
    private readonly Dictionary<string , Counter<int>> _cachedCounters = new();
    private readonly Dictionary<string, Histogram<int>> _cachedHistograms = new();
    
    public Histogram<int> Histogram(string name, string? unit = null)
    {
        if (_cachedHistograms.TryGetValue(name, out Histogram<int>? value))
        {
            return value;
        }
        var histogram = _meter.CreateHistogram<int>(name, unit);
        _cachedHistograms.Add(name, histogram);
        return histogram;
    }
    
    public Counter<int> Counter(string name, string? unit = null)
    {
        if (_cachedCounters.TryGetValue(name, out Counter<int>? value))
        {
            return value;
        }

        var counter = _meter.CreateCounter<int>(name, unit);
        _cachedCounters.Add(name, counter);
        return counter;
    }
}