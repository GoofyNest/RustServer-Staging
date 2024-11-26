using Facepunch;
using UnityEngine;

namespace ConVar;

[Factory("reports")]
public class reports : ConsoleSystem
{
	[ClientVar(Default = "600")]
	[ServerVar(Default = "600")]
	public static int ExceptionReportMaxLength
	{
		get
		{
			return ExceptionReporter.ReportMessageMaxLength;
		}
		set
		{
			ExceptionReporter.ReportMessageMaxLength = Mathf.Max(value, 250);
		}
	}
}
