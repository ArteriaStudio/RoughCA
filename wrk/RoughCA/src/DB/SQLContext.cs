using Arteria_s.App.RoughCA;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Windows.Security.Authentication.OnlineId;
using Windows.Security.Cryptography.Certificates;
using Certificate = Arteria_s.App.RoughCA.Certificate;

namespace Arteria_s.DB.Base
{
	//　データベースコンテキスト
	public class SQLContext : VSQLContext
	{
		public NpgsqlConnection m_pConnection;

		public SQLContext()
		{
			m_pConnection = null;
		}

		//　
		public SQLContext(string DatabaseServer, string DatabaseName, string SchemaName, string ClientKey, string ClientCrt, string TrustCrt)
		{
			var pBuilder = new NpgsqlConnectionStringBuilder();
			pBuilder.Host = DatabaseServer;
			pBuilder.Database = DatabaseName;
			pBuilder.Username = SchemaName;
			pBuilder.SslMode = SslMode.VerifyFull;
			//pBuilder.SslMode = SslMode.Disable;
			pBuilder.SslCertificate = ClientCrt;
			pBuilder.SslKey = ClientKey;
			pBuilder.RootCertificate = TrustCrt;
			var pConnectionString = pBuilder.ConnectionString;

			m_pConnection = new NpgsqlConnection(pConnectionString);
			m_pConnection.Open();
		}

		private NpgsqlTransaction	m_pTransaction = null;
		//　トランザクションを開始
		public override bool BeginTransaction()
		{
			m_pTransaction = m_pConnection.BeginTransaction();
			return (true);
		}

		//　
		~SQLContext()
		{
			if (m_pConnection != null)
			{
				m_pConnection.Close();
			}
			m_pConnection = null;
		}

		public NpgsqlCommand CreateCommand(string pSQL)
		{
			return(new NpgsqlCommand(pSQL, m_pConnection));
		}

		public override bool LoadOrgProfile(ref OrgProfile pOrgProfile, uint uInstance)
		{
			var pSQL = "SELECT OrgKey, OrgName, OrgUnitName, LocalityName, ProvinceName, CountryName, ServerName, UpdateAt, TrustName, IssueName FROM TOrgProfile WHERE OrgKey = @OrgKey";
			using (var pCommand = new NpgsqlCommand(pSQL, m_pConnection))
			{
				pCommand.Parameters.Clear();
				pCommand.Parameters.AddWithValue("OrgKey", uInstance);
				using (var pReader = pCommand.ExecuteReader())
				{
					while (pReader.Read())
					{
						pOrgProfile.OrgKey = pReader.GetInt64(0);
						pOrgProfile.OrgName = pReader.GetString(1);
						pOrgProfile.OrgUnitName = pReader.GetString(2);
						pOrgProfile.LocalityName = pReader.GetString(3);
						pOrgProfile.ProvinceName = pReader.GetString(4);
						pOrgProfile.CountryName = pReader.GetString(5);
						pOrgProfile.ServerName = pReader.GetString(6);
						pOrgProfile.UpdataAt = pReader.GetDateTime(7);
						pOrgProfile.TrustName = pReader.GetString(8);
						pOrgProfile.IssueName = pReader.GetString(9);
					}
				}
			}

			return (true);
		}

