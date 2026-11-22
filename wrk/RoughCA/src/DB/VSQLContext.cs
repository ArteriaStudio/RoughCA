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
		abstract public bool LoadSignRequest(uint uAuthorityId, string pSubjectName, ref string pKeyPem, ref ItemsMentioned pItems);
		abstract public bool SaveSignRequest(uint uAuthorityId, string pKeyPem, ItemsMentioned pItems);
		abstract public bool LoadCertificate(string pCommonName, uint uAuthorityId, ref ItemsMentioned pItems, ref string pCertPem, ref string pKeyPem);
		abstract public bool IsExistSubject(string pSerialNumber, string pSubjectName, uint uAuthorityId);
		abstract public bool SaveCertificate(uint uAuthorityId, uint uInstance, ItemsMentioned pItems, string pCertPem, string pKeyPem);
		abstract public bool Revoke(uint uAuthorityId, string SerialNumber);
		abstract public ObservableCollection<Certificate> ListupCertificates(Int64 m_uAuthorityId);
		abstract public Certificate Fetch(string pSerialNumber);
		abstract public byte[] GenerateCRL(uint m_uAuthorityId, int iDays, X509Certificate2 m_pCertificate);
		abstract public bool BeginTransaction();
		abstract public bool Commit();
		abstract public bool Rollback();
		public virtual void Close() { }
	}
}
