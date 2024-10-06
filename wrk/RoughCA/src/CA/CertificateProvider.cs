using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Arteria_s.App.RoughCA
{
	//　証明書、署名要求のデータを作成する関数群（関数を跨いだ状態を持たない）
	public static class CertificateProvider
	{
		//　認証局署名要求を生成
		public static CertificateRequest CreateSignRequest(ECDsaCng pKeys, OrgProfile pOrgProfile, string pCommonName)
		{
			//　証明書のサブジェクト名を生成
			var pSubjectName = ItemsMentioned.GenerateSubjectName(pOrgProfile, pCommonName);

			//　署名要求を生成（der形式）
			CertificateRequest pRequest = new CertificateRequest(pSubjectName, pKeys, HashAlgorithmName.SHA512);

			//　CA制約：証明書が認証局であるか否かを指定する。
			pRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, true, 2, true));
			pRequest.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(pRequest.PublicKey, false));
			var pKeyUsageFlags = X509KeyUsageFlags.NonRepudiation | X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.EncipherOnly | X509KeyUsageFlags.CrlSign;
			pRequest.CertificateExtensions.Add(new X509KeyUsageExtension(pKeyUsageFlags, false));
			/*
			OidCollection pEnhancedKeyUsageFlags = new OidCollection
			{
				// https://oidref.com/1.3.6.1.5.5.7.3.2
				new Oid("1.3.6.1.5.5.7.3.1"),   //　serverAuth
				new Oid("1.3.6.1.5.5.7.3.3"),	//　codeSigning
				new Oid("1.3.6.1.5.5.7.3.4"),	//　emailProtection
			};
			pRequest.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(pEnhancedKeyUsageFlags, false));
			*/
			/*
			 //　※ 自己署名証明書の生成時に使っているロジック（ユニーク性は保証されていない）
			Span<byte> pSerialNumber = stackalloc byte[8];
			RandomNumberGenerator.Fill(pSerialNumber);
			 */

			return (pRequest);
		}

		//　署名要求（メール証明書）を生成
		public static CertificateRequest CreateSignRequestForClient(ECDsaCng pKeys, OrgProfile pOrgProfile, X500DistinguishedName pSubjectName, string pCommonName, string pEmail, bool fCA)
		{
			//　署名要求を生成（der形式）
			CertificateRequest pRequest = new CertificateRequest(pSubjectName, pKeys, HashAlgorithmName.SHA512);

			//　CA制約：証明書が認証局であるか否かを指定する。
			pRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(fCA, fCA, 0, true));
			pRequest.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(pRequest.PublicKey, false));
			var pKeyUsageFlags = X509KeyUsageFlags.NonRepudiation | X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.EncipherOnly;
			pRequest.CertificateExtensions.Add(new X509KeyUsageExtension(pKeyUsageFlags, false));
			OidCollection pEnhancedKeyUsageFlags = new OidCollection
			{
				// https://oidref.com/1.3.6.1.5.5.7.3.2
				new Oid("1.3.6.1.5.5.7.3.4"),	//　emailProtection
			};
			pRequest.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(pEnhancedKeyUsageFlags, false));

			var pBuilder = new SubjectAlternativeNameBuilder();
			pBuilder.AddEmailAddress(pEmail);
			var pExtBuilt = pBuilder.Build(true);
			pRequest.CertificateExtensions.Add(new X509SubjectAlternativeNameExtension(pExtBuilt.RawData));

			return (pRequest);
		}

		//　署名要求（サーバ証明書）を生成
		public static CertificateRequest CreateSignRequestForServer(ECDsaCng pKeys, OrgProfile pOrgProfile, X500DistinguishedName pSubjectName, string pCommonName, string pDnsName, bool fCA)
		{
			//　署名要求を生成（der形式）
			CertificateRequest pRequest = new CertificateRequest(pSubjectName, pKeys, HashAlgorithmName.SHA512);

			//　CA制約：証明書が認証局であるか否かを指定する。
			pRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(fCA, fCA, 2, true));
			pRequest.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(pRequest.PublicKey, false));
			var pKeyUsageFlags = X509KeyUsageFlags.NonRepudiation | X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.EncipherOnly;
			pRequest.CertificateExtensions.Add(new X509KeyUsageExtension(pKeyUsageFlags, false));
			OidCollection pEnhancedKeyUsageFlags = new OidCollection
			{
				// https://oidref.com/1.3.6.1.5.5.7.3.2
				new Oid("1.3.6.1.5.5.7.3.1"),   //　serverAuth
			};
			pRequest.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(pEnhancedKeyUsageFlags, false));

			var pBuilder = new SubjectAlternativeNameBuilder();
			pBuilder.AddDnsName(pDnsName);
			var pExtBuilt = pBuilder.Build(true);
			pRequest.CertificateExtensions.Add(new X509SubjectAlternativeNameExtension(pExtBuilt.RawData));

			return (pRequest);
		}

		//　自己署名証明書を作成
		public static X509Certificate2	CreateSelfCertificate(CertificateRequest pRequest, string CommonName, int iLifeDays, string pServerName)
		{
			//　機関情報アクセスを署名要求に追加
			List<string> pOCSP = new List<string>();
			List<string> pICAs = new List<string>();
			pICAs.Add("http://" + pServerName + "/" + CommonName + ".crt");
			pRequest.CertificateExtensions.Add(new X509AuthorityInformationAccessExtension(pOCSP, pICAs, false));

			//　失効リスト配布ポイントを署名要求に追加
			List<string> pCDP = new List<string>();
			pCDP.Add("http://" + pServerName + "/" + CommonName + ".crl");
			var pCDPExtension = CertificateRevocationListBuilder.BuildCrlDistributionPointExtension(pCDP, false);
			pRequest.CertificateExtensions.Add(pCDPExtension);

			//　認証局識別子を署名要求に追加。発行認証局の「鍵識別子（SKI）」
			X509SubjectKeyIdentifierExtension	pSKI = null;
			foreach (var pExtension in pRequest.CertificateExtensions)
			{
				//　拡張情報からSKIを探索し、そのキーをAKIに転写
				if (pExtension.Oid.Value == "2.5.29.14")
				{
					pSKI = (X509SubjectKeyIdentifierExtension)pExtension;
					break;
				}
			}
			if (pSKI != null)
			{
				var pAKI = X509AuthorityKeyIdentifierExtension.CreateFromSubjectKeyIdentifier(pSKI);
				pRequest.CertificateExtensions.Add(pAKI);
			}

			var pNotBefore = DateTimeOffset.UtcNow;
			var pNotAfter = DateTimeOffset.UtcNow.AddDays(iLifeDays);
			return(pRequest.CreateSelfSigned(pNotBefore, pNotAfter));
		}

		//　署名要求（証明書記載事項を転記）を生成
		public static CertificateRequest CreateSignRequestForUpdate(ECDsaCng pKeys, OrgProfile pOrgProfile, X509Certificate2 pBaseCertificate)
		{
			//　証明書のサブジェクトを複写
			string pSubject = pBaseCertificate.Subject;

			//　署名要求を生成（der形式）
			CertificateRequest pRequest = new CertificateRequest(pSubject, pKeys, HashAlgorithmName.SHA512);

			//　拡張属性を転記
			foreach (var pBaseExtension in pBaseCertificate.Extensions)
			{
				pRequest.CertificateExtensions.Add(pBaseExtension);
			}

			return (pRequest);
		}

		private static List<string>	m_pRejectOids = new List<string>()
		{
			"1.3.6.1.5.5.7.1.1",	//　AIA
			"2.5.29.31",			//　CDP
			"2.5.29.35",			//　AKI
		};

		//　証明要求に署名を行い、証明書を作成する。
		//　pTrustCrt：認証局の証明書（秘密鍵が内包されている必要）
		//　pServerName：AIA、CRPを設定する際に用いる証明書、失効情報を保存、公開するURL
		public static X509Certificate2 CreateCertificate(CertificateRequest pRequest, Certificate pTrustCrt, int iLifeDays, string pServerName)
		{
			//　PKI の設計風習によれば、この辺りで認証局側処理がX509Extensionの証明書に対する制限事項を設定する必要（2024/08/25）
			//　要は、鍵使用制約や基本制限などを署名要求によらずに設定すべし、みたいなこと
			//　より理想的には、署名要求の記載事項を運用条件に従って検証、確認して記載事項を転写する動きを求めている筈。ワークフロー的な話

			//　署名要求に記載されたAIA, CDP, AKIを除去
			//　１）コレクションから除去対象を探索
			Collection<X509Extension>	pRejectExtensions = new Collection<X509Extension>();
			foreach (var pExtension in pRequest.CertificateExtensions)
			{
				foreach (var pOidValue in m_pRejectOids)
				{
					if (pExtension.Oid.Value == pOidValue)
					{
						//　除去対象の拡張属性を発見
						pRejectExtensions.Add(pExtension);
						continue;
					}
				}
				//　除去対象外の拡張属性を発見
				//pNewExtensions.Add(pExtension);
			}
			//　２）コレクションから除去対象を削除
			foreach (var pExtension in pRejectExtensions)
			{
				pRequest.CertificateExtensions.Remove(pExtension);
			}

			//　機関情報アクセスを署名要求に追加
			List<string> pOCSP = new List<string>();
			List<string> pICAs = new List<string>();
			pICAs.Add("http://" + pServerName + "/" + pTrustCrt.m_pItems.CommonName + RoughCA_Const.CRT_EXTENSION);
			pRequest.CertificateExtensions.Add(new X509AuthorityInformationAccessExtension(pOCSP, pICAs, false));

			//　失効リスト配布ポイントを署名要求に追加
			List<string> pCDP = new List<string>();
			pCDP.Add("http://" + pServerName + "/" + pTrustCrt.m_pItems.CommonName + RoughCA_Const.CRL_EXTENSION);
			var pCDPExtension = CertificateRevocationListBuilder.BuildCrlDistributionPointExtension(pCDP, false);
			pRequest.CertificateExtensions.Add(pCDPExtension);

			//　認証局識別子を署名要求に追加。発行認証局の「鍵識別子（SKI）」
			foreach (var pExtension in pTrustCrt.m_pCertificate.Extensions)
			{
				//　拡張情報からSKIを探索し、そのキーをAKIに転写
				if (pExtension.Oid.Value == "2.5.29.14")
				{
					var pSKI = (X509SubjectKeyIdentifierExtension)pExtension;
					var pAKI = X509AuthorityKeyIdentifierExtension.CreateFromSubjectKeyIdentifier(pSKI);
					pRequest.CertificateExtensions.Add(pAKI);
					break;
				}
			}

			//　証明書の有効期限を計算
			var pNotBefore    = DateTimeOffset.UtcNow;
			var pNotAfter     = DateTimeOffset.UtcNow.AddDays(iLifeDays);

			//　証明書のシリアル番号を生成
			//　※ 自己署名証明書の生成時に使っているロジック（ユニーク性は保証されていない）
			Span<byte> pSerialNumber = stackalloc byte[8];
			RandomNumberGenerator.Fill(pSerialNumber);
			byte[] pBytes = pSerialNumber.ToArray();

			//　署名要求の内容に従って、証明書を作成する。
			var pDSAKey = ECDsaCertificateExtensions.GetECDsaPrivateKey(pTrustCrt.m_pCertificate);
			var pGenerator = X509SignatureGenerator.CreateForECDsa(pDSAKey);
			var pIssueName = pTrustCrt.m_pCertificate.SubjectName;
			return (pRequest.Create(pIssueName, pGenerator, pNotBefore, pNotAfter, pBytes));
//			return (pRequest.Create(pTrustCrt.m_pCertificate, pNotBefore, pNotAfter, pSerialNumber));
		}
	}
}
