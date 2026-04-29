using Arteria_s.DB;
using Arteria_s.DB.Base;
using Npgsql;
using System;

namespace Arteria_s.App.RoughCA
{
	//　組織プロファイル
	public class OrgProfile : Data
	{
		public OrgProfile()
		{
			OrgKey = 0;
			SerialNumber = 0;
		}
//		public string TrustName { get; set; }	//　ルート認証局名
//		public string IssueName { get; set; }	//　発行認証局名
		public long OrgKey { get; set; }
		public string OrgName { get; set; }
		public string OrgUnitName { get; set; }
		public string LocalityName { get; set; }
		public string ProvinceName { get; set; }
		public string CountryName { get; set; }
		public string ServerName { get; set; }
		public long SerialNumber { get; set; }
		public DateTime UpdataAt { get; set; }

		public override bool Validate()
		{
			if (IsNotNull(OrgName) == false)
			{
				return (false);
			}
			if (IsNotNull(OrgUnitName) == false)
			{
				return (false);
			}
			if (IsNotNull(LocalityName) == false)
			{
				return (false);
			}
			if (IsNotNull(ProvinceName) == false)
			{
				return (false);
			}
			if (IsNotNull(CountryName) == false)
			{
				return (false);
			}
			if (IsValidCountryCode(CountryName) == false)
			{
				return (false);
			}
			if (IsNotNull(ServerName) == false)
			{
				return (false);
			}
			return (true);
		}
	}
}
