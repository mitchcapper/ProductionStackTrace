using Pillar.Demystifier;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace ProductionStackTrace {
	internal interface IBuilderProxy {
		IBuilderProxy Append(in string text);
		IBuilderProxy AppendFilename(in string filename, in int lineNumber = 0);
		IBuilderProxy AppendLine(in string text);
		IBuilderProxy AppendLine();
		IBuilderProxy AppendMessage(in string message);
		IBuilderProxy AppendMethodInfo(ResolvedMethod method);

	}
	internal class StyledBuilderProxy : IBuilderProxy {
		private StyledBuilder builder;
		private StyledBuilderOption option;
		public override string ToString() => builder.ToString();
		public override int GetHashCode() => builder.GetHashCode();

		public StyledBuilderProxy(StyledBuilder builder, StyledBuilderOption option = null) {
			this.builder = builder;
			this.option = option ?? StyledBuilderOption.GlobalOption;
		}
		public IBuilderProxy Append(in string text) {
			builder.Append(text);
			return this;
		}

		public IBuilderProxy AppendLine(in string text) {
			builder.AppendLine(text);
			return this;
		}

		public IBuilderProxy AppendLine() {
			builder.AppendLine();
			return this;
		}

		public IBuilderProxy AppendMethodInfo(ResolvedMethod method) {
			method.Append(builder, option);
			return this;
		}

		public IBuilderProxy AppendMessage(in string message) {
			builder.Append(option.MessageStyle, message);
			return this;
		}

		public IBuilderProxy AppendFilename(in string filename, in int lineNumber = 0) {
			this.Append(" in ");
			builder.AppendPath(
                        option.SourcePathStyle,
                        option.SourceFileStyle,
                        filename,option.ShortenSourceFilePath);

                if (lineNumber != 0)
                {
                    builder.Append(":line ");
                    builder.Append(option.LineNumberStyle,lineNumber.ToString());
                }
			return this;
		}
	}
	internal class StringBuilderProxy(StringBuilder builder) : IBuilderProxy {
		public IBuilderProxy Append(in string text) {
			builder.Append(text);
			return this;
		}
		public IBuilderProxy AppendLine() {
			builder.AppendLine();
			return this;
		}
		public IBuilderProxy AppendFormat(IFormatProvider provider, in string format, params object[] args) {
			builder.AppendFormat(provider, format, args);
			return this;
		}

		public IBuilderProxy AppendLine(in string text) {
			builder.AppendLine(text);
			return this;
		}
		public IBuilderProxy AppendMethodInfo(ResolvedMethod method) {
			method.Append(builder);
			return this;
		}
		public override string ToString() => builder.ToString();
		public override int GetHashCode() => builder.GetHashCode();

		public IBuilderProxy AppendMessage(in string message) => Append(message);

		public IBuilderProxy AppendFilename(in string filename, in int lineNumber = 0) => Append($" in {(filename)}{(lineNumber == 0 ? "" : $":line {lineNumber}")}");
	}
}
