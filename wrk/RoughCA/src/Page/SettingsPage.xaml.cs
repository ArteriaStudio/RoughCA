using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Arteria_s.App.RoughCA
{
	/// <summary>
	/// An empty page that can be used on its own or navigated to within a Frame.
	/// </summary>
	public sealed partial class SettingsPage : Page
	{
		public SettingsPage()
		{
			try
			{
				this.InitializeComponent();
			} catch (Exception ex)
			{
				Debug.WriteLine(ex);
			}

			ConnectionTab.Navigate(typeof(BasicParametersPage));
			AuthorityTag.Navigate(typeof(IdentityPage));

			var pApp = App.Current as RoughCA.App;
			if (pApp.m_pPrepareFlags.bExistDbParams == false)
			{
				//　設定情報（接続情報入力画面）に遷移
//				ParametersTab.Navigate(typeof(BasicParametersPage));
			}
			else if (pApp.m_pPrepareFlags.bExistOrgProfile == false)
			{
				//　設定情報（接続情報入力画面）に遷移
//				ParametersTab..Navigate(typeof(IdentityPage));
			}
			else
			{
				//　既定の初期画面に遷移
				//ExportTag.Navigate(typeof());
			}
		}

		private void ParametersTab_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			switch (this.ParametersTab.SelectedIndex)
			{
			case 0:
				//TabContents.Navigate(typeof(BasicParametersPage));
				break;
			case 1:
				//TabContents.Navigate(typeof(IdentityPage));
				break;
			case 2:
				break;
			}
		}

		private void Lock_Click(object sender, RoutedEventArgs e)
		{
			var pBasicParametersPage = ConnectionTab.Content as BasicParametersPage;
			var pIdentityPage = AuthorityTag.Content as IdentityPage;

			pBasicParametersPage.IsWriteable(Lock.IsChecked);
			pIdentityPage.IsWriteable(Lock.IsChecked);
		}
	}
}
