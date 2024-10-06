using Arteria_s.DB.Base;
using Npgsql;
using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Arteria_s.App.RoughCA
{
	public class Certificate : IEquatable<Certificate>
	{
		public ItemsMentioned		m_pItems = new ItemsMentioned();
		public X509Certificate2 	m_pCertificate { get; set; }
		public string				m_pCrt;
		public string				m_pKey;

		public Certificate()
		{
		}

		private bool IsNull(string pValue)
		{
			if (pValue == null)
			{
				return (true);
			}
			if (pValue.Equals(""))
			{
				return (true);
			}
			return (false);
		}

		//　共通名が一致する証明書を入力
		public bool Load(SQLContext pSQLContext, string pCommonName, uint uAuthorityId)
		{
			//　共通名が一致する証明書を入力
			var pSQL = "SELECT SequenceNumber, SerialNumber, SubjectName, CommonName, TypeOf, Revoked, LaunchAt, ExpireAt, PemData, KeyData FROM TIssuedCerts WHERE CommonName = @CommonName AND Revoked = FALSE AND LaunchAt <= now() AND now() < ExpireAt AND AuthorityId = @AuthorityId;";
			using (var pCommand = new NpgsqlCommand(pSQL, pSQLContext.m_pConnection))
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
						m_pItems.SerialNumber   = pReader.GetString(1);
						m_pItems.SubjectName    = pReader.GetString(2);
						m_pItems.CommonName     = pReader.GetString(3);
						m_pItems.TypeOf         = (CertificateType)pReader.GetInt32(4);
						m_pItems.Revoked        = pReader.GetBoolean(5);
						m_pItems.LaunchAt       = pReader.GetDateTime(6);
						m_pItems.ExpireAt       = pReader.GetDateTime(7);
						m_pCrt                  = pReader.GetString(8);
						m_pKey                  = pReader.GetString(9);

						iCount++;
					}
					if (iCount == 0)
					{
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

			return (true);
		}

		//　
		//　pKeyData：当該証明書に紐付く秘密鍵
		public bool Save(SQLContext pSQLContext, uint uAuthorityId, string pKeyData = null)
		{
			var status = true;

			if (pKeyData != null)
			{
				m_pKey = pKeyData;
			}

			var pTransaction = pSQLContext.BeginTransaction();

			try
			{
				var pSQL_UPDATE = "UPDATE TIssuedCerts SET Revoked = True, RevokeAt = now() WHERE AuthorityId = @AuthorityId AND CommonName = @CommonName";
				using (var pCommand = new NpgsqlCommand(pSQL_UPDATE, pSQLContext.m_pConnection))
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
				using (var pCommand = new NpgsqlCommand(pSQL, pSQLContext.m_pConnection))
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

				pTransaction.Commit();
			}
			catch (Exception ex)
			{
				pTransaction.Rollback();
				Debug.WriteLine(ex);
				status = false;
			}

			return (status);
		}

		//　
		//　pKeyData：当該証明書に紐付く秘密鍵
		public bool Save2(SQLContext pSQLContext, uint uAuthorityId, string pKeyData = null)
		{
			var status = true;

			if (pKeyData != null)
			{
				m_pKey = pKeyData;
			}

			try
			{
				var pSQL_UPDATE = "UPDATE TIssuedCerts SET Revoked = True, RevokeAt = now() WHERE AuthorityId = @AuthorityId AND CommonName = @CommonName";
				using (var pCommand = new NpgsqlCommand(pSQL_UPDATE, pSQLContext.m_pConnection))
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
				using (var pCommand = new NpgsqlCommand(pSQL, pSQLContext.m_pConnection))
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
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex);
				status = false;
			}

			return (status);
		}

		//　
		public void Prepare()
		{
			//if ((m_pItems.KeyData != null) && (m_pItems.KeyData.Length > 0))
			if ((m_pKey != null) && (m_pKey.Length > 0))
			{
				if (m_pItems.TypeOf == CertificateType.Demand)
				{
					;
				}
				else
				{
					m_pCertificate = X509Certificate2.CreateFromPem(m_pCrt, m_pKey);
				}
			}
			else
			{
				m_pCertificate = X509Certificate2.CreateFromPem(m_pCrt);
			}

			return;
		}

		//　認証局証明書を生成
		//　pCommonName：
		//　pCACertificate：署名する認証局の証明書データ
		public bool CreateForAuthority(OrgProfile pOrgProfile, string pCommonName, Certificate pCACertificate)
		{
			//　秘密鍵を生成（楕円曲線方式）
			ECDsaCng pKeys = new ECDsaCng(RoughCA_Const.ECDSAKEY_SIZE);

			//　認証局の署名要求を生成
			var pRequest = CertificateProvider.CreateSignRequest(pKeys, pOrgProfile, pCommonName);
			if (pRequest == null)
			{
				return (false);
			}
			//　デバッグ用：署名要求のバイト列を生成
			//var pBytes = pRequest.CreateSigningRequest();

			if (pCACertificate == null)
			{
				var iLifeDays = 365 * 10;
				m_pCertificate = CertificateProvider.CreateSelfCertificate(pRequest, pCommonName, iLifeDays, pOrgProfile.ServerName);
				if (m_pCertificate == null)
				{
					return (false);
				}
				//　自己署名証明書を生成した時は、秘密鍵が証明書オブジェクトに含まれている。
			}
			else
			{
				//　ルート認証局の証明書データで署名した発行認証局の証明書を生成
				var iLifeDays = 365 * 5;
				m_pCertificate = CertificateProvider.CreateCertificate(pRequest, pCACertificate, iLifeDays, pOrgProfile.ServerName);
				if (m_pCertificate == null)
				{
					return (false);
				}
				//　通常のシーケンスだと証明書オブジェクトに鍵ペアは含まれない。
			}

			//　証明書記載情報の主要なものをキャッシュ
			m_pItems.SequenceNumber = 0;
			m_pItems.SerialNumber   = m_pCertificate.SerialNumber;
			m_pItems.SubjectName    = m_pCertificate.SubjectName.Name;
			m_pItems.CommonName     = ItemsMentioned.GetDistinguishedValue("CN", m_pCertificate.SubjectName);
			m_pItems.TypeOf         = CertificateType.CA;
			m_pItems.Revoked        = false;
			m_pItems.LaunchAt       = m_pCertificate.NotBefore;
			m_pItems.ExpireAt       = m_pCertificate.NotAfter;
			
			//　証明書をPEM 形式に変換して保管
			m_pCrt = m_pCertificate.ExportCertificatePem();

			//　秘密鍵をPEM 形式に変換して保管
			m_pKey = pKeys.ExportECPrivateKeyPem();

			return (true);
		}

		//　認証局証明書を生成
		//　pCACertificate：認証局証明書
		//　pSignRequest：署名する認証局の署名要求データ
		public bool CreateForAuthority(OrgProfile pOrgProfile, Certificate pCACertificate, CertificateRequest pSignRequest)
		{
			//　ルート認証局の証明書データで署名した発行認証局の証明書を生成
			var iLifeDays = 365 * 5;
			m_pCertificate = CertificateProvider.CreateCertificate(pSignRequest, pCACertificate, iLifeDays, pOrgProfile.ServerName);
			if (m_pCertificate == null)
			{
				return (false);
			}
			//　通常のシーケンスだと証明書オブジェクトに鍵ペアは含まれない。

			//　証明書記載情報の主要なものをキャッシュ
			m_pItems.SequenceNumber = 0;
			m_pItems.SerialNumber   = m_pCertificate.SerialNumber;
			m_pItems.SubjectName    = m_pCertificate.SubjectName.Name;
			m_pItems.CommonName     = ItemsMentioned.GetDistinguishedValue("CN", m_pCertificate.SubjectName);
			m_pItems.TypeOf         = CertificateType.CA;
			m_pItems.Revoked        = false;
			m_pItems.LaunchAt       = m_pCertificate.NotBefore;
			m_pItems.ExpireAt       = m_pCertificate.NotAfter;

			m_pCrt = m_pCertificate.ExportCertificatePem();
			m_pKey = "";

			return (true);
		}

		//　サーバー証明書を生成（署名要求から証明書を作成）
		//　pCACertificate：認証局証明書
		//　pSignRequest：署名する認証局の署名要求データ
		public bool CreateForServer(OrgProfile pOrgProfile, Certificate pCACertificate, CertificateRequest pSignRequest)
		{
			//　ルート認証局の証明書データで署名した発行認証局の証明書を生成
			var iLifeDays = 365 * 1;
			m_pCertificate = CertificateProvider.CreateCertificate(pSignRequest, pCACertificate, iLifeDays, pOrgProfile.ServerName);
			if (m_pCertificate == null)
			{
				return (false);
			}
			//　通常のシーケンスだと証明書オブジェクトに鍵ペアは含まれない。

			//　証明書記載情報の主要なものをキャッシュ
			m_pItems.SequenceNumber = 0;
			m_pItems.SerialNumber   = m_pCertificate.SerialNumber;
			m_pItems.SubjectName    = m_pCertificate.SubjectName.Name;
			m_pItems.CommonName     = ItemsMentioned.GetDistinguishedValue("CN", m_pCertificate.SubjectName);
			m_pItems.TypeOf         = CertificateType.Server;
			m_pItems.Revoked        = false;
			m_pItems.LaunchAt       = m_pCertificate.NotBefore;
			m_pItems.ExpireAt       = m_pCertificate.NotAfter;

			m_pCrt = m_pCertificate.ExportCertificatePem();
			m_pKey = "";

			return (true);
		}

		//　サーバ証明書を生成
		public bool CreateForServer(OrgProfile pOrgProfile, string pCommonName, string pFQDN, Certificate pCACertificate, bool fCA)
		{
			//　認証局の証明書データが指定されていない時はエラー
			if (pCACertificate == null)
			{
				return (false);
			}

			//　証明書のサブジェクト名を生成
			var pSubjectName = ItemsMentioned.GenerateSubjectName(pOrgProfile, pCommonName);

			//　秘密鍵を生成（楕円曲線方式）
			ECDsaCng pKeys = new ECDsaCng(RoughCA_Const.ECDSAKEY_SIZE);

			//　サーバ証明書用の署名要求を生成
			var pRequest = CertificateProvider.CreateSignRequestForServer(pKeys, pOrgProfile, pSubjectName, pCommonName, pFQDN, fCA);
			if (pRequest == null)
			{
				return (false);
			}
			//　指定された認証局の証明書で署名
			var iLifeDays = 365 * 1;
			m_pCertificate = CertificateProvider.CreateCertificate(pRequest, pCACertificate, iLifeDays, pOrgProfile.ServerName);
			if (m_pCertificate == null)
			{
				return (false);
			}

			//　証明書記載情報の主要なものをキャッシュ
			m_pItems.SequenceNumber = 0;
			m_pItems.SerialNumber   = m_pCertificate.SerialNumber;
			m_pItems.SubjectName    = m_pCertificate.SubjectName.Name;
			m_pItems.CommonName     = ItemsMentioned.GetDistinguishedValue("CN", m_pCertificate.SubjectName);
			m_pItems.TypeOf         = CertificateType.Server;
			m_pItems.Revoked        = false;
			m_pItems.LaunchAt       = m_pCertificate.NotBefore;
			m_pItems.ExpireAt       = m_pCertificate.NotAfter;

			//　証明書データをPEM 形式に変換して保管
			m_pCrt = m_pCertificate.ExportCertificatePem();

			//　秘密鍵データをPEM 形式に変換して保管
			m_pKey = pKeys.ExportECPrivateKeyPem();

			return (true);
		}

		//　メール証明書を生成
		public bool CreateForClient(OrgProfile pOrgProfile, string pCommonName, string pMailAddress, Certificate pCACertificate, bool fCA)
		{
			//　認証局の証明書データが指定されていない時はエラー
			if (pCACertificate == null)
			{
				return (false);
			}

			//　証明書のサブジェクト名を生成
			var pSubjectName = ItemsMentioned.GenerateSubjectName(pOrgProfile, pCommonName);

			//　秘密鍵を生成（楕円曲線方式）
			ECDsaCng pKeys = new ECDsaCng(RoughCA_Const.ECDSAKEY_SIZE);

			//　メール証明書用の署名要求を生成
			var pRequest = CertificateProvider.CreateSignRequestForClient(pKeys, pOrgProfile, pSubjectName, pCommonName, pMailAddress, fCA);
			if (pRequest == null)
			{
				return (false);
			}
			//　指定された認証局の証明書で署名
			var iLifeDays = 365 * 1;
			m_pCertificate = CertificateProvider.CreateCertificate(pRequest, pCACertificate, iLifeDays, pOrgProfile.ServerName);
			if (m_pCertificate == null)
			{
				return (false);
			}

			//　証明書記載情報の主要なものをキャッシュ
			//FetchProperties(m_pCertificate, pKeys, CertificateType.Client);
			m_pItems.SequenceNumber = 0;
			m_pItems.SerialNumber   = m_pCertificate.SerialNumber;
			m_pItems.SubjectName    = m_pCertificate.SubjectName.Name;
			m_pItems.CommonName     = ItemsMentioned.GetDistinguishedValue("CN", m_pCertificate.SubjectName);
			m_pItems.TypeOf         = CertificateType.Client;
			m_pItems.Revoked        = false;
			m_pItems.LaunchAt       = m_pCertificate.NotBefore;
			m_pItems.ExpireAt       = m_pCertificate.NotAfter;

			//　証明書データをPEM 形式に変換して保管
			m_pCrt = m_pCertificate.ExportCertificatePem();

			//　秘密鍵データをPEM 形式に変換して保管
			m_pKey = pKeys.ExportECPrivateKeyPem();

			return (true);
		}

		//　指定された証明書データに基づいて有効期限を延長した証明書を作成する。
		//　for Updateとあるが、Oracleの行ロックとは関係ない。
		//　pBaseCertificate：転記元の証明書
		public bool CreateForUpdate(OrgProfile pOrgProfile, Certificate pBaseCertificate, Certificate pCACertificate)
		{
			//　認証局の証明書データが指定されていない時はエラー
			if ((pCACertificate == null) || (pCACertificate.m_pCertificate == null))
			{
				return (false);
			}
			//　継承元の証明書が指定されていない時もエラー
			if ((pBaseCertificate == null) || (pBaseCertificate.m_pCertificate == null))
			{
				return (false);
			}

			//　秘密鍵を生成（楕円曲線方式）
			ECDsaCng pKeys = new ECDsaCng(RoughCA_Const.ECDSAKEY_SIZE);

			//　メール証明書用の署名要求を生成
			var pRequest = CertificateProvider.CreateSignRequestForUpdate(pKeys, pOrgProfile, pBaseCertificate.m_pCertificate);
			if (pRequest == null)
			{
				return (false);
			}
			//　指定された認証局の証明書で署名
			var iLifeDays = 365 * 1;
			m_pCertificate = CertificateProvider.CreateCertificate(pRequest, pCACertificate, iLifeDays, pOrgProfile.ServerName);
			if (m_pCertificate == null)
			{
				return (false);
			}

			//　証明書記載情報の主要なものをキャッシュ
			//FetchProperties(m_pCertificate, pKeys, pBaseCertificate.m_pItems.TypeOf);
			m_pItems.SequenceNumber = 0;
			m_pItems.SerialNumber   = m_pCertificate.SerialNumber;
			m_pItems.SubjectName    = m_pCertificate.SubjectName.Name;
			m_pItems.CommonName     = ItemsMentioned.GetDistinguishedValue("CN", m_pCertificate.SubjectName);
			m_pItems.TypeOf         = pBaseCertificate.m_pItems.TypeOf;
			m_pItems.Revoked        = false;
			m_pItems.LaunchAt       = m_pCertificate.NotBefore;
			m_pItems.ExpireAt       = m_pCertificate.NotAfter;

			//　証明書データをPEM 形式に変換して保管
			m_pCrt = m_pCertificate.ExportCertificatePem();

			//　秘密鍵データをPEM 形式に変換して保管
			m_pKey = pKeys.ExportECPrivateKeyPem();

			return (true);
		}

		//　証明書を失効
		public bool Revoke(SQLContext pSQLContext, uint uAuthorityId)
		{
			var pSQL = "UPDATE TIssuedCerts SET Revoked = @Revoked, RevokeAt = now() WHERE SerialNumber = @SerialNumber AND AuthorityId = @AuthorityId";
			using (var pCommand = new NpgsqlCommand(pSQL, pSQLContext.m_pConnection))
			{
				m_pItems.Revoked = true;

				pCommand.Parameters.Clear();
				pCommand.Parameters.AddWithValue("SerialNumber", m_pItems.SerialNumber);
				pCommand.Parameters.AddWithValue("Revoked", m_pItems.Revoked);
				pCommand.Parameters.AddWithValue("AuthorityId", (Int64)uAuthorityId);
				pCommand.ExecuteNonQuery();
			}

			return (true);
		}

		//　有効な証明祖の中に同一のサブジェクト名を持つ要素が存在するか検査
		protected bool IsExistSubject(SQLContext pSQLContext, string pSerialNumber, string pSubjectName, uint uAuthorityId)
		{
			var pSQL = "SELECT SequenceNumber, SerialNumber, SubjectName FROM TIssuedCerts WHERE SerialNumber <> @SerialNumber AND SubjectName = @SubjectName AND Revoked = FALSE AND LaunchAt <= now() AND now() < ExpireAt AND AuthorityId = @AuthorityId;";
			using (var pCommand = new NpgsqlCommand(pSQL, pSQLContext.m_pConnection))
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

		//　有効な証明祖の中に同一のサブジェクト名を持つ要素が存在するか検査
		public bool IsExistSubject(SQLContext pSQLContext, uint uAuthorityId)
		{
			var pSQL = "SELECT SequenceNumber, SerialNumber, SubjectName FROM TIssuedCerts WHERE SerialNumber <> @SerialNumber AND SubjectName = @SubjectName AND Revoked = FALSE AND LaunchAt <= now() AND now() < ExpireAt AND AuthorityId = @AuthorityId;";
			using (var pCommand = new NpgsqlCommand(pSQL, pSQLContext.m_pConnection))
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

		//　証明書データの有効性を検査
		public bool	Validate()
		{
			if (m_pItems.SequenceNumber == -1)
			{
				return (false);
			}
			if (m_pCertificate == null)
			{
				return (false);
			}
			if (IsNull(m_pItems.SerialNumber))
			{
				return (false);
			}
			if (IsNull(m_pItems.CommonName))
			{
				return (false);
			}

			return (true);
		}

		public bool IsHaveKey()
		{
			if (m_pKey == null)
			{
				return (false);
			}
			if (m_pKey.Equals(""))
			{
				return (false);
			}
			return (true);
		}
		/*
		//　X509証明書データとECDsa鍵データをオブジェクトに入力
		//　pKeys：証明書の署名に用いる当該認証局の秘密鍵
		private void FetchProperties(X509Certificate2 pCertificate, ECDsaCng pKeys, CertificateType eTypeOf)
		{
			m_pItems.SequenceNumber = 0;
			m_pItems.SerialNumber   = pCertificate.SerialNumber;
			m_pItems.SubjectName    = pCertificate.SubjectName.Name;
			m_pItems.CommonName     = ItemsMentioned.GetDistinguishedValue("CN", pCertificate.SubjectName);
			m_pItems.TypeOf         = eTypeOf;
			m_pItems.Revoked        = false;
			m_pItems.LaunchAt       = pCertificate.NotBefore;
			m_pItems.ExpireAt       = pCertificate.NotAfter;
			m_pCrt        = pCertificate.ExportCertificatePem();
			if (pKeys != null)
			{
				//m_pItems.KeyData = pKeys.ExportECPrivateKeyPem();
				m_pKey = pKeys.ExportECPrivateKeyPem();
			}

			if (m_pCertificate != null)
			{
				//m_pCertificate = X509Certificate2.CreateFromPem(m_pItems.PemData, m_pItems.KeyData);
			}
		}
		*/
		//　指定したフォルダに証明書と鍵をファイル形式で出力
		public string Export(string pExportFolder)
		{
			//　証明書が秘密鍵を持っていれば、それを出力する。
			var pPrivateKey = m_pCertificate.GetECDsaPrivateKey();
			if (pPrivateKey != null)
			{
				var pBytesOfKey = pPrivateKey.ExportECPrivateKey();
				var pExportFilepathOfKey = pExportFolder + "\\" + m_pItems.CommonName + RoughCA_Const.KEY_EXTENSION;
				File.WriteAllBytes(pExportFilepathOfKey, pBytesOfKey);
			}

			var pBytesOfCrt = m_pCertificate.Export(X509ContentType.Cert);
			var pExportFilepathOfCrt = pExportFolder + "\\" + m_pItems.CommonName + RoughCA_Const.CRT_EXTENSION;
			File.WriteAllBytes(pExportFilepathOfCrt, pBytesOfCrt);

			return (pExportFilepathOfCrt);
		}

		//　指定した証明書データを含むファイルを入力
		public bool Import(string pImportFilepath)
		{
			var pBytes = File.ReadAllBytes(pImportFilepath);
			m_pCertificate = new X509Certificate2(pBytes);
			if (m_pCertificate == null)
			{
				return (false);
			}
			m_pItems.SequenceNumber = 0;
			m_pItems.SerialNumber   = m_pCertificate.SerialNumber;
			m_pItems.SubjectName    = m_pCertificate.SubjectName.Name;
			m_pItems.CommonName     = ItemsMentioned.GetDistinguishedValue("CN", m_pCertificate.SubjectName);
			m_pItems.TypeOf         = CertificateType.CA;
			m_pItems.Revoked        = false;
			m_pItems.LaunchAt       = m_pCertificate.NotBefore;
			m_pItems.ExpireAt       = m_pCertificate.NotAfter;
			m_pCrt = m_pCertificate.ExportCertificatePem();
			m_pKey = null;

			return (true);
		}

		//　秘密鍵と証明書をファイルに出力
		protected void DumpToFile(ECDsaCng pKeys)
		{
			var pBytesOfKey = pKeys.ExportECPrivateKey();
			File.WriteAllBytes("D:/tmp/" + m_pItems.CommonName + RoughCA_Const.KEY_EXTENSION, pBytesOfKey);
			var pBytesOfCrt = m_pCertificate.Export(X509ContentType.Cert);
			File.WriteAllBytes("D:/tmp/" + m_pItems.CommonName + RoughCA_Const.CRT_EXTENSION, pBytesOfCrt);
		}

		//　証明書データに収められた秘密鍵を比較（動作検証用）
		protected void Dump()
		{
			var pKey = m_pCertificate.GetECDsaPrivateKey();
			if (pKey == null)
			{
				;
			}
			else
			{
				var pRoot = pKey.ExportECPrivateKeyPem();
				if (pRoot == m_pKey)
				{
					//　ルート証明書を自己署名した証明書データは、このケースに来る。
					//　つまり、X509オブジェクトのインスタンスに何故か秘密鍵が含まれている。
					Debug.WriteLine("Match");
				}
				else
				{
					Debug.WriteLine("UnMatch");
				}
			}
		}

		//　鍵をダンプ（デバッグ用）
		protected void Dump(ECDsaCng pKeys)
		{
			//　秘密鍵をテキストに変換
			var pTextOfKey1 = pKeys.ExportPkcs8PrivateKey();
			var pText1 = Convert.ToBase64String(pTextOfKey1);
			Debug.WriteLine("PrivateKey(PKCS#8):" + pText1);
		}

		public bool Equals(Certificate pOther)
		{
			if (pOther.m_pItems.SerialNumber != m_pItems.SerialNumber)
			{
				return false;
			}

			return(true);
		}
	}
}
