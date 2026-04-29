using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using System.Globalization;
using Windows.Storage.Pickers;
using WinRT.Interop;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Arteria_s.App.RoughCA
{
	/// <summary>
	/// An empty page that can be used on its own or navigated to within a Frame.
	/// </summary>
	public sealed partial class IdentityPage : Page
	{
		private OrgProfile m_pOrgProfile;

		public IdentityPage()
		{
			this.InitializeComponent();

			var m_pCertsStock = Authority.Instance;
			m_pOrgProfile = m_pCertsStock.m_pOrgProfile;
			if (m_pOrgProfile == null ) {
				m_pOrgProfile = new OrgProfile();
			}

			var pApp = App.Current as RoughCA.App;
			var pDbParams = pApp.m_pDbParams;
//			m_pOrgProfile.TrustName = pDbParams.TrustName;
//			m_pOrgProfile.IssueName = pDbParams.IssueName;
		}

		//　TODO: データオブジェクト側に検査処理を寄せること
		private bool IsNotNull(string pText)
		{
			if (pText == null)
			{
				return (false);
			}
			if (pText.Length <= 0)
			{
				return (false);
			}
			return (true);
		}
		//　有効なCountryCodeであるかを検査
		public static bool IsValidCountryCode(string code)
		{
			if (code?.Length != 2) return false;
			try
			{
				var region = new RegionInfo(code.ToUpper());
				return true;
			}
			catch (ArgumentException)
			{
				return false;
			}
		}


		private bool Validate()
		{
			if (IsNotNull(OrgName.Text) == false)
			{
				return (false);
			}
			if (IsNotNull(OrgUnitName.Text) == false)
			{
				return (false);
			}
			if (IsNotNull(ProvinceName.Text) == false)
			{
				return (false);
			}
			if (IsNotNull(LocalityName.Text) == false)
			{
				return (false);
			}
			if (IsNotNull(CountryName.Text) == false)
			{
				return (false);
			}
			if (IsValidCountryCode(CountryName.Text) == false)
			{
				return (false);
			}
			if (IsNotNull(ServerName.Text) == false)
			{
				return (false);
			}
			/*
			if (IsNotNull(TrustName.Text) == false)
			{
				return (false);
			}
			if (IsNotNull(IssueName.Text) == false)
			{
				return (false);
			}
			*/

			return (true);
		}
/*
		private void Save_Click(object sender, RoutedEventArgs e)
		{
			if (Validate() == false)
			{
				return;
			}

			var pApp = App.Current as RoughCA.App;
			DbParams m_pDbParams;
			pApp.SaveOrgProfile(m_pDbParams);
		}
*/

		private void Settings_TextChanged(object sender, TextChangedEventArgs e)
		{
			if (Validate() == false)
			{
				//Save.IsEnabled = false;
			}
			else
			{
				//Save.IsEnabled = true;
			}

			return;
		}

		
		private void IdentityParameters_LostFocus(object sender, RoutedEventArgs e)
		{
			if (m_bWriteable == false)
			{
				return;
			}
			if (Validate() == false)
			{
				return;
			}

			var pApp = App.Current as RoughCA.App;
			var pDbParams = pApp.m_pDbParams;
/*
			pDbParams.HostName
			pDbParams.InstanceName
			pDbParams.SchemaName
			pDbParams.ClientKey
			pDbParams.ClientCrt
			pDbParams.TrustCrt
			pDbParams.IdentityName
*/
/*
			pDbParams.TrustName = TrustName.Text;
			pDbParams.IssueName = IssueName.Text;
*/
			pApp.SaveOrgProfile(pDbParams);

		}

		public void IsWriteable(bool? bWriteable)
		{
			if (bWriteable == null)
			{
				return;
			}
//			TrustName.IsReadOnly     = !bWriteable.Value;
//			IssueName.IsReadOnly     = !bWriteable.Value;
			OrgName.IsReadOnly       = !bWriteable.Value;
			OrgUnitName.IsReadOnly   = !bWriteable.Value;
			LocalityName.IsReadOnly  = !bWriteable.Value;
			ProvinceName.IsReadOnly  = !bWriteable.Value;
			CountryName.IsReadOnly   = !bWriteable.Value;
			ServerName.IsReadOnly    = !bWriteable.Value;

			m_bWriteable = bWriteable.Value;
		}
		private bool m_bWriteable = false;

		private void BrowseClientKey_Click(object sender, RoutedEventArgs e)
		{
			;
		}

		//　署名要求を作成
		private void SignRequest_Click(object sender, RoutedEventArgs e)
		{
			var pApp        = App.Current as RoughCA.App;
			var pWindow     = pApp.m_pWindow as MainWindow;
			var pProfile    = pApp.m_pProfile;
			var pDbParams   = pApp.m_pDbParams;
			var pSQLContext = pApp.GetSQLContext();
			var pAuthority  = Authority.Instance;

			if (pAuthority.CreateForDemand(pSQLContext, pDbParams.uInstance, pDbParams.IdentityName) == false)
			{
				//　エラー
				;
			}
		}

		//　署名済み証明書を受入
		private async void CertAccept_Click(object sender, RoutedEventArgs e)
		{
			var pPicker = new FileOpenPicker();
			pPicker.FileTypeFilter.Add(RoughCA_Const.CRT_EXTENSION);

			var pApp = App.Current as RoughCA.App;
			var hWnd = WindowNative.GetWindowHandle(pApp.m_pWindow);
			InitializeWithWindow.Initialize(pPicker, hWnd);
			var pFile = await pPicker.PickSingleFileAsync();
			if (pFile != null)
			{
				Debug.Write(pFile.Path);

				var pProfile = pApp.m_pProfile;
				var pSQLContext = pApp.GetSQLContext();
				//　署名された証明書を入力する。
				var pAuthority = Authority.Instance;
				var pDbParams = pApp.m_pDbParams;
				if (pAuthority.ImportCertificate(pSQLContext, pDbParams.uInstance, pFile.Path) == false)
				{
					//　エラー
					;
				}
			}
		}
	}
}
