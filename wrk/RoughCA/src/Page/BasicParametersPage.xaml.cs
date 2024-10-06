using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using Windows.Storage.Pickers;
using Windows.Storage;
using Arteria_s.UI.Base;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Arteria_s.App.RoughCA
{
	/// <summary>
	/// An empty page that can be used on its own or navigated to within a Frame.
	/// </summary>
	public sealed partial class BasicParametersPage : Page
	{
		private DbParams m_pDbParams;
		private bool m_bIsDirty;

		public BasicParametersPage()
		{
			this.InitializeComponent();
			var pApp = App.Current as RoughCA.App;
			var pProfile = pApp.m_pProfile;
			m_pDbParams = pProfile.m_pDbParams;
			m_bIsDirty = false;
		}

		private bool IsNotNull(string pText)
		{
			if (pText == null)
			{
				return(false);
			}
			if (pText.Length <= 0)
			{
				return (false);
			}
			return (true);
		}

		private bool Varidate()
		{
			if (IsNotNull(DatabaseServerName.Text) == false)
			{
				return(false);
			}
			if (IsNotNull(InstanceName.Text) == false)
			{
				return (false);
			}
			if (IsNotNull(SchemaName.Text) == false)
			{
				return (false);
			}
			if (IsNotNull(ClientKey.Text) == false)
			{
				return (false);
			}
			if (IsNotNull(ClientCrt.Text) == false)
			{
				return (false);
			}
			if (IsNotNull(RootCACrt.Text) == false)
			{
				return (false);
			}
			if (IsNotNull(IdentityName.Text) == false)
			{
				return (false);
			}

			return (true);
		}

		private void Save_Click(object sender, RoutedEventArgs e)
		{
			if (Varidate() == false)
			{
				return;
			}

			var pApp = App.Current as RoughCA.App;
			var bResult = pApp.m_pProfile.Save();
			//Save.IsEnabled = false;
		}


		private void Settings_TextChanged(object sender, TextChangedEventArgs e)
		{
			if (Varidate() == false)
			{
				//Save.IsEnabled = false;
			}
			else
			{
				if (m_bIsDirty == true)
				{
					//Save.IsEnabled = true;
				}
			}
			m_bIsDirty = true;

			return;
		}

		private async void BrowseClienCrt_Click(object sender, RoutedEventArgs e)
		{
			//　コモンダイアログを使う道は、忘れるか諦めるのが好ましい（2022/10/12）
			//　最適解：エクスプローラでファイルをドロップしろと利用者に伝える
			//　相互運用機能に頼る（2022/10/15）
			var pApp = Application.Current as App;
			var pOpenPicker = PickerHelper.NewFileOpenPicker(pApp.m_pWindow);

			pOpenPicker.ViewMode = PickerViewMode.List;
			pOpenPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
			pOpenPicker.FileTypeFilter.Add(".csv");
			pOpenPicker.FileTypeFilter.Add("*");
			//　FileTypeFilterのリストから初期表示に使うフィルターを選択する方法は未実装（2023/01/22）

			// 2023/01/22　ChatGPT Suggestion is follow.
			// pOpenPicker.SuggestedFileType = ".csv";
			// pOpenPicker.SuggestedFileType = pOpenPicker.FileTypeFilter[1];
			//　いや理想的な実装はそうだけど、それ（SuggestedFileType）が実装されていないのですよ…

			StorageFile file = await pOpenPicker.PickSingleFileAsync();
			if (file != null)
			{
				this.ClientCrt.Text = file.Path;
			}
			else
			{
				;
			}
		}

		private async void BrowseRootCACrt_Click(object sender, RoutedEventArgs e)
		{
			//　コモンダイアログを使う道は、忘れるか諦めるのが好ましい（2022/10/12）
			//　最適解：エクスプローラでファイルをドロップしろと利用者に伝える
			//　相互運用機能に頼る（2022/10/15）
			var pApp = Application.Current as App;
			var pOpenPicker = PickerHelper.NewFileOpenPicker(pApp.m_pWindow);

			pOpenPicker.ViewMode = PickerViewMode.List;
			pOpenPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
			pOpenPicker.FileTypeFilter.Add(".csv");
			pOpenPicker.FileTypeFilter.Add("*");
			//　FileTypeFilterのリストから初期表示に使うフィルターを選択する方法は未実装（2023/01/22）

			// 2023/01/22　ChatGPT Suggestion is follow.
			// pOpenPicker.SuggestedFileType = ".csv";
			// pOpenPicker.SuggestedFileType = pOpenPicker.FileTypeFilter[1];
			//　いや理想的な実装はそうだけど、それ（SuggestedFileType）が実装されていないのですよ…

			StorageFile file = await pOpenPicker.PickSingleFileAsync();
			if (file != null)
			{
				this.RootCACrt.Text = file.Path;
			}
			else
			{
				;
			}

		}

		private async void BrowseClientKey_Click(object sender, RoutedEventArgs e)
		{
			//　コモンダイアログを使う道は、忘れるか諦めるのが好ましい（2022/10/12）
			//　最適解：エクスプローラでファイルをドロップしろと利用者に伝える
			//　相互運用機能に頼る（2022/10/15）
			var pApp = Application.Current as App;
			var pOpenPicker = PickerHelper.NewFileOpenPicker(pApp.m_pWindow);

			pOpenPicker.ViewMode = PickerViewMode.List;
			pOpenPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
			pOpenPicker.FileTypeFilter.Add(".csv");
			pOpenPicker.FileTypeFilter.Add("*");
			//　FileTypeFilterのリストから初期表示に使うフィルターを選択する方法は未実装（2023/01/22）

			// 2023/01/22　ChatGPT Suggestion is follow.
			// pOpenPicker.SuggestedFileType = ".csv";
			// pOpenPicker.SuggestedFileType = pOpenPicker.FileTypeFilter[1];
			//　いや理想的な実装はそうだけど、それ（SuggestedFileType）が実装されていないのですよ…

			StorageFile file = await pOpenPicker.PickSingleFileAsync();
			if (file != null)
			{
				this.ClientKey.Text = file.Path;
			}
			else
			{
				;
			}

		}

		private void BasicParameters_LostFocus(object sender, RoutedEventArgs e)
		{
			if (m_bWriteable == false)
			{
				return;
			}
			if (Varidate() == false)
			{
				return;
			}
			var pApp = App.Current as RoughCA.App;
			var bResult = pApp.m_pProfile.Save();
		}

		public void IsWriteable(bool? bWriteable)
		{
			if (bWriteable == null)
			{
				return;
			}
			DatabaseServerName.IsReadOnly = !bWriteable.Value;
			InstanceName.IsReadOnly       = !bWriteable.Value;
			SchemaName.IsReadOnly         = !bWriteable.Value;
			ClientKey.IsReadOnly          = !bWriteable.Value;
			ClientCrt.IsReadOnly          = !bWriteable.Value;
			RootCACrt.IsReadOnly          = !bWriteable.Value;
			IdentityName.IsReadOnly       = !bWriteable.Value;

			BrowseClientKey.IsEnabled = bWriteable.Value;
			BrowseClientCrt.IsEnabled = bWriteable.Value;
			BrowseRootCACrt.IsEnabled = bWriteable.Value;

			m_bWriteable = bWriteable.Value;
		}
		private bool m_bWriteable = false;
	}
}
