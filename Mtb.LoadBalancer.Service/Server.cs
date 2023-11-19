
internal class Server
{
    public string Name { get; }
    public Uri BaseUrl { get; }
    public TimeSpan AverageResponseTime { get; private set; }
    public int RequestCount { get; private set; }

    private long _requestCount;

    public bool IsTemporarilyExcluded { get; private set; }
    public DateTime? ExclusionStartTime { get; private set; }

    private TimeSpan ExclusionTimeInSecs = TimeSpan.FromSeconds(3);

    public bool JustReintegrated { get; private set; } //flag to give a chance on excluded server to serve request

    public Server(string name, string baseUrl)
    {
        Name = name;
        BaseUrl = new Uri(baseUrl);
        AverageResponseTime = TimeSpan.Zero;
        RequestCount = 0;
    }

    /// <summary>
    /// Calculates average response time of a server
    /// </summary>
    /// <param name="responseTime"></param>
    public void UpdateAverageResponseTime(TimeSpan responseTime)
    {
        AverageResponseTime = ((AverageResponseTime * _requestCount) + responseTime) / (++_requestCount);
    }

    public void IncrementRequestCount()
    {
        RequestCount++;
    }

    /// <summary>
    /// Exclude Temporarily based on the value of ExclusionTimeInSecs
    /// </summary>
    public void ExcludeTemporarily()
    {
        IsTemporarilyExcluded = true;
        ExclusionStartTime = DateTime.UtcNow;
    }

    /// <summary>
    /// MAke Server available again to accept requests
    /// </summary>
    public void Reintegrate()
    {
        IsTemporarilyExcluded = false;
        ExclusionStartTime = null;
        JustReintegrated = true;
        ResetAverageResponseTime();
    }

    public void MarkAsServed()
    {
        JustReintegrated = false;  // Reset flag after serving a request
    }

    private void ResetAverageResponseTime()
    {
        AverageResponseTime = TimeSpan.Zero;
        _requestCount = 0;
    }

    /// <summary>
    /// Check if we can re-add the excluded server to available servers
    /// </summary>
    /// <returns></returns>
    public bool ShouldReintegrate()
    {
        return IsTemporarilyExcluded && (DateTime.UtcNow - ExclusionStartTime) >= ExclusionTimeInSecs;
    }
}
