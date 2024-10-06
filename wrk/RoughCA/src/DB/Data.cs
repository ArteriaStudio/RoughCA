using System.Text.RegularExpressions;

namespace Arteria_s.DB
{
	public abstract class Data
	{
		public static bool IsNotNull(string pText)
		{
			if (pText == null)
			{
				return (false);
			}
			if (pText.Length <= 0)
			{
				return (false);
			}
			return (true);
		}

		public static bool IsNull(string pValue)
		{
			if (pValue == null)
			{
				return (true);
			}
			pValue = pValue.Trim();
			if (pValue.Length <= 0)
			{
				return (true);
			}
			return (false);
		}

		//　メールアドレスの書式検査
		public static bool IsValidMail(string pValue)
		{
			if (IsNotNull(pValue) == false)
			{
				return (false);
			}
			var pRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
			if (pRegex.IsMatch(pValue) == false)
			{
				return (false);
			}

			return (true);
		}

		//　メールアドレスの書式検査
		public static bool IsValidFQDN(string pValue)
		{
			if (IsNotNull(pValue) == false)
			{
				return (false);
			}
			var pRegex = new Regex(@"[^@\s]+\.[^@\s]+$");
			if (pRegex.IsMatch(pValue) == false)
			{
				return (false);
			}

			return (true);
		}

		public static bool IsValidCommonName(string pValue)
		{
			if (IsNotNull(pValue) == false)
			{
				return (false);
			}
			if (pValue.IndexOf(';') != -1)
			{
				//　使用不可文字を検出
				return (false);
			}

			return (true);
		}

		public abstract bool Validate();
	}
}
