using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using Windows.Storage.Pickers;
using Windows.Storage;
using ArteriaS.UI.Base;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace ArteriaS.App.RoughCA
{
	/// <summary>
	/// An empty page that can be used on its own or navigated to within a Frame.
	/// </summary>
	public sealed partial class BasicParametersPage : Page
	{
		private DbParams m_pDbParams;
		private bool m_bIsDirty;
		private bool m_bNeedsReconnect;
		private bool m_bWriteable = false;

		public BasicParametersPage()
		{
			var pApp = App.Current as RoughCA.App;
			m_pDbParams = pApp.m_pDbParams ?? Profile.CreateDefaultDbParams();
			m_bIsDirty = false;
			m_bNeedsReconnect = false;

			this.InitializeComponent();
			UpdateEffectiveSqlitePath();
			m_bIsDirty = false;
			m_bNeedsReconnect = false;
		}

		private bool IsNotNull(string pText)
		{
			return !string.IsNullOrWhiteSpace(pText);
		}

		private bool Varidate()
		{
			if (!IsNotNull(ProfileNameBox.Text))
			{
				return false;
			}

			if (DBDrivers.SelectedIndex == 0)
			{
				// SQLite
				return IsNotNull(SqliteDatabaseName.Text) || IsNotNull(SqliteFilePath.Text);
			}
			else if (DBDrivers.SelectedIndex == 1)
			{
				// PostgreSQL
				if (!IsNotNull(DatabaseServerName.Text)) return false;
				if (!IsNotNull(DatabaseName.Text)) return false;
				if (!IsNotNull(SchemaName.Text)) return false;
				if (!IsNotNull(ClientKey.Text)) return false;
				if (!IsNotNull(ClientCrt.Text)) return false;
				if (!IsNotNull(RootCACrt.Text)) return false;
				return true;
			}

			return false;
		}

		private void UpdateEffectiveSqlitePath()
		{
			var pApp = App.Current as RoughCA.App;
			if (pApp?.m_pProfile == null || SqliteEffectivePathText == null)
			{
				return;
			}
			var customPath = SqliteFilePath?.Text?.Trim();
			if (!string.IsNullOrWhiteSpace(customPath))
			{
				SqliteEffectivePathText.Text = customPath;
			}
			else
			{
				SqliteEffectivePathText.Text = pApp.m_pProfile.GetDefaultSqlitePath(SqliteDatabaseName?.Text);
			}
		}

		private void Settings_TextChanged(object sender, TextChangedEventArgs e)
		{
			m_bIsDirty = true;
			m_bNeedsReconnect = true;
		}

		private void SqliteSettings_TextChanged(object sender, TextChangedEventArgs e)
		{
			m_bIsDirty = true;
			m_bNeedsReconnect = true;
			UpdateEffectiveSqlitePath();
		}

		private async void BrowseSqliteFile_Click(object sender, RoutedEventArgs e)
		{
			var pApp = Application.Current as App;
			var pOpenPicker = PickerHelper.NewFileOpenPicker(pApp.m_pWindow);

			pOpenPicker.ViewMode = PickerViewMode.List;
			pOpenPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
			pOpenPicker.FileTypeFilter.Add(".db");
			pOpenPicker.FileTypeFilter.Add(".sqlite");
			pOpenPicker.FileTypeFilter.Add("*");

			StorageFile file = await pOpenPicker.PickSingleFileAsync();
			if (file != null)
			{
				this.SqliteFilePath.Text = file.Path;
				UpdateEffectiveSqlitePath();
			}
		}

		private async void BrowseClienCrt_Click(object sender, RoutedEventArgs e)
		{
			var pApp = Application.Current as App;
			var pOpenPicker = PickerHelper.NewFileOpenPicker(pApp.m_pWindow);

			pOpenPicker.ViewMode = PickerViewMode.List;
			pOpenPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
			pOpenPicker.FileTypeFilter.Add(".crt");
			pOpenPicker.FileTypeFilter.Add("*");

			StorageFile file = await pOpenPicker.PickSingleFileAsync();
			if (file != null)
			{
				this.ClientCrt.Text = file.Path;
			}
		}

		private async void BrowseRootCACrt_Click(object sender, RoutedEventArgs e)
		{
			var pApp = Application.Current as App;
			var pOpenPicker = PickerHelper.NewFileOpenPicker(pApp.m_pWindow);

			pOpenPicker.ViewMode = PickerViewMode.List;
			pOpenPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
			pOpenPicker.FileTypeFilter.Add(".crt");
			pOpenPicker.FileTypeFilter.Add("*");

			StorageFile file = await pOpenPicker.PickSingleFileAsync();
			if (file != null)
			{
				this.RootCACrt.Text = file.Path;
			}
		}

		private async void BrowseClientKey_Click(object sender, RoutedEventArgs e)
		{
			var pApp = Application.Current as App;
			var pOpenPicker = PickerHelper.NewFileOpenPicker(pApp.m_pWindow);

			pOpenPicker.ViewMode = PickerViewMode.List;
			pOpenPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
			pOpenPicker.FileTypeFilter.Add(".key");
			pOpenPicker.FileTypeFilter.Add("*");

			StorageFile file = await pOpenPicker.PickSingleFileAsync();
			if (file != null)
			{
				this.ClientKey.Text = file.Path;
			}
		}

		private void BasicParameters_LostFocus(object sender, RoutedEventArgs e)
		{
			if (m_bWriteable == false || m_bIsDirty == false)
			{
				return;
			}
			if (Varidate() == false)
			{
				return;
			}

			var pApp = App.Current as RoughCA.App;
			pApp.m_pProfile.Save(m_pDbParams);
			m_bIsDirty = false;
			if (pApp.m_pWindow is MainWindow pWindow)
			{
				pWindow.UpdateLocationBadge(pApp);
			}
		}

		//　編集解除時に呼び出され、変更があれば保存・再接続を行う
		public bool CommitIfModified(out string errorMessage)
		{
			errorMessage = null;
			if (!m_bNeedsReconnect && !m_bIsDirty)
			{
				return true;
			}
			if (Varidate() == false)
			{
				errorMessage = "データベース接続設定に未入力の項目があります。";
				return false;
			}

			var pApp = App.Current as RoughCA.App;
			var ok = pApp.SaveDbParamsAndReconnect(m_pDbParams, out errorMessage);
			m_bIsDirty = false;
			m_bNeedsReconnect = false;

			if (pApp.m_pWindow is MainWindow pWindow)
			{
				pWindow.UpdateLocationBadge(pApp);
			}
			return ok;
		}

		private void ApplyReconnectButton_Click(object sender, RoutedEventArgs e)
		{
			if (Varidate() == false)
			{
				return;
			}

			var pApp = App.Current as RoughCA.App;
			var ok = pApp.SaveDbParamsAndReconnect(m_pDbParams, out var err);
			m_bIsDirty = false;
			m_bNeedsReconnect = false;

			if (pApp.m_pWindow is MainWindow pWindow)
			{
				pWindow.RefreshAfterProfileChange(pApp, ok, $"プロファイル '{m_pDbParams.ProfileName}' の設定を保存して再接続しました。", err);
			}
		}

		private async void DeleteProfileButton_Click(object sender, RoutedEventArgs e)
		{
			var pApp = App.Current as RoughCA.App;
			if (pApp == null || pApp.m_pProfile.Profiles.Count <= 1)
			{
				return;
			}

			var targetName = m_pDbParams.ProfileName;
			var dialog = new ContentDialog
			{
				Title = "プロファイルを削除",
				Content = $"プロファイル '{targetName}' を設定一覧から削除しますか？\n（※データベースファイル自体は削除されません）",
				PrimaryButtonText = "削除",
				CloseButtonText = "キャンセル",
				DefaultButton = ContentDialogButton.Close,
				XamlRoot = this.Content.XamlRoot
			};

			try
			{
				var result = await dialog.ShowAsync();
				if (result == ContentDialogResult.Primary)
				{
					var ok = pApp.DeleteProfile(targetName, out var err);
					if (pApp.m_pWindow is MainWindow pWindow)
					{
						pWindow.RefreshAfterProfileChange(pApp, ok, $"プロファイル '{targetName}' を削除し、'{pApp.m_pDbParams?.ProfileName}' へ切り替えました。", err);
					}
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine("DeleteProfileButton_Click error: " + ex);
			}
		}

		public void IsWriteable(bool? bWriteable)
		{
			if (bWriteable == null)
			{
				return;
			}
			var editable = bWriteable.Value;

			ProfileNameBox.IsReadOnly     = !editable;
			SqliteDatabaseName.IsReadOnly = !editable;
			SqliteFilePath.IsReadOnly     = !editable;
			DatabaseServerName.IsReadOnly = !editable;
			DatabaseName.IsReadOnly       = !editable;
			SchemaName.IsReadOnly         = !editable;
			ClientKey.IsReadOnly          = !editable;
			ClientCrt.IsReadOnly          = !editable;
			RootCACrt.IsReadOnly          = !editable;

			BrowseSqliteFile.IsEnabled     = editable;
			BrowseClientKey.IsEnabled      = editable;
			BrowseClientCrt.IsEnabled      = editable;
			BrowseRootCACrt.IsEnabled      = editable;
			DBDrivers.IsEnabled            = editable;
			ApplyReconnectButton.IsEnabled = editable;

			var pApp = App.Current as RoughCA.App;
			DeleteProfileButton.IsEnabled = editable && (pApp?.m_pProfile?.Profiles.Count > 1);

			UpdateDisplayState(editable);

			m_bWriteable = editable;
		}

		private void UpdateDisplayState(bool bWriteable)
		{
			if (bWriteable == false)
			{
				ExpanderSQLite.IsEnabled = false;
				ExpanderPostgreSQL.IsEnabled = false;
			}
			else
			{
				switch (DBDrivers.SelectedIndex)
				{
				case 0: // SQLite
					ExpanderSQLite.IsEnabled = true;
					ExpanderSQLite.IsExpanded = true;
					ExpanderPostgreSQL.IsEnabled = false;
					ExpanderPostgreSQL.IsExpanded = false;
					break;
				case 1: // PostgreSQL
					ExpanderSQLite.IsEnabled = false;
					ExpanderSQLite.IsExpanded = false;
					ExpanderPostgreSQL.IsEnabled = true;
					ExpanderPostgreSQL.IsExpanded = true;
					break;
				}
			}
		}

		//　表示状態を更新
		private void DBDrivers_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			m_bIsDirty = true;
			m_bNeedsReconnect = true;
			UpdateDisplayState(m_bWriteable);
		}
	}
}
