using Npgsql;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Arteria_s.DB.Base;

namespace Arteria_s.App.RoughCA
{
	//　証明書データをコレクションするクラス
	//　認証局クラス
	public class Authority
	{
		private Authority()
		{
			;
		}

		public static Authority Instance { get; set; } = new Authority();

		public Certificate	m_pAuthorityItem;	//　認証局証明書

		public bool Validate(SQLContext pSQLContext)
		{
			if (m_pAuthorityItem == null)
			{
				return(false);
			}
			if (m_pAuthorityItem.IsHaveKey() == false)
			{
				return (false);
			}
			return (true);
		}

		public OrgProfile	m_pOrgProfile;
		private uint		m_uAuthorityId; 	//　認証局識別子
		private string		m_pAuthorityName;	//　認証局名

		//　認証局の証明書と鍵を入力
		public bool Load(SQLContext pSQLContext, string pIdentityName)
		{
			//　認証局識別子を作成
			m_uAuthorityId = ConvertIdentity(pIdentityName);
			m_pAuthorityName = pIdentityName;

			//　組織プロファイルと認証局情報を入力
			var iUserIdentity = 0;
			m_pOrgProfile = new OrgProfile();
			m_pOrgProfile.Load(pSQLContext, iUserIdentity);
			if (m_pOrgProfile.Validate() == false)
			{
				;
			}
			else
			{
				//　認証局の証明書データを入力
				m_pAuthorityItem = new Certificate();
				var pAuthorityName = pIdentityName;
				if (m_pAuthorityItem.Load(pSQLContext, pAuthorityName, m_uAuthorityId) == false)
				{
					//　自己署名認証局の証明書を作成
					if (m_pAuthorityItem.CreateForAuthority(m_pOrgProfile, pAuthorityName, null) == false)
					{
						//　異常系：証明書の作成に失敗
						return (false);
					}
					if (m_pAuthorityItem.Validate() == false)
					{
						return (false);
					}
					if (m_pAuthorityItem.IsHaveKey() == false)
					{
						return (false);
					}
					if (m_pAuthorityItem.Save(pSQLContext, m_uAuthorityId) == false)
					{
						return (false);
					}
				}
			}

			return (true);
		}

		//　サーバ証明書を生成する。
		//　fOverWrite：同一のサブジェクトを持つ証明書があった場合に有効な証明書を当該証明書に差し替える。
		public bool CreateForServer(SQLContext pSQLContext, string pCommonName, string pFQDN, bool fCA, bool fOverWrite)
		{
			var pCertificate = new Certificate();
			if (pCertificate.CreateForServer(m_pOrgProfile, pCommonName, pFQDN, m_pAuthorityItem, fCA) == false)
			{
				throw (new AppException(AppError.FailureCreateCertificate, AppFacility.Error, AppFlow.CreateCertificateForServer, pCommonName));
			}
			if (pCertificate.Validate() == false)
			{
				throw (new AppException(AppError.ExistSameCertificate, AppFacility.Error, AppFlow.CreateCertificateForServer, pCommonName));
			}
			if (pCertificate.IsHaveKey() == false)
			{
				throw (new AppException(AppError.ExistSameCertificate, AppFacility.Error, AppFlow.CreateCertificateForServer, pCommonName));
			}
			if (pCertificate.IsExistSubject(pSQLContext, m_uAuthorityId) == true)
			{
				//　同一のサブジェクトを持つ証明書が既に発行されている。
				if (fOverWrite == false)
				{
					throw (new AppException(AppError.ExistSameCertificate, AppFacility.Error, AppFlow.CreateCertificateForServer, pCommonName));
				}
			}
			if (pCertificate.Save(pSQLContext, m_uAuthorityId) == false)
			{
				throw (new AppException(AppError.FailreSaveCertificate, AppFacility.Error, AppFlow.CreateCertificateForServer, pCommonName));
			}

			return (true);
		}

