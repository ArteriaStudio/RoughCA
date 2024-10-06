using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
			var pCertificate = pAuthority.Fetch(pSQLContext, pSerialNumber);
			if ((pCertificate != null) && (pCertificate.m_pCertificate != null)) {
				//　ダウンロードフォルダにファイルを出力する。
				var pExportFolder = System.Environment.GetEnvironmentVariable("USERPROFILE") + "\\Downloads";
				var pExportFilepath = pCertificate.Export(pExportFolder);

				pWindow.AddMessage(new Message(AppFacility.Complete, "証明書をファイルに出力しました。", pCertificate.m_pItems.CommonName, pExportFilepath));
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

			//　DBコネクションにおいてトランザクジョンを開始
			// ※ このトランザクションクラスは、リスナパタンでかなり使いやすい設計。
			var pTransaction = pSQLContext.BeginTransaction();
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
				pAuthority.Revoke(pSQLContext, pCertificate);

				//　有効期限を延長した証明書を発行する。
				pAuthority.Update(pSQLContext, pCertificate);

				//　メモリ上のデータを更新する。
				foreach (var pCert in m_pCertificates)
				{
					if (pCert.m_pItems.SerialNumber == pSerialNumber)
					{
						pCert.m_pItems.Revoked = true;
						break;
					}
				}
				pTransaction.Commit();
				pWindow.AddMessage(new Message(AppFacility.Complete, "証明書の有効期限を延長しました。", pCertificate.m_pItems.CommonName));
			}
			catch (AppException pException)
			{
				pTransaction.Rollback();
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
			var pTransaction = pSQLContext.BeginTransaction();
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
				pAuthority.Revoke(pSQLContext, pCertificate);

				//　メモリ上のデータを更新する。
				foreach (var pCert in m_pCertificates)
				{
					if (pCert.m_pItems.SerialNumber == pSerialNumber)
					{
						pCert.m_pItems.Revoked = true;
						break;
					}
				}
				pTransaction.Commit();
				pWindow.AddMessage(new Message(AppFacility.Complete, "証明書を失効しました。", pCertificate.m_pItems.CommonName));
			}
			catch (Exception)
			{
				pTransaction.Rollback();
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

		//　CRL を最新に更新
		private void RefreshButton_Click(object sender, RoutedEventArgs e)
		{
			//　
			var pApp = App.Current as RoughCA.App;
			var pWindow = pApp.m_pWindow as MainWindow;
			var pSQLContext = pApp.GetSQLContext();
			var pAuthority = Authority.Instance;

			//　DBコネクションにおいてトランザクジョンを開始
			// ※ このトランザクションクラスは、リスナパタンでかなり使いやすい設計。
			var pTransaction = pSQLContext.BeginTransaction();
			try
			{
				//　失効リストを生成する。
				var iCrlDays = 128;
				var pBytes = pAuthority.GenerateCRL(pSQLContext, iCrlDays);

				//　ダウンロードフォルダにファイルを出力する。
				var pExportFolder = System.Environment.GetEnvironmentVariable("USERPROFILE") + "\\Downloads";
				var pExportFilepath = pAuthority.ExportCRL(pExportFolder, pBytes);

				pTransaction.Commit();

				/*
				//　
				var pBytes2 = pAuthority.GenerateRootCRL(pSQLContext, iCrlDays);
				var pExportFolder2 = System.Environment.GetEnvironmentVariable("USERPROFILE") + "\\Downloads";
				var pExportFilepath2 = pAuthority.ExportRootCRL(pExportFolder2, pBytes);
				*/


				var pText = "次のパスにファイルを出力しました。";
				var pMessage = new Message(AppFacility.Info, pText, pExportFilepath);
				pWindow.AddMessage(pMessage);
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex.ToString());
				pTransaction.Rollback();
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

		//　署名要求に署名して証明書を作成（認証局が署名して証明書を登録するルート）
		private async void SignatureButton_Click(object sender, RoutedEventArgs e)
		{
			var pPicker = new FileOpenPicker();
			pPicker.FileTypeFilter.Add(RoughCA_Const.CSR_EXTENSION);

			var pApp = App.Current as RoughCA.App;
			var hWnd = WindowNative.GetWindowHandle(pApp.m_pWindow);
			InitializeWithWindow.Initialize(pPicker, hWnd);
			var pFile = await pPicker.PickSingleFileAsync();
			if (pFile != null)
			{
				Debug.Write(pFile.Path);

				var pProfile = pApp.m_pProfile;
				var pSQLContext = pApp.GetSQLContext();
				var pAuthority = Authority.Instance;
				var fOverWrite = true;
				if (pAuthority.CreateForServerCert(pSQLContext, pFile.Path, fOverWrite) == false)
				{
					;
				}
				else
				{
					;
				}
			}
		}

		//　署名要求に署名して認証局証明書を作成（認証局が署名して証明書を登録するルート）
		private async void SignatureCAButton_Click(object sender, RoutedEventArgs e)
		{
			var pPicker = new FileOpenPicker();
			pPicker.FileTypeFilter.Add(RoughCA_Const.CSR_EXTENSION);

			var pApp = App.Current as RoughCA.App;
			var hWnd = WindowNative.GetWindowHandle(pApp.m_pWindow);
			InitializeWithWindow.Initialize(pPicker, hWnd);
			var pFile = await pPicker.PickSingleFileAsync();
			if (pFile != null) {
				Debug.Write(pFile.Path);

				var pProfile = pApp.m_pProfile;
				var pSQLContext = pApp.GetSQLContext();
				var pAuthority = Authority.Instance;
				var fOverWrite = true;
				if (pAuthority.CreateForCACert(pSQLContext, pFile.Path, fOverWrite) == false)
				{
					;
				}
				else
				{
					;
				}
			}
		}
	}
}
