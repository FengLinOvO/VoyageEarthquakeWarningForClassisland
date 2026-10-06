namespace Voyage.EarthquakeWarning.Services;

public partial class EewService
{
    private bool _simulationRunning;

    public async Task StartSimulationFromSettingsAsync()
    {
        if (_simulationRunning) return;

        _simulationRunning = true;

        try { await _engine.RunSimulationAsync(); }
        finally { _simulationRunning = false; }
    }

    public void StopSimulation() => _engine.StopSimulation();
}
