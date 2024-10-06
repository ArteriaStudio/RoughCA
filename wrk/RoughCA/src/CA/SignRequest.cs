using Arteria_s.DB.Base;
using Npgsql;
using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.IO;
using System.Diagnostics;

namespace Arteria_s.App.RoughCA
{
	public class SignRequest : IEquatable<SignRequest>
	{
		public ItemsMentioned		m_pItems = new ItemsMentioned();
		public CertificateRequest	m_pRequest = null;
		public DateTime				m_RevokeAt;
		public string				m_pKey;

		public SignRequest()
		{
			;
		}

		private bool IsNull(string pValue)
		{
			if (pValue == null)
			{
				return(true);
			}
			if (pValue.Equals("")) {
				return (true);
			}
			return(false);
		}

		//　
		public bool Validate()
		{
			if (m_pRequest == null)
			{
				return (false);
			}
			if (IsNull(m_pItems.CommonName))
			{
				return (false);
			}
			if (IsNull(m_pItems.SubjectName))
			{
				return (false);
			}

			return (true);
		}

		//　鍵を所有しているかを確認
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

		//　署名要求が存在するかを確認する。
		public static bool IsExists(SQLContext pSQLContext, uint uAuthorityId, string pSubjectName, string pCommonName)
		{
			var pSQL = "SELECT SequenceNumber, SubjectName FROM TSignRequest WHERE SubjectName = @SubjectName AND Revoked = FALSE AND LaunchAt <= now() AND now() < ExpireAt AND AuthorityId = @AuthorityId;";
			using (var pCommand = new NpgsqlCommand(pSQL, pSQLContext.m_pConnection))
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

		//　署名要求が存在するかを確認する。
		public bool Load(SQLContext pSQLContext, uint uAuthorityId, string pSubjectName)
		{
			var pSQL = "SELECT SequenceNumber, SubjectName, KeyData FROM TSignRequest WHERE SubjectName = @SubjectName AND Revoked = FALSE AND LaunchAt <= now() AND now() < ExpireAt AND AuthorityId = @AuthorityId;";
			using (var pCommand = new NpgsqlCommand(pSQL, pSQLContext.m_pConnection))
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
						m_pItems.SubjectName    = pReader.GetString(1);
						//m_pItems.KeyData        = pReader.GetString(2);
						m_pKey                  = pReader.GetString(2);

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

		//　
		public bool Save(SQLContext pSQLContext, uint uAuthorityId)
		{
			var status = true;

			var pTransaction = pSQLContext.BeginTransaction();

			try
			{
				var pSQL_UPDATE = "UPDATE TSignRequest SET Revoked = True, RevokeAt = now() WHERE AuthorityId = @AuthorityId AND CommonName = @CommonName";
				using (var pCommand = new NpgsqlCommand(pSQL_UPDATE, pSQLContext.m_pConnection))
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
				using (var pCommand = new NpgsqlCommand(pSQL, pSQLContext.m_pConnection))
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

		//　認証局署名要求を生成
		public bool CreateForRemand(OrgProfile pOrgProfile, string pCommonName)
		{
			//　証明書のサブジェクト名を生成
			var pSubjectName = ItemsMentioned.GenerateSubjectName(pOrgProfile, pCommonName);

			//　秘密鍵を生成（楕円曲線方式）
			ECDsaCng pKeys = new ECDsaCng(RoughCA_Const.ECDSAKEY_SIZE);

			//　メール証明書用の署名要求を生成
			m_pRequest = CertificateProvider.CreateSignRequest(pKeys, pOrgProfile, pCommonName);
			if (m_pRequest == null)
			{
				return (false);
			}

			//　証明書記載情報の主要なものをキャッシュ
			var pNotBefore = DateTime.UtcNow;
			var pNotAfter  = DateTime.UtcNow.AddDays(128);

			m_pItems.SequenceNumber = 0;
			//m_pItems.SerialNumber   = pRequest.SerialNumber;
			m_pItems.SubjectName    = m_pRequest.SubjectName.Name;
			m_pItems.CommonName     = ItemsMentioned.GetDistinguishedValue("CN", m_pRequest.SubjectName);
			m_pItems.TypeOf         = CertificateType.Demand;
			m_pItems.Revoked        = false;
			m_pItems.LaunchAt       = pNotBefore;
			m_pItems.ExpireAt       = pNotAfter;

			//　秘密鍵をPEM 形式で保管
			m_pKey = pKeys.ExportECPrivateKeyPem();

			return (true);
		}

		//　指定したフォルダに署名要求をDER 形式でファイルに出力
		public string Export(string pExportFolder)
		{
			var pBytesOfReq = m_pRequest.CreateSigningRequest();
			var pExportFilepathOfReq = pExportFolder + "\\" + m_pItems.CommonName + RoughCA_Const.CSR_EXTENSION;
			File.WriteAllBytes(pExportFilepathOfReq, pBytesOfReq);

			return (pExportFilepathOfReq);
		}

		public bool Equals(SignRequest other)
		{
			throw new NotImplementedException();
		}

		//　
		public void Dump()
		{
			Debug.WriteLine("Subject: " + m_pRequest.SubjectName.Name);
			foreach (var pCertificateExtension in m_pRequest.CertificateExtensions)
			{
				Debug.WriteLine("Extension: " + pCertificateExtension.Oid.FriendlyName + " = " + pCertificateExtension.RawData);
			}

			Debug.WriteLine(m_pRequest.ToString());
		}
	}
}
