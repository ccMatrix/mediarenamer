// *******************************************************************************
//  Title:			Parser.cs
//  Description:	Parses through the folder structure to find episodes and 
//					queries them using the OnlineParser
//  Author:			Benjamin Schirmer (www.codename-matrix.de)
// *******************************************************************************

using System;
using System.IO;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using MediaRenamer;

namespace TVShowRenamer
{
	/// <summary>
	/// Parser Class for TVShows
	/// </summary>
	public class Parser
	{
		private String seriesPath = "";
		private ListBox output = null;

		// This are the regular Expressions to find season and episode number
		private static String[] regEx = {	
									@"([0-9]+)x([0-9]+)-([0-9]+)",
									@"s([0-9]+)e([0-9]+)",
									@"s([0-9]+) e([0-9]+)",
									@"([0-9]+)x([0-9]+)", 
									@"([0-9]{1,})([0-9]{2})"
								 };

		// Valid extensions
		public static String[] extension = { ".avi", ".mkv", ".mpg", ".mpeg", ".mov", ".wmv",
										 ".rpm", ".ogm", ".srt", ".sub", ".mp4" };

		public Parser(String path, ListBox o)
		{
			if (!path.EndsWith(@"\")) path += @"\";
			seriesPath = path;
			output = o;
		}

		public Parser()
		{
		}

		/// <summary>
		/// Checks for valid extensions
		/// </summary>
		/// <param name="name">filename</param>
		/// <returns></returns>
		private bool isValidExt(String name)
		{
			name = name.ToLower();
			foreach (String ext in extension)
			{
				if (name.EndsWith(ext)) return true;
			}
			return false;
		}

		/// <summary>
		/// Scan folder. Will be called recursively to find alle episodes
		/// </summary>
		/// <param name="folder">folder to scan</param>
		private void scanFolder(String folder)
		{
			String[] elements;
			// First, let's scan the files ...
			elements = Directory.GetFiles(folder);
			Log.Add(folder);
			for (int i=0; i<elements.Length; i++)
			{
				if (isValidExt(elements[i]))
				{
					FileAttributes fAttr = File.GetAttributes(elements[i]);
					if ( (fAttr | FileAttributes.ReadOnly) 
						== fAttr)
					{
						//MessageBox.Show("File is write protected: \n"+elements[i]);
					}
					else
					{
						Episode ep = parseFile(elements[i]);
						if (ep == null) continue;
						if (!ep.special && ep.needRenaming())
						{
							writeEpisode( ep );
						}
					}
				}
			}
			// ... then scan all the directories
			elements = Directory.GetDirectories(folder);
			for (int i=0; i<elements.Length; i++)
			{
				scanFolder(elements[i]);
			}
		}

		/// <summary>
		/// Parses a filename for episode information
		/// </summary>
		/// <param name="file">complete filename</param>
		/// <returns>filled Episode class</returns>
		public Episode parseFile(String file)
		{
			Episode ep = new Episode(file);
			FileInfo fi = new FileInfo(file);
			try
			{	
				String dir = "";
				dir = fi.Directory.FullName;
				while (true)
				{
					if (dir == null) break;
					DirectoryInfo di = new DirectoryInfo(dir);
					// Skip those folders since they probably contain no episodes but extras/bonus material
					if ( (di.Name.ToLower().IndexOf("season") != 0) &&
                        (di.Name.ToLower().IndexOf("series") != 0) &&
                        (di.Name.ToLower().IndexOf("extra") != 0) &&
                        (di.Name.ToLower().IndexOf("special") != 0) &&
                        (di.Name.ToLower().IndexOf("bonus") != 0) &&
                        (di.Name.ToLower().IndexOf("dvd") != 0)
						) 
					{
						ep.series = di.Name;
						Regex reg = new Regex("([0-9]{4})");
						Match m = null;
						m = reg.Match(ep.series);
						if (m.Success)
						{
							int year = Int32.Parse(m.Groups[1].Captures[0].Value);
							if (year <= DateTime.Now.Year)
							{
								ep.year = year;
								ep.series = ep.series.Replace(m.Groups[1].Captures[0].Value, "");
							}
						}
						ep.series = ep.series.Replace("()", "");
						break;
					}
					else
					{
						dir = di.Parent.FullName;
					}
				}

				String title = "";
				String series = "";
				String name = fi.Name;
				bool matched = false;
				// Scan for season and episode
				String[] regExp = Parser.regEx;
				foreach (String pat in regExp)
				{
					Regex reg = new Regex(pat);
					Match m = null;
					m = reg.Match(name.ToLower());
					if (m.Success)
					{
						ep.season = Int32.Parse(m.Groups[1].Captures[0].Value);
						ep.episode = Int32.Parse(m.Groups[2].Captures[0].Value);
						int[] eps = new int[m.Groups.Count-2];
						for (int i=0; i<eps.Length; i++)
						{
							eps[i] = Int32.Parse(m.Groups[i+2].Captures[0].Value);
						}
						ep.episodes = eps;
						
						series = name.Substring( 0, name.ToLower().IndexOf(m.Value));
						//series = series.Replace(".", " ");
						series = eregi_replace("([a-zA-Z]{1})([0-9]{1})", "\\1 \\2", series);
						series = eregi_replace("([0-9]{1})([a-zA-Z]{1})", "\\1 \\2", series);
						series = series.Replace("_", " ");
						series = series.Replace("  ", " ");
						series = series.Replace(" - ", " ");
						series = series.Replace("[", "");
						series = series.Replace("]", "");
						series = series.Replace("(", "");
						series = series.Replace(")", "");
						series = series.Trim();
						ep.altSeries = ep.series;
						ep.series = series;
						if (ep.series == "") ep.series = ep.altSeries;
						
						title = name.Substring( name.ToLower().IndexOf(m.Value)+m.Value.Length );
						title = title.Replace(" - ", "");
						title = title.Trim();
						title = title.Substring(0, title.LastIndexOf("."));
						ep.title = title;
						
						// find title using online parser
						OnlineParser oParse = new OnlineParser();
						oParse.getEpisodeData(ref ep);
						
						matched=true;
						
						break;
					}
				}
				// no matching data found. try to find title of episode anyway
				if (!matched)
				{
					ep.season = 0;
					ep.episode = 0;
					if (file.IndexOf("-") > 0)
					{
						title = file.Substring( file.IndexOf("-")+1 );
						title = title.Trim();
						title = title.Substring(0, title.LastIndexOf("."));
						ep.title = title;
					}
				}
			}
			catch (Exception E)
			{
				Log.Add("ParseFile("+ep.filename+"): "+E.Message);
			}

			return ep;
		}

		/// <summary>
		/// Verify movie name is a TV episode
		/// If it has a season and episode numbers its possibly an episode.
		/// </summary>
		/// <param name="file">filename</param>
		/// <returns>true if valid, false if invalid</returns>
		public static bool validEpisodeFile(String file)
		{
			String[] regEx2 = Parser.regEx;
			FileInfo fi = new FileInfo(file);
			foreach (String pat in regEx2)
			{
				Regex reg = new Regex(pat);
				Match m = null;
				m = reg.Match(fi.Name);
				if (m.Success)
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Replace regular expression in string
		/// </summary>
		/// <param name="pat">Search pattern</param>
		/// <param name="newStr">New String. \\0..\\n for found values</param>
		/// <param name="input">Inputstring</param>
		/// <returns>modified String</returns>
		public static String eregi_replace(String pat, String newStr, String input)
		{
			Regex reg = new Regex(pat, RegexOptions.IgnoreCase | RegexOptions.None);
			Match m = null;
			m = reg.Match(input);
			if (m.Success)
			{
				for (int i=0; i<m.Groups.Count; i++)
				{
					newStr = newStr.Replace("\\"+i, m.Groups[i].Captures[0].Value);
				}
				return input.Replace(m.Groups[0].Captures[0].Value, newStr);
			}
			else
			{
				return input;
			}
		}

		/// <summary>
		/// Add episode to output listbox
		/// </summary>
		/// <param name="ep"></param>
		private void writeEpisode(Episode ep)
		{
			output.Items.Add(ep);
		}

		/// <summary>
		/// Executes the actual scan
		/// </summary>
		public void startScan()
		{
			if (seriesPath == "")
				return;
			scanFolder(seriesPath);
		}

	}
}