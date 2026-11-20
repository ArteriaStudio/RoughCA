using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using WinRT.Interop;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace ArteriaS.App.RoughCA
{
	/// <summary>
	/// An empty page that can be used on its own or navigated to within a Frame.
	/// </summary>
	public sealed partial class DefaultPage : Page
	{
		private ObservableCollection<Certificate> m_pCertificates;

		public DefaultPage()
		{
			this.InitializeComponent();

			var pApp = App.Current as RoughCA.App;
			var pProfile = pApp.m_pProfile;
			var pSQLContext = pApp.GetSQLContext();
			if (pSQLContext != null )
			{
				var pAuthority = Authority.Instance;
				m_pCertificates = pAuthority.Listup(pSQLContext);
			}

			UpdateFormState();
		}

		private void UpdateFormState()
		{
			bool IsEnabled = true;
			if (CertsList.Items.Count <= 0)
			{
				IsEnabled = false;
			}
			else if (CertsList.SelectedIndex == -1)
			{
				IsEnabled = false;
			}
			RevokeButton.IsEnabled = IsEnabled;
			UpdateButton.IsEnabled = IsEnabled;
			ExportButton.IsEnabled = IsEnabled;
		}

		//　
		private void CertsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			UpdateFormState();
		}

		//　
		private async void DisplayExportDialog(string pCommonName, string pSerialNumber)
		{
			ContentDialog pExportDialog = new ContentDialog
			{
				Title = "証明書をエクスポート",
				Content = "証明書（" + pCommonName + "：" + pSerialNumber + "）をファイルに出力します。\nよろしいですか？",
				CloseButtonText = "いいえ",
				PrimaryButtonText = "はい",
				DefaultButton = ContentDialogButton.Primary,
				XamlRoot = this.Content.XamlRoot
			};

			try
			{
				var eResult = await pExportDialog.ShowAsync();
				this.OnChoiceExportDialog(eResult, pSerialNumber);
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex);
			}

			return;
		}

		//　証明書をエクスポートする処理（利用者の同意を得た契機）
		private void OnChoiceExportDialog(ContentDialogResult eResult, string pSerialNumber)
		{
			if (eResult != ContentDialogResult.Primary)
			{
				return;
			}
			var pApp = App.Current as RoughCA.App;
			var pWindow = pApp.m_pWindow as MainWindow;
			var pSQLContext = pApp.GetSQLContext();
			var pAuthority = Authority.Instance;

			try
			{
				if (pSQLContext.BeginTransaction() == false)
				{
					return;
				}
				var pCertificate = pAuthority.Fetch(pSQLContext, pSerialNumber);
				if ((pCertificate != null) && (pCertificate.m_pCertificate != null))
				{
					//　設定された出力先フォルダ・出力形式でファイルを出力する。
					var pExportFolder = pApp.m_pProfile.ResolveExportFolder(pApp.m_pDbParams);
					var bIsPem = pApp.m_pProfile.IsPemExport(pApp.m_pDbParams);
					var pExportFilepath = pCertificate.Export(pExportFolder, bIsPem);

					pWindow.AddMessage(new Message(AppFacility.Complete, "証明書をファイルに出力しました。", pCertificate.m_pItems.CommonName, pExportFilepath));
				}
				pSQLContext.Commit();
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex);
			}
		}

		//　選択した証明書をファイルに出力する。
		private void ExportButton_Click(object sender, RoutedEventArgs e)
		{
			if (CertsList.SelectedIndex == -1)
			{
				//　選択項目がなければ処理なし
				return;
			}

			var pCommonName   = m_pCertificates[CertsList.SelectedIndex].m_pItems.CommonName;
			var pSerialNumber = m_pCertificates[CertsList.SelectedIndex].m_pItems.SerialNumber;

			DisplayExportDialog(pCommonName, pSerialNumber);

			return;
		}

		//　
		private async void DisplayUpdateDialog(string pCommonName, string pSerialNumber)
		{
			ContentDialog pDialog = new ContentDialog
			{
				Title = "証明書を更新",
				Content = "証明書（" + pCommonName + "：" + pSerialNumber + "）の有効期限を延長した証明書を発行します。\nよろしいですか？",
				CloseButtonText = "いいえ",
				PrimaryButtonText = "はい",
				DefaultButton = ContentDialogButton.Primary,
				XamlRoot = this.Content.XamlRoot
			};

			try
			{
				var eResult = await pDialog.ShowAsync();
				this.OnChoiceUpdateDialog(eResult, pSerialNumber);
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex);
			}

			return;
		}

		//　証明書の有効期限を延長した証明書を発行する処理（利用者の同意を得た契機）
		private void OnChoiceUpdateDialog(ContentDialogResult eResult, string pSerialNumber)
		{
			if (eResult != ContentDialogResult.Primary)
			{
				return;
			}

			//　
			var pApp = App.Current as RoughCA.App;
			var pWindow = pApp.m_pWindow as MainWindow;
			var pProfile = pApp.m_pProfile;
			var pSQLContext = pApp.GetSQLContext();
			var pAuthority = Authority.Instance;
			var pDBParams = pApp.m_pDbParams;

			//　DBコネクションにおいてトランザクジョンを開始
			// ※ このトランザクションクラスは、リスナパタンでかなり使いやすい設計。
			pSQLContext.BeginTransaction();
			try
			{
				//　選択された証明書データを獲得
				var pCertificate = pAuthority.Fetch(pSQLContext, pSerialNumber);
				if (pCertificate.m_pItems.Revoked == true)
				{
					//　失効した証明書を選択した。
					throw (new AppException(AppError.InvalidCertificate, AppFacility.Error, AppFlow.CreateCertificateForUpdate, pCertificate.m_pItems.SerialNumber));
				}

				//　証明書を失効する。
				var uAuthorityId = pAuthority.GetAuthorityId();
				pSQLContext.Revoke(uAuthorityId, pCertificate.m_pItems.SerialNumber);

				//　有効期限を延長した証明書を発行する。
				pAuthority.Update(pSQLContext, pDBParams.uInstance, pCertificate);

				//　メモリ上のデータを更新する。
				foreach (var pCert in m_pCertificates)
				{
					if (pCert.m_pItems.SerialNumber == pSerialNumber)
					{
						pCert.m_pItems.Revoked = true;
						break;
					}
				}
				pSQLContext.Commit();
				pWindow.AddMessage(new Message(AppFacility.Complete, "証明書の有効期限を延長しました。", pCertificate.m_pItems.CommonName));
			}
			catch (AppException pException)
			{
				//pTransaction.Rollback();
				pSQLContext.Rollback();
				pWindow.AddMessage(new Message(pException.m_eFacility, pException.GetText(), pException.GetParameter()));
			}

			return;
		}

		//　有効期限を更新した証明書を発行
		private void UpdateButton_Click(object sender, RoutedEventArgs e)
		{
			if (CertsList.SelectedIndex == -1)
			{
				//　選択項目がなければ処理なし
				return;
			}

			var pCommonName   = m_pCertificates[CertsList.SelectedIndex].m_pItems.CommonName;
			var pSerialNumber = m_pCertificates[CertsList.SelectedIndex].m_pItems.SerialNumber;

			//　ダイアログを開始
			DisplayUpdateDialog(pCommonName, pSerialNumber);

			return;
		}

		private async void DisplayRevokeDialog(string pCommonName, string pSerialNumber)
		{
			ContentDialog pDialog = new ContentDialog
			{
				Title = "証明書を失効",
				Content = "証明書（" + pCommonName + "：" + pSerialNumber + "）を失効させます。\nよろしいですか？",
				CloseButtonText = "いいえ",
				PrimaryButtonText = "はい",
				DefaultButton = ContentDialogButton.Primary,
				XamlRoot = this.Content.XamlRoot
			};

			try
			{
				var eResult = await pDialog.ShowAsync();
				this.OnChoiceRevokeDialog(eResult, pSerialNumber);
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex);
			}

			return;
		}

		//　
		private void OnChoiceRevokeDialog(ContentDialogResult eResult, string pSerialNumber)
		{
			if (eResult != ContentDialogResult.Primary)
			{
				return;
			}

			//　
			var pApp = App.Current as RoughCA.App;
			var pWindow = pApp.m_pWindow as MainWindow;
			var pSQLContext = pApp.GetSQLContext();
			var pAuthority = Authority.Instance;

			//　DBコネクションにおいてトランザクジョンを開始
			// ※ このトランザクションクラスは、リスナパタンでかなり使いやすい設計。
			//var pTransaction = pSQLContext.BeginTransaction();
			pSQLContext.BeginTransaction();
			try
			{
				//　選択された証明書データを獲得する。
				var pCertificate = pAuthority.Fetch(pSQLContext, pSerialNumber);
				if (pCertificate.m_pItems.Revoked == true)
				{
					//　失効した証明書を選択した。
					throw (new AppException(AppError.InvalidCertificate, AppFacility.Error, AppFlow.Revoke, pCertificate.m_pItems.SerialNumber));
				}

				//　証明書を失効する。
				var uAuthorityId = pAuthority.GetAuthorityId();
				pSQLContext.Revoke(uAuthorityId, pCertificate.m_pItems.SerialNumber);

				//　メモリ上のデータを更新する。
				foreach (var pCert in m_pCertificates)
				{
					if (pCert.m_pItems.SerialNumber == pSerialNumber)
					{
						pCert.m_pItems.Revoked = true;
						break;
					}
				}
				pSQLContext.Commit();
				pWindow.AddMessage(new Message(AppFacility.Complete, "証明書を失効しました。", pCertificate.m_pItems.CommonName));
			}
			catch (Exception)
			{
				pSQLContext.Rollback();
			}
			
			return;
		}


		//　指定された証明書を失効させる。
		private void RevokeButton_Click(object sender, RoutedEventArgs e)
		{
			if (CertsList.SelectedIndex == -1)
			{
				//　選択項目がなければ処理なし
				return;
			}

			var pCommonName   = m_pCertificates[CertsList.SelectedIndex].m_pItems.CommonName;
			var pSerialNumber = m_pCertificates[CertsList.SelectedIndex].m_pItems.SerialNumber;

			//　ダイアログを開始
			DisplayRevokeDialog(pCommonName, pSerialNumber);

			return;
		}

		//　CRL を最新に更新（ルート認証局および有効な発行認証局すべてを対象に生成・出力）
		private void RefreshButton_Click(object sender, RoutedEventArgs e)
		{
			var pApp = App.Current as RoughCA.App;
			var pWindow = pApp.m_pWindow as MainWindow;
			var pSQLContext = pApp.GetSQLContext();
			var pAuthority = Authority.Instance;

			//　署名可能な有効な認証局証明書（ルートCA・発行CA）を取得
			var pTargetAuthorities = pAuthority.ListupSigningAuthorities(pSQLContext);
			if ((pTargetAuthorities.Count == 0) && (pAuthority.m_pAuthorityItem != null))
			{
				pTargetAuthorities.Add(pAuthority.m_pAuthorityItem);
			}
			if (pTargetAuthorities.Count == 0)
			{
				pWindow.AddMessage(new Message(AppFacility.Error, "CRLを生成できる有効な認証局証明書が見つかりませんでした。", ""));
				return;
			}

			//　設定された出力先フォルダを取得
			var pExportFolder = pApp.m_pProfile.ResolveExportFolder(pApp.m_pDbParams);
			var pExportedFiles = new List<(string CommonName, string Filepath)>();

			//　DBコネクションにおいてトランザクションを開始
			var pTransaction = pSQLContext.BeginTransaction();
			try
			{
				var iCrlDays = 128;
				foreach (var pCACert in pTargetAuthorities)
				{
					//　各認証局が発行した失効証明書のみを含むCRLを生成・出力する
					var pBytes = pAuthority.GenerateCRL(pSQLContext, iCrlDays, pCACert);
					var pExportFilepath = pAuthority.ExportCRL(pExportFolder, pBytes, pCACert);
					pExportedFiles.Add((pCACert.m_pItems.CommonName, pExportFilepath));
				}

				pSQLContext.Commit();

				foreach (var pExported in pExportedFiles)
				{
					var pText = $"次のパスにファイルを出力しました。（{pExported.CommonName}）";
					var pMessage = new Message(AppFacility.Info, pText, pExported.Filepath);
					pWindow.AddMessage(pMessage);
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex.ToString());
				pSQLContext.Rollback();
				pWindow.AddMessage(new Message(AppFacility.Error, "CRLの生成に失敗しました。", ex.Message));
			}

			return;
		}

		//　証明書一覧を再表示
		private void ReloadButton_Click(object sender, RoutedEventArgs e)
		{
			var pApp = App.Current as RoughCA.App;
			var pProfile = pApp.m_pProfile;
			var pSQLContext = pApp.GetSQLContext();
			var pAuthority = Authority.Instance;
			var pNewCertificates = pAuthority.Listup(pSQLContext);

			//　カーソルとキャッシュの差分を探索して、差分となった要素を更新
			List<Certificate>	pDeleteList = new List<Certificate>();
			List<Certificate>	pAppendList = new List<Certificate>();

			if (pNewCertificates != null)
			{
				foreach (var pNewCertificate in pNewCertificates)
				{
					if (m_pCertificates.Contains(pNewCertificate) == false)
					{
						pAppendList.Add(pNewCertificate);
					}
				}
				foreach (var pOldCertificate in m_pCertificates)
				{
					if (pNewCertificates.Contains(pOldCertificate) == false)
					{
						pDeleteList.Add(pOldCertificate);
					}
				}
			}
			foreach (var pDeleteItem in  pDeleteList)
			{
				m_pCertificates.Remove(pDeleteItem);
			}
			foreach (var pAppendItem in pAppendList)
			{
				m_pCertificates.Add(pAppendItem);
			}
		}

		//　署名に使用する認証局を選択するダイアログを表示
		private async Task<Certificate> SelectSigningAuthorityAsync(string pTitle, string pCsrFileName, string pPreferredCommonName)
		{
			var pApp = App.Current as RoughCA.App;
			var pSQLContext = pApp.GetSQLContext();
			var pAuthority = Authority.Instance;

			var pAuthorities = pAuthority.ListupSigningAuthorities(pSQLContext);
			if (pAuthorities.Count == 0)
			{
				var pWindow = pApp.m_pWindow as MainWindow;
				pWindow.AddMessage(new Message(AppFacility.Error, "署名に使用できる有効な認証局証明書が見つかりませんでした。", ""));
				return (null);
			}

			var pComboBox = new ComboBox
			{
				Header = "署名する認証局",
				HorizontalAlignment = HorizontalAlignment.Stretch
			};

			int iDefaultIndex = -1;
			for (int i = 0; i < pAuthorities.Count; i++)
			{
				var pCert = pAuthorities[i];
				pComboBox.Items.Add(new ComboBoxItem
				{
					Content = pAuthority.FormatAuthorityDisplayName(pCert),
					Tag = pCert
				});

				if ((iDefaultIndex == -1) &&
					!string.IsNullOrEmpty(pPreferredCommonName) &&
					string.Equals(pCert.m_pItems.CommonName, pPreferredCommonName, StringComparison.Ordinal))
				{
					iDefaultIndex = i;
				}
			}
			pComboBox.SelectedIndex = (iDefaultIndex >= 0) ? iDefaultIndex : 0;

			var pPanel = new StackPanel { Spacing = 12 };
			pPanel.Children.Add(new TextBlock
			{
				Text = $"対象ファイル：{pCsrFileName}",
				TextWrapping = TextWrapping.Wrap
			});
			pPanel.Children.Add(pComboBox);

			var pDialog = new ContentDialog
			{
				Title = pTitle,
				Content = pPanel,
				PrimaryButtonText = "署名",
				CloseButtonText = "キャンセル",
				DefaultButton = ContentDialogButton.Primary,
				XamlRoot = this.Content.XamlRoot
			};

			try
			{
				var eResult = await pDialog.ShowAsync();
				if (eResult == ContentDialogResult.Primary && pComboBox.SelectedItem is ComboBoxItem pSelected)
				{
					return (pSelected.Tag as Certificate);
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex);
			}

			return (null);
		}

		//　署名要求に署名して証明書を作成（認証局が署名して証明書を登録するルート）
		private async void SignatureButton_Click(object sender, RoutedEventArgs e)
		{
			var pPicker = new FileOpenPicker();
			pPicker.FileTypeFilter.Add(RoughCA_Const.CSR_EXTENSION);

			var pApp = App.Current as RoughCA.App;
			var pWindow = pApp.m_pWindow as MainWindow;
			var hWnd = WindowNative.GetWindowHandle(pApp.m_pWindow);
			InitializeWithWindow.Initialize(pPicker, hWnd);
			var pFile = await pPicker.PickSingleFileAsync();
			if (pFile != null)
			{
				Debug.Write(pFile.Path);

				var pAuthority = Authority.Instance;
				var pIssuerCert = await SelectSigningAuthorityAsync(
					"署名要求に署名",
					pFile.Name,
					pAuthority.m_pOrgProfile?.IssueName);
				if (pIssuerCert == null)
				{
					return;
				}

				var pSQLContext = pApp.GetSQLContext();
				var pDBParams = pApp.m_pDbParams;
				var fOverWrite = true;
				try
				{
					if (pAuthority.CreateForServerCert(pSQLContext, pDBParams.uInstance, pFile.Path, fOverWrite, pIssuerCert))
					{
						pWindow.AddMessage(new Message(AppFacility.Complete, "署名要求に署名して証明書を発行しました。", $"{pFile.Name}（発行元: {pIssuerCert.m_pItems.CommonName}）"));
					}
				}
				catch (AppException pException)
				{
					pWindow.AddMessage(new Message(pException.m_eFacility, pException.GetText(), pException.GetParameter()));
				}
				catch (Exception ex)
				{
					Debug.WriteLine(ex);
					pWindow.AddMessage(new Message(AppFacility.Error, "署名要求の署名に失敗しました。", ex.Message));
				}
			}
		}

		//　署名要求に署名して認証局証明書を作成（認証局が署名して証明書を登録するルート）
		private async void SignatureCAButton_Click(object sender, RoutedEventArgs e)
		{
			var pPicker = new FileOpenPicker();
			pPicker.FileTypeFilter.Add(RoughCA_Const.CSR_EXTENSION);

			var pApp = App.Current as RoughCA.App;
			var pWindow = pApp.m_pWindow as MainWindow;
			var hWnd = WindowNative.GetWindowHandle(pApp.m_pWindow);
			InitializeWithWindow.Initialize(pPicker, hWnd);
			var pFile = await pPicker.PickSingleFileAsync();
			if (pFile != null)
			{
				Debug.Write(pFile.Path);

				var pAuthority = Authority.Instance;
				var pIssuerCert = await SelectSigningAuthorityAsync(
					"認証局署名要求に署名",
					pFile.Name,
					pAuthority.m_pOrgProfile?.TrustName);
				if (pIssuerCert == null)
				{
					return;
				}

				var pSQLContext = pApp.GetSQLContext();
				var pDBParams = pApp.m_pDbParams;
				var fOverWrite = true;
				try
				{
					if (pAuthority.CreateForCACert(pSQLContext, pDBParams.uInstance, pFile.Path, fOverWrite, pIssuerCert))
					{
						pWindow.AddMessage(new Message(AppFacility.Complete, "認証局署名要求に署名してCA証明書を発行しました。", $"{pFile.Name}（発行元: {pIssuerCert.m_pItems.CommonName}）"));
					}
				}
				catch (AppException pException)
				{
					pWindow.AddMessage(new Message(pException.m_eFacility, pException.GetText(), pException.GetParameter()));
				}
				catch (Exception ex)
				{
					Debug.WriteLine(ex);
					pWindow.AddMessage(new Message(AppFacility.Error, "認証局署名要求の署名に失敗しました。", ex.Message));
				}
			}
		}

		//　一覧表示用のフォーマット・バッジヘルパー
		public static string FormatDate(DateTime dt)
		{
			if (dt == DateTime.MinValue)
			{
				return "-";
			}
			return dt.ToString("yyyy/MM/dd");
		}

		public static string GetStatusText(bool revoked, DateTime expireAt)
		{
			if (revoked)
			{
				return "失効";
			}
			if (expireAt != DateTime.MinValue && expireAt <= DateTime.Now)
			{
				return "期限切れ";
			}
			if (expireAt != DateTime.MinValue && expireAt <= DateTime.Now.AddDays(30))
			{
				return "期限間近";
			}
			return "有効";
		}

		public static Brush GetStatusBackground(bool revoked, DateTime expireAt)
		{
			var isDark = Application.Current?.RequestedTheme == ApplicationTheme.Dark;
			if (revoked || (expireAt != DateTime.MinValue && expireAt <= DateTime.Now))
			{
				return new SolidColorBrush(isDark
					? Windows.UI.Color.FromArgb(0x2B, 0xFF, 0x99, 0xA4)
					: Windows.UI.Color.FromArgb(0x1F, 0xC4, 0x2B, 0x1C));
			}
			if (expireAt != DateTime.MinValue && expireAt <= DateTime.Now.AddDays(30))
			{
				return new SolidColorBrush(isDark
					? Windows.UI.Color.FromArgb(0x2B, 0xFC, 0xE1, 0x00)
					: Windows.UI.Color.FromArgb(0x24, 0xC1, 0x9C, 0x00));
			}
			return new SolidColorBrush(isDark
				? Windows.UI.Color.FromArgb(0x2B, 0x2E, 0xA0, 0x43)
				: Windows.UI.Color.FromArgb(0x1F, 0x10, 0x7C, 0x10));
		}

		public static Brush GetStatusForeground(bool revoked, DateTime expireAt)
		{
			var isDark = Application.Current?.RequestedTheme == ApplicationTheme.Dark;
			if (revoked || (expireAt != DateTime.MinValue && expireAt <= DateTime.Now))
			{
				return new SolidColorBrush(isDark
					? Windows.UI.Color.FromArgb(0xFF, 0xFF, 0x99, 0xA4)
					: Windows.UI.Color.FromArgb(0xFF, 0xB4, 0x23, 0x18));
			}
			if (expireAt != DateTime.MinValue && expireAt <= DateTime.Now.AddDays(30))
			{
				return new SolidColorBrush(isDark
					? Windows.UI.Color.FromArgb(0xFF, 0xFC, 0xE1, 0x00)
					: Windows.UI.Color.FromArgb(0xFF, 0x8A, 0x5A, 0x00));
			}
			return new SolidColorBrush(isDark
				? Windows.UI.Color.FromArgb(0xFF, 0x6C, 0xCB, 0x5F)
				: Windows.UI.Color.FromArgb(0xFF, 0x0E, 0x6A, 0x0E));
		}

		public static string GetTypeLabel(CertificateType typeOf)
		{
			return typeOf switch
			{
				CertificateType.CA => "CA",
				CertificateType.Server => "Server",
				CertificateType.Client => "Client",
				CertificateType.Demand => "CSR",
				CertificateType.RemoteDesktopListener => "RDP",
				CertificateType.CodeSign => "CodeSign",
				_ => typeOf.ToString()
			};
		}
	}
}
