using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using JitMagic.MVVMLibLite;

namespace ProductionStackTrace.Analyze.WPF.ViewModels {
	public class MainViewModel : OurViewModelBase {
		public MainViewModel() {
			ExecPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"ProductionStackTrace.Analyze.Console.exe");
			TermCmdLine = ExecPath;
			var args = Environment.GetCommandLineArgs();
			var lastWasSym = false;
			_SymbolPaths="";
			foreach (var arg in args){
				if (arg == "-s")
					lastWasSym = true;
				else if (lastWasSym){
					_SymbolPaths += arg + "; ";
					lastWasSym = false;
				}
			}
			if (! string.IsNullOrWhiteSpace(_SymbolPaths))
				SymbolPaths = _SymbolPaths.TrimEnd(' ', ';');
			

		}
		private string ExecPath;
		private string CLIArgs;
		public string TermCmdLine {
			get => _TermCmdLine;
			set => Set(ref _TermCmdLine, value);
		}
		private string _TermCmdLine;


		public string input {
			get => _input;
			set {
				if (Set(ref _input, value))
					ConvertInput();
			}
		}
		private string _input;


		public string SymbolPaths {
			get => _SymbolPaths;
			set {
				if (Set(ref _SymbolPaths, value)) {
					UpdateSymbols();
				}
			}
		}

		private void UpdateSymbols() {
			var paths = SymbolPaths.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			CLIArgs = "";
			if (paths.Any()) {
				CLIArgs += String.Join(" ", paths.Select(p => $"-s \"{p}\""));
			}
			TermCmdLine = ExecPath + (String.IsNullOrWhiteSpace(CLIArgs) ? "" : " " + CLIArgs);
			if (ModeTerminal)
				CLIRestartCmd.Execute();
			else if (!String.IsNullOrWhiteSpace(input))
				ConvertInput();

		}

		private async Task ConvertInput() {
			// Runs the process specified by TermCmdLine in a hidden window.  Writes the text from "input" to the process's stdin then close stdin.  Capture all the output from the program and put it into the output property. Use Async functions when possible.
			if (String.IsNullOrWhiteSpace(input)) {
				output = "";
				return;
			}

			try {
				// Create process info with the command line
				var processInfo = new System.Diagnostics.ProcessStartInfo {
					FileName = ExecPath,
					Arguments = CLIArgs,
					UseShellExecute = false,
					CreateNoWindow = true,
					RedirectStandardInput = true,
					RedirectStandardOutput = true,
					RedirectStandardError = true
				};

				// Start the process
				using var process = new System.Diagnostics.Process();
				process.StartInfo = processInfo;
				process.EnableRaisingEvents = true;

				var outputBuilder = new StringBuilder();
				var errorBuilder = new StringBuilder();

				// Set up output handlers
				process.OutputDataReceived += (sender, e) => {
					if (e.Data != null)
						outputBuilder.AppendLine(e.Data);
				};

				process.ErrorDataReceived += (sender, e) => {
					if (e.Data != null)
						errorBuilder.AppendLine(e.Data);
				};

				// Start process and readers
				process.Start();
				process.BeginOutputReadLine();
				process.BeginErrorReadLine();

				// Write input to process
				if (!string.IsNullOrEmpty(input)) {
					await process.StandardInput.WriteAsync(input);
					await process.StandardInput.FlushAsync();
					process.StandardInput.Close();
				}

				// Wait for the process to complete
				await process.WaitForExitAsync();

				// Combine standard output and error
				var combinedOutput = outputBuilder.ToString();
				if (errorBuilder.Length > 0) {
					combinedOutput += Environment.NewLine + errorBuilder.ToString();
				}

				// Update the output property
				output = combinedOutput;
			} catch (Exception ex) {
				output = $"Error: {ex.Message}";
			}
		}

		private string _SymbolPaths;



		public bool ModeUI {
			get => _ModeUI;
			set {
				if (Set(ref _ModeUI, value)) {
					ModeTerminal = !value;
					RaisePropertyChanged(() => ModeUIVisible); //this will handle both dont need in other
					RaisePropertyChanged(() => ModeTerminalVisible);
				}
			}
		}
		private bool _ModeUI = true;

		public bool ModeTerminal {
			get => _ModeTerminal;
			set {
				if (Set(ref _ModeTerminal, value))
					ModeUI = !value;

			}
		}
		private bool _ModeTerminal;

		public Visibility ModeUIVisible => ModeUI ? Visibility.Visible : Visibility.Collapsed;
		public Visibility ModeTerminalVisible => ModeTerminal ? Visibility.Visible : Visibility.Collapsed;

		public EasyWindowsTerminalControl.EasyTerminalControl terminal;

		public OurCommand CLIRestartCmd => GetOurCmd(CLIRestart);
		public async Task CLIRestart() {
			await terminal.RestartTerm();
		}
		public OurCommand CLIClearCmd => GetOurCmdSync(CLIClear);
		public void CLIClear() {
			terminal.ConPTYTerm.ClearUITerminal();
		}

		public string output {
			get => _output;
			set => Set(ref _output, value);
		}
		private string _output;



	}
}
