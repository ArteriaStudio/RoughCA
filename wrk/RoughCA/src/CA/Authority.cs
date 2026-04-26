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

		public bool Validate(VSQLContext pSQLContext)
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
		public bool Load(VSQLContext pSQLContext, string pIdentityName)
		{
			//　認証局識別子を作成
			m_uAuthorityId = ConvertIdentity(pIdentityName);
			m_pAuthorityName = pIdentityName;

			//　組織プロファイルと認証局情報を入力
			var iUserIdentity = 0;
			m_pOrgProfile = new OrgProfile();
			pSQLContext.LoadOrgProfile(ref m_pOrgProfile, iUserIdentity);
//			m_pOrgProfile.Load(pSQLContext, iUserIdentity);
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
		public bool CreateForServer(VSQLContext pSQLContext, string pCommonName, string pFQDN, bool fCA, bool fOverWrite)
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
			if (pSQLContext.IsExistSubject(pCertificate.m_pItems.SerialNumber, pCertificate.m_pItems.SubjectName, m_uAuthorityId) == true)
//			if (pCertificate.IsExistSubject(pSQLContext, m_uAuthorityId) == true)
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
		public bool CreateForClient(VSQLContext pSQLContext, string pCommonName, string pMailAddress, bool fCA, bool fOverWrite)
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
			if (pSQLContext.IsExistSubject(pCertificate.m_pItems.SerialNumber, pCertificate.m_pItems.SubjectName, m_uAuthorityId) == true)
//			if (pCertificate.IsExistSubject(pSQLContext, m_uAuthorityId) == true)
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

		//　リモートデスクトップ接続リスナー証明書を生成する。
		//　fOverWrite：同一のサブジェクトを持つ証明書があった場合に有効な証明書を当該証明書に差し替える。
		public bool CreateForRDSign(VSQLContext pSQLContext, string pCommonName, string pFQDN, string pNetAddress, bool fCA, bool fOverWrite)
		{
			var pCertificate = new Certificate();
			if (pCertificate.CreateForRDSign(m_pOrgProfile, pCommonName, pFQDN, pNetAddress, m_pAuthorityItem, fCA) == false)
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
			if (pSQLContext.IsExistSubject(pCertificate.m_pItems.SerialNumber, pCertificate.m_pItems.SubjectName, m_uAuthorityId) == true)
//			if (pCertificate.IsExistSubject(pSQLContext, m_uAuthorityId) == true)
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
		

		//　認証局署名要求を生成する。
		public bool CreateForDemand(VSQLContext pSQLContext, string pCommonName)
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
		public bool CreateForCACert(VSQLContext pSQLContext, string pImportFilepath, bool fOverWrite)
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
			if (pSQLContext.IsExistSubject(pCertificate.m_pItems.SerialNumber, pCertificate.m_pItems.SubjectName, m_uAuthorityId) == true)
//			if (pCertificate.IsExistSubject(pSQLContext, m_uAuthorityId) == true)
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
		public bool CreateForServerCert(VSQLContext pSQLContext, string pImportFilepath, bool fOverWrite)
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
			if (pSQLContext.IsExistSubject(pCertificate.m_pItems.SerialNumber, pCertificate.m_pItems.SubjectName, m_uAuthorityId) == true)
//			if (pCertificate.IsExistSubject(pSQLContext, m_uAuthorityId) == true)
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
		public bool ImportCertificate(VSQLContext pSQLContext, string pImportFilepath)
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
		public bool Update(VSQLContext pSQLContext, Certificate pBaseCertificate)
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
		public ObservableCollection<Certificate> Listup(VSQLContext pSQLContext)
		{
			return (pSQLContext.ListupCertificates(m_uAuthorityId));
		}

		//　<summary>シリアル番号で証明書を取得</summary>
		public Certificate Fetch(VSQLContext pSQLContext, string pSerialNumber)
		{
			return (pSQLContext.Fetch(pSerialNumber));
		}

		//　CRLを生成
		public byte[] GenerateCRL(VSQLContext pSQLContext, int iDays)
		{
			return (pSQLContext.GenerateCRL(m_uAuthorityId, iDays, m_pAuthorityItem.m_pCertificate));
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
			pSQLContext.SaveOrgProfile(m_pOrgProfile);
//			m_pOrgProfile.Save(pSQLContext);
		}

		public uint GetAuthorityId()
		{
			return (m_uAuthorityId);
		}
	}
}
