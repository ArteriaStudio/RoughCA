using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
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
			if (IsNotNull(ServerName.Text) == false)
			{
				return (false);
			}

			return (true);
		}

		private void Save_Click(object sender, RoutedEventArgs e)
		{
			if (Validate() == false)
			{
				return;
			}

			var pApp = App.Current as RoughCA.App;
			pApp.SaveOrgProfile();
		}


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
			pApp.SaveOrgProfile();
		}

		public void IsWriteable(bool? bWriteable)
		{
			if (bWriteable == null)
			{
				return;
			}
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
			var pSQLContext = pApp.GetSQLContext();
			var pAuthority  = Authority.Instance;

			if (pAuthority.CreateForDemand(pSQLContext, pProfile.m_pDbParams.IdentityName) == false)
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
				if (pAuthority.ImportCertificate(pSQLContext, pFile.Path) == false)
				{
					//　エラー
					;
				}
			}
		}
	}
}
