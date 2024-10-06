using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Windows.ApplicationModel.Resources;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Arteria_s.App.RoughCA
{
	/// <summary>
	/// An empty window that can be used on its own or navigated to within a Frame.
	/// </summary>
	public sealed partial class MainWindow : Window
	{
		public MainWindow()
		{
			this.InitializeComponent();

			var pApp = App.Current as RoughCA.App;
			if (pApp.m_pPrepareFlags.bExistDbParams == false)
			{
				//　設定情報（接続情報入力画面）に遷移
				TransitPage("Settings");
			}
			else if (pApp.m_pPrepareFlags.bExistOrgProfile == false)
			{
				//　設定情報（接続情報入力画面）に遷移
				TransitPage("Settings");
			}
			else
			{
				//　既定の初期画面に遷移
				TransitPage("ListupCertificate");
			}
			this.MessageFrame.Navigate(typeof(MessagesPage));
		}

		private void DumpCertificate(string pFilepath)
		{
			X509Certificate pCert = new X509Certificate(pFilepath);
			string resultsTrue = pCert.ToString(true);
			Console.WriteLine(resultsTrue);
			string resultsFalse = pCert.ToString(false);
			Console.WriteLine(resultsFalse);
		}

		private void TransitPage(string pTag)
		{
			var pResourceLoader = new ResourceLoader();
			var pIdent = pTag.ToString() + "/Content";
			var pCaption = pResourceLoader.GetString(pIdent);

			Caption.Text = pCaption;

			switch (pTag)
			{
			case "ListupCertificate":
				ContentFrame.Navigate(typeof(DefaultPage));
				break;
			case "CreateCertificate":
				ContentFrame.Navigate(typeof(IssuePage));
				break;
			case "Request":
				ContentFrame.Navigate(typeof(RequestPage));
				break;
			case "Signing":
				ContentFrame.Navigate(typeof(SigningPage));
				break;
			case "Settings":
				ContentFrame.Navigate(typeof(SettingsPage));
				break;
			case "Author":
				ContentFrame.Navigate(typeof(VersionPage));
				break;
			}
		}

		private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
		{
			var clickedItem = args.SelectedItem;
			var clickedItemContainer = args.SelectedItemContainer;
			
			var pItem = clickedItem as NavigationViewItem;
			var pTag = pItem.Tag as string;
			Debug.WriteLine("Tag=" + pTag);

			TransitPage(pTag);
		}

		public void AddMessage(Message pMessage)
		{
			var pMessagesPage = MessageFrame.Content as MessagesPage;
			pMessagesPage.AddMessage(pMessage);
		}
	}
}