		//　メール証明書を生成する。
		public bool CreateForClient(SQLContext pSQLContext, string pCommonName, string pMailAddress, bool fCA, bool fOverWrite)
		{
			var pCertificate = new Certificate();
			if (pCertificate.CreateForClient(m_pOrgProfile, pCommonName, pMailAddress, m_pAuthorityItem, fCA) == false)
			{
				throw (new AppException(AppError.FailureCreateCertificate, AppFacility.Error, AppFlow.CreateCertificateForClient, pCommonName));
			}
			if (pCertificate.Validate() == false)
			{
				throw (new AppException(AppError.ExistSameCertificate, AppFacility.Error, AppFlow.CreateCertificateForClient, pCommonName));
			}
			if (pCertificate.IsHaveKey() == false)
			{
				throw (new AppException(AppError.ExistSameCertificate, AppFacility.Error, AppFlow.CreateCertificateForClient, pCommonName));
			}
			if (pCertificate.IsExistSubject(pSQLContext, m_uAuthorityId) == true)
			{
				//　同一のサブジェクトを持つ証明書が既に発行されている。
				if (fOverWrite == false)
				{
					throw (new AppException(AppError.ExistSameCertificate, AppFacility.Error, AppFlow.CreateCertificateForClient, pCommonName));
				}
			}
			if (pCertificate.Save(pSQLContext, m_uAuthorityId) == false)
			{
				throw (new AppException(AppError.FailreSaveCertificate, AppFacility.Error, AppFlow.CreateCertificateForClient, pCommonName));
			}

			return (true);
		}

		//　認証局署名要求を生成する。
		public bool CreateForDemand(SQLContext pSQLContext, string pCommonName)
		{
			var pSignRequest = new SignRequest();
			if (pSignRequest.CreateForRemand(m_pOrgProfile, pCommonName) == false)
			{
				throw (new AppException(AppError.FailureCreateCertificate, AppFacility.Error, AppFlow.CreateCertificateForClient, pCommonName));
			}
			if (pSignRequest.Validate() == false)
			{
				throw (new AppException(AppError.ExistSameCertificate, AppFacility.Error, AppFlow.CreateCertificateForClient, pCommonName));
			}
			if (pSignRequest.IsHaveKey() == false)
			{
				throw (new AppException(AppError.ExistSameCertificate, AppFacility.Error, AppFlow.CreateCertificateForClient, pCommonName));
			}
			if (pSignRequest.Save(pSQLContext, m_uAuthorityId) == false)
			{
				throw (new AppException(AppError.FailreSaveCertificate, AppFacility.Error, AppFlow.CreateCertificateForClient, pCommonName));
			}
			var pExportFolder = System.Environment.GetEnvironmentVariable("USERPROFILE") + "\\Downloads";
			var pExportFilepath = pSignRequest.Export(pExportFolder);

			return (true);
		}

		//　CA証明書の署名要求に署名してCA証明書を作成する。
		//　
		public bool CreateForCACert(SQLContext pSQLContext, string pImportFilepath, bool fOverWrite)
		{
			var pBytes = File.ReadAllBytes(pImportFilepath);

			//　署名要求に記載された事項も入力（CertificateRequestLoadOptions.UnsafeLoadCertificateExtensions）
			//var pSignRequest = CertificateRequest.LoadSigningRequest(pBytes, HashAlgorithmName.SHA512);
			var pSignRequest = CertificateRequest.LoadSigningRequest(pBytes, HashAlgorithmName.SHA512, CertificateRequestLoadOptions.UnsafeLoadCertificateExtensions);
			if (pSignRequest == null) {
				return (false);
			}
			var pCommonName = ItemsMentioned.GetDistinguishedValue("CN", pSignRequest.SubjectName);

			var pCertificate = new Certificate();
			if (pCertificate.CreateForAuthority(m_pOrgProfile, m_pAuthorityItem, pSignRequest) == false)
			{
				throw (new AppException(AppError.FailureCreateCertificate, AppFacility.Error, AppFlow.CreateCertificateForServer, pCommonName));
			}
			if (pCertificate.Validate() == false)
			{
				return (false);
			}
			//　認証局が発行した有効な証明書の中にサブジェクト名の重複がないことを検査
			if (pCertificate.IsExistSubject(pSQLContext, m_uAuthorityId) == true)
			{
				//　同一の共通名を持つ証明書が存在する。
				if (fOverWrite == false)
				{
					throw (new AppException(AppError.ExistSameCertificate, AppFacility.Error, AppFlow.CreateCertificateForServer, pCommonName));
				}
			}
			if (pCertificate.Save(pSQLContext, m_uAuthorityId) == false)
			{
				throw (new AppException(AppError.FailreSaveCertificate, AppFacility.Error, AppFlow.CreateCertificateForServer, pCommonName));
			}

			return (true);
		}

