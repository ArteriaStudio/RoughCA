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
		}
		public long OrgKey { get; set; }
		public string OrgName { get; set; }
		public string OrgUnitName { get; set; }
		public string LocalityName { get; set; }
		public string ProvinceName { get; set; }
		public string CountryName { get; set; }
		public string ServerName { get; set; }
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
			if (IsNotNull(ServerName) == false)
			{
				return (false);
			}
			return (true);
		}

		//　データベースから入力
		public bool Load(SQLContext pSQLContext, int iUserIdentity)
		{
			var pSQL = "SELECT OrgKey, OrgName, OrgUnitName, LocalityName, ProvinceName, CountryName, ServerName, UpdateAt FROM TOrgProfile WHERE OrgKey = @OrgKey";
			using (var pCommand = new NpgsqlCommand(pSQL, pSQLContext.m_pConnection))
			{
				pCommand.Parameters.Clear();
				pCommand.Parameters.AddWithValue("OrgKey", iUserIdentity);
				using (var pReader = pCommand.ExecuteReader())
				{
					while (pReader.Read())
					{
						OrgKey       = pReader.GetInt64(0);
						OrgName      = pReader.GetString(1);
						OrgUnitName  = pReader.GetString(2);
						LocalityName = pReader.GetString(3);
						ProvinceName = pReader.GetString(4);
						CountryName  = pReader.GetString(5);
						ServerName   = pReader.GetString(6);
						UpdataAt     = pReader.GetDateTime(7);
					}
				}
			}

			return (true);
		}

		//　データベースに保存
		public bool Save(SQLContext pSQLContext)
		{
			var pSQL = "INSERT INTO TOrgProfile VALUES (@OrgKey, @OrgName, @OrgUnitName, @LocalityName, @ProvinceName, @CountryName, @ServerName, now())";
			pSQL += " ON CONFLICT ON CONSTRAINT torgprofile_pkey DO UPDATE SET OrgName = @OrgName, OrgUnitName = @OrgUnitName, LocalityName = @LocalityName, ProvinceName = @ProvinceName, CountryName = @CountryName, ServerName = @ServerName, UpdateAt = now()";
			using (var pCommand = new NpgsqlCommand(pSQL, pSQLContext.m_pConnection))
			{
				pCommand.Parameters.Clear();
				pCommand.Parameters.AddWithValue("OrgKey", OrgKey);
				pCommand.Parameters.AddWithValue("OrgName", OrgName);
				pCommand.Parameters.AddWithValue("OrgUnitName", OrgUnitName);
				pCommand.Parameters.AddWithValue("LocalityName", LocalityName);
				pCommand.Parameters.AddWithValue("ProvinceName", ProvinceName);
				pCommand.Parameters.AddWithValue("CountryName", CountryName);
				pCommand.Parameters.AddWithValue("ServerName", ServerName);
				pCommand.ExecuteNonQuery();
			}
			return (true);
		}
	}
}
