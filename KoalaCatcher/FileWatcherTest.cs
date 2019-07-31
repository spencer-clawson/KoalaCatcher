using System;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace KoalaCatcherTest
{
	[TestFixture]
	public class FileWatcher
	{
		/*
		 * The program takes 2 arguments, the directory to watch and a file pattern, example: program.exe "c:\file folder" *.txt
		
		The path may be an absolute path, relative to the current directory, or UNC.
		Use the modified date of the file as a trigger that the file has changed.
		Check for changes every 10 seconds.
		When a file is created output a line to the console with its name and how many lines are in it.
		When a file is modified output a line with its name and the change in number of lines (use a + or - to indicate more or less).
		When a file is deleted output a line with its name.
		Files will be ASCII or UTF-8 and will use Windows line separators (CR LF).
		Multiple files may be changed at the same time, can be up to 2 GB in size, and may be locked for several seconds at a time.
		Use multiple threads so that the program doesn't block on a single large or locked file.
		Program will be run on Windows 10.
		File names are case insensitive.
		 */

		[Test]
		public void CheckFiles()
		{
			CheckFiles("","");
			CheckFiles("","txt");
			CheckFiles("",".txt");
			CheckFiles("","*.txt");
			CheckFiles("","*.csv");
			CheckFiles(Path.GetTempPath(), "*.txt");
			//CheckFiles("\\\\guppy\\pub\\spencer\\","");
			//Directory.SetCurrentDirectory("C:\\users");
			//CheckFiles("Spencer","");
			
		}

		public void CheckFiles(string directory, string fileType)
		{
			var fileWatcher = new KoalaCatcher.FileWatcher(directory, fileType);
			fileWatcher.CheckFiles();

			var file = CreateTestFile(400);
			var info = new FileInfo(file);
			var renamedFile = $"{fileWatcher.Directory}\\{info.Name.Replace(info.Extension,"")}{fileWatcher.FileType.Replace("*","")}";

			info.MoveTo(renamedFile);
			Assert.AreEqual($"{info.Name} 400\r\n", fileWatcher.CheckFiles());
			
			using (var writer = File.AppendText(renamedFile))
			{
				writer.WriteLine();
			}
			Assert.AreEqual($"{info.Name} +1\r\n", fileWatcher.CheckFiles());
			
			File.Delete(renamedFile);
			Assert.AreEqual($"{info.Name}\r\n", fileWatcher.CheckFiles());
		}

		[Test]
		public void OpenFile()
		{
			var file = CreateTestFile(20);

			//does it open files
			using (var stream = KoalaCatcher.FileWatcher.OpenFile(file))
			{
				Assert.IsNotNull(stream);
			}

			//if we lock the file for a few seconds, will it still open
			Thread lockThread = new Thread(new ThreadStart(() =>
			{
				using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None))
				{
					Thread.Sleep(2000);
				}
			}));
			Thread testThread = new Thread(new ThreadStart(() =>
			{
				Assert.DoesNotThrow(() =>
				{
					using (var stream = KoalaCatcher.FileWatcher.OpenFile(file))
					{
						Assert.IsNotNull(stream);
					}
				});
				
			}));
			lockThread.Start();
			testThread.Start();
			lockThread.Join();
			testThread.Join();


			//if we lock the file for a "long" time, it will fail
			lockThread = new Thread(new ThreadStart(() =>
			{
				using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None))
				{
					Thread.Sleep(10000);
				}
			}));
			testThread = new Thread(new ThreadStart(() =>
			{
				Assert.Catch(() =>
				{
					using (var stream = KoalaCatcher.FileWatcher.OpenFile(file)) { }
				});
				
			}));
			lockThread.Start();
			testThread.Start();
			lockThread.Join();
			testThread.Join();
			

			File.Delete(file);
		}

		[Test]
		public void CountLines()
		{
			var file = CreateTestFile(20);
			Assert.AreEqual(20L, KoalaCatcher.FileWatcher.CountLines(file));
			File.Delete(file);

			file = CreateTestFile(4000000);
			var start = DateTime.Now;
			Assert.AreEqual( 4000000L, KoalaCatcher.FileWatcher.CountLines(file));
			
			var info = new FileInfo(file);
			Console.WriteLine($"It took {(DateTime.Now - start).TotalMilliseconds} ms to count the lines in a {info.Length / 1024} KB file.");

			File.Delete(file);
		}

		[Test]
		public void FormatResult()
		{
			Assert.AreEqual(
				"file1",
				KoalaCatcher.FileWatcher.FormatResult(KoalaCatcher.FileWatcher.FileStatus.Deleted, "file1", 0, 0));

			Assert.AreEqual(
				"file1 100",
				KoalaCatcher.FileWatcher.FormatResult(KoalaCatcher.FileWatcher.FileStatus.New, "file1", 0, 100));

			Assert.AreEqual(
				"file1 +10",
				KoalaCatcher.FileWatcher.FormatResult(KoalaCatcher.FileWatcher.FileStatus.Exisiting, "file1", 90, 100));

			Assert.AreEqual(
				"file1 -10",
				KoalaCatcher.FileWatcher.FormatResult(KoalaCatcher.FileWatcher.FileStatus.Exisiting, "file1", 100, 90));
		}


		

		static string CreateTestFile(int numberOfLines)
		{
			var file = Path.GetTempFileName();

			string g = string.Join("", Enumerable.Repeat(new Guid().ToString("n"), 20));

			try
			{
				using (StreamWriter sw = File.AppendText(file))
				{
					for (int x = 0; x < numberOfLines; x++)
					{
						sw.WriteLine(g);
					}
				}
			}
			catch (Exception ex)
			{
				
			}

			return file;
		}
	}
}