		//　サーバー証明書の署名要求に署名してサーバー証明書を作成する。
		//　
		public bool CreateForServerCert(SQLContext pSQLContext, string pImportFilepath, bool fOverWrite)
		{
			var pBytes = File.ReadAllBytes(pImportFilepath);

			//　署名要求に記載された事項も入力（CertificateRequestLoadOptions.UnsafeLoadCertificateExtensions）
			//var pSignRequest = CertificateRequest.LoadSigningRequest(pBytes, HashAlgorithmName.SHA512);
			var pSignRequest = CertificateRequest.LoadSigningRequest(pBytes, HashAlgorithmName.SHA512, CertificateRequestLoadOptions.UnsafeLoadCertificateExtensions);
			if (pSignRequest == null) {
				return (false);
			}
			var pCommonName = ItemsMentioned.GetDistinguishedValue("CN", pSignRequest.SubjectName);

			var pBuilder = new SubjectAlternativeNameBuilder();
			pBuilder.AddDnsName(pCommonName);
			var pExtBuilt = pBuilder.Build(true);
			pSignRequest.CertificateExtensions.Add(new X509SubjectAlternativeNameExtension(pExtBuilt.RawData));

			var pCertificate = new Certificate();
			if (pCertificate.CreateForServer(m_pOrgProfile, m_pAuthorityItem, pSignRequest) == false)
			{
				throw (new AppException(AppError.FailureCreateCertificate, AppFacility.Error, AppFlow.CreateCertificateForServer, pCommonName));
			}
			if (pCertificate.Validate() == false)
			{
				return (false);
			}
			//　認証局が発行した有効な証明書の中にサブジェクト名の重複がないことを検査
			if (pCertificate.IsExistSubject(pSQLContext, m_uAuthorityId) == true)
			{
				//　同一の共通名を持つ証明書が存在する。
				if (fOverWrite == false)
				{
					throw (new AppException(AppError.ExistSameCertificate, AppFacility.Error, AppFlow.CreateCertificateForServer, pCommonName));
				}
			}
			if (pCertificate.Save(pSQLContext, m_uAuthorityId) == false)
			{
				throw (new AppException(AppError.FailreSaveCertificate, AppFacility.Error, AppFlow.CreateCertificateForServer, pCommonName));
			}

			return (true);
		}

		//　CA証明書をデータベースに登録する。
		public bool ImportCertificate(SQLContext pSQLContext, string pImportFilepath)
		{
			var pCertificate = new Certificate();
			if (pCertificate.Import(pImportFilepath) == false)
			{
				return (false);
			}
			var pCommonName = ItemsMentioned.GetDistinguishedValue("CN", pCertificate.m_pCertificate.SubjectName);

			//　証明書に該当する署名要求が存在するかを確かめる。
			var pSignRequest = new SignRequest();
			if (pSignRequest.Load(pSQLContext, m_uAuthorityId, pCertificate.m_pCertificate.SubjectName.Name) == false)
			{
				//　該当する署名要求が存在しない。
				return (false);
			}
			if (pCertificate.Save(pSQLContext, m_uAuthorityId, pSignRequest.m_pKey) == false)
			{
				throw (new AppException(AppError.FailreSaveCertificate, AppFacility.Error, AppFlow.CreateCertificateForServer, pCommonName));
			}

			return (true);
		}

