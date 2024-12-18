using Unity.Profiling;

namespace Facepunch.Rust.Profiling;

public struct RustProfilerRecorder
{
	public string ColumnName;

	public ProfilerRecorder Recorder;

	public RustProfilerRecorder(string column, ProfilerCategory category, string sample, int sampleCount = 1, ProfilerRecorderOptions options = ProfilerRecorderOptions.Default)
	{
		ColumnName = column;
		Recorder = ProfilerRecorder.StartNew(category, sample, sampleCount, options);
	}
}
