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
using System.Numerics;
using System.Security.Cryptography;
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
		private const long LAYOUT_VERSION = 22;

		//　
		//　DatabaseName：データベース名
		//　SqliteFilePath：任意のSQLiteファイルパス（空の場合は既定フォルダに {DatabaseName}.db を作成）
		public SQLiteContext(string DatabaseServer, string DatabaseName, string SchemaName, string ClientKey, string ClientCrt, string TrustCrt, string SqliteFilePath = "")
		{
			string pProfilePath;
			if (!string.IsNullOrWhiteSpace(SqliteFilePath))
			{
				pProfilePath = SqliteFilePath.Trim();
				var pDir = System.IO.Path.GetDirectoryName(pProfilePath);
				if (!string.IsNullOrWhiteSpace(pDir))
				{
					DirectoryHelper.CreateDirectorySafe(pDir);
				}
			}
			else
			{
				var pAppDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
				System.Diagnostics.Debug.WriteLine("AppData: " + pAppDataFolder);

				//　ユーザープロファイルのフォルダ名を生成
				var pProfileFolderPath = System.IO.Path.Combine(pAppDataFolder, m_pCompanyName, m_pAppName);
				DirectoryHelper.CreateDirectorySafe(pProfileFolderPath);

				var dbFileName = string.IsNullOrWhiteSpace(DatabaseName) ? m_pAppName : DatabaseName.Trim();
				if (!dbFileName.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
				{
					dbFileName += ".db";
				}
				pProfilePath = System.IO.Path.Combine(pProfileFolderPath, dbFileName);
			}

			m_pConnection = Open(LAYOUT_VERSION, pProfilePath);
		}

		public override void Close()
		{
			if (m_pConnection != null)
			{
				m_pConnection.Close();
				m_pConnection.Dispose();
				m_pConnection = null;
			}
			SqliteConnection.ClearAllPools();
		}

		//　
		~SQLiteContext()
		{
			Close();
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
			pSQLs.Add("CREATE TABLE TOrgProfile (OrgKey INTEGER NOT NULL, OrgName TEXT NOT NULL, OrgunitName TEXT NOT NULL, LocalityName TEXT NOT NULL, ProvinceName TEXT NOT NULL, CountryName TEXT NOT NULL, ServerName TEXT NOT NULL, SerialNumber INTEGER NOT NULL, TrustName TEXT NOT NULL, IssueName TEXT NOT NULL, UpdateAt TEXT NOT NULL, CONSTRAINT TOrgProfile_pkey PRIMARY KEY (OrgKey));");
			pSQLs.Add("DROP TABLE IF EXISTS TCounters;");
			pSQLs.Add("CREATE TABLE TCounters (CrlNumber TEXT NOT NULL);");
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
			var pSQL = "SELECT OrgKey, OrgName, OrgUnitName, LocalityName, ProvinceName, CountryName, ServerName, SerialNumber, TrustName, IssueName, UpdateAt FROM TOrgProfile WHERE OrgKey = @OrgKey";
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
						pOrgProfile.TrustName    = pReader.GetString(8);
						pOrgProfile.IssueName    = pReader.GetString(9);
						pOrgProfile.UpdataAt     = pReader.GetDateTime(10);
					}
				}
			}
			m_pConnection.Close();
			return (true);
		}

		public override bool SaveOrgProfile(OrgProfile pOrgProfile)
		{
			m_pConnection.Open();
			var pSQL = "INSERT INTO TOrgProfile (OrgKey, OrgName, OrgUnitName, LocalityName, ProvinceName, CountryName, ServerName, SerialNumber, TrustName, IssueName, UpdateAt) VALUES (@OrgKey, @OrgName, @OrgUnitName, @LocalityName, @ProvinceName, @CountryName, @ServerName, @SerialNumber, @TrustName, @IssueName, CURRENT_TIMESTAMP)";
			pSQL += " ON CONFLICT (OrgKey) DO UPDATE SET OrgName = @OrgName, OrgUnitName = @OrgUnitName, LocalityName = @LocalityName, ProvinceName = @ProvinceName, CountryName = @CountryName, ServerName = @ServerName, SerialNumber = @SerialNumber, TrustName = @TrustName, IssueName = @IssueName, UpdateAt = CURRENT_TIMESTAMP";
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
				pCommand.Parameters.Add(new SqliteParameter("TrustName",    pOrgProfile.TrustName));
				pCommand.Parameters.Add(new SqliteParameter("IssueName",    pOrgProfile.IssueName));
				var lResult = pCommand.ExecuteNonQuery();
				Debug.Assert(lResult == 1, $"SQL={pSQL}; SQL文を設定し忘れていませんか？CommandTextメンバーに設定する必要があります。");
			}
			m_pConnection.Close();
			return (true);
		}

		public override bool IsExists(uint uAuthorityId, string pSubjectName, string pCommonName)
		{
			m_pConnection.Open();
			var pSQL = "SELECT SequenceNumber, SubjectName FROM TSignRequest WHERE SubjectName = @SubjectName AND Revoked = FALSE AND LaunchAt <= now() AND now() < ExpireAt AND AuthorityId = @AuthorityId;";
			using (var pCommand = m_pConnection.CreateCommand())
			{
				pCommand.CommandText = pSQL;
				pCommand.Parameters.Clear();
				pCommand.Parameters.AddWithValue("SubjectName", pSubjectName);
				pCommand.Parameters.AddWithValue("AuthorityId", (Int64)uAuthorityId);
				using (var pReader = pCommand.ExecuteReader())
				{
					int iCount = 0;
					while (pReader.Read())
					{
						iCount++;
					}
					if (iCount == 0)
					{
						m_pConnection.Close();
						return (false);
					}
				}
			}
			m_pConnection.Close();
			return (true);
		}

		public override bool LoadSignRequest(uint uAuthorityId, string pSubjectName, ref string pKeyPem, ref ItemsMentioned pItems)
		{
			m_pConnection.Open();
			var pSQL = "SELECT SequenceNumber, SubjectName, KeyData FROM TSignRequest WHERE SubjectName = @SubjectName AND Revoked = FALSE AND LaunchAt <= now() AND now() < ExpireAt AND AuthorityId = @AuthorityId;";
			using (var pCommand = m_pConnection.CreateCommand())
			{
				pCommand.CommandText = pSQL;
				pCommand.Parameters.Clear();
				pCommand.Parameters.AddWithValue("SubjectName", pSubjectName);
				pCommand.Parameters.AddWithValue("AuthorityId", (Int64)uAuthorityId);
				using (var pReader = pCommand.ExecuteReader())
				{
					int iCount = 0;
					while (pReader.Read())
					{
						pItems.SequenceNumber = pReader.GetInt64(0);
						pItems.SubjectName = pReader.GetString(1);
						pKeyPem = pReader.IsDBNull(2) ? null : pReader.GetString(2);

						iCount++;
					}
					if (iCount == 0)
					{
						m_pConnection.Close();
						return (false);
					}
				}
			}
			m_pConnection.Close();
			return (true);
		}

		public override bool SaveSignRequest(uint uAuthorityId, string pKeyPem, ItemsMentioned pItems)
		{
			var status = true;

			BeginTransaction();
			try
			{
				var pSQL_UPDATE = "UPDATE TSignRequest SET Revoked = True, RevokeAt = now() WHERE AuthorityId = @AuthorityId AND CommonName = @CommonName";
				using (var pCommand = m_pConnection.CreateCommand())
				{
					pCommand.CommandText = pSQL_UPDATE;
					pCommand.Parameters.Clear();
					pCommand.Parameters.AddWithValue("AuthorityId", (Int64)uAuthorityId);
					pCommand.Parameters.AddWithValue("CommonName", pItems.CommonName);
					pCommand.ExecuteNonQuery();
				}

				var pSQL = "INSERT INTO TSignRequest (AuthorityId, SequenceNumber, SubjectName, CommonName, TypeOf, LaunchAt, ExpireAt, KeyData)";
				pSQL += " VALUES (@AuthorityId, NEXTVAL('SQ_REQTS'), @SubjectName, @CommonName, @TypeOf, @LaunchAt, @ExpireAt, @KeyData)";
				pSQL += " ON CONFLICT ON CONSTRAINT tsignrequest_pkey DO UPDATE SET";
				pSQL += " SubjectName = @SubjectName, CommonName = @CommonName, TypeOf = @TypeOf,";
				pSQL += " LaunchAt = @LaunchAt, ExpireAt = @ExpireAt, KeyData = @KeyData";
				using (var pCommand = m_pConnection.CreateCommand())
				{
					pCommand.CommandText = pSQL;
					pCommand.Parameters.Clear();
					pCommand.Parameters.AddWithValue("AuthorityId", (Int64)uAuthorityId);
					pCommand.Parameters.AddWithValue("SequenceNumber", pItems.SequenceNumber);
					pCommand.Parameters.AddWithValue("SubjectName", pItems.SubjectName);
					pCommand.Parameters.AddWithValue("CommonName", pItems.CommonName);
					pCommand.Parameters.AddWithValue("TypeOf", (int)pItems.TypeOf);
					pCommand.Parameters.AddWithValue("LaunchAt", pItems.LaunchAt);
					pCommand.Parameters.AddWithValue("ExpireAt", pItems.ExpireAt);
					pCommand.Parameters.AddWithValue("KeyData", (object)pKeyPem ?? DBNull.Value);
					pCommand.ExecuteNonQuery();
				}

				Commit();
			}
			catch (Exception ex)
			{
				Rollback();
				Debug.WriteLine(ex);
				status = false;
			}
			return (status);
		}

		public override bool LoadCertificate(string pCommonName, uint uAuthorityId, ref ItemsMentioned pItems, ref string pCertPem, ref string pKeyPem)
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
						pItems.SequenceNumber = pReader.GetInt64(0);
						pItems.SerialNumber   = pReader.GetString(1);
						pItems.SubjectName    = pReader.GetString(2);
						pItems.CommonName     = pReader.GetString(3);
						pItems.TypeOf         = (CertificateType)pReader.GetInt32(4);
						pItems.Revoked        = pReader.GetBoolean(5);
						pItems.LaunchAt       = pReader.GetDateTime(6);
						pItems.ExpireAt       = pReader.GetDateTime(7);
						pCertPem = pReader.GetString(8);
						pKeyPem = pReader.IsDBNull(9) ? null : pReader.GetString(9);

						iCount++;
					}
					if (iCount == 0)
					{
						Console.WriteLine("準正常系：認証局の証明書が未発行です。（The certificate from the certificate authority has not been issued.）");
						return (false);
					}
				}
			}
			m_pConnection.Close();
			return (true);
		}

		public override bool IsExistSubject(string pSerialNumber, string pSubjectName, uint uAuthorityId)
		{
			m_pConnection.Open();
			var pSQL = "SELECT SequenceNumber, SerialNumber, SubjectName FROM TIssuedCerts WHERE SerialNumber <> @SerialNumber AND SubjectName = @SubjectName AND Revoked = FALSE AND LaunchAt <= CURRENT_TIMESTAMP AND CURRENT_TIMESTAMP < ExpireAt AND AuthorityId = @AuthorityId;";
			using (var pCommand = m_pConnection.CreateCommand())
			{
				pCommand.CommandText = pSQL;
				pCommand.Parameters.Clear();
				pCommand.Parameters.AddWithValue("SerialNumber", pSerialNumber);
				pCommand.Parameters.AddWithValue("SubjectName", pSubjectName);
				pCommand.Parameters.AddWithValue("AuthorityId", (Int64)uAuthorityId);
				using (var pReader = pCommand.ExecuteReader())
				{
					int iCount = 0;
					while (pReader.Read())
					{
						iCount++;
					}
					if (iCount == 0)
					{
						return (false);
					}
				}
			}
			m_pConnection.Close();
			return (true);
		}

		//　シーケンス番号を獲得
		protected long FetchSequenceNumber(uint uOrgKey)
		{
			var uSequenceNumber = 0L;
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
						uSequenceNumber = pReader.GetInt64(0);
					}
				}
			}
			return (uSequenceNumber);
		}

		//　シリアル番号を更新
		protected bool UpdateSequenceNumber(uint uOrgKey, long lSequenceNumber)
		{
			var pSQL_SerialNumber = "UPDATE TOrgProfile SET SerialNumber = @SerialNumber WHERE OrgKey = @OrgKey";
			using (var pCommand = m_pConnection.CreateCommand())
			{
				pCommand.CommandText = pSQL_SerialNumber;
				pCommand.Parameters.Clear();
				pCommand.Parameters.AddWithValue("OrgKey", (Int64)uOrgKey);
				pCommand.Parameters.AddWithValue("SerialNumber", (Int64)lSequenceNumber);
				pCommand.ExecuteNonQuery();
			}
			return (true);
		}

		//　（前提）呼び出し元がトランザクションとコネクションのライフタイムを管理する
		public override bool SaveCertificate(uint uAuthorityId, uint uInstance, ItemsMentioned pItems, string pCertPem, string pKeyPem)
		{
			var status = true;
			try
			{
				var pSQL_UPDATE = "UPDATE TIssuedCerts SET Revoked = True, RevokeAt = CURRENT_TIMESTAMP WHERE AuthorityId = @AuthorityId AND CommonName = @CommonName";
				using (var pCommand = m_pConnection.CreateCommand())
				{
					pCommand.CommandText = pSQL_UPDATE;
					pCommand.Parameters.Clear();
					pCommand.Parameters.AddWithValue("AuthorityId", (Int64)uAuthorityId);
					pCommand.Parameters.AddWithValue("CommonName", pItems.CommonName);
					pCommand.ExecuteNonQuery();
				}

				long uSequenceNumber = FetchSequenceNumber(uInstance);
				uSequenceNumber += 1;

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
					pCommand.Parameters.AddWithValue("SequenceNumber", uSequenceNumber);
					pCommand.Parameters.AddWithValue("SerialNumber", pItems.SerialNumber);
					pCommand.Parameters.AddWithValue("SubjectName", pItems.SubjectName);
					pCommand.Parameters.AddWithValue("CommonName", pItems.CommonName);
					pCommand.Parameters.AddWithValue("TypeOf", (int)pItems.TypeOf);
					pCommand.Parameters.AddWithValue("LaunchAt", pItems.LaunchAt.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss"));
					pCommand.Parameters.AddWithValue("ExpireAt", pItems.ExpireAt.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss"));
					pCommand.Parameters.AddWithValue("PemData", pCertPem);
					pCommand.Parameters.AddWithValue("KeyData", (object)pKeyPem ?? DBNull.Value);
					pCommand.ExecuteNonQuery();
				}

				if (UpdateSequenceNumber(uInstance, uSequenceNumber) == false)
				{
					;
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex);
				status = false;
			}
			return (status);
		}

		public override bool Revoke(uint uAuthorityId, string SerialNumber)
		{
			var pSQL = "UPDATE TIssuedCerts SET Revoked = @Revoked, RevokeAt = CURRENT_TIMESTAMP WHERE SerialNumber = @SerialNumber AND AuthorityId = @AuthorityId";
			using (var pCommand = m_pConnection.CreateCommand())
			{
				var Revoked = true;
				pCommand.CommandText = pSQL;
				pCommand.Parameters.Clear();
				pCommand.Parameters.AddWithValue("SerialNumber", SerialNumber);
				pCommand.Parameters.AddWithValue("Revoked", Revoked);
				pCommand.Parameters.AddWithValue("AuthorityId", (Int64)uAuthorityId);
				pCommand.ExecuteNonQuery();
			}
			return (true);
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
						var pCertPem = pReader.GetString(7);
						var pKeyPem = pReader.IsDBNull(8) ? null : pReader.GetString(8);
						pCertificate.LoadFromPem(pCertPem, pKeyPem);
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
			var pCertificate = new Certificate();
			var pSQL = "SELECT SequenceNumber, SerialNumber, CommonName, TypeOf, Revoked, LaunchAt, ExpireAt, PemData, KeyData FROM TIssuedCerts WHERE SerialNumber = @SerialNumber AND TypeOf <> @TypeOf;";
			using (var pCommand = m_pConnection.CreateCommand())
			{
				pCommand.CommandText = pSQL;
				pCommand.Parameters.Clear();
				pCommand.Parameters.AddWithValue("SerialNumber", pSerialNumber);
				pCommand.Parameters.AddWithValue("TypeOf", (int)CertificateType.Demand);
				using (var pReader = pCommand.ExecuteReader())
				{
					var iCount = 0;
					while (pReader.Read())
					{
						pCertificate.m_pItems.SequenceNumber = pReader.GetInt64(0);
						pCertificate.m_pItems.SerialNumber = pReader.GetString(1);
						pCertificate.m_pItems.CommonName = pReader.GetString(2);
						pCertificate.m_pItems.TypeOf = (CertificateType)pReader.GetInt32(3);
						pCertificate.m_pItems.Revoked = pReader.GetBoolean(4);
						pCertificate.m_pItems.LaunchAt = pReader.GetDateTime(5);
						pCertificate.m_pItems.ExpireAt = pReader.GetDateTime(6);
						var pCertPem = pReader.GetString(7);
						var pKeyPem = pReader.IsDBNull(8) ? null : pReader.GetString(8);
						pCertificate.LoadFromPem(pCertPem, pKeyPem);
						iCount++;
					}
					if (iCount == 0)
					{
						return (null);
					}
				}
			}
			return (pCertificate);
		}

		public override byte[] GenerateCRL(uint m_uAuthorityId, int iDays, X509Certificate2 m_pCertificate)
		{
			byte[] pBytes;
			var pBuilder = new CertificateRevocationListBuilder();

			var pSQL = "SELECT SerialNumber, RevokeAt FROM TIssuedCerts WHERE Revoked = TRUE AND AuthorityId = @AuthorityId;";
			using (var pCommand = m_pConnection.CreateCommand())
			{
				pCommand.CommandText = pSQL;
				pCommand.Parameters.Clear();
				pCommand.Parameters.AddWithValue("AuthorityId", (Int64)m_uAuthorityId);
				using (var pReader = pCommand.ExecuteReader())
				{
					while (pReader.Read())
					{
						var SerialNumber = Convert.FromHexString(pReader.GetString(0));
						var RevokeAt = pReader.GetDateTime(1);
						pBuilder.AddEntry(SerialNumber, RevokeAt);
					}
				}
			}

			BigInteger iCRLNumber = 0;

			//　CRL番号を取得
			pSQL = "SELECT CRLNumber FROM TCounters;";
			using (var pCommand = m_pConnection.CreateCommand())
			{
				pCommand.Parameters.Clear();
				pCommand.CommandText = pSQL;
				using (var pReader = pCommand.ExecuteReader())
				{
					while (pReader.Read())
					{
						var pNumber = pReader.GetString(0);
						iCRLNumber = BigInteger.Parse(pNumber, NumberStyles.HexNumber);
						iCRLNumber++;
						break;
					}
				}
				DateTimeOffset pNextUpdate = DateTimeOffset.Now.AddDays(iDays);
				pBytes = pBuilder.Build(m_pCertificate, iCRLNumber, pNextUpdate, HashAlgorithmName.SHA512);
			}

			//　CRLNumberのカウンタを更新
			pSQL = "UPDATE TCounters SET CrlNumber = @CrlNumber;";
			using (var pCommand = m_pConnection.CreateCommand())
			{
				var pNumber = iCRLNumber.ToString("X");
				pCommand.Parameters.Clear();
				pCommand.CommandText = pSQL;
				pCommand.Parameters.AddWithValue("CrlNumber", pNumber);
				pCommand.ExecuteNonQuery();
			}

			return (pBytes);
		}

		private SqliteTransaction	m_pTransaction = null;

		//　トランザクションを開始
		public override bool BeginTransaction()
		{
			m_pConnection.Open();
			m_pTransaction = m_pConnection.BeginTransaction();
			return (true);
		}

		public override bool Commit()
		{
			m_pTransaction.Commit();
			m_pConnection.Close();
			return (true);
		}

		public override bool Rollback()
		{
			m_pTransaction.Rollback();
			m_pConnection.Close();
			return (true);
		}
	}
}
