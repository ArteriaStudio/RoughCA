using ArteriaS.DB.Base;
using ArteriaS.OS.Base;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics.X86;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace ArteriaS.App.RoughCA
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

		public Certificate	m_pAuthorityItem;		//　既定の発行認証局証明書
		public Certificate	m_pRootAuthorityItem;	//　ルート認証局証明書

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

		//　認証局の証明書と鍵を入力（未発行の場合は自動作成）
		public bool Load(VSQLContext pSQLContext, string pIdentityName, uint uInstance)
		{
			return LoadInternal(pSQLContext, pIdentityName, uInstance, loadOrgProfileFromDb: true, out _, out _);
		}

		//　現在の組織プロファイルに基づき、未発行のルート認証局・発行認証局の鍵と自己証明書を作成して確保する
		public bool EnsureAuthorityCertificates(VSQLContext pSQLContext, string pIdentityName, uint uInstance, out bool createdRoot, out bool createdIssue)
		{
			return LoadInternal(pSQLContext, pIdentityName, uInstance, loadOrgProfileFromDb: false, out createdRoot, out createdIssue);
		}

		private bool LoadInternal(VSQLContext pSQLContext, string pIdentityName, uint uInstance, bool loadOrgProfileFromDb, out bool createdRoot, out bool createdIssue)
		{
			createdRoot = false;
			createdIssue = false;

			//　切り替え時に前の認証局情報が残らないようリセット
			m_pAuthorityItem = null;
			m_pRootAuthorityItem = null;
			if (loadOrgProfileFromDb || m_pOrgProfile == null)
			{
				m_pOrgProfile = new OrgProfile();
			}

			//　認証局識別子を作成
			m_uAuthorityId = ConvertIdentity(pIdentityName);
			m_pAuthorityName = pIdentityName;

			//　組織プロファイルと認証局情報を入力
			if (loadOrgProfileFromDb)
			{
				pSQLContext.LoadOrgProfile(ref m_pOrgProfile, uInstance);
			}

			if (m_pOrgProfile.Validate() == false)
			{
				return (false);
			}

			//　ルート認証局の証明書データを入力（存在しなければ鍵と自己署名証明書を新規作成）
			m_pAuthorityItem = new Certificate();
			var pAuthorityName = m_pOrgProfile.TrustName;
			if (m_pAuthorityItem.Load(pSQLContext, pAuthorityName, m_uAuthorityId) == false)
			{
				//　ルート認証局の証明書を作成
				if (CreateAuthority(pSQLContext, pAuthorityName, uInstance) == false)
				{
					return (false);
				}
				createdRoot = true;
			}
			m_pRootAuthorityItem = m_pAuthorityItem;

			//　発行認証局名がルート認証局名と異なる場合、発行認証局の証明書を確保
			if (!string.Equals(m_pOrgProfile.IssueName, pAuthorityName, StringComparison.Ordinal))
			{
				var pIssueCert = new Certificate();
				if (pIssueCert.Load(pSQLContext, m_pOrgProfile.IssueName, m_uAuthorityId) == false)
				{
					//　発行認証局の証明書を作成
					if (CreateForAuthority(pSQLContext, m_pOrgProfile.IssueName, uInstance) == false)
					{
						return (false);
					}
					createdIssue = true;
					//　作成した発行認証局証明書を読み込み直す
					pIssueCert.Load(pSQLContext, m_pOrgProfile.IssueName, m_uAuthorityId);
				}
				m_pAuthorityItem = pIssueCert;
			}

			return (true);
		}

		//　ルート認証局の証明書を新規作成
		private bool CreateAuthority(VSQLContext pSQLContext, string pAuthorityName, uint uInstance)
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
			if (m_pAuthorityItem.Save(pSQLContext, m_uAuthorityId, uInstance) == false)
			{
				return (false);
			}
			return (true);
		}

		//　発行認証局の証明書を新規作成
		//　pCommonName：共通名に記す認証局名
		private bool CreateForAuthority(VSQLContext pSQLContext, string pCommonName, uint uInstance)
		{
			var pCertificate = new Certificate();
			if (pCertificate.CreateForAuthority(m_pOrgProfile, pCommonName, m_pAuthorityItem) == false)
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
			{
				//　同一のサブジェクトを持つ証明書が既に発行されている。
				var fOverWrite = false;
				if (fOverWrite == false)
				{
					throw (new AppException(AppError.ExistSameCertificate, AppFacility.Error, AppFlow.CreateCertificateForServer, pCommonName));
				}
			}
			if (pCertificate.Save(pSQLContext, m_uAuthorityId, uInstance) == false)
			{
				throw (new AppException(AppError.FailreSaveCertificate, AppFacility.Error, AppFlow.CreateCertificateForServer, pCommonName));
			}

			return (true);
		}

		//　サーバ証明書を生成する。
		//　fOverWrite：同一のサブジェクトを持つ証明書があった場合に有効な証明書を当該証明書に差し替える。
		public bool CreateForServer(VSQLContext pSQLContext, uint uInstance, string pCommonName, string pFQDN, bool fCA, bool fOverWrite, Certificate pIssuerAuthority = null)
		{
			var pSigner = pIssuerAuthority ?? m_pAuthorityItem;
			var pCertificate = new Certificate();
			if (pCertificate.CreateForServer(m_pOrgProfile, pCommonName, pFQDN, pSigner, fCA) == false)
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
			{
				//　同一のサブジェクトを持つ証明書が既に発行されている。
				if (fOverWrite == false)
				{
					throw (new AppException(AppError.ExistSameCertificate, AppFacility.Error, AppFlow.CreateCertificateForServer, pCommonName));
				}
			}
			if (pCertificate.Save(pSQLContext, m_uAuthorityId, uInstance) == false)
			{
				throw (new AppException(AppError.FailreSaveCertificate, AppFacility.Error, AppFlow.CreateCertificateForServer, pCommonName));
			}

			return (true);
		}

		//　メール証明書を生成する。
		public bool CreateForClient(VSQLContext pSQLContext, uint uInstance, string pCommonName, string pMailAddress, bool fCA, bool fOverWrite, Certificate pIssuerAuthority = null)
		{
			var pSigner = pIssuerAuthority ?? m_pAuthorityItem;
			var pCertificate = new Certificate();
			if (pCertificate.CreateForClient(m_pOrgProfile, pCommonName, pMailAddress, pSigner, fCA) == false)
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
			{
				//　同一のサブジェクトを持つ証明書が既に発行されている。
				if (fOverWrite == false)
				{
					throw (new AppException(AppError.ExistSameCertificate, AppFacility.Error, AppFlow.CreateCertificateForClient, pCommonName));
				}
			}
			if (pCertificate.Save(pSQLContext, m_uAuthorityId, uInstance) == false)
			{
				throw (new AppException(AppError.FailreSaveCertificate, AppFacility.Error, AppFlow.CreateCertificateForClient, pCommonName));
			}

			return (true);
		}

		//　リモートデスクトップ接続リスナー証明書を生成する。
		//　fOverWrite：同一のサブジェクトを持つ証明書があった場合に有効な証明書を当該証明書に差し替える。
		public bool CreateForRDSign(VSQLContext pSQLContext, uint uInstance, string pCommonName, string pFQDN, string pNetAddress, bool fCA, bool fOverWrite, Certificate pIssuerAuthority = null)
		{
			var pSigner = pIssuerAuthority ?? m_pAuthorityItem;
			var pCertificate = new Certificate();
			if (pCertificate.CreateForRDSign(m_pOrgProfile, pCommonName, pFQDN, pNetAddress, pSigner, fCA) == false)
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
			{
				//　同一のサブジェクトを持つ証明書が既に発行されている。
				if (fOverWrite == false)
				{
					throw (new AppException(AppError.ExistSameCertificate, AppFacility.Error, AppFlow.CreateCertificateForServer, pCommonName));
				}
			}
			if (pCertificate.Save(pSQLContext, m_uAuthorityId, uInstance) == false)
			{
				throw (new AppException(AppError.FailreSaveCertificate, AppFacility.Error, AppFlow.CreateCertificateForServer, pCommonName));
			}

			return (true);
		}

		//　認証局署名要求を生成する。
		public bool CreateForDemand(VSQLContext pSQLContext, uint uInstance, string pCommonName, string pExportFolder = null)
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
			if (pSignRequest.Save(pSQLContext, m_uAuthorityId, uInstance) == false)
			{
				throw (new AppException(AppError.FailreSaveCertificate, AppFacility.Error, AppFlow.CreateCertificateForClient, pCommonName));
			}
			if (string.IsNullOrWhiteSpace(pExportFolder))
			{
				pExportFolder = System.Environment.GetEnvironmentVariable("USERPROFILE") + "\\Downloads";
			}
			var pExportFilepath = pSignRequest.Export(pExportFolder);

			return (true);
		}

		//　CA証明書の署名要求に署名してCA証明書を作成する。
		//　
		public bool CreateForCACert(VSQLContext pSQLContext, uint uInstance, string pImportFilepath, bool fOverWrite, Certificate pIssuerAuthority = null)
		{
			var pSigner = pIssuerAuthority ?? m_pAuthorityItem;
			var pBytes = File.ReadAllBytes(pImportFilepath);

			//　署名要求に記載された事項も入力（CertificateRequestLoadOptions.UnsafeLoadCertificateExtensions）
			var pSignRequest = CertificateRequest.LoadSigningRequest(pBytes, HashAlgorithmName.SHA512, CertificateRequestLoadOptions.UnsafeLoadCertificateExtensions);
			if (pSignRequest == null) {
				return (false);
			}
			var pCommonName = ItemsMentioned.GetDistinguishedValue("CN", pSignRequest.SubjectName);

			var pCertificate = new Certificate();
			if (pCertificate.CreateForAuthority(m_pOrgProfile, pSigner, pSignRequest) == false)
			{
				throw (new AppException(AppError.FailureCreateCertificate, AppFacility.Error, AppFlow.CreateCertificateForServer, pCommonName));
			}
			if (pCertificate.Validate() == false)
			{
				return (false);
			}
			//　認証局が発行した有効な証明書の中にサブジェクト名の重複がないことを検査
			if (pSQLContext.IsExistSubject(pCertificate.m_pItems.SerialNumber, pCertificate.m_pItems.SubjectName, m_uAuthorityId) == true)
			{
				//　同一の共通名を持つ証明書が存在する。
				if (fOverWrite == false)
				{
					throw (new AppException(AppError.ExistSameCertificate, AppFacility.Error, AppFlow.CreateCertificateForServer, pCommonName));
				}
			}
			if (pCertificate.Save(pSQLContext, m_uAuthorityId, uInstance) == false)
			{
				throw (new AppException(AppError.FailreSaveCertificate, AppFacility.Error, AppFlow.CreateCertificateForServer, pCommonName));
			}

			return (true);
		}

		//　サーバー証明書の署名要求に署名してサーバー証明書を作成する。
		//　
		public bool CreateForServerCert(VSQLContext pSQLContext, uint uInstance, string pImportFilepath, bool fOverWrite, Certificate pIssuerAuthority = null)
		{
			var pSigner = pIssuerAuthority ?? m_pAuthorityItem;
			var pBytes = File.ReadAllBytes(pImportFilepath);

			//　署名要求に記載された事項も入力（CertificateRequestLoadOptions.UnsafeLoadCertificateExtensions）
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
			if (pCertificate.CreateForServer(m_pOrgProfile, pSigner, pSignRequest) == false)
			{
				throw (new AppException(AppError.FailureCreateCertificate, AppFacility.Error, AppFlow.CreateCertificateForServer, pCommonName));
			}
			if (pCertificate.Validate() == false)
			{
				return (false);
			}
			//　認証局が発行した有効な証明書の中にサブジェクト名の重複がないことを検査
			if (pSQLContext.IsExistSubject(pCertificate.m_pItems.SerialNumber, pCertificate.m_pItems.SubjectName, m_uAuthorityId) == true)
			{
				//　同一の共通名を持つ証明書が存在する。
				if (fOverWrite == false)
				{
					throw (new AppException(AppError.ExistSameCertificate, AppFacility.Error, AppFlow.CreateCertificateForServer, pCommonName));
				}
			}
			if (pCertificate.Save(pSQLContext, m_uAuthorityId, uInstance) == false)
			{
				throw (new AppException(AppError.FailreSaveCertificate, AppFacility.Error, AppFlow.CreateCertificateForServer, pCommonName));
			}

			return (true);
		}

		//　CA証明書をデータベースに登録する。
		public bool ImportCertificate(VSQLContext pSQLContext, uint uInstance, string pImportFilepath)
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
			if (pCertificate.AttachPrivateKey(pSignRequest.m_pPrivateKey) == false)
			{
				return (false);
			}
			if (pCertificate.Save(pSQLContext, m_uAuthorityId, uInstance) == false)
			{
				throw (new AppException(AppError.FailreSaveCertificate, AppFacility.Error, AppFlow.CreateCertificateForServer, pCommonName));
			}

			return (true);
		}

		// <summary>有効期限を延長した証明書を発行</summary>
		// <param>pBaseCertificate：元にする証明書</param>
		public bool Update(VSQLContext pSQLContext, uint uInstance, Certificate pBaseCertificate)
		{
			//　元の証明書の IssuerName から発行元の認証局を自動特定する
			var pIssuerAuthority = FindIssuerAuthority(pSQLContext, pBaseCertificate);
			if (pIssuerAuthority == null)
			{
				throw (new AppException(AppError.InvalidCertificate, AppFacility.Error, AppFlow.CreateCertificateForUpdate, pBaseCertificate.m_pItems.CommonName));
			}

			var pCertificate = new Certificate();
			if (pCertificate.CreateForUpdate(m_pOrgProfile, pBaseCertificate, pIssuerAuthority) == false)
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
			if (pCertificate.Save(pSQLContext, m_uAuthorityId, uInstance) == false)
			{
				throw (new AppException(AppError.FailreSaveCertificate, AppFacility.Error, AppFlow.CreateCertificateForUpdate, pBaseCertificate.m_pItems.CommonName));
			}

			return (true);
		}

		//　指定された証明書の発行元（IssuerName）に対応する有効な認証局証明書を探索する
		private Certificate FindIssuerAuthority(VSQLContext pSQLContext, Certificate pTargetCertificate)
		{
			if ((pTargetCertificate == null) || (pTargetCertificate.m_pCertificate == null))
			{
				return (null);
			}

			var pIssuerRawData = pTargetCertificate.m_pCertificate.IssuerName.RawData;
			var pAuthorities = ListupSigningAuthorities(pSQLContext);
			foreach (var pAuthorityCert in pAuthorities)
			{
				if ((pAuthorityCert.m_pCertificate != null) &&
					pAuthorityCert.m_pCertificate.SubjectName.RawData.SequenceEqual(pIssuerRawData))
				{
					return (pAuthorityCert);
				}
			}

			//　自己署名証明書自身の更新時は、失効処理直後で有効CA一覧から外れている可能性があるため対象自身をフォールバック確認
			if (pTargetCertificate.IsHaveKey() &&
				pTargetCertificate.m_pItems.TypeOf == CertificateType.CA &&
				pTargetCertificate.m_pCertificate.SubjectName.RawData.SequenceEqual(pIssuerRawData))
			{
				return (pTargetCertificate);
			}

			return (null);
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

		//　署名可能な有効な認証局証明書（秘密鍵ありのCA証明書）一覧を取得
		public List<Certificate> ListupSigningAuthorities(VSQLContext pSQLContext)
		{
			if (pSQLContext == null)
			{
				return (new List<Certificate>());
			}
			return (pSQLContext.ListupSigningAuthorities(m_uAuthorityId));
		}

		//　UI選択用の認証局表示名を生成
		public string FormatAuthorityDisplayName(Certificate pAuthorityCert)
		{
			if ((pAuthorityCert == null) || (pAuthorityCert.m_pItems == null))
			{
				return ("");
			}

			var pCommonName = pAuthorityCert.m_pItems.CommonName;
			bool isRoot  = (m_pOrgProfile != null) && string.Equals(pCommonName, m_pOrgProfile.TrustName, StringComparison.Ordinal);
			bool isIssue = (m_pOrgProfile != null) && string.Equals(pCommonName, m_pOrgProfile.IssueName, StringComparison.Ordinal);

			if (isRoot && isIssue)
			{
				return ($"{pCommonName}（ルート / 発行認証局）");
			}
			if (isRoot)
			{
				return ($"{pCommonName}（ルート認証局）");
			}
			if (isIssue)
			{
				return ($"{pCommonName}（既定の発行認証局）");
			}
			return (pCommonName);
		}

		//　<summary>シリアル番号で証明書を取得</summary>
		public Certificate Fetch(VSQLContext pSQLContext, string pSerialNumber)
		{
			return (pSQLContext.Fetch(pSerialNumber));
		}

		//　CRLを生成
		public byte[] GenerateCRL(VSQLContext pSQLContext, int iDays, Certificate pAuthorityCert = null)
		{
			var pTarget = pAuthorityCert ?? m_pAuthorityItem;
			return (pSQLContext.GenerateCRL(m_uAuthorityId, iDays, pTarget.m_pCertificate));
		}

		public string ExportCRL(string pExportFolder, byte[] pBytesOfCrl, Certificate pAuthorityCert = null)
		{
			DirectoryHelper.CreateDirectorySafe(pExportFolder);
			var pTarget = pAuthorityCert ?? m_pAuthorityItem;
			var pFilepath = Path.Combine(pExportFolder, pTarget.m_pItems.CommonName + RoughCA_Const.CRL_EXTENSION);
			File.WriteAllBytes(pFilepath, pBytesOfCrl);

			return (pFilepath);
		}

		//　組織プロファイルを保存
		public void SaveOrgProfile(SQLContext pSQLContext)
		{
			pSQLContext.SaveOrgProfile(m_pOrgProfile);
		}

		public uint GetAuthorityId()
		{
			return (m_uAuthorityId);
		}
	}
}
