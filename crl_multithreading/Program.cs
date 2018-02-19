/*
 * Создано в SharpDevelop.
 * Пользователь: artem279
 * Дата: 18.01.2018
 * Время: 11:14
 * 
 * Для изменения этого шаблона используйте меню "Инструменты | Параметры | Кодирование | Стандартные заголовки".
 */
using System;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;

namespace crl_multithreading
{
	
	public struct StateInfo
	{
		public string filename; //file
		public string wpath; //workpath
		public Mutex locker; //mutex (lock object)
	}
	
	class Program
	{
		
		public static List<string> files = new List<string>();
		
		//сигнал для потока
		static ManualResetEvent _doneEvent = new ManualResetEvent(false);
		
		//Общее кол-во потоков к обработке
		static int numberOfThreads;
		
		public static List<string> GetFileInSubDir(string dir)
		{
			string file = "";
			foreach (string f in Directory.GetFiles(dir))
			{
				FileInfo fi = new FileInfo(f);
				if(fi.Extension == ".crl")
				{
					Console.WriteLine("Добавляем файл в коллекцию: {0}",f);
					file = f;
					files.Add(file);
				}
			}
			
			foreach (string d in Directory.GetDirectories(dir))
			{
				GetFileInSubDir(d);
			}
			
			return files;
		}
		

		static void ThreadProcess(Object stateInfo)
        {
			StateInfo s = (StateInfo)stateInfo;
            try
				{

					X509CRL2 crlfile = new X509CRL2(s.filename);
					List<string> data = new List<string>();
					foreach(var rl in crlfile.RevokedCertificates)
					{
						string [] parts = crlfile.IssuerName.Name.Split(',');
						string CNuc = "";
						foreach(string p in parts)
						{
							if(p.Contains("CN=")) {CNuc = p.Replace("CN=","").Trim().Replace(";"," ").Replace("|","").Replace("\n"," ").Replace("\t", " ").Replace("NULL","").Replace("\"","");}
						}
						Console.WriteLine("{0} {1} {2} {3} {4}", CNuc, rl.ReasonCode, rl.ReasonMessage, rl.SerialNumber, rl.RevocationDate);
						data.Add(CNuc +"|"+ rl.ReasonCode +"|"+ rl.ReasonMessage +"|"+ rl.SerialNumber +"|"+ rl.RevocationDate);
					}
					s.locker.WaitOne();
						Thread.Sleep(50);
						using (StreamWriter crlwriter = new StreamWriter(s.wpath + "RevocationList.csv",true,Encoding.Default))
						{
							foreach(var line in data)
							{
								crlwriter.WriteLine(line);
							}
							crlwriter.Flush();
						}
						Console.WriteLine("Thread is done!");

					s.locker.ReleaseMutex();
				}
            catch { Console.WriteLine("Something wrong"); }
			finally { if (Interlocked.Decrement(ref numberOfThreads) == 0) { _doneEvent.Set(); } }
        }
		
		
		/// <summary>
		/// Хост-процедура
		/// </summary>
		/// <param name="numthreads">Кол-во одновременно работающих потоков (максимальное кол-во потоков)</param>
		/// <param name="fileslist">список файлов к обработке</param>
		/// <param name="workpath">рабочая директория программы</param>
		public static void createthreadparser(int numthreads, List<string> fileslist, string workpath)
		{
			Mutex mut = new Mutex();
			ThreadPool.SetMaxThreads(numthreads, numthreads);
			ThreadPool.SetMinThreads(numthreads, numthreads);
			
			numberOfThreads = fileslist.Count;
			
			foreach (var file in fileslist)
			{
				StateInfo s = new StateInfo {wpath = workpath, locker = mut, filename = file};
				ThreadPool.QueueUserWorkItem(new WaitCallback(ThreadProcess), (object)s);
			}
			
			_doneEvent.WaitOne();
		}
		
		public static void Main(string[] args)
		{
			//задаём рабочую директорию
			string path = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)+"\\";
			List<string> lst = new List<string>();
			Stopwatch watch = new Stopwatch();
			lst = GetFileInSubDir("C:\\CRLPath\\");
			watch.Start();
			createthreadparser(15, lst, path);
			watch.Stop();
			TimeSpan ts = watch.Elapsed;
        	string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
        	Console.WriteLine("RunTime " + elapsedTime);
        	File.AppendAllText(Path.Combine(path, "timelog.log"), "Runtime: " + elapsedTime + Environment.NewLine, Encoding.Default);
			Console.Write("Press any key to continue . . . ");
			Console.ReadKey(true);
		}
	}
}