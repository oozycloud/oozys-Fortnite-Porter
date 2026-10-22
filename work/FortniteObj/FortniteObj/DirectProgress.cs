using System;

namespace FortniteObj;

public sealed class DirectProgress(Action<string> report) : IProgress<string>
{
	public void Report(string value)
	{
		report(value);
	}
}
