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
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex);
			}

			ConnectionTab.Navigated += (s, e) => ApplyWriteableState();
			AuthorityTag.Navigated  += (s, e) => ApplyWriteableState();

			ConnectionTab.Navigate(typeof(BasicParametersPage));
			AuthorityTag.Navigate(typeof(IdentityPage));

			var pApp = App.Current as RoughCA.App;
			if (pApp.m_pPrepareFlags.bExistDbParams == false)
			{
				ParametersTab.SelectedIndex = 0;
			}
			else if (pApp.m_pPrepareFlags.bExistOrgProfile == false || pApp.m_pPrepareFlags.bExistAuthority == false)
			{
				//　DB接続は済んでいるが認証局情報・証明書が未作成の場合は「基本認証局情報」タブを初期表示
				ParametersTab.SelectedIndex = 1;
				ShowInfoBar(
					InfoBarSeverity.Informational,
					"認証局の初期設定が必要です",
					"右上の「編集」ボタンから認証局名と組織情報を入力し、編集を解除するとルート認証局の鍵と自己証明書が作成されます。");
			}
		}

		private void ShowInfoBar(InfoBarSeverity severity, string title, string message)
		{
			EditModeInfoBar.Severity = severity;
			EditModeInfoBar.Title = title;
			EditModeInfoBar.Message = message;
			EditModeInfoBar.IsOpen = true;
		}

		private void ApplyWriteableState()
		{
			var isUnlocked = Lock?.IsChecked == true;
			if (ConnectionTab?.Content is BasicParametersPage pBasicParametersPage)
			{
				pBasicParametersPage.IsWriteable(isUnlocked);
			}
			if (AuthorityTag?.Content is IdentityPage pIdentityPage)
			{
				pIdentityPage.IsWriteable(isUnlocked);
			}
		}

		private void ParametersTab_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			//　タブ切り替え時も現在の編集ロック状態を子ページへ確実に同期
			ApplyWriteableState();
		}

		private void Lock_Click(object sender, RoutedEventArgs e)
		{
			var pBasicParametersPage = ConnectionTab.Content as BasicParametersPage;
			var pIdentityPage = AuthorityTag.Content as IdentityPage;

			var isUnlocked = Lock.IsChecked == true;
			LockStatusIcon.Glyph = isUnlocked ? "\uE785" : "\uE72E";

			pBasicParametersPage?.IsWriteable(Lock.IsChecked);
			pIdentityPage?.IsWriteable(Lock.IsChecked);

			if (isUnlocked)
			{
				ShowInfoBar(
					InfoBarSeverity.Informational,
					"編集モード",
					"設定を編集できます。入力が終わったらもう一度「編集」ボタンを押してロックすると、入力チェックと認証局証明書の作成が実行されます。");
				return;
			}

			//　編集解除（ロック）時：接続設定の確定 → 組織プロファイルのチェック → 未作成ならルート認証局の鍵と自己証明書を作成
			CommitOnLockRelease(pBasicParametersPage, pIdentityPage);
		}

		private void CommitOnLockRelease(BasicParametersPage pBasicParametersPage, IdentityPage pIdentityPage)
		{
			var pApp = App.Current as RoughCA.App;
			var pWindow = pApp?.m_pWindow as MainWindow;
			if (pApp == null)
			{
				return;
			}

			//　1. データベース接続設定に変更があれば保存・再接続
			if (pBasicParametersPage != null)
			{
				if (!pBasicParametersPage.CommitIfModified(out var dbError))
				{
					ShowInfoBar(InfoBarSeverity.Error, "接続設定エラー", dbError ?? "データベース接続に失敗しました。");
					pWindow?.AddMessage(new Message(AppFacility.Error, "データベース接続設定の適用に失敗しました。", pApp.m_pDbParams?.ProfileName ?? "", dbError));
					return;
				}
			}

			if (pIdentityPage == null)
			{
				EditModeInfoBar.IsOpen = false;
				return;
			}

			//　2. 認証局名・券面表示情報の入力チェック
			var missingFields = pIdentityPage.GetMissingFields();
			if (missingFields.Count > 0)
			{
				var missingText = string.Join("・", missingFields);
				ShowInfoBar(
					InfoBarSeverity.Warning,
					"認証局情報が未入力です",
					$"次の項目が未入力または不正なため、認証局の証明書はまだ作成されていません：{missingText}");
				pWindow?.AddMessage(new Message(AppFacility.Info, "未入力項目があるため認証局証明書の作成をスキップしました。", pApp.m_pDbParams?.ProfileName ?? "", missingText));
				return;
			}

			//　3. 組織プロファイルを保存し、ルート認証局（および発行認証局）の鍵と自己証明書を作成・確保
			var orgProfile = pIdentityPage.GetCurrentOrgProfile();
			var ok = pApp.CommitOrgProfileAndEnsureAuthority(
				pApp.m_pDbParams,
				orgProfile,
				out var createdRoot,
				out var createdIssue,
				out var authError);

			if (!ok)
			{
				ShowInfoBar(InfoBarSeverity.Error, "認証局の初期化に失敗しました", authError ?? "認証局の鍵または証明書の作成に失敗しました。");
				pWindow?.AddMessage(new Message(AppFacility.Error, "認証局の鍵・証明書の作成に失敗しました。", orgProfile.TrustName, authError));
				return;
			}

			pWindow?.UpdateLocationBadge(pApp);

			if (createdRoot && createdIssue)
			{
				ShowInfoBar(
					InfoBarSeverity.Success,
					"認証局を作成しました",
					$"ルート認証局「{orgProfile.TrustName}」および発行用認証局「{orgProfile.IssueName}」の鍵と自己証明書を作成しました。");
				pWindow?.AddMessage(new Message(AppFacility.Complete, "ルート認証局および発行用認証局の鍵と証明書を作成しました。", orgProfile.TrustName, orgProfile.IssueName));
			}
			else if (createdRoot)
			{
				ShowInfoBar(
					InfoBarSeverity.Success,
					"ルート認証局を作成しました",
					$"ルート認証局「{orgProfile.TrustName}」の鍵と自己証明書を作成しました。");
				pWindow?.AddMessage(new Message(AppFacility.Complete, "ルート認証局の鍵と自己証明書を作成しました。", orgProfile.TrustName));
			}
			else if (createdIssue)
			{
				ShowInfoBar(
					InfoBarSeverity.Success,
					"発行用認証局を作成しました",
					$"発行用認証局「{orgProfile.IssueName}」の鍵と証明書を作成しました。");
				pWindow?.AddMessage(new Message(AppFacility.Complete, "発行用認証局の鍵と証明書を作成しました。", orgProfile.IssueName));
			}
			else
			{
				ShowInfoBar(
					InfoBarSeverity.Success,
					"設定を保存しました",
					$"認証局「{orgProfile.TrustName}」の設定を保存しました（証明書は作成済みです）。");
				pWindow?.AddMessage(new Message(AppFacility.Complete, "認証局設定を保存しました。", orgProfile.TrustName));
			}
		}
	}
}
