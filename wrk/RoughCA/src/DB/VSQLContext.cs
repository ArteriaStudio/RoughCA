using Arteria_s.App.RoughCA;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Arteria_s.DB.Base
{
	public abstract class VSQLContext
	{
		abstract public bool LoadOrgProfile(ref OrgProfile pOrgProfile, uint uInstance);
		abstract public bool SaveOrgProfile(OrgProfile pOrgProfile);
		abstract public bool IsExists(uint uAuthorityId, string pSubjectName, string pCommonName);
		abstract public bool LoadSignRequest(uint uAuthorityId, string pSubjectName, ref string m_pKey, ref ItemsMentioned m_pItems);
		abstract public bool SaveSignRequest(uint uAuthorityId, string m_pKey, ItemsMentioned m_pItems);
		abstract public bool LoadCertificate(string pCommonName, uint uAuthorityId, ref ItemsMentioned m_pItems, ref string m_pCrt, ref string m_pKey);
		//abstract public bool IsExistSubject(uint uAuthorityId, Certificate pCertificate, ItemsMentioned m_pItems);
		abstract public bool IsExistSubject(string pSerialNumber, string pSubjectName, uint uAuthorityId);
		abstract public bool SaveCertificate(uint uAuthorityId, uint uInstance, ItemsMentioned m_pItems, string m_pCrt, string m_pKey);
		abstract public bool Revoke(uint uAuthorityId, string SerialNumber);
		abstract public ObservableCollection<Certificate> ListupCertificates(Int64 m_uAuthorityId);
		abstract public Certificate Fetch(string pSerialNumber);
		//abstract public bool Save2(uint uAuthorityId, uint uInstance, ItemsMentioned m_pItems, string m_pCrt, string m_pKey);
		abstract public byte[] GenerateCRL(uint m_uAuthorityId, int iDays, X509Certificate2 m_pCertificate);
		abstract public bool BeginTransaction();
		abstract public bool Commit();
		abstract public bool Rollback();
	}
}