		// <summary>有効期限を延長した証明書を発行</summary>
		// <param>pBaseCertificate：元にする証明書</param>
		public bool Update(SQLContext pSQLContext, Certificate pBaseCertificate)
		{
			var pCertificate = new Certificate();
			if (pCertificate.CreateForUpdate(m_pOrgProfile, pBaseCertificate, m_pAuthorityItem) == false)
			{
				throw (new AppException(AppError.FailureCreateCertificate, AppFacility.Error, AppFlow.CreateCertificateForUpdate, pBaseCertificate.m_pItems.CommonName));
			}
			if (pCertificate.Validate() == false)
			{
				throw (new AppException(AppError.ExistSameCertificate, AppFacility.Error, AppFlow.CreateCertificateForUpdate, pBaseCertificate.m_pItems.CommonName));
			}
			if (pCertificate.IsHaveKey() == false)
			{
				throw (new AppException(AppError.ExistSameCertificate, AppFacility.Error, AppFlow.CreateCertificateForUpdate, pBaseCertificate.m_pItems.CommonName));
			}
			if (pCertificate.Save2(pSQLContext, m_uAuthorityId) == false)
			{
				throw (new AppException(AppError.FailreSaveCertificate, AppFacility.Error, AppFlow.CreateCertificateForUpdate, pBaseCertificate.m_pItems.CommonName));
			}

			return (true);
		}

		//　証明書を失効
		public bool Revoke(SQLContext pSQLContext, Certificate pCertificate)
		{
			if (pCertificate.Revoke(pSQLContext, m_uAuthorityId) == false)
			{
				throw (new AppException(AppError.FailreSaveCertificate, AppFacility.Error, AppFlow.Revoke, pCertificate.m_pItems.CommonName));
			}

			return (true);
		}

		//　CRC32を計算
		private uint ConvertIdentity(string pIdentityName)
		{
			var pBytes = UTF32Encoding.UTF8.GetBytes(pIdentityName);
			uint uHash = System.IO.Hashing.Crc32.HashToUInt32(pBytes);
			return (uHash);
		}

		//　
		public ObservableCollection<Certificate> Listup(SQLContext pSQLContext)
		{
			var pCertificates = new ObservableCollection<Certificate>();

			var pSQL = "SELECT SequenceNumber, SerialNumber, CommonName, TypeOf, Revoked, LaunchAt, ExpireAt, PemData, KeyData FROM TIssuedCerts WHERE AuthorityId = @AuthorityId AND Revoked = FALSE AND LaunchAt <= now() AND now() < ExpireAt AND TypeOf <> @TypeOf;";
			using (var pCommand = new NpgsqlCommand(pSQL, pSQLContext.m_pConnection))
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
						pCertificate.m_pItems.SerialNumber   = pReader.GetString(1);
						pCertificate.m_pItems.CommonName     = pReader.GetString(2);
						pCertificate.m_pItems.TypeOf         = (CertificateType)pReader.GetInt32(3);
						pCertificate.m_pItems.Revoked        = pReader.GetBoolean(4);
						pCertificate.m_pItems.LaunchAt       = pReader.GetDateTime(5);
						pCertificate.m_pItems.ExpireAt       = pReader.GetDateTime(6);
						pCertificate.m_pCrt                  = pReader.GetString(7);
						pCertificate.m_pKey                  = pReader.GetString(8);
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

		//　<summary>シリアル番号で証明書を取得</summary>
		public Certificate Fetch(SQLContext pSQLContext, string pSerialNumber)
		{
			var pCertificate = new Certificate();

			var pSQL = "SELECT SequenceNumber, SerialNumber, CommonName, TypeOf, Revoked, LaunchAt, ExpireAt, PemData, KeyData FROM TIssuedCerts WHERE SerialNumber = @SerialNumber AND TypeOf <> @TypeOf;";
			using (var pCommand = new NpgsqlCommand(pSQL, pSQLContext.m_pConnection))
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
						pCertificate.m_pItems.SerialNumber   = pReader.GetString(1);
						pCertificate.m_pItems.CommonName     = pReader.GetString(2);
						pCertificate.m_pItems.TypeOf         = (CertificateType)pReader.GetInt32(3);
						pCertificate.m_pItems.Revoked        = pReader.GetBoolean(4);
						pCertificate.m_pItems.LaunchAt       = pReader.GetDateTime(5);
						pCertificate.m_pItems.ExpireAt       = pReader.GetDateTime(6);
						pCertificate.m_pCrt                  = pReader.GetString(7);
						pCertificate.m_pKey                  = pReader.GetString(8);
						pCertificate.Prepare();
						iCount ++;
					}
					if (iCount == 0)
					{
						return (null);
					}
				}
			}

