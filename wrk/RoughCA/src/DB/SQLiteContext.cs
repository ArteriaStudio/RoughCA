using Arteria_s.App.RoughCA;
using Arteria_s.OS.Base;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Common;
using System.Data.SQLite;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography.X509Certificates;

namespace Arteria_s.DB.Base
{
	//　データベースコンテキスト
	public class SQLiteContext : VSQLContext
	{
		SqliteConnection	m_pConnection;

		public SQLiteContext()
		{
			m_pConnection = null;
		}

		private static readonly string m_pCompanyName = "Arteria";
		private static readonly string m_pAppName = "RoughCA";
		private const long LAYOUT_VERSION = 20;

		//　
		public SQLiteContext(string DatabaseServer, string DatabaseName, string SchemaName, string ClientKey, string ClientCrt, string TrustCrt)
		{
			var pAppDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
			System.Diagnostics.Debug.WriteLine("AppData: " + pAppDataFolder);

			//　ユーザープロファイルのフォルダ名を生成
			var pProfileFolderPath = pAppDataFolder + "/" + m_pCompanyName + "/" + m_pAppName;

			DirectoryHelper.CreateDirectorySafe(pProfileFolderPath);

			//　ユーザープロファイルのパス名を生成
			var pProfilePath = pProfileFolderPath + "/" + m_pAppName + ".db";
			m_pConnection = Open(LAYOUT_VERSION, pProfilePath);
		}

		//　
		~SQLiteContext()
		{
			if (m_pConnection != null)
			{
				m_pConnection.Close();
			}
			m_pConnection = null;
		}

		protected bool IsUpgradeLayout(SqliteCommand pCommand, long lRequireRevision)
		{
			try
			{
				pCommand.CommandText = "PRAGMA user_version";
				using (var pReader = pCommand.ExecuteReader())
				{
					while (pReader.Read())
					{
						var lRevision = pReader.GetInt64(0);
						if (lRequireRevision > lRevision)
						{
							return (true);
						}
					}
				}
			}
			catch (SqliteException e)
			{
				//　Brunch: レイアウトバージョンのテーブルが存在しない
				System.Diagnostics.Debug.Write("" + e.ToString());
				if (e.SqliteErrorCode == 1)
				{
					return (true);
				}
			}

			return (false);
		}

		//　テーブルレイアウトを最新に更新
		protected bool DoUpgradeLayout(SqliteCommand pCommand)
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
			pSQLs.Add("CREATE TABLE TIssuedCerts(SequenceNumber INTEGER NOT NULL, SerialNumber TEXT NOT NULL UNIQUE, SubjectName TEXT NOT NULL, CommonName TEXT NOT NULL, TypeOf INTEGER NOT NULL, Revoked INTEGER NOT NULL, LaunchAt  TEXT NOT NULL, ExpireAt TEXT NOT NULL, RevokeAt TEXT, AuthorityId INTEGER NOT NULL, PemData TEXT NOT NULL, KeyData TEXT, CONSTRAINT TIssuedCerts_pkey  PRIMARY KEY (AuthorityId, SequenceNumber));");
			pSQLs.Add("DROP TABLE IF EXISTS TOrgProfile;");
			pSQLs.Add("CREATE TABLE TOrgProfile (OrgKey INTEGER NOT NULL, OrgName TEXT NOT NULL, OrgunitName TEXT NOT NULL, LocalityName TEXT NOT NULL, ProvinceName TEXT NOT NULL, CountryName TEXT NOT NULL, ServerName TEXT NOT NULL, SerialNumber INTEGER NOT NULL, UpdateAt TEXT NOT NULL, CONSTRAINT TOrgProfile_pkey PRIMARY KEY (OrgKey));");
			pSQLs.Add(@$"PRAGMA user_version = {LAYOUT_VERSION};");

			foreach (var pSQL in pSQLs)
			{
				pCommand.CommandText = pSQL;
				pCommand.ExecuteNonQuery();
			}

			return (true);
		}

