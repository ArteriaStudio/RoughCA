using System;
using System.IO;

namespace Arteria_s.OS.Base
{
	public static class DirectoryHelper
	{
		public static bool CreateDirectorySafe(String pPath)
		{
			if (Directory.Exists(pPath) == true)
			{
				return(true);
			}
			try
			{
				var pDirectoryInfo = Directory.CreateDirectory(pPath);
				return(true);
			} catch {
				;
			}
			return (false);
		}
	}
}
