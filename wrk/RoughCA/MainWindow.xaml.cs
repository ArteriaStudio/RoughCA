using ArteriaS.UI.Base;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Windows.ApplicationModel.Resources;
using Windows.Storage.Pickers;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace ArteriaS.App.RoughCA
{
	/// <summary>
	/// An empty window that can be used on its own or navigated to within a Frame.
	/// </summary>
	public sealed partial class MainWindow : Window
	{
		private string m_pCurrentTag = "ListupCertificate";

		public MainWindow()
		{
			this.InitializeComponent();

			this.ExtendsContentIntoTitleBar = true;
			this.SetTitleBar(AppTitleBar);

			var pApp = App.Current as RoughCA.App;
			UpdateLocationBadge(pApp);

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

		public void UpdateLocationBadge(RoughCA.App pApp)
		{
			if (pApp?.m_pDbParams == null)
			{
				LocationBadge.Visibility = Visibility.Collapsed;
				return;
			}
			var profileName = string.IsNullOrWhiteSpace(pApp.m_pDbParams.ProfileName) ? "Default" : pApp.m_pDbParams.ProfileName;
			var driver = string.IsNullOrWhiteSpace(pApp.m_pDbParams.DriverName) ? "Unconfigured" : pApp.m_pDbParams.DriverName;
			var dbName = string.IsNullOrWhiteSpace(pApp.m_pDbParams.DatabaseName) ? "-" : pApp.m_pDbParams.DatabaseName;
			Location.Text = $"{profileName}  ({driver} · {dbName})";
			TitleBarText.Text = $"RoughCA [{profileName}]";
			LocationBadge.Visibility = Visibility.Visible;
		}

		public void SetMessageLogExpanded(bool isExpanded)
		{
			MessageRow.Height = isExpanded ? new GridLength(180) : GridLength.Auto;
		}

		//　右上プロファイル切り替えメニューを開くたびに最新一覧を構築
		private void ProfileSwitchFlyout_Opening(object sender, object e)
		{
			ProfileSwitchFlyout.Items.Clear();

			var pApp = App.Current as RoughCA.App;
			if (pApp?.m_pProfile == null)
			{
				return;
			}

			var activeName = pApp.m_pProfile.ActiveProfileName;
			foreach (var profile in pApp.m_pProfile.Profiles)
			{
				var name = profile.ProfileName;
				var driver = string.IsNullOrWhiteSpace(profile.DriverName) ? "-" : profile.DriverName;
				var dbName = string.IsNullOrWhiteSpace(profile.DatabaseName) ? "-" : profile.DatabaseName;

				var item = new ToggleMenuFlyoutItem
				{
					Text = $"{name}  ({driver} · {dbName})",
					IsChecked = string.Equals(name, activeName, StringComparison.OrdinalIgnoreCase),
					Tag = name
				};
				item.Click += ProfileMenuItem_Click;
				ProfileSwitchFlyout.Items.Add(item);
			}

			ProfileSwitchFlyout.Items.Add(new MenuFlyoutSeparator());

			var addItem = new MenuFlyoutItem
			{
				Text = "新しいプロファイルを追加...",
				Icon = new FontIcon { Glyph = "\uE710" }
			};
			addItem.Click += AddProfileMenuItem_Click;
			ProfileSwitchFlyout.Items.Add(addItem);

			var settingsItem = new MenuFlyoutItem
			{
				Text = "接続設定を開く",
				Icon = new FontIcon { Glyph = "\uE713" }
			};
			settingsItem.Click += (s, args) => TransitPage("Settings");
			ProfileSwitchFlyout.Items.Add(settingsItem);
		}

		private void ProfileMenuItem_Click(object sender, RoutedEventArgs e)
		{
			if (sender is ToggleMenuFlyoutItem item && item.Tag is string targetName)
			{
				var pApp = App.Current as RoughCA.App;
				if (pApp == null)
				{
					return;
				}
				if (string.Equals(targetName, pApp.m_pProfile.ActiveProfileName, StringComparison.OrdinalIgnoreCase))
				{
					return;
				}

				ExecuteSwitchProfile(targetName);
			}
		}

		private async void AddProfileMenuItem_Click(object sender, RoutedEventArgs e)
		{
			await ShowAddProfileDialogAsync();
		}

		public void ExecuteSwitchProfile(string profileName)
		{
			var pApp = App.Current as RoughCA.App;
			if (pApp == null)
			{
				return;
			}

			var ok = pApp.SwitchProfile(profileName, out var errorMessage);
			RefreshAfterProfileChange(pApp, ok, $"認証局プロファイル '{profileName}' に切り替えました。", errorMessage);
		}

		public void RefreshAfterProfileChange(RoughCA.App pApp, bool connected, string successText, string errorMessage)
		{
			UpdateLocationBadge(pApp);

			if (!connected && !string.IsNullOrWhiteSpace(errorMessage))
			{
				AddMessage(new Message(AppFacility.Error, "データベース接続に失敗しました。", pApp.m_pDbParams?.ProfileName ?? "", errorMessage));
				TransitPage("Settings");
				return;
			}

			var detailPath = string.Equals(pApp.m_pDbParams?.DriverName, "SQLite", StringComparison.OrdinalIgnoreCase)
				? pApp.m_pProfile.ResolveSqlitePath(pApp.m_pDbParams)
				: $"{pApp.m_pDbParams?.HostName}/{pApp.m_pDbParams?.DatabaseName}";

			AddMessage(new Message(AppFacility.Info, successText, pApp.m_pDbParams?.ProfileName ?? "", detailPath));

			if (pApp.m_pPrepareFlags.bExistDbParams == false || pApp.m_pPrepareFlags.bExistOrgProfile == false)
			{
				TransitPage("Settings");
			}
			else
			{
				var targetTag = string.IsNullOrWhiteSpace(m_pCurrentTag) ? "ListupCertificate" : m_pCurrentTag;
				TransitPage(targetTag);
			}
		}

		private async System.Threading.Tasks.Task ShowAddProfileDialogAsync()
		{
			var pApp = App.Current as RoughCA.App;
			if (pApp == null)
			{
				return;
			}

			var profileNameBox = new TextBox
			{
				Header = "プロファイル名",
				PlaceholderText = "例: 社内検証CA"
			};
			var driverCombo = new ComboBox
			{
				Header = "データベースドライバー",
				HorizontalAlignment = HorizontalAlignment.Stretch,
				SelectedIndex = 0
			};
			driverCombo.Items.Add("SQLite");
			driverCombo.Items.Add("Postgres");

			var dbNameBox = new TextBox
			{
				Header = "データベース名 / 認証局識別子",
				PlaceholderText = "例: DevCA"
			};

			var sqlitePathBox = new TextBox
			{
				Header = "SQLite ファイルパス（任意）",
				PlaceholderText = "空欄時は既定フォルダに {DB名}.db を自動作成"
			};
			var browseButton = new Button
			{
				Content = "参照",
				VerticalAlignment = VerticalAlignment.Bottom
			};
			browseButton.Click += async (s, args) =>
			{
				var picker = PickerHelper.NewFileOpenPicker(this);
				picker.ViewMode = PickerViewMode.List;
				picker.FileTypeFilter.Add(".db");
				picker.FileTypeFilter.Add(".sqlite");
				picker.FileTypeFilter.Add("*");
				var file = await picker.PickSingleFileAsync();
				if (file != null)
				{
					sqlitePathBox.Text = file.Path;
				}
			};

			var sqlitePathGrid = new Grid { ColumnSpacing = 8 };
			sqlitePathGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			sqlitePathGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
			Grid.SetColumn(sqlitePathBox, 0);
			Grid.SetColumn(browseButton, 1);
			sqlitePathGrid.Children.Add(sqlitePathBox);
			sqlitePathGrid.Children.Add(browseButton);

			var errorText = new TextBlock
			{
				Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xE8, 0x11, 0x23)),
				Visibility = Visibility.Collapsed,
				TextWrapping = TextWrapping.Wrap
			};

			driverCombo.SelectionChanged += (s, args) =>
			{
				sqlitePathGrid.Visibility = driverCombo.SelectedIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
			};

			var panel = new StackPanel { Spacing = 12, MinWidth = 420 };
			panel.Children.Add(profileNameBox);
			panel.Children.Add(driverCombo);
			panel.Children.Add(dbNameBox);
			panel.Children.Add(sqlitePathGrid);
			panel.Children.Add(errorText);

			var dialog = new ContentDialog
			{
				Title = "新しい認証局プロファイルを追加",
				Content = panel,
				PrimaryButtonText = "作成して切り替え",
				CloseButtonText = "キャンセル",
				DefaultButton = ContentDialogButton.Primary,
				XamlRoot = this.Content.XamlRoot
			};

			dialog.PrimaryButtonClick += (s, args) =>
			{
				var name = profileNameBox.Text?.Trim() ?? "";
				var dbName = dbNameBox.Text?.Trim() ?? "";
				var driver = driverCombo.SelectedIndex == 1 ? "Postgres" : "SQLite";

				if (string.IsNullOrWhiteSpace(name))
				{
					errorText.Text = "プロファイル名を入力してください。";
					errorText.Visibility = Visibility.Visible;
					args.Cancel = true;
					return;
				}
				if (pApp.m_pProfile.FindProfile(name) != null)
				{
					errorText.Text = $"プロファイル '{name}' は既に存在します。";
					errorText.Visibility = Visibility.Visible;
					args.Cancel = true;
					return;
				}
				if (string.IsNullOrWhiteSpace(dbName))
				{
					dbName = name;
				}

				var newParams = Profile.CreateDefaultDbParams(name, dbName);
				newParams.DriverName = driver;
				if (driver == "SQLite")
				{
					newParams.SqliteFilePath = sqlitePathBox.Text?.Trim() ?? "";
				}

				var ok = pApp.AddAndSwitchProfile(newParams, out var err);
				RefreshAfterProfileChange(pApp, ok, $"新しい認証局プロファイル '{name}' を作成して切り替えました。", err);
			};

			try
			{
				await dialog.ShowAsync();
			}
			catch (Exception ex)
			{
				Debug.WriteLine("ShowAddProfileDialogAsync error: " + ex);
			}
		}

		private void DumpCertificate(string pFilepath)
		{
			X509Certificate pCert = X509CertificateLoader.LoadCertificateFromFile(pFilepath);
			string resultsTrue = pCert.ToString(true);
			Console.WriteLine(resultsTrue);
			string resultsFalse = pCert.ToString(false);
			Console.WriteLine(resultsFalse);
		}

		public void TransitPage(string pTag)
		{
			if (string.IsNullOrEmpty(pTag))
			{
				return;
			}

			m_pCurrentTag = pTag;

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
			if (args.IsSettingsSelected)
			{
				TransitPage("Settings");
				return;
			}

			var clickedItem = args.SelectedItem;
			if (clickedItem is NavigationViewItem pItem && pItem.Tag is string pTag)
			{
				Debug.WriteLine("Tag=" + pTag);
				TransitPage(pTag);
			}
		}

		public void AddMessage(Message pMessage)
		{
			var pMessagesPage = MessageFrame.Content as MessagesPage;
			pMessagesPage?.AddMessage(pMessage);
		}
	}
}
