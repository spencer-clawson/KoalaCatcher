using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace KoalaCatcher
{
	public class FileWatcher
	{
		public FileWatcher(string directory, string fileType)
		{
			if (string.IsNullOrEmpty(directory))
			{
				Directory = System.IO.Directory.GetCurrentDirectory();
			}
			else if (!directory.Contains("\\\\") && !directory.Contains(":"))  //then it must be a relative path
			{
				Directory = System.IO.Directory.GetCurrentDirectory() + "\\" + directory.Replace("\\", "");
			}
			else
			{
				Directory = directory;
			}

			if (string.IsNullOrEmpty(fileType)) fileType = "*.txt";
			if (!fileType.Contains(".")) fileType = "." + fileType;
			if (!fileType.Contains("*")) fileType = "*" + fileType;
			FileType = fileType;
		}
		public string Directory { get; }
		public string FileType { get; }
		
		private bool _checking = false;
		
		private readonly Dictionary<string, FileLineCount> _fileLineCounts = new Dictionary<string, FileLineCount>();  //key is the file path
		private class FileLineCount
		{
			public DateTime Modified;
			public long LineCount;
			public string OriginalCaseName;
		}

		/// <summary>
		/// Checks the FileWatcher.Directory for changes to Directory.FileType files and returns the changes
		/// </summary>
		public string CheckFiles()
		{
			if (string.IsNullOrEmpty(Directory)) return string.Empty;

			if (!System.IO.Directory.Exists(Directory)) return $"Can not access {Directory}, or it does not exist.";

			if (_checking) return string.Empty;  //if the check takes long time we don't want to keep firing up threads before it's completed
			_checking = true;
			
			var threads = new List<Thread>();
			var result = new StringBuilder();
		
			try
			{
				//check for new or modified files
				var files = System.IO.Directory.GetFiles(Directory, FileType);
				foreach (var file in files)
				{
					FileLineCount fileLineCount;
					var fileInfo = new FileInfo(file);
					bool newFile = false, modified = false;

					//exisiting files
					if (_fileLineCounts.TryGetValue(file.ToUpper(), out fileLineCount))
					{
						if (fileInfo.LastWriteTime != fileLineCount.Modified)
						{
							modified = true;
							fileLineCount.Modified = fileInfo.LastWriteTime;
						}
					}
					//new files
					else
					{
						newFile = true;
						fileLineCount = new FileLineCount()
						{
							Modified = fileInfo.LastWriteTime,
							OriginalCaseName = fileInfo.Name
						};
						_fileLineCounts.Add(file.ToUpper(), fileLineCount);
						
					}

					if (newFile || modified)
					{
						var thread = new Thread(() =>
						{
							var lines = CountLines(file);
							result.AppendLine(FormatResult(newFile ? FileStatus.New : FileStatus.Exisiting, fileInfo.Name, fileLineCount.LineCount, lines));
							fileLineCount.LineCount = lines;
						});
						thread.Start();
						threads.Add(thread);
					}
				}

				// check for deleted files
				var upperFiles = files.Select(f => f.ToUpper()).ToArray();
				var deleted = new List<string>();
				foreach (var fileStat in _fileLineCounts)
				{
					if (!upperFiles.Contains(fileStat.Key))
					{
						result.AppendLine(FormatResult(FileStatus.Deleted, fileStat.Value.OriginalCaseName, 0, 0));
						deleted.Add(fileStat.Key);
					}
				}
				
				foreach (var file in deleted)
				{
					_fileLineCounts.Remove(file);
				}


				foreach (var thread in threads)
				{
					thread.Join();
				}
			}
			catch (Exception ex)
			{
				//Log ex
				result.AppendLine("Error: " + ex.Message);
			}

			_checking = false;
			return result.ToString();
		}

		
		
		public enum FileStatus
		{
			New, Exisiting, Deleted
		}
		public static string FormatResult(FileStatus fileStatus, string name, long oldCount, long newCount)
		{
			switch (fileStatus)
			{
				case FileStatus.New:
					return $"{name} {newCount}";
				case FileStatus.Exisiting:
					var lines = newCount - oldCount;
					var plusSign = lines > 0 ? "+" : "";
					return $"{name} {plusSign}{lines}";
				case FileStatus.Deleted:
					return name;
			}

			return string.Empty;
		}
		

		public static long CountLines(string file)
		{
			if (string.IsNullOrEmpty(file)) return 0;

			long count = 0;

			try
			{
				using (var stream = OpenFile(file))
				{
					while (stream.ReadLine() != null)
					{
						count++;
					}
				}				
			}
			catch (Exception ex)
			{
				//Log ex
				Console.WriteLine(ex.Message);
			}

			return count;
		}

		/// <summary>
		/// Tries for up to 5 seconds to open the given file
		/// </summary>
		/// <exception cref="Exception">If the file is unable to be opened</exception>
		public static StreamReader OpenFile(string file)
		{
			const int timeoutSeconds = 5;

			for (int i = 0; i < timeoutSeconds * 2; i++)
			{
				try
				{
					return new StreamReader(file);
				}
				catch
				{
					Thread.Sleep(500);
				}
			}

			throw new TimeoutException($"Unable to open file {file} for {timeoutSeconds} seconds");
		}
	}
}