		public override bool SaveOrgProfile(OrgProfile pOrgProfile)
		{
			var pSQL = "INSERT INTO TOrgProfile VALUES (@OrgKey, @OrgName, @OrgUnitName, @LocalityName, @ProvinceName, @CountryName, @ServerName, @TrustName, @IssueName, now())";
			pSQL += " ON CONFLICT ON CONSTRAINT TOrgProfile_pkey DO UPDATE SET OrgName = @OrgName, OrgUnitName = @OrgUnitName, LocalityName = @LocalityName, ProvinceName = @ProvinceName, CountryName = @CountryName, ServerName = @ServerName, TrustName = @TrustName, IssueName = @IssueName, UpdateAt = now()";
			using (var pCommand = new NpgsqlCommand(pSQL, m_pConnection))
			{
				pCommand.Parameters.Clear();
				pCommand.Parameters.AddWithValue("OrgKey", pOrgProfile.OrgKey);
				pCommand.Parameters.AddWithValue("OrgName", pOrgProfile.OrgName);
				pCommand.Parameters.AddWithValue("OrgUnitName", pOrgProfile.OrgUnitName);
				pCommand.Parameters.AddWithValue("LocalityName", pOrgProfile.LocalityName);
				pCommand.Parameters.AddWithValue("ProvinceName", pOrgProfile.ProvinceName);
				pCommand.Parameters.AddWithValue("CountryName", pOrgProfile.CountryName);
				pCommand.Parameters.AddWithValue("ServerName", pOrgProfile.ServerName);
				pCommand.Parameters.AddWithValue("TrustName", pOrgProfile.TrustName);
				pCommand.Parameters.AddWithValue("IssueName", pOrgProfile.IssueName);
				pCommand.ExecuteNonQuery();
			}

			return (true);
		}

		//　
		public override bool IsExists(uint uAuthorityId, string pSubjectName, string pCommonName)
		{
			var pSQL = "SELECT SequenceNumber, SubjectName FROM TSignRequest WHERE SubjectName = @SubjectName AND Revoked = FALSE AND LaunchAt <= now() AND now() < ExpireAt AND AuthorityId = @AuthorityId;";
			using (var pCommand = new NpgsqlCommand(pSQL, m_pConnection))
			{
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
						return (false);
					}
				}
			}

