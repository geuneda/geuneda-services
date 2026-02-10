using System;
using System.Diagnostics;

namespace Geuneda.Services.Editor
{
	/// <summary>
	/// 터미널에서 사용되는 git 커맨드 프로세스를 실행합니다.
	/// </summary>
	/// <author>
	/// https://blog.somewhatabstract.com/2015/06/22/getting-information-about-your-git-repository-with-c/
	/// </author>
	public class GitEditorProcess : IDisposable
	{
		private const string DefaultPathToGitBinary = "git";

		private readonly Process Process;

		/// <summary>
		/// <inheritdoc cref="Process.ExitCode"/>
		/// </summary>
		public int ExitCode => Process.ExitCode;

		public GitEditorProcess(string workingDir, string pathToGitBinary = DefaultPathToGitBinary)
		{
			var startInfo = new ProcessStartInfo
			{
				UseShellExecute = false,
				RedirectStandardOutput = true,
				FileName = pathToGitBinary,
				CreateNoWindow = true,
				WorkingDirectory = workingDir
			};

			Process = new Process { StartInfo = startInfo };
		}

		/// <summary>
		/// 이 Unity 프로젝트가 git 저장소인지 확인합니다
		/// </summary>
		public bool IsValidRepo()
		{
			return ExecuteCommand("rev-parse --is-inside-work-tree") == "true";
		}

		/// <summary>
		/// Unity 프로젝트의 git 브랜치 이름을 가져옵니다.
		/// </summary>
		public string GetBranch()
		{
			return ExecuteCommand("rev-parse --abbrev-ref HEAD");
		}

		/// <summary>
		/// Unity 프로젝트의 git 커밋 해시를 가져옵니다.
		/// </summary>
		public string GetCommitHash()
		{
			return ExecuteCommand($"rev-parse HEAD");
		}

		/// <summary>
		/// 주어진 커밋 시점의 상태와 현재 작업 디렉토리의 차이를 가져옵니다.
		/// </summary>
		public string GetDiffFromCommit(string commitHash)
		{
			return ExecuteCommand($"diff --word-diff=porcelain {commitHash} -- {Process.StartInfo.WorkingDirectory}");
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			Process?.Dispose();
		}
		
		/// <summary>
		/// 커맨드를 실행합니다. 예: "status --verbose"
		/// </summary>
		private string ExecuteCommand(string args)
		{
			Process.StartInfo.Arguments = args;
			Process.Start();
			var output = Process.StandardOutput.ReadToEnd().Trim();
			Process.WaitForExit();
			return output;
		}
	}
}
