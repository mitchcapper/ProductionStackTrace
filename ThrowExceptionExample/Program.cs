using Newtonsoft.Json;
using Pillar.Demystifier;
using ProductionStackTrace;
using ProductionStackTrace.Test.Library;
using System;
using System.Diagnostics;

namespace ThrowExceptionExample {
	internal class Program {
		static void Main(string[] args) {
			StyledBuilderOption.GlobalOption.ShortenSourceFilePath=true;
			try {
				SomeClass.B();
			} catch (Exception ex) {
				//Console.Error.WriteLine(ex.ToStringDemystified());Console.Error.WriteLine("\n\n");
				ExceptionReporting.SerializeMethodArgs = true;
				var expReport = ExceptionReporting.GetExceptionReportObject(ex);
				expReport.LoadObjectPropertiesFromRaw();
				var report = JsonConvert.SerializeObject(expReport,Formatting.Indented);
				var deserializedReport = JsonConvert.DeserializeObject<LogExceptionReport>(report);
				//Console.WriteLine(report);return;
				
				Console.Error.WriteLine(deserializedReport.ToString(true));

				Console.Error.WriteLine(ExceptionReporting.GetExceptionReport(ex, true) + "\n\n");
				
				
			}
		}
	}
}