		//　データベースを開く
		//　lVersion: レイアウトバージョン番号
		protected SqliteConnection Open(long lVersion, string pProfilepath)
		{
			if (pProfilepath.Length <= 0)
			{
				return (null);
			}
			var pConnectionString = "Data Source=" + pProfilepath;
			var pConnection = new SqliteConnection(pConnectionString);
			{
				pConnection.Open();

				using (var pCommand = pConnection.CreateCommand())
				{
					if (IsUpgradeLayout(pCommand, lVersion) == true)
					{
						DoUpgradeLayout(pCommand);
					}
					else
					{
						;
					}
				}
				pConnection.Close();
			}
			return (pConnection);
		}

		//　PostgreSQL セッション変数を更新
		protected void SetSessionVariable(string VariableName, string VariableValue)
		{
			var pSQL = "SELECT SET_CONFIG(@VariableName, @VariableValue, false);";
			using (var pCommand = m_pConnection.CreateCommand())
			{
				pCommand.CommandText = pSQL;
				pCommand.Parameters.Clear();
				pCommand.Parameters.AddWithValue("VariableName", VariableName);
				pCommand.Parameters.AddWithValue("VariableValue", VariableValue);

				try
				{
					pCommand.ExecuteNonQuery();
				}
				catch (SqliteException e)
				{
					System.Diagnostics.Debug.WriteLine($"Mesage: {e.Message} Code: {e.ErrorCode}");
				}
			}

			return;
		}

		protected string GetSessionVariable(string VariableName)
		{
			string	VariableValue = null;

			var pSQL = "SELECT current_setting(@VariableName);";
			using (var pCommand = m_pConnection.CreateCommand())
			{
				pCommand.CommandText = pSQL;
				pCommand.Parameters.Clear();
				pCommand.Parameters.AddWithValue("VariableName", VariableName);

				try
				{
					using (var pReader = pCommand.ExecuteReader())
					{
						while (pReader.Read())
						{
							VariableValue = pReader.GetString(0);
						}
					}
				}
				catch (SqliteException e)
				{
					System.Diagnostics.Debug.WriteLine($"Mesage: {e.Message} Code: {e.ErrorCode}");
				}
			}

			return (VariableValue);
		}

		public override bool LoadOrgProfile(ref OrgProfile pOrgProfile, uint uInstance)
		{
			m_pConnection.Open();
			var pSQL = "SELECT OrgKey, OrgName, OrgUnitName, LocalityName, ProvinceName, CountryName, ServerName, SerialNumber, UpdateAt FROM TOrgProfile WHERE OrgKey = @OrgKey";
			using (var pCommand = m_pConnection.CreateCommand())
			{
				pCommand.CommandText = pSQL;
				pCommand.Parameters.Clear();
				pCommand.Parameters.AddWithValue("OrgKey", uInstance);
				using (var pReader = pCommand.ExecuteReader())
				{
					while (pReader.Read())
					{
						pOrgProfile.OrgKey       = pReader.GetInt64(0);
						pOrgProfile.OrgName      = pReader.GetString(1);
						pOrgProfile.OrgUnitName  = pReader.GetString(2);
						pOrgProfile.LocalityName = pReader.GetString(3);
						pOrgProfile.ProvinceName = pReader.GetString(4);
						pOrgProfile.CountryName  = pReader.GetString(5);
						pOrgProfile.ServerName   = pReader.GetString(6);
						pOrgProfile.SerialNumber = pReader.GetInt64(7);
						pOrgProfile.UpdataAt     = pReader.GetDateTime(8);
					}
				}
			}
			m_pConnection.Close();
			return (true);
		}