			return (true);
		}

		public override bool LoadSignRequest(uint uAuthorityId, string pSubjectName, ref string m_pKey, ref ItemsMentioned m_pItems)
		{
			var pSQL = "SELECT SequenceNumber, SubjectName, KeyData FROM TSignRequest WHERE SubjectName = @SubjectName AND Revoked = FALSE AND LaunchAt <= now() AND now() < ExpireAt AND AuthorityId = @AuthorityId;";
			using (var pCommand = new NpgsqlCommand(pSQL, m_pConnection))
			{
				pCommand.Parameters.Clear();
				pCommand.Parameters.AddWithValue("SubjectName", pSubjectName);
				pCommand.Parameters.AddWithValue("AuthorityId", (Int64)uAuthorityId);
				using (var pReader = pCommand.ExecuteReader())
				{
					int iCount = 0;
					while (pReader.Read())
					{
						m_pItems.SequenceNumber = pReader.GetInt64(0);
						m_pItems.SubjectName = pReader.GetString(1);
						//m_pItems.KeyData        = pReader.GetString(2);
						m_pKey = pReader.GetString(2);

						iCount++;
					}
					if (iCount == 0)
					{
						return (false);
					}
				}
			}

			return (true);
		}

		public override bool SaveSignRequest(uint uAuthorityId, string m_pKey, ItemsMentioned m_pItems)
		{
			var status = true;

			BeginTransaction();
			//var pTransaction = BeginTransaction();

			try
			{
				var pSQL_UPDATE = "UPDATE TSignRequest SET Revoked = True, RevokeAt = now() WHERE AuthorityId = @AuthorityId AND CommonName = @CommonName";
				using (var pCommand = new NpgsqlCommand(pSQL_UPDATE, m_pConnection))
				{
					pCommand.Parameters.Clear();
					pCommand.Parameters.AddWithValue("AuthorityId", (Int64)uAuthorityId);
					pCommand.Parameters.AddWithValue("CommonName", m_pItems.CommonName);
					pCommand.ExecuteNonQuery();
				}

				var pSQL = "INSERT INTO TSignRequest (AuthorityId, SequenceNumber, SubjectName, CommonName, TypeOf, LaunchAt, ExpireAt, KeyData)";
				pSQL += " VALUES (@AuthorityId, NEXTVAL('SQ_REQTS'), @SubjectName, @CommonName, @TypeOf, @LaunchAt, @ExpireAt, @KeyData)";
				pSQL += " ON CONFLICT ON CONSTRAINT tsignrequest_pkey DO UPDATE SET";
				pSQL += " SubjectName = @SubjectName, CommonName = @CommonName, TypeOf = @TypeOf,";
				pSQL += " LaunchAt = @LaunchAt, ExpireAt = @ExpireAt, KeyData = @KeyData";
				using (var pCommand = new NpgsqlCommand(pSQL, m_pConnection))
				{
					pCommand.Parameters.Clear();
					pCommand.Parameters.AddWithValue("AuthorityId", (Int64)uAuthorityId);
					pCommand.Parameters.AddWithValue("SequenceNumber", m_pItems.SequenceNumber);
					pCommand.Parameters.AddWithValue("SubjectName", m_pItems.SubjectName);
					pCommand.Parameters.AddWithValue("CommonName", m_pItems.CommonName);
					pCommand.Parameters.AddWithValue("TypeOf", (int)m_pItems.TypeOf);
					pCommand.Parameters.AddWithValue("LaunchAt", m_pItems.LaunchAt);
					pCommand.Parameters.AddWithValue("ExpireAt", m_pItems.ExpireAt);
					pCommand.Parameters.AddWithValue("KeyData", m_pKey);
					pCommand.ExecuteNonQuery();
				}

				//pTransaction.Commit();
				Commit();
			}
			catch (Exception ex)
			{
				//pTransaction.Rollback();
				Rollback();
				Debug.WriteLine(ex);
				status = false;
			}

			return (status);
		}

		//　
		public override bool LoadCertificate(string pCommonName, uint uAuthorityId, ref ItemsMentioned m_pItems, ref string m_pCrt, ref string m_pKey)
		{
			//　共通名が一致する証明書を入力
			var pSQL = "SELECT SequenceNumber, SerialNumber, SubjectName, CommonName, TypeOf, Revoked, LaunchAt, ExpireAt, PemData, KeyData FROM TIssuedCerts WHERE CommonName = @CommonName AND Revoked = FALSE AND LaunchAt <= now() AND now() < ExpireAt AND AuthorityId = @AuthorityId;";
			using (var pCommand = CreateCommand(pSQL))
			{
				pCommand.Parameters.Clear();
				pCommand.Parameters.AddWithValue("CommonName", pCommonName);
				pCommand.Parameters.AddWithValue("AuthorityId", (Int64)uAuthorityId);
				using (var pReader = pCommand.ExecuteReader())
				{
					int iCount = 0;
					while (pReader.Read())
					{
						m_pItems.SequenceNumber = pReader.GetInt64(0);
						m_pItems.SerialNumber = pReader.GetString(1);
						m_pItems.SubjectName = pReader.GetString(2);
						m_pItems.CommonName = pReader.GetString(3);
						m_pItems.TypeOf = (CertificateType)pReader.GetInt32(4);
						m_pItems.Revoked = pReader.GetBoolean(5);
						m_pItems.LaunchAt = pReader.GetDateTime(6);
						m_pItems.ExpireAt = pReader.GetDateTime(7);
						m_pCrt = pReader.GetString(8);
						m_pKey = pReader.GetString(9);

						iCount++;
					}
					if (iCount == 0)
					{
						return (false);
					}
				}
			}
			/*
			//if ((m_pItems.KeyData != null) && (m_pItems.KeyData.Length > 0))
			if ((m_pKey != null) && (m_pKey.Length > 0))
			{
				m_pCertificate = X509Certificate2.CreateFromPem(m_pCrt, m_pKey);
			}
			else
			{
				m_pCertificate = X509Certificate2.CreateFromPem(m_pCrt);
			}
			*/
			return (true);
		}

		public override bool SaveCertificate(uint uAuthorityId, uint uInstance, ItemsMentioned m_pItems, string m_pCrt, string m_pKey)
		{
			var status = true;

			var pTransaction = BeginTransaction();

			try
			{
				var pSQL_UPDATE = "UPDATE TIssuedCerts SET Revoked = True, RevokeAt = now() WHERE AuthorityId = @AuthorityId AND CommonName = @CommonName";
				using (var pCommand = new NpgsqlCommand(pSQL_UPDATE, m_pConnection))
				{
					pCommand.Parameters.Clear();
					pCommand.Parameters.AddWithValue("AuthorityId", (Int64)uAuthorityId);
					pCommand.Parameters.AddWithValue("CommonName", m_pItems.CommonName);
					pCommand.ExecuteNonQuery();
				}

				var pSQL = "INSERT INTO TIssuedCerts (AuthorityId, SequenceNumber, SerialNumber, SubjectName, CommonName, TypeOf, Revoked, LaunchAt, ExpireAt, PemData, KeyData)";
				pSQL += " VALUES (@AuthorityId, NEXTVAL('SQ_REQTS'), @SerialNumber, @SubjectName, @CommonName, @TypeOf, FALSE, @LaunchAt, @ExpireAt, @PemData, @KeyData)";
				pSQL += " ON CONFLICT ON CONSTRAINT tissuedcerts_pkey DO UPDATE SET";
				pSQL += " SerialNumber = @SerialNumber, SubjectName = @SubjectName, CommonName = @CommonName, TypeOf = @TypeOf,";
				pSQL += " LaunchAt = @LaunchAt, ExpireAt = @ExpireAt, PemData = @PemData, KeyData = @KeyData";
				using (var pCommand = new NpgsqlCommand(pSQL, m_pConnection))
				{
					pCommand.Parameters.Clear();
					pCommand.Parameters.AddWithValue("AuthorityId", (Int64)uAuthorityId);
					pCommand.Parameters.AddWithValue("SequenceNumber", m_pItems.SequenceNumber);
					pCommand.Parameters.AddWithValue("SerialNumber", m_pItems.SerialNumber);
					pCommand.Parameters.AddWithValue("SubjectName", m_pItems.SubjectName);
					pCommand.Parameters.AddWithValue("CommonName", m_pItems.CommonName);
					pCommand.Parameters.AddWithValue("TypeOf", (int)m_pItems.TypeOf);
					pCommand.Parameters.AddWithValue("LaunchAt", m_pItems.LaunchAt);
					pCommand.Parameters.AddWithValue("ExpireAt", m_pItems.ExpireAt);
					pCommand.Parameters.AddWithValue("PemData", m_pCrt);
					pCommand.Parameters.AddWithValue("KeyData", m_pKey);
					pCommand.ExecuteNonQuery();
				}

				//pTransaction.Commit();
				Commit();
			}
			catch (Exception ex)
			{
				//pTransaction.Rollback();
				Rollback();
				Debug.WriteLine(ex);
				status = false;
			}

			return (status);
		}

		//　有効な証明祖の中に同一のサブジェクト名を持つ要素が存在するか検査
		public override bool IsExistSubject(string pSerialNumber, string pSubjectName, uint uAuthorityId)
		{
			var pSQL = "SELECT SequenceNumber, SerialNumber, SubjectName FROM TIssuedCerts WHERE SerialNumber <> @SerialNumber AND SubjectName = @SubjectName AND Revoked = FALSE AND LaunchAt <= now() AND now() < ExpireAt AND AuthorityId = @AuthorityId;";
			using (var pCommand = new NpgsqlCommand(pSQL, m_pConnection))
			{
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
			return (true);
		}

		/*
		//　有効な証明祖の中に同一のサブジェクト名を持つ要素が存在するか検査
		public override bool IsExistSubject(uint uAuthorityId, Certificate pCertificate, ItemsMentioned m_pItems)
		{
			var pSQL = "SELECT SequenceNumber, SerialNumber, SubjectName FROM TIssuedCerts WHERE SerialNumber <> @SerialNumber AND SubjectName = @SubjectName AND Revoked = FALSE AND LaunchAt <= now() AND now() < ExpireAt AND AuthorityId = @AuthorityId;";
			using (var pCommand = new NpgsqlCommand(pSQL, m_pConnection))
			{
				pCommand.Parameters.Clear();
				pCommand.Parameters.AddWithValue("SerialNumber", m_pItems.SerialNumber);
				pCommand.Parameters.AddWithValue("SubjectName", m_pItems.SubjectName);
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

			return (true);
		}
		*/

		//　証明書を失効
		public override bool Revoke(uint uAuthorityId, string SerialNumber)
		{
			var pSQL = "UPDATE TIssuedCerts SET Revoked = @Revoked, RevokeAt = now() WHERE SerialNumber = @SerialNumber AND AuthorityId = @AuthorityId";
			using (var pCommand = new NpgsqlCommand(pSQL, m_pConnection))
			{
				var Revoked = true;

				pCommand.Parameters.Clear();
				pCommand.Parameters.AddWithValue("SerialNumber", SerialNumber);
				pCommand.Parameters.AddWithValue("Revoked", Revoked);
				pCommand.Parameters.AddWithValue("AuthorityId", (Int64)uAuthorityId);
				pCommand.ExecuteNonQuery();
			}

			return (true);
		}

		//　
		public override ObservableCollection<Certificate> ListupCertificates(Int64 m_uAuthorityId)
		{
			var pCertificates = new ObservableCollection<Certificate>();

			var pSQL = "SELECT SequenceNumber, SerialNumber, CommonName, TypeOf, Revoked, LaunchAt, ExpireAt, PemData, KeyData FROM TIssuedCerts WHERE AuthorityId = @AuthorityId AND Revoked = FALSE AND LaunchAt <= now() AND now() < ExpireAt AND TypeOf <> @TypeOf;";
			using (var pCommand = new NpgsqlCommand(pSQL, m_pConnection))
			{
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

			return (pCertificates);
		}

		public override Certificate Fetch(string pSerialNumber)
		{
			var pCertificate = new Certificate();

			var pSQL = "SELECT SequenceNumber, SerialNumber, CommonName, TypeOf, Revoked, LaunchAt, ExpireAt, PemData, KeyData FROM TIssuedCerts WHERE SerialNumber = @SerialNumber AND TypeOf <> @TypeOf;";
			using (var pCommand = new NpgsqlCommand(pSQL, m_pConnection))
			{
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
						pCertificate.m_pCrt = pReader.GetString(7);
						pCertificate.m_pKey = pReader.GetString(8);
						pCertificate.Prepare();
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
/*
		public override bool Save2(uint uAuthorityId, uint uInstance, ItemsMentioned m_pItems, string m_pCrt, string m_pKey)
		{
			var pSQL_UPDATE = "UPDATE TIssuedCerts SET Revoked = True, RevokeAt = now() WHERE AuthorityId = @AuthorityId AND CommonName = @CommonName";
			using (var pCommand = new NpgsqlCommand(pSQL_UPDATE, m_pConnection))
			{
				pCommand.Parameters.Clear();
				pCommand.Parameters.AddWithValue("AuthorityId", (Int64)uAuthorityId);
				pCommand.Parameters.AddWithValue("CommonName", m_pItems.CommonName);
				pCommand.ExecuteNonQuery();
			}

			var pSQL = "INSERT INTO TIssuedCerts (AuthorityId, SequenceNumber, SerialNumber, SubjectName, CommonName, TypeOf, LaunchAt, ExpireAt, PemData, KeyData)";
			pSQL += " VALUES (@AuthorityId, NEXTVAL('SQ_REQTS'), @SerialNumber, @SubjectName, @CommonName, @TypeOf, @LaunchAt, @ExpireAt, @PemData, @KeyData)";
			pSQL += " ON CONFLICT ON CONSTRAINT tissuedcerts_pkey DO UPDATE SET";
			pSQL += " SerialNumber = @SerialNumber, SubjectName = @SubjectName, CommonName = @CommonName, TypeOf = @TypeOf,";
			pSQL += " LaunchAt = @LaunchAt, ExpireAt = @ExpireAt, PemData = @PemData, KeyData = @KeyData";
			using (var pCommand = new NpgsqlCommand(pSQL, m_pConnection))
			{
				pCommand.Parameters.Clear();
				pCommand.Parameters.AddWithValue("AuthorityId", (Int64)uAuthorityId);
				pCommand.Parameters.AddWithValue("SequenceNumber", m_pItems.SequenceNumber);
				pCommand.Parameters.AddWithValue("SerialNumber", m_pItems.SerialNumber);
				pCommand.Parameters.AddWithValue("SubjectName", m_pItems.SubjectName);
				pCommand.Parameters.AddWithValue("CommonName", m_pItems.CommonName);
				pCommand.Parameters.AddWithValue("TypeOf", (int)m_pItems.TypeOf);
				pCommand.Parameters.AddWithValue("LaunchAt", m_pItems.LaunchAt);
				pCommand.Parameters.AddWithValue("ExpireAt", m_pItems.ExpireAt);
				pCommand.Parameters.AddWithValue("PemData", m_pCrt);
				pCommand.Parameters.AddWithValue("KeyData", m_pKey);
				pCommand.ExecuteNonQuery();
			}

			return (true);
		}
*/
		//　CRLを生成
		public override byte[] GenerateCRL(uint m_uAuthorityId, int iDays, X509Certificate2 m_pCertificate)
		{
			byte[] pBytes;
			var pBuilder = new CertificateRevocationListBuilder();

			var pSQL = "SELECT SerialNumber, RevokeAt FROM TIssuedCerts WHERE Revoked = TRUE AND AuthorityId = @AuthorityId;";
			using (var pCommand = new NpgsqlCommand(pSQL, m_pConnection))
			{
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
			using (var pCommand = new NpgsqlCommand(pSQL, m_pConnection))
			{
				pCommand.Parameters.Clear();
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
			using (var pCommand = new NpgsqlCommand(pSQL, m_pConnection))
			{
				var pNumber = iCRLNumber.ToString("X");
				pCommand.Parameters.Clear();
				pCommand.Parameters.AddWithValue("CrlNumber", pNumber);
				pCommand.ExecuteNonQuery();
			}

			return (pBytes);
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

		//　PostgreSQL セッション変数を更新
		protected void SetSessionVariable(string VariableName, string VariableValue)
		{
			var pSQL = "SELECT SET_CONFIG(@VariableName, @VariableValue, false);";
			using (var pCommand = new NpgsqlCommand(pSQL, m_pConnection))
			{
				pCommand.Parameters.Clear();
				pCommand.Parameters.AddWithValue("VariableName", VariableName);
				pCommand.Parameters.AddWithValue("VariableValue", VariableValue);

				try
				{
					pCommand.ExecuteNonQuery();
				}
				catch (PostgresException e)
				{
					System.Diagnostics.Debug.WriteLine($"Mesage: {e.MessageText} Code: {e.ErrorCode}");
				}
			}

			return;
		}

		protected string GetSessionVariable(string VariableName)
		{
			string	VariableValue = null;

			var pSQL = "SELECT current_setting(@VariableName);";
			using (var pCommand = new NpgsqlCommand(pSQL, m_pConnection))
			{
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
				catch (PostgresException e)
				{
					System.Diagnostics.Debug.WriteLine($"Mesage: {e.MessageText} Code: {e.ErrorCode}");
				}
			}

			return (VariableValue);
		}
	}

	public class SQLContextEx : SQLContext
	{
		public SQLContextEx(string DatabaseServer, string DatabaseName, string SchemaName, string ClientKey, string ClientCrt, string TrustCrt, string AccountName, string AccessToken) : base(DatabaseServer, DatabaseName, SchemaName, ClientKey, ClientCrt, TrustCrt)
		{
			SetSessionVariable("app.AccountName", AccountName);
			SetSessionVariable("app.AccessToken", AccessToken);
			GetSessionVariable("app.AccountName");
		}
	}
}
