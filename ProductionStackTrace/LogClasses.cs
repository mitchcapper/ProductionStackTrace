using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductionStackTrace {

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
	public class LogStackTrace {
		public IEnumerable<LogStackFrame> StackFrames;
		public override string ToString() { var sb = new StringBuilder(0xff); ToString(sb); return sb.ToString(); }
		public void ToString(StringBuilder builder) => ToString(new StringBuilderProxy(builder));
		internal void ToString(IBuilderProxy builder) {
			bool isFirstLine = true;
			foreach (var frame in StackFrames) {
				if (isFirstLine) {
					isFirstLine = false;
				} else {
					builder.Append(Environment.NewLine);
				}

				frame.ToString(builder);


			}
		}
	}
	public class LogStackFrame {
		private EnhancedStackFrame frame;
		public LogStackFrame(EnhancedStackFrame frame) { this.frame = frame; from_raw = true; }
		public LogStackFrame() { }
		public void LoadObjectPropertiesFromRaw() {
			if (!from_raw)
				return;
			from_raw = false;
			var method = frame.GetMethod();
			MethodMetadataToken = GetMetadataToken(method);
			IlOffset = frame.GetILOffset();
			SourceFileName = frame.GetFileName();
			SourceFileLine = frame.GetFileLineNumber();
			if (ExceptionReporting.SerializeMethodArgs) 
				RawMethodInfo = frame.MethodInfo;
			else
				MethodInfo = frame.MethodInfo.ToString();
			IsLastFrameFromForeignExceptionStackTrace = ExceptionReporting.GetIsLastFrameFromForeignExceptionStackTrace(frame);
		}
		private int GetMetadataToken(System.Reflection.MethodBase method_info) {
			try { 
				return method_info.MetadataToken;
			} catch (InvalidOperationException) {
				return 0;
			}
		}
		public override string ToString() { var sb = new StringBuilder(0xff); ToString(sb); return sb.ToString(); }
		public void ToString(StringBuilder builder) => ToString(new StringBuilderProxy(builder));
		internal void ToString(IBuilderProxy builder) {
			var strAt = ExceptionReporting.GetRuntimeResourceString("Word_At") ?? "at";
			var method = from_raw ? frame.GetMethod() : null;
			builder.Append($"   {strAt} ");
			var filename = SourceFileName ?? (from_raw ? frame.GetFileName() : null);
			var no_file_info = String.IsNullOrWhiteSpace(filename);
			if (no_file_info) {
				builder.Append(ShortAssemblyName);
				builder.Append("!");
				builder.Append($"0x{(from_raw ? GetMetadataToken(method) : MethodMetadataToken):x8}");
				builder.Append("!");
			}
			if (from_raw || RawMethodInfo != null)
				builder.AppendMethodInfo(RawMethodInfo ?? frame.MethodInfo);
			else
				builder.Append(MethodInfo);

			if (no_file_info) {
				var ilOffset = from_raw ? frame.GetILOffset() : IlOffset;
				if (ilOffset != -1) {
					// Output the IL Offset, which we can later map to a filename+line,
					// using information inside a matching PDB file
					builder.Append($" +0x{ilOffset:x}");
				}
			} else
				builder.AppendFilename(filename, from_raw ? frame.GetFileLineNumber() : SourceFileLine);


			if (from_raw ? ExceptionReporting.GetIsLastFrameFromForeignExceptionStackTrace(frame) : IsLastFrameFromForeignExceptionStackTrace) {
				builder.Append(Environment.NewLine);
				builder.Append(ExceptionReporting.GetRuntimeResourceString("Exception_EndStackTraceFromPreviousThrow") ??
					"--- End of stack trace from previous location where exception was thrown ---");
			}

		}
		public int MethodMetadataToken { get; set; }
		public string ShortAssemblyName { get; set; }
		public ResolvedMethod RawMethodInfo {get;set; }
		public string MethodInfo { get; set; }
		public string SourceFileName { get; set; }
		public int SourceFileLine { get; set; }
		public int IlOffset { get; set; }
		public bool IsLastFrameFromForeignExceptionStackTrace { get; set; }
		private bool from_raw;
	}
	public class LogExceptionReport {
		public Type Type { get; set; }
		public string Message { get; set; }

		public void LoadObjectPropertiesFromRaw() {
			foreach (var frame in StackTrace.StackFrames)
				frame.LoadObjectPropertiesFromRaw();
			if (AssemblyInfo != null) {
				foreach (var assembly in AssemblyInfo)
					assembly.LoadObjectPropertiesFromRaw();
			}
			InnerException?.LoadObjectPropertiesFromRaw();

		}
		public LogStackTrace StackTrace { get; set; }
		public LogExceptionReport InnerException { get; set; }
		public LogAssemblyInfo[] AssemblyInfo { get; set; }
		public override string ToString() { var sb = new StringBuilder(0xff); ToString(sb); return sb.ToString(); }
		public void ToString(StringBuilder builder) => ToString(new StringBuilderProxy(builder));
		public string ToString(Pillar.Demystifier.StyledBuilderOption option) {
			var sb = new StyledBuilderProxy(new Pillar.Demystifier.StyledBuilder(), option);
			ToString(sb);
			return sb.ToString();
		}
		public string ToString(bool Colorize) => Colorize ? ToString(Pillar.Demystifier.StyledBuilderOption.GlobalOption) : ToString();

		internal void ToString(IBuilderProxy builder) {
			builder.Append(Type.ToString());
			if (!string.IsNullOrEmpty(Message))
				builder.Append(": ").AppendMessage(Message);

			if (InnerException != null) {
				builder.Append(" ---> ");
				InnerException.ToString(builder);
				builder.Append(Environment.NewLine).Append("   ");
				builder.Append(ExceptionReporting.GetRuntimeResourceString("Exception_EndOfInnerExceptionStack") ??
					"--- End of inner exception stack trace ---");

			}
			builder.Append(Environment.NewLine);
			StackTrace.ToString(builder);
			if (AssemblyInfo?.Length > 0) {
				builder.AppendLine();
				builder.AppendLine("==========");
				foreach (var assembly in AssemblyInfo) {
					assembly.ToString(builder);
					builder.AppendLine();
				}
			}
		}
	}
	public class LogAssemblyInfo {
		public LogAssemblyInfo() { }
		internal LogAssemblyInfo(ExceptionReporting.AssemblyReportInfo info) {
			this.info = info;
			from_raw = true;
		}
		public string ShortName { get; set; }//may have an incrementer at end if multiple assemblies with same short name
		public string OrigShortName { get; set; }
		public string AssemblyFullName { get; set; }
		public uint Age { get; set; }
		public Guid Guid { get; set; }
		public string PdbFileName { get; set; }
		public bool DebugInfoPresent { get; set; }
		private ExceptionReporting.AssemblyReportInfo info;
		private bool from_raw;
		public void LoadObjectPropertiesFromRaw() {
			if (!from_raw)
				return;
			from_raw = false;
			AssemblyFullName = info.Assembly.FullName;
			DebugInfoPresent = info.DebugInfo != null;
			if (DebugInfoPresent) {
				Guid = info.DebugInfo.Guid;
				Age = info.DebugInfo.Age;
				PdbFileName = info.DebugInfo.Path;
				OrigShortName = info.ShortName;
			}
		}
		public override string ToString() { var sb = new StringBuilder(0xff); ToString(sb); return sb.ToString(); }
		public void ToString(StringBuilder builder) => ToString(new StringBuilderProxy(builder));
		internal void ToString(IBuilderProxy builder) {

			builder.Append($"MODULE: {ShortName} => {(from_raw ? info.Assembly.FullName : AssemblyFullName)};");
			if ((from_raw ? info.DebugInfo != null : DebugInfoPresent)) {
				builder.Append($" G:{(from_raw ? info.DebugInfo.Guid : Guid):N}; A:{(from_raw ? info.DebugInfo.Age : Age)}");

				var pdbFileName = from_raw ? info.DebugInfo.Path : PdbFileName;
				var pos = pdbFileName.LastIndexOfAny(new[] { '\\', '/' });
				if (pos != -1)
					pdbFileName = pdbFileName.Substring(pos + 1);
				if (!string.Equals(pdbFileName, (from_raw ? info.ShortName : OrigShortName) + ".pdb", StringComparison.OrdinalIgnoreCase))
					builder.Append("; F:").Append(pdbFileName);
			}

		}


	}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

}