		public override bool SaveOrgProfile(OrgProfile pOrgProfile)
		{
			m_pConnection.Open();
			var pSQL = "INSERT INTO TOrgProfile VALUES (@OrgKey, @OrgName, @OrgUnitName, @LocalityName, @ProvinceName, @CountryName, @ServerName, @SerialNumber, CURRENT_TIMESTAMP)";
			pSQL += " ON CONFLICT (OrgKey) DO UPDATE SET OrgName = @OrgName, OrgUnitName = @OrgUnitName, LocalityName = @LocalityName, ProvinceName = @ProvinceName, CountryName = @CountryName, ServerName = @ServerName, SerialNumber = @SerialNumber, UpdateAt = CURRENT_TIMESTAMP";
			using (var pCommand = m_pConnection.CreateCommand())
			{
				pCommand.CommandText = pSQL;
				pCommand.Parameters.Clear();
				pCommand.Parameters.Add(new SqliteParameter("OrgKey",       pOrgProfile.OrgKey));
				pCommand.Parameters.Add(new SqliteParameter("OrgName",      pOrgProfile.OrgName));
				pCommand.Parameters.Add(new SqliteParameter("OrgUnitName",  pOrgProfile.OrgUnitName));
				pCommand.Parameters.Add(new SqliteParameter("LocalityName", pOrgProfile.LocalityName));
				pCommand.Parameters.Add(new SqliteParameter("ProvinceName", pOrgProfile.ProvinceName));
				pCommand.Parameters.Add(new SqliteParameter("CountryName",  pOrgProfile.CountryName));
				pCommand.Parameters.Add(new SqliteParameter("SerialNumber", pOrgProfile.SerialNumber));
				pCommand.Parameters.Add(new SqliteParameter("ServerName",   pOrgProfile.ServerName));
				var lResult = pCommand.ExecuteNonQuery();
				Debug.Assert(lResult == 1, $"SQL={pSQL}; SQL文を設定し忘れていませんか？CommandTextメンバーに設定する必要があります。");


			}
			m_pConnection.Close();
			return (true);
		}

		public override bool IsExists(uint uAuthorityId, string pSubjectName, string pCommonName)
		{
			throw new System.NotImplementedException();
		}

		public override bool LoadSignRequest(uint uAuthorityId, string pSubjectName, ref string m_pKey, ref ItemsMentioned m_pItems)
		{
			throw new System.NotImplementedException();
		}

		public override bool SaveSignRequest(uint uAuthorityId, string m_pKey, ItemsMentioned m_pItems)
		{
			throw new System.NotImplementedException();
		}

		public override bool LoadCertificate(string pCommonName, uint uAuthorityId, ref ItemsMentioned m_pItems, ref string m_pCrt, ref string m_pKey, X509Certificate2 m_pCertificate)
		{
			//　共通名が一致する証明書を入力
			var pSQL = "SELECT SequenceNumber, SerialNumber, SubjectName, CommonName, TypeOf, Revoked, LaunchAt, ExpireAt, PemData, KeyData FROM TIssuedCerts WHERE CommonName = @CommonName AND Revoked = FALSE AND LaunchAt <= CURRENT_TIMESTAMP AND CURRENT_TIMESTAMP < ExpireAt AND AuthorityId = @AuthorityId;";

			m_pConnection.Open();
			using (var pCommand = m_pConnection.CreateCommand())
			{
				pCommand.CommandText = pSQL;
				pCommand.Parameters.Clear();
				pCommand.Parameters.AddWithValue("CommonName", pCommonName);
				pCommand.Parameters.AddWithValue("AuthorityId", (Int64)uAuthorityId);
				using (var pReader = pCommand.ExecuteReader())
				{
					int iCount = 0;
					while (pReader.Read())
					{
						m_pItems.SequenceNumber = pReader.GetInt64(0);
						m_pItems.SerialNumber   = pReader.GetString(1);
						m_pItems.SubjectName    = pReader.GetString(2);
						m_pItems.CommonName     = pReader.GetString(3);
						m_pItems.TypeOf         = (CertificateType)pReader.GetInt32(4);
						m_pItems.Revoked        = pReader.GetBoolean(5);
						m_pItems.LaunchAt       = pReader.GetDateTime(6);
						m_pItems.ExpireAt       = pReader.GetDateTime(7);
						m_pCrt = pReader.GetString(8);
						m_pKey = pReader.GetString(9);

						iCount++;
					}
					if (iCount == 0)
					{
						Console.WriteLine("準正常系：認証局の証明書が未発行です。（The certificate from the certificate authority has not been issued.）");
						return (false);
					}
				}
			}
			//if ((m_pItems.KeyData != null) && (m_pItems.KeyData.Length > 0))
			if ((m_pKey != null) && (m_pKey.Length > 0))
			{
				m_pCertificate = X509Certificate2.CreateFromPem(m_pCrt, m_pKey);
			}
			else
			{
				m_pCertificate = X509Certificate2.CreateFromPem(m_pCrt);
			}
			m_pConnection.Close();
			return (true);
		}