			return (pCertificate);
		}

		//　CRLを生成
		public byte[] GenerateCRL(SQLContext pSQLContext, int iDays)
		{
			byte[]	pBytes;
			var pBuilder = new CertificateRevocationListBuilder();

			var pSQL = "SELECT SerialNumber, RevokeAt FROM TIssuedCerts WHERE Revoked = TRUE AND AuthorityId = @AuthorityId;";
			using (var pCommand = new NpgsqlCommand(pSQL, pSQLContext.m_pConnection))
			{
				pCommand.Parameters.Clear();
				pCommand.Parameters.AddWithValue("AuthorityId", (Int64)m_uAuthorityId);
				using (var pReader = pCommand.ExecuteReader())
				{
					while (pReader.Read())
					{
						var SerialNumber = Convert.FromHexString(pReader.GetString(0));
						var RevokeAt     = pReader.GetDateTime(1);
						pBuilder.AddEntry(SerialNumber, RevokeAt);
					}
				}
			}

			BigInteger iCRLNumber = 0;

			//　CRL番号を取得
			pSQL = "SELECT CRLNumber FROM TCounters;";
			using (var pCommand = new NpgsqlCommand(pSQL, pSQLContext.m_pConnection))
			{
				pCommand.Parameters.Clear();
				using (var pReader = pCommand.ExecuteReader())
				{
					while (pReader.Read())
					{
						var pNumber = pReader.GetString(0);
						iCRLNumber = BigInteger.Parse(pNumber, NumberStyles.HexNumber);
						iCRLNumber ++;
						break;
					}
				}
				DateTimeOffset pNextUpdate = DateTimeOffset.Now.AddDays(iDays);
				pBytes = pBuilder.Build(m_pAuthorityItem.m_pCertificate, iCRLNumber, pNextUpdate, HashAlgorithmName.SHA512);
			}

			//　CRLNumberのカウンタを更新
			pSQL = "UPDATE TCounters SET CrlNumber = @CrlNumber;";
			using (var pCommand = new NpgsqlCommand(pSQL, pSQLContext.m_pConnection))
			{
				var pNumber = iCRLNumber.ToString("X");
				pCommand.Parameters.Clear();
				pCommand.Parameters.AddWithValue("CrlNumber", pNumber);
				pCommand.ExecuteNonQuery();
			}

			return (pBytes);
		}

		public string ExportCRL(string pExportFolder, byte[] pBytesOfCrl)
		{
			var pFilepath = pExportFolder + "\\" + m_pAuthorityItem.m_pItems.CommonName + RoughCA_Const.CRL_EXTENSION;
			File.WriteAllBytes(pFilepath, pBytesOfCrl);

			return (pFilepath);
		}

		//　組織プロファイルを保存
		public void SaveOrgProfile(SQLContext pSQLContext)
		{
			m_pOrgProfile.Save(pSQLContext);
		}
	}
}
