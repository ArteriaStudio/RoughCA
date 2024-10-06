using System;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;

namespace Arteria_s.App.RoughCA
{
	public enum CertificateType
	{
		Unknown,
		CA,
		Server,
		Client,
		Demand,
	}

	public class RoughCA_Const
	{
		//　EC（楕円曲線暗号）で用いるキー長。2015年からChromeium系は、P-521は非対応（2024/08/17）
		public const int		ECDSAKEY_SIZE = 384;
		public const string		CRT_EXTENSION = ".crt";
		public const string		CSR_EXTENSION = ".csr";
		public const string		KEY_EXTENSION = ".key";
		public const string		CRL_EXTENSION = ".crl";
	};

	public class ItemsMentioned
	{
		public long 				SequenceNumber { get; set; }
		public string				SerialNumber { get; set; }
		public string				SubjectName { get; set; }
		public string				CommonName { get; set; }
		public CertificateType		TypeOf { get; set; }
		public bool 				Revoked { get; set; }
		public DateTime 			LaunchAt { get; set; }
		public DateTime 			ExpireAt { get; set; }
		//public string				PemData { get; set; }

		public ItemsMentioned()
		{
			SequenceNumber = -1;
			SerialNumber   = "";
			SubjectName    = "";
			CommonName     = "";
			TypeOf         = 0;
			Revoked        = false;
			//PemData        = "";
		}

		//　サブジェクト名を生成
		public static X500DistinguishedName GenerateSubjectName(OrgProfile pOrgProfile, string pCommonName)
		{
			var pBuilder = new X500DistinguishedNameBuilder();
			pBuilder.AddCommonName(pCommonName);
			pBuilder.AddLocalityName(pOrgProfile.LocalityName);
			pBuilder.AddOrganizationName(pOrgProfile.OrgName);
			pBuilder.AddOrganizationalUnitName(pOrgProfile.OrgUnitName);
			pBuilder.AddCountryOrRegion(pOrgProfile.CountryName);
			pBuilder.AddStateOrProvinceName(pOrgProfile.ProvinceName);
			return (pBuilder.Build());
		}

		//　X500DistinguishedNameの項目をダンプ出力
		public static void DumpDistinguishedName(X500DistinguishedName pName)
		{
			foreach (var pItem in pName.EnumerateRelativeDistinguishedNames())
			{
				Debug.WriteLine("item: " + pItem.GetSingleElementType().FriendlyName + " = " + pItem.GetSingleElementValue());
			}

			return;
		}

		//　
		public static string GetDistinguishedValue(string pFriendlyName, X500DistinguishedName pName)
		{
			foreach (var pItem in pName.EnumerateRelativeDistinguishedNames())
			{
				if (pItem.GetSingleElementType().FriendlyName == pFriendlyName)
				{
					return(pItem.GetSingleElementValue());
				}
			}
			return("");
		}
	}
}
