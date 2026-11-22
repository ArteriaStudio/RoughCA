using ArteriaS.UI.Base;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace ArteriaS.App.RoughCA
{
	public sealed partial class ExportParametersPage : Page
	{
		private DbParams m_pDbParams;
		private bool m_bIsDirty = false;
		private bool m_bWriteable = false;

		public ExportParametersPage()
		{
			var pApp = App.Current as RoughCA.App;
			m_pDbParams = pApp?.m_pDbParams ?? Profile.CreateDefaultDbParams();

			this.InitializeComponent();
			UpdateEffectiveExportFolder();
			m_bIsDirty = false;
		}

		private void UpdateEffectiveExportFolder()
		{
			var pApp = App.Current as RoughCA.App;
			if (pApp?.m_pProfile == null || ExportEffectiveFolderText == null)
			{
				return;
			}
			var customFolder = ExportFolderBox?.Text?.Trim();
			if (!string.IsNullOrWhiteSpace(customFolder))
			{
				ExportEffectiveFolderText.Text = customFolder;
			}
			else
			{
				ExportEffectiveFolderText.Text = pApp.m_pProfile.GetDefaultExportFolder();
			}
		}

		private void ExportFormatButtons_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (m_bWriteable)
			{
				m_bIsDirty = true;
			}
		}

		private void ExportFolderBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			if (m_bWriteable)
			{
				m_bIsDirty = true;
			}
			UpdateEffectiveExportFolder();
		}

		private async void BrowseExportFolder_Click(object sender, RoutedEventArgs e)
		{
			var pApp = Application.Current as App;
			var pFolderPicker = PickerHelper.NewFolderPicker(pApp.m_pWindow);

			pFolderPicker.ViewMode = PickerViewMode.List;
			pFolderPicker.SuggestedStartLocation = PickerLocationId.Downloads;
			pFolderPicker.FileTypeFilter.Add("*");

			StorageFolder folder = await pFolderPicker.PickSingleFolderAsync();
			if (folder != null)
			{
				ExportFolderBox.Text = folder.Path;
				m_pDbParams.ExportFolder = folder.Path;
				m_bIsDirty = true;
				UpdateEffectiveExportFolder();
			}
		}

		private void ExportParameters_LostFocus(object sender, RoutedEventArgs e)
		{
			if (m_bWriteable == false || m_bIsDirty == false)
			{
				return;
			}
			SaveCurrentSettings();
		}

		public bool CommitIfModified()
		{
			if (!m_bIsDirty)
			{
				return true;
			}
			return SaveCurrentSettings();
		}

		private bool SaveCurrentSettings()
		{
			var pApp = App.Current as RoughCA.App;
			if (pApp?.m_pProfile == null)
			{
				return false;
			}
			m_pDbParams.ExportFolder = ExportFolderBox?.Text?.Trim() ?? "";
			var ok = pApp.m_pProfile.Save(m_pDbParams);
			if (ok)
			{
				m_bIsDirty = false;
			}
			return ok;
		}

		public void IsWriteable(bool? bWriteable)
		{
			if (bWriteable == null)
			{
				return;
			}
			var editable = bWriteable.Value;

			ExportFormatButtons.IsEnabled = editable;
			ExportFolderBox.IsReadOnly      = !editable;
			BrowseExportFolder.IsEnabled  = editable;

			m_bWriteable = editable;
		}
	}
}
