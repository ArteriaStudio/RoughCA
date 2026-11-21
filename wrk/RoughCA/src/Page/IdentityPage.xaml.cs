using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
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
			if (IsNotNull(TrustName.Text) == false)
			{
				return (false);
			}
			if (IsNotNull(IssueName.Text) == false)
			{
				return (false);
			}

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

		
		//　未入力または不正な項目名の一覧を返す
		public List<string> GetMissingFields()
		{
			var missing = new List<string>();
			if (!IsNotNull(TrustName.Text))    missing.Add("ルート認証局名");
			if (!IsNotNull(IssueName.Text))    missing.Add("発行用認証局名");
			if (!IsNotNull(OrgName.Text))      missing.Add("組織名");
			if (!IsNotNull(OrgUnitName.Text))  missing.Add("部署名");
			if (!IsNotNull(ProvinceName.Text)) missing.Add("都道府県");
			if (!IsNotNull(LocalityName.Text)) missing.Add("市町村");
			if (!IsNotNull(CountryName.Text))
			{
				missing.Add("国名");
			}
			else if (!IsValidCountryCode(CountryName.Text.Trim()))
			{
				missing.Add("国名（2文字のISO国コード、例: JP）");
			}
			if (!IsNotNull(ServerName.Text))   missing.Add("証明情報公開サーバ");
			return missing;
		}

		//　現在の画面入力値を同期した OrgProfile を取得
		public OrgProfile GetCurrentOrgProfile()
		{
			if (m_pOrgProfile == null)
			{
				m_pOrgProfile = new OrgProfile();
			}
			m_pOrgProfile.TrustName    = TrustName.Text?.Trim() ?? "";
			m_pOrgProfile.IssueName    = IssueName.Text?.Trim() ?? "";
			m_pOrgProfile.OrgName      = OrgName.Text?.Trim() ?? "";
			m_pOrgProfile.OrgUnitName  = OrgUnitName.Text?.Trim() ?? "";
			m_pOrgProfile.ProvinceName = ProvinceName.Text?.Trim() ?? "";
			m_pOrgProfile.LocalityName = LocalityName.Text?.Trim() ?? "";
			m_pOrgProfile.CountryName  = CountryName.Text?.Trim().ToUpperInvariant() ?? "";
			m_pOrgProfile.ServerName   = ServerName.Text?.Trim() ?? "";
			return m_pOrgProfile;
		}

		private void IdentityParameters_LostFocus(object sender, RoutedEventArgs e)
		{
			//　認証局証明書の生成を伴う確定処理は、編集解除（ロック）時に SettingsPage から一括実行する
		}

		public void IsWriteable(bool? bWriteable)
		{
			if (bWriteable == null)
			{
				return;
			}
			var isReadOnly = !bWriteable.Value;
			TrustName.IsReadOnly     = isReadOnly;
			IssueName.IsReadOnly     = isReadOnly;
			OrgName.IsReadOnly       = isReadOnly;
			OrgUnitName.IsReadOnly   = isReadOnly;
			LocalityName.IsReadOnly  = isReadOnly;
			ProvinceName.IsReadOnly  = isReadOnly;
			CountryName.IsReadOnly   = isReadOnly;
			ServerName.IsReadOnly    = isReadOnly;

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

			var pIdentityName = pDbParams.DatabaseName;
			var pExportFolder = pProfile.ResolveExportFolder(pDbParams);
			if (pAuthority.CreateForDemand(pSQLContext, pDbParams.uInstance, pIdentityName, pExportFolder) == false)
//			if (pAuthority.CreateForDemand(pSQLContext, pDbParams.uInstance, pDbParams.IdentityName) == false)
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
