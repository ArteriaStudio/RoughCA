using Arteria_s.DB;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Arteria_s.App.RoughCA
{
	public class DbParams : Data
	{
		public DbParams()
		{
			UserIdentity = -1;
			HostName     = "";
			InstanceName = "";
			SchemaName   = "";
			ClientKey    = "";
			ClientCrt    = "";
			TrustCrt     = "";
			IdentityName = "";
			TrustName    = "";
			IssueName    = "";
			DriverName   = "";
			uInstance    = 0;
		}

		[JsonPropertyName("UserIdentity")]
		public int UserIdentity { get; set; }
		[JsonPropertyName("HostName")]
		public string HostName { get; set; }
		[JsonPropertyName("InstanceName")]
		public string InstanceName { get; set; }
		[JsonPropertyName("SchemaName")]
		public string SchemaName { get; set; }
		[JsonPropertyName("ClientKey")]
		public string ClientKey { get; set; }
		[JsonPropertyName("ClientCrt")]
		public string ClientCrt { get; set; }
		[JsonPropertyName("TrustCrt")]
		public string TrustCrt { get; set; }
		[JsonPropertyName("IdentityName")]
		public string IdentityName { get; set; }
		[JsonPropertyName("TrustName")]
		public string TrustName { get; set; }
		[JsonPropertyName("IssueName")]
		public string IssueName { get; set; }
		[JsonPropertyName("DriverName")]
		public string DriverName { get; set; }
		[JsonPropertyName("uInstance")]
		public uint uInstance { get; set; }

		public override bool Validate()
		{
			if (IsNull(DriverName) == true)
			{
				//　データベースドライバを選択していない。
				return (false);
			}
			if (DriverName.Equals("Postgres") == true)
			{
				if (IsNull(HostName) == true)
				{
					return (false);
				}
				if (IsNull(InstanceName) == true)
				{
					return (false);
				}
				if (IsNull(SchemaName) == true)
				{
					return (false);
				}
				if (IsNull(ClientKey) == true)
				{
					return (false);
				}
				if (IsNull(ClientCrt) == true)
				{
					return (false);
				}
				if (IsNull(TrustCrt) == true)
				{
					return (false);
				}
				if (IsNull(IdentityName) == true)
				{
					return (false);
				}
			}
			else if (DriverName.Equals("SQLite") == true)
			{
				//　SQLite に依存するパラメータはない。
				;
			}
			else
			{
				return (false);
			}
			/*
			if (IsNull(TrustName) == true)
			{
				return (false);
			}
			if (IsNull(IssueName) == true)
			{
				return (false);
			}
			*/

			return (true);
		}
	}

	public class Profile : ProfileBase
	{
		private static readonly string m_pCompanyName = "Arteria";
		private static readonly string m_pAppName = "RoughCA";
		private const long LAYOUT_VERSION = 15;
		private static SqliteConnection m_pConnection;
//		private static string m_pProfilepath;
		//public DbParams m_pDbParams;

		public Profile(string pArgument) : base(m_pCompanyName, m_pAppName)
		{
			//m_pProfilepath = pArgument;
		}
	
		public bool Load(ref DbParams m_pDbParams)
		{
			m_pConnection = Open(LAYOUT_VERSION, m_pProfilePath);
			if (m_pConnection == null)
			{
				return(false);
			}

			//　データベース接続情報を入力
			m_pDbParams = LoadDbParams(m_pProfileFolderPath);
//			m_pDbParams = LoadDbParams(m_pConnection);
			if (m_pDbParams == null)
			{
				var pAppDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
				var pFolderPath = pAppDataFolder + "\\postgresql";

				m_pDbParams = new DbParams();
				m_pDbParams.UserIdentity = 0;
				m_pDbParams.HostName     = "";
				m_pDbParams.InstanceName = m_pAppName;
				m_pDbParams.SchemaName   = "aploper";
				m_pDbParams.ClientKey    = pFolderPath + "\\postgresql.key";
				m_pDbParams.ClientCrt    = pFolderPath + "\\postgresql.crt";
				m_pDbParams.TrustCrt     = pFolderPath + "\\root.crt";
				m_pDbParams.IdentityName = "";
				m_pDbParams.TrustName    = "";
				m_pDbParams.IssueName    = "";
				//m_pDbParams.DriverName   = "SQLite";
				m_pDbParams.DriverName = "Postgres";
				m_pDbParams.uInstance  = 0;
			}

			return (true);
		}

		public bool Save(DbParams m_pDbParams)
		{
			/*
			m_pConnection = Open(LAYOUT_VERSION, m_pProfilePath);
			if (m_pConnection == null)
			{
				return (false);
			}
			return(SaveDbParams(m_pConnection, m_pDbParams));
			*/
			return (SaveDbParams(m_pProfileFolderPath, m_pDbParams));
		}

		//　テーブルレイアウトを最新に更新
		protected override bool DoUpgradeLayout(SqliteCommand pCommand)
		{
			List<string> pSQLs = new List<string>();

			pSQLs.Add(@"DROP TABLE IF EXISTS LayoutVersion;");
			pSQLs.Add(@"CREATE TABLE IF NOT EXISTS LayoutVersion (Revision INTEGER);");
			pSQLs.Add(@$"INSERT INTO LayoutVersion VALUES ({LAYOUT_VERSION});");
			pSQLs.Add("DROP TABLE IF EXISTS DbParams;");
			pSQLs.Add("CREATE TABLE DbParams (UserIdentity INTEGER NOT NULL, HostName TEXT NOT NULL, InstanceName TEXT NOT NULL, SchemaName TEXT NOT NULL, ClientKey TEXT NOT NULL, ClientCrt TEXT NOT NULL, TrustCrt TEXT NOT NULL, IdentityName TEXT NOT NULL, TrustName TEXT NOT NULL, IssueName TEXT NOT NULL, DriverName TEXT NOT NULL, PRIMARY KEY (UserIdentity))");
			pSQLs.Add("DROP TABLE IF EXISTS OrgProfile;");
			//pSQLs.Add("CREATE TABLE OrgProfile (OrgKey INTEGER NOT NULL, CaName TEXT NOT NULL, OrgName TEXT NOT NULL, OrgUnitName TEXT NOT NULL, localityName TEXT NULL, ProvinceName NOT NULL, countryName NOT NULL, PRIMARY KEY (OrgKey))");
			pSQLs.Add("DROP TABLE IF EXISTS IssuedCerts;");
			//pSQLs.Add("CREATE TABLE IssuedCerts (SequenceNumber INTEGER NOT NULL, SerialNumber TEXT NOT NULL, CommonName TEXT NOT NULL, Revoked INTEGER NOT NULL,  PemData TEXT NOT NULL, PRIMARY KEY (SequenceNumber))");
			pSQLs.Add("DROP TABLE IF EXISTS TIssuedCerts;");
			pSQLs.Add("CREATE TABLE TIssuedCerts(SequenceNumber INTEGER NOT NULL, SerialNumber TEXT NOT NULL UNIQUE, SubjectName TEXT NOT NULL, CommonName TEXT NOT NULL, TypeOf INTEGER NOT NULL, Revoked INTEGER NOT NULL, LaunchAt  TEXT NOT NULL, ExpireAt TEXT NOT NULL, RevokeAt TEXT, AuthorityId INTEGER NOT NULL, CONSTRAINT TIssuedCerts_pkey  PRIMARY KEY (AuthorityId, SequenceNumber));");
			pSQLs.Add(@$"PRAGMA user_version = {LAYOUT_VERSION};");

			foreach (var pSQL in pSQLs)
			{
				pCommand.CommandText = pSQL;
				pCommand.ExecuteNonQuery();
			}

			return(true);
		}

		//　
		protected DbParams LoadDbParams(SqliteConnection pConnection)
		{
			var pDbParams = new DbParams();

			try
			{
				pConnection.Open();
				var pSQL = "SELECT UserIdentity, HostName, InstanceName, SchemaName, ClientKey, ClientCrt, TrustCrt, IdentityName, TrustName, IssueName, DriverName FROM DbParams WHERE UserIdentity == 0";
				var pCommand = new SqliteCommand(pSQL, pConnection);
				using (var pReader = pCommand.ExecuteReader())
				{
					while (pReader.Read())
					{
						pDbParams.UserIdentity = pReader.GetInt32(0);
						pDbParams.HostName     = pReader.GetString(1);
						pDbParams.InstanceName = pReader.GetString(2);
						pDbParams.SchemaName   = pReader.GetString(3);
						pDbParams.ClientKey    = pReader.GetString(4);
						pDbParams.ClientCrt	   = pReader.GetString(5);
						pDbParams.TrustCrt     = pReader.GetString(6);
						pDbParams.IdentityName = pReader.GetString(7);
						pDbParams.TrustName    = pReader.GetString(8);
						pDbParams.IssueName    = pReader.GetString(9);
						pDbParams.DriverName   = pReader.GetString(10);
					}
				}
				pConnection.Close();
			}
			catch (SqliteException e)
			{
				//　Brunch: レイアウトバージョンのテーブルが存在しない
				System.Diagnostics.Debug.Write("" + e.ToString());
				if (e.SqliteErrorCode == 1)
				{
					return (null);
				}
			}
			if (pDbParams.UserIdentity == -1)
			{
				return (null);
			}

			return (pDbParams);
		}

		private const string pProfileFilename = "RoughCA.json";

		//　
		protected DbParams LoadDbParams(string pProfileFolder)
		{
			var pFilepath = pProfileFolder + "\\" + pProfileFilename;
			if (File.Exists(pFilepath) == false)
			{
				return (null);
			}
			var pJSON = File.ReadAllText(pFilepath);
			return (JsonSerializer.Deserialize<DbParams>(pJSON));
		}

		//　
		protected bool SaveDbParams(string pProfileFolder, DbParams pDbParams)
		{
			var pFilepath = pProfileFolder + "\\" + pProfileFilename;
			var pOptions = new JsonSerializerOptions() { WriteIndented = true };
			var pJSON = JsonSerializer.Serialize(pDbParams);
			File.WriteAllText(pFilepath, pJSON);
			return (true);
		}

		//
		protected bool SaveDbParams(SqliteConnection pConnection, DbParams pDbParams)
		{
			try
			{
				pDbParams.UserIdentity = 0;
				pDbParams.InstanceName = pDbParams.InstanceName.ToLower();
				pConnection.Open();
				var pSQL = "INSERT INTO DbParams VALUES (@UserIdentity, @HostName, @InstanceName, @SchemaName, @ClientKey, @ClientCrt, @TrustCrt, @IdentityName, @TrustName, @IssueName, @DriverName)";
				pSQL += " ON CONFLICT (UserIdentity) DO UPDATE SET HostName = @HostName, InstanceName = @InstanceName, SchemaName = @SchemaName, ClientKey = @ClientKey, ClientCrt = @ClientCrt, TrustCrt = @TrustCrt, IdentityName = @IdentityName, TrustName = @TrustName, IssueName = @IssueName, DriverName = @DriverName";
				var pCommand = new SqliteCommand(pSQL, pConnection);
				pCommand.Parameters.Clear();
				pCommand.Parameters.Add(new SqliteParameter("UserIdentity", pDbParams.UserIdentity));
				pCommand.Parameters.Add(new SqliteParameter("HostName",     pDbParams.HostName));
				pCommand.Parameters.Add(new SqliteParameter("InstanceName", pDbParams.InstanceName));
				pCommand.Parameters.Add(new SqliteParameter("SchemaName",   pDbParams.SchemaName));
				pCommand.Parameters.Add(new SqliteParameter("ClientKey",    pDbParams.ClientKey));
				pCommand.Parameters.Add(new SqliteParameter("ClientCrt",    pDbParams.ClientCrt));
				pCommand.Parameters.Add(new SqliteParameter("TrustCrt",     pDbParams.TrustCrt));
				pCommand.Parameters.Add(new SqliteParameter("IdentityName", pDbParams.IdentityName));
				pCommand.Parameters.Add(new SqliteParameter("TrustName",    pDbParams.TrustName));
				pCommand.Parameters.Add(new SqliteParameter("IssueName",    pDbParams.IssueName));
				pCommand.Parameters.Add(new SqliteParameter("DriverName",   pDbParams.DriverName));
				var nCount = pCommand.ExecuteNonQuery();
				if (nCount <= 0)
				{
					return (false);
				}
				pConnection.Close();
			}
			catch (SqliteException e)
			{
				//　Brunch: レイアウトバージョンのテーブルが存在しない
				System.Diagnostics.Debug.Write("" + e.ToString());
				if (e.SqliteErrorCode == 1)
				{
					return (false);
				}
			}

			return (true);
		}
	}
}