		public override bool IsExistSubject(string pSerialNumber, string pSubjectName, uint uAuthorityId)
		{
			throw new System.NotImplementedException();
		}

		//　シリアル番号を獲得
		protected bool FetchSerialNumber(uint uOrgKey, ref long SerialNumber)
		{
			var pSQL_SerialNumber = "SELECT SerialNumber FROM TOrgProfile WHERE OrgKey = @OrgKey";
			using (var pCommand = m_pConnection.CreateCommand())
			{
				pCommand.CommandText = pSQL_SerialNumber;
				pCommand.Parameters.Clear();
				pCommand.Parameters.AddWithValue("OrgKey", (Int64)uOrgKey);
				using (var pReader = pCommand.ExecuteReader())
				{
					while (pReader.Read())
					{
						SerialNumber = pReader.GetInt64(0);
					}
				}
			}
			return (true);
		}

		//　シリアル番号を更新
		protected bool UpdateSerialNumber(uint uOrgKey, long lSerialNumber)
		{
			var pSQL_SerialNumber = "UPDATE TOrgProfile SET SerialNumber = @SerialNumber WHERE OrgKey = @OrgKey";
			using (var pCommand = m_pConnection.CreateCommand())
			{
				pCommand.CommandText = pSQL_SerialNumber;
				pCommand.Parameters.Clear();
				pCommand.Parameters.AddWithValue("OrgKey", (Int64)uOrgKey);
				pCommand.Parameters.AddWithValue("SerialNumber", (Int64)lSerialNumber);
				pCommand.ExecuteNonQuery();
			}
			return (true);
		}

