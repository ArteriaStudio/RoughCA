using Arteria_s.DB.Base;
using Arteria_s.OS.Base;
using Npgsql;
using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Arteria_s.App.RoughCA
{
	public class Certificate : IEquatable<Certificate>
	{
		public ItemsMentioned		m_pItems = new ItemsMentioned();
		public X509Certificate2 	m_pCertificate { get; set; }

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

		//　PEM文字列からX509Certificate2を構築して保持する
		public bool LoadFromPem(string pCertPem, string pKeyPem = null)
		{
			if (string.IsNullOrEmpty(pCertPem))
			{
				return (false);
			}

			m_pCertificate?.Dispose();
			if (!string.IsNullOrEmpty(pKeyPem))
			{
				m_pCertificate = X509Certificate2.CreateFromPem(pCertPem, pKeyPem);
			}
			else
			{
				m_pCertificate = X509Certificate2.CreateFromPem(pCertPem);
			}

			return (true);
		}

		//　保持している証明書に秘密鍵を紐付ける
		public bool AttachPrivateKey(ECDsa pPrivateKey)
		{
			if ((m_pCertificate == null) || (pPrivateKey == null))
			{
				return (false);
			}

			var pWithKey = m_pCertificate.CopyWithPrivateKey(pPrivateKey);
			m_pCertificate.Dispose();
			m_pCertificate = pWithKey;
			return (true);
		}

		//　証明書をPEM形式で取得
		public string ExportCertificatePem()
		{
			return (m_pCertificate?.ExportCertificatePem());
		}

		//　秘密鍵をPEM形式で取得（未保持の場合はnull）
		public string ExportPrivateKeyPem()
		{
			if ((m_pCertificate == null) || (m_pCertificate.HasPrivateKey == false))
			{
				return (null);
			}

			using (var pPrivateKey = m_pCertificate.GetECDsaPrivateKey())
			{
				return (pPrivateKey?.ExportECPrivateKeyPem());
			}
		}

		//　共通名が一致する証明書を入力
		public bool Load(VSQLContext pSQLContext, string pCommonName, uint uAuthorityId)
		{
			string pCertPem = null;
			string pKeyPem = null;
			var status = pSQLContext.LoadCertificate(pCommonName, uAuthorityId, ref m_pItems, ref pCertPem, ref pKeyPem);
			if (status == false)
			{
				return (false);
			}

			return (LoadFromPem(pCertPem, pKeyPem));
		}

		//　証明書と（保持していれば）秘密鍵を保存
		public bool Save(VSQLContext pSQLContext, uint uAuthorityId, uint uInstance)
		{
			var pCertPem = ExportCertificatePem();
			var pKeyPem = ExportPrivateKeyPem();
			var bResult = pSQLContext.SaveCertificate(uAuthorityId, uInstance, m_pItems, pCertPem, pKeyPem);
			return (bResult);
		}

		//　認証局証明書を生成
		//　pCommonName：共通名に記す認証局名
		//　pCACertificate：署名する認証局の証明書データ
		public bool CreateForAuthority(OrgProfile pOrgProfile, string pCommonName, Certificate pCACertificate)
		{
			//　秘密鍵を生成（楕円曲線方式）
			using (ECDsaCng pKeys = new ECDsaCng(RoughCA_Const.ECDSAKEY_SIZE))
			{
				//　認証局の署名要求を生成
				var pRequest = CertificateProvider.CreateSignRequest(pKeys, pOrgProfile, pCommonName);
				if (pRequest == null)
				{
					return (false);
				}

				m_pCertificate?.Dispose();
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
					using (var pSignedCert = CertificateProvider.CreateCertificate(pRequest, pCACertificate, iLifeDays, pOrgProfile.ServerName))
					{
						if (pSignedCert == null)
						{
							return (false);
						}
						//　CA署名証明書には秘密鍵が含まれないため、生成した秘密鍵を紐付ける。
						m_pCertificate = pSignedCert.CopyWithPrivateKey(pKeys);
					}
				}
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

			return (true);
		}

		//　認証局証明書を生成
		//　pCACertificate：認証局証明書
		//　pSignRequest：署名する認証局の署名要求データ
		public bool CreateForAuthority(OrgProfile pOrgProfile, Certificate pCACertificate, CertificateRequest pSignRequest)
		{
			//　ルート認証局の証明書データで署名した発行認証局の証明書を生成
			var iLifeDays = 365 * 5;
			m_pCertificate?.Dispose();
			m_pCertificate = CertificateProvider.CreateCertificate(pSignRequest, pCACertificate, iLifeDays, pOrgProfile.ServerName);
			if (m_pCertificate == null)
			{
				return (false);
			}
			//　署名要求からの発行では秘密鍵は含まれない。

			//　証明書記載情報の主要なものをキャッシュ
			m_pItems.SequenceNumber = 0;
			m_pItems.SerialNumber   = m_pCertificate.SerialNumber;
			m_pItems.SubjectName    = m_pCertificate.SubjectName.Name;
			m_pItems.CommonName     = ItemsMentioned.GetDistinguishedValue("CN", m_pCertificate.SubjectName);
			m_pItems.TypeOf         = CertificateType.CA;
			m_pItems.Revoked        = false;
			m_pItems.LaunchAt       = m_pCertificate.NotBefore;
			m_pItems.ExpireAt       = m_pCertificate.NotAfter;

			return (true);
		}

		//　サーバー証明書を生成（署名要求から証明書を作成）
		//　pCACertificate：認証局証明書
		//　pSignRequest：署名する認証局の署名要求データ
		public bool CreateForServer(OrgProfile pOrgProfile, Certificate pCACertificate, CertificateRequest pSignRequest)
		{
			//　ルート認証局の証明書データで署名した発行認証局の証明書を生成
			var iLifeDays = 365 * 1;
			m_pCertificate?.Dispose();
			m_pCertificate = CertificateProvider.CreateCertificate(pSignRequest, pCACertificate, iLifeDays, pOrgProfile.ServerName);
			if (m_pCertificate == null)
			{
				return (false);
			}
			//　署名要求からの発行では秘密鍵は含まれない。

			//　証明書記載情報の主要なものをキャッシュ
			m_pItems.SequenceNumber = 0;
			m_pItems.SerialNumber   = m_pCertificate.SerialNumber;
			m_pItems.SubjectName    = m_pCertificate.SubjectName.Name;
			m_pItems.CommonName     = ItemsMentioned.GetDistinguishedValue("CN", m_pCertificate.SubjectName);
			m_pItems.TypeOf         = CertificateType.Server;
			m_pItems.Revoked        = false;
			m_pItems.LaunchAt       = m_pCertificate.NotBefore;
			m_pItems.ExpireAt       = m_pCertificate.NotAfter;

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
			using (ECDsaCng pKeys = new ECDsaCng(RoughCA_Const.ECDSAKEY_SIZE))
			{
				//　サーバ証明書用の署名要求を生成
				var pRequest = CertificateProvider.CreateSignRequestForServer(pKeys, pOrgProfile, pSubjectName, pCommonName, pFQDN, fCA);
				if (pRequest == null)
				{
					return (false);
				}
				//　指定された認証局の証明書で署名
				var iLifeDays = 365 * 1;
				using (var pSignedCert = CertificateProvider.CreateCertificate(pRequest, pCACertificate, iLifeDays, pOrgProfile.ServerName))
				{
					if (pSignedCert == null)
					{
						return (false);
					}
					m_pCertificate?.Dispose();
					m_pCertificate = pSignedCert.CopyWithPrivateKey(pKeys);
				}
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
			using (ECDsaCng pKeys = new ECDsaCng(RoughCA_Const.ECDSAKEY_SIZE))
			{
				//　メール証明書用の署名要求を生成
				var pRequest = CertificateProvider.CreateSignRequestForClient(pKeys, pOrgProfile, pSubjectName, pCommonName, pMailAddress, fCA);
				if (pRequest == null)
				{
					return (false);
				}
				//　指定された認証局の証明書で署名
				var iLifeDays = 365 * 1;
				using (var pSignedCert = CertificateProvider.CreateCertificate(pRequest, pCACertificate, iLifeDays, pOrgProfile.ServerName))
				{
					if (pSignedCert == null)
					{
						return (false);
					}
					m_pCertificate?.Dispose();
					m_pCertificate = pSignedCert.CopyWithPrivateKey(pKeys);
				}
			}

			//　証明書記載情報の主要なものをキャッシュ
			m_pItems.SequenceNumber = 0;
			m_pItems.SerialNumber   = m_pCertificate.SerialNumber;
			m_pItems.SubjectName    = m_pCertificate.SubjectName.Name;
			m_pItems.CommonName     = ItemsMentioned.GetDistinguishedValue("CN", m_pCertificate.SubjectName);
			m_pItems.TypeOf         = CertificateType.Client;
			m_pItems.Revoked        = false;
			m_pItems.LaunchAt       = m_pCertificate.NotBefore;
			m_pItems.ExpireAt       = m_pCertificate.NotAfter;

			return (true);
		}

		//　サーバ証明書を生成
		public bool CreateForRDSign(OrgProfile pOrgProfile, string pCommonName, string pFQDN, string pNetAddress, Certificate pCACertificate, bool fCA)
		{
			//　認証局の証明書データが指定されていない時はエラー
			if (pCACertificate == null)
			{
				return (false);
			}

			//　証明書のサブジェクト名を生成
			var pSubjectName = ItemsMentioned.GenerateSubjectName(pOrgProfile, pCommonName);

			//　秘密鍵を生成（楕円曲線方式）
			using (ECDsaCng pKeys = new ECDsaCng(RoughCA_Const.ECDSAKEY_SIZE))
			{
				//　サーバ証明書用の署名要求を生成
				var pRequest = CertificateProvider.CreateSignRequestForRDSign(pKeys, pOrgProfile, pSubjectName, pCommonName, pFQDN, pNetAddress, fCA);
				if (pRequest == null)
				{
					return (false);
				}
				//　指定された認証局の証明書で署名
				var iLifeDays = 365 * 1;
				using (var pSignedCert = CertificateProvider.CreateCertificate(pRequest, pCACertificate, iLifeDays, pOrgProfile.ServerName))
				{
					if (pSignedCert == null)
					{
						return (false);
					}
					m_pCertificate?.Dispose();
					m_pCertificate = pSignedCert.CopyWithPrivateKey(pKeys);
				}
			}

			//　証明書記載情報の主要なものをキャッシュ
			m_pItems.SequenceNumber = 0;
			m_pItems.SerialNumber   = m_pCertificate.SerialNumber;
			m_pItems.SubjectName    = m_pCertificate.SubjectName.Name;
			m_pItems.CommonName     = ItemsMentioned.GetDistinguishedValue("CN", m_pCertificate.SubjectName);
			m_pItems.TypeOf         = CertificateType.RemoteDesktopListener;
			m_pItems.Revoked        = false;
			m_pItems.LaunchAt       = m_pCertificate.NotBefore;
			m_pItems.ExpireAt       = m_pCertificate.NotAfter;

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
			using (ECDsaCng pKeys = new ECDsaCng(RoughCA_Const.ECDSAKEY_SIZE))
			{
				//　メール証明書用の署名要求を生成
				var pRequest = CertificateProvider.CreateSignRequestForUpdate(pKeys, pOrgProfile, pBaseCertificate.m_pCertificate);
				if (pRequest == null)
				{
					return (false);
				}
				//　指定された認証局の証明書で署名
				var iLifeDays = 365 * 1;
				using (var pSignedCert = CertificateProvider.CreateCertificate(pRequest, pCACertificate, iLifeDays, pOrgProfile.ServerName))
				{
					if (pSignedCert == null)
					{
						return (false);
					}
					m_pCertificate?.Dispose();
					m_pCertificate = pSignedCert.CopyWithPrivateKey(pKeys);
				}
			}

			//　証明書記載情報の主要なものをキャッシュ
			m_pItems.SequenceNumber = 0;
			m_pItems.SerialNumber   = m_pCertificate.SerialNumber;
			m_pItems.SubjectName    = m_pCertificate.SubjectName.Name;
			m_pItems.CommonName     = ItemsMentioned.GetDistinguishedValue("CN", m_pCertificate.SubjectName);
			m_pItems.TypeOf         = pBaseCertificate.m_pItems.TypeOf;
			m_pItems.Revoked        = false;
			m_pItems.LaunchAt       = m_pCertificate.NotBefore;
			m_pItems.ExpireAt       = m_pCertificate.NotAfter;

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
			return ((m_pCertificate != null) && m_pCertificate.HasPrivateKey);
		}

		//　指定したフォルダに証明書と鍵をファイル形式で出力
		public string Export(string pExportFolder, bool bPemFormat = false)
		{
			DirectoryHelper.CreateDirectorySafe(pExportFolder);

			//　証明書が秘密鍵を持っていれば、それを出力する。
			using (var pPrivateKey = m_pCertificate.GetECDsaPrivateKey())
			{
				if (pPrivateKey != null)
				{
					var pExportFilepathOfKey = Path.Combine(pExportFolder, m_pItems.CommonName + RoughCA_Const.KEY_EXTENSION);
					if (bPemFormat)
					{
						var pPemOfKey = pPrivateKey.ExportECPrivateKeyPem();
						File.WriteAllText(pExportFilepathOfKey, pPemOfKey, Encoding.ASCII);
					}
					else
					{
						var pBytesOfKey = pPrivateKey.ExportECPrivateKey();
						File.WriteAllBytes(pExportFilepathOfKey, pBytesOfKey);
					}
				}
			}

			var pExportFilepathOfCrt = Path.Combine(pExportFolder, m_pItems.CommonName + RoughCA_Const.CRT_EXTENSION);
			if (bPemFormat)
			{
				var pPemOfCrt = m_pCertificate.ExportCertificatePem();
				File.WriteAllText(pExportFilepathOfCrt, pPemOfCrt, Encoding.ASCII);
			}
			else
			{
				var pBytesOfCrt = m_pCertificate.Export(X509ContentType.Cert);
				File.WriteAllBytes(pExportFilepathOfCrt, pBytesOfCrt);
			}

			return (pExportFilepathOfCrt);
		}

		//　指定した証明書データを含むファイルを入力
		public bool Import(string pImportFilepath)
		{
			var pBytes = File.ReadAllBytes(pImportFilepath);
			m_pCertificate?.Dispose();
			m_pCertificate = X509CertificateLoader.LoadCertificate(pBytes);
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

		//　証明書データに収められた秘密鍵を確認（動作検証用）
		protected void Dump()
		{
			using (var pKey = m_pCertificate?.GetECDsaPrivateKey())
			{
				if (pKey == null)
				{
					Debug.WriteLine("NoPrivateKey");
				}
				else
				{
					Debug.WriteLine("HasPrivateKey: " + pKey.KeySize);
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