		public override bool SaveCertificate(uint uAuthorityId, uint uInstance, ref ItemsMentioned m_pItems, ref string m_pCrt, ref string m_pKey, X509Certificate2 m_pCertificate)
		{
			var status = true;
			try
			{
				var pSQL_UPDATE = "UPDATE TIssuedCerts SET Revoked = True, RevokeAt = CURRENT_TIMESTAMP WHERE AuthorityId = @AuthorityId AND CommonName = @CommonName";
				m_pConnection.Open();
				using (var pCommand = m_pConnection.CreateCommand())
				{
					pCommand.CommandText = pSQL_UPDATE;
					pCommand.Parameters.Clear();
					pCommand.Parameters.AddWithValue("AuthorityId", (Int64)uAuthorityId);
					pCommand.Parameters.AddWithValue("CommonName", m_pItems.CommonName);
					pCommand.ExecuteNonQuery();
				}

				long uSerialNumber = 0;
				if (FetchSerialNumber(uInstance, ref uSerialNumber) == false)
				{
					;
				}
				uSerialNumber += 1;

				var pSQL = "INSERT INTO TIssuedCerts (AuthorityId, SequenceNumber, SerialNumber, SubjectName, CommonName, TypeOf, Revoked, LaunchAt, ExpireAt, PemData, KeyData)";
				pSQL += " VALUES (@AuthorityId, @SequenceNumber, @SerialNumber, @SubjectName, @CommonName, @TypeOf, FALSE, @LaunchAt, @ExpireAt, @PemData, @KeyData)";
				pSQL += " ON CONFLICT (AuthorityId, SequenceNumber) DO UPDATE SET";
				pSQL += " SerialNumber = @SerialNumber, SubjectName = @SubjectName, CommonName = @CommonName, TypeOf = @TypeOf,";
				pSQL += " LaunchAt = @LaunchAt, ExpireAt = @ExpireAt, PemData = @PemData, KeyData = @KeyData";
				using (var pCommand = m_pConnection.CreateCommand())
				{
					pCommand.CommandText = pSQL;
					pCommand.Parameters.Clear();
					pCommand.Parameters.AddWithValue("AuthorityId", (Int64)uAuthorityId);
					pCommand.Parameters.AddWithValue("SequenceNumber", m_pItems.SequenceNumber);
					pCommand.Parameters.AddWithValue("SerialNumber", uSerialNumber);	//　ここで新しいシリアル番号を登録する必要がある。（2026/04/26）
					pCommand.Parameters.AddWithValue("SubjectName", m_pItems.SubjectName);
					pCommand.Parameters.AddWithValue("CommonName", m_pItems.CommonName);
					pCommand.Parameters.AddWithValue("TypeOf", (int)m_pItems.TypeOf);
					pCommand.Parameters.AddWithValue("LaunchAt", m_pItems.LaunchAt.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss"));
					pCommand.Parameters.AddWithValue("ExpireAt", m_pItems.ExpireAt.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss"));
					pCommand.Parameters.AddWithValue("PemData", m_pCrt);
					pCommand.Parameters.AddWithValue("KeyData", m_pKey);
					pCommand.ExecuteNonQuery();
				}

				if (UpdateSerialNumber(uAuthorityId, uSerialNumber) == false)
				{
					;
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex);
				status = false;
			}
			m_pConnection.Close();
			return (status);
		}

		public override bool Revoke(uint uAuthorityId, string SerialNumber)
		{
			throw new System.NotImplementedException();
		}

		public override ObservableCollection<Certificate> ListupCertificates(long m_uAuthorityId)
		{
			var pCertificates = new ObservableCollection<Certificate>();

			var pSQL = "SELECT SequenceNumber, SerialNumber, CommonName, TypeOf, Revoked, LaunchAt, ExpireAt, PemData, KeyData FROM TIssuedCerts WHERE AuthorityId = @AuthorityId AND Revoked = FALSE AND LaunchAt <= CURRENT_TIMESTAMP AND CURRENT_TIMESTAMP < ExpireAt AND TypeOf <> @TypeOf;";
			m_pConnection.Open();
			using (var pCommand = m_pConnection.CreateCommand())
			{
				pCommand.CommandText = pSQL;
				pCommand.Parameters.Clear();
				pCommand.Parameters.AddWithValue("AuthorityId", (Int64)m_uAuthorityId);
				pCommand.Parameters.AddWithValue("TypeOf", (int)CertificateType.Demand);
				using (var pReader = pCommand.ExecuteReader())
				{
					while (pReader.Read())
					{
						var pCertificate = new Certificate();
						pCertificate.m_pItems.SequenceNumber = pReader.GetInt64(0);
						pCertificate.m_pItems.SerialNumber = pReader.GetString(1);
						pCertificate.m_pItems.CommonName = pReader.GetString(2);
						pCertificate.m_pItems.TypeOf = (CertificateType)pReader.GetInt32(3);
						pCertificate.m_pItems.Revoked = pReader.GetBoolean(4);
						pCertificate.m_pItems.LaunchAt = pReader.GetDateTime(5);
						pCertificate.m_pItems.ExpireAt = pReader.GetDateTime(6);
						pCertificate.m_pCrt = pReader.GetString(7);
						pCertificate.m_pKey = pReader.GetString(8);
						pCertificate.Prepare();
						pCertificates.Add(pCertificate);
					}
					if (pCertificates.Count == 0)
					{
						return (null);
					}
				}
			}
			m_pConnection.Close();
			return (pCertificates);
		}

		public override Certificate Fetch(string pSerialNumber)
		{
			throw new System.NotImplementedException();
		}

		public override bool Save2(uint uAuthorityId, uint uInstance, ItemsMentioned m_pItems, string m_pCrt, string m_pKey)
		{
			throw new System.NotImplementedException();
		}

		public override byte[] GenerateCRL(uint m_uAuthorityId, int iDays, X509Certificate2 m_pCertificate)
		{
			throw new System.NotImplementedException();
		}

		private SqliteTransaction	m_pTransaction = null;

		//　トランザクションを開始
		public override bool BeginTransaction()
		{
			m_pTransaction = m_pConnection.BeginTransaction();
			return (true);
		}

		public override bool Commit()
		{
			m_pTransaction.Commit();
			return (true);
		}

		public override bool Rollback()
		{
			m_pTransaction.Rollback();
			return (true);
		}
	}
}
